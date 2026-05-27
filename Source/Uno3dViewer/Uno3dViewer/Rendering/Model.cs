using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using AiMesh = Silk.NET.Assimp.Mesh;
using AiMaterial = Silk.NET.Assimp.Material;
using AiTexture = Silk.NET.Assimp.Texture;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace Uno3dViewer.Rendering;

public sealed class Model : IDisposable
{
    public List<Mesh> Meshes { get; } = new();
    private readonly List<Texture> _textures = new();

    public Vector3 BoundsMin { get; private set; } = new(float.PositiveInfinity);
    public Vector3 BoundsMax { get; private set; } = new(float.NegativeInfinity);
    public string Source { get; private set; } = "<empty>";

    private void Add(Mesh m)
    {
        Meshes.Add(m);
        BoundsMin = Vector3.Min(BoundsMin, m.BoundsMin);
        BoundsMax = Vector3.Max(BoundsMax, m.BoundsMax);
    }

    public void Dispose()
    {
        foreach (var m in Meshes) m.Dispose();
        Meshes.Clear();
        foreach (var t in _textures) t.Dispose();
        _textures.Clear();
    }

    public static Model CreateCube(GL gl)
    {
        const float p = 0.5f;
        var faces = new (Vector3 N, Vector3 A, Vector3 B, Vector3 C, Vector3 D)[]
        {
            ( Vector3.UnitZ, new(-p,-p, p), new( p,-p, p), new( p, p, p), new(-p, p, p)),
            (-Vector3.UnitZ, new( p,-p,-p), new(-p,-p,-p), new(-p, p,-p), new( p, p,-p)),
            ( Vector3.UnitX, new( p,-p, p), new( p,-p,-p), new( p, p,-p), new( p, p, p)),
            (-Vector3.UnitX, new(-p,-p,-p), new(-p,-p, p), new(-p, p, p), new(-p, p,-p)),
            ( Vector3.UnitY, new(-p, p, p), new( p, p, p), new( p, p,-p), new(-p, p,-p)),
            (-Vector3.UnitY, new(-p,-p,-p), new( p,-p,-p), new( p,-p, p), new(-p,-p, p)),
        };

        var verts = new Vertex[24];
        var idx = new uint[36];
        for (int f = 0; f < 6; f++)
        {
            int s = f * 4;
            var face = faces[f];
            verts[s + 0] = new Vertex(face.A, face.N, new Vector2(0, 0));
            verts[s + 1] = new Vertex(face.B, face.N, new Vector2(1, 0));
            verts[s + 2] = new Vertex(face.C, face.N, new Vector2(1, 1));
            verts[s + 3] = new Vertex(face.D, face.N, new Vector2(0, 1));
            int o = f * 6;
            idx[o + 0] = (uint)s;       idx[o + 1] = (uint)(s + 1); idx[o + 2] = (uint)(s + 2);
            idx[o + 3] = (uint)s;       idx[o + 4] = (uint)(s + 2); idx[o + 5] = (uint)(s + 3);
        }

        var model = new Model { Source = "<cube>" };
        var mesh = new Mesh(gl, verts, idx) { DiffuseColor = new Vector3(0.78f, 0.80f, 0.85f) };
        model.Add(mesh);
        return model;
    }

    public static unsafe Model LoadFromFile(GL gl, string path)
    {
        var assimp = Assimp.GetApi();
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var isGltf = ext is ".gltf" or ".glb";
        var flagsRaw = PostProcessSteps.Triangulate
                     | PostProcessSteps.GenerateSmoothNormals
                     | PostProcessSteps.JoinIdenticalVertices
                     | PostProcessSteps.ImproveCacheLocality
                     | PostProcessSteps.PreTransformVertices;
        if (!isGltf) flagsRaw |= PostProcessSteps.FlipUVs;
        var flags = (uint)flagsRaw;
        var scene = assimp.ImportFile(path, flags);
        if (scene == null || (scene->MFlags & (uint)SceneFlags.Incomplete) != 0 || scene->MRootNode == null)
            throw new Exception($"Assimp failed to load '{path}': {assimp.GetErrorStringS()}");

        var model = new Model { Source = path };
        var modelDir = Path.GetDirectoryName(path) ?? "";

        var materialCount = (int)scene->MNumMaterials;
        var matTextures = new Texture?[materialCount];
        for (int i = 0; i < materialCount; i++)
        {
            matTextures[i] = LoadMaterialTexture(gl, assimp, scene, scene->MMaterials[i], modelDir, model);
        }

        try
        {
            for (uint i = 0; i < scene->MNumMeshes; i++)
            {
                var aiMesh = scene->MMeshes[i];
                var mesh = BuildMesh(gl, aiMesh);
                var matIdx = (int)aiMesh->MMaterialIndex;
                if (matIdx >= 0 && matIdx < materialCount)
                    mesh.DiffuseTexture = matTextures[matIdx];
                model.Add(mesh);
            }
        }
        finally
        {
            assimp.ReleaseImport(scene);
        }
        return model;
    }

    private static unsafe Texture? LoadMaterialTexture(
        Silk.NET.OpenGL.GL gl, Assimp api, Scene* scene, AiMaterial* material, string modelDir, Model model)
    {
        TextureType useType = (TextureType)0;
        bool found = false;
        for (int t = 1; t <= 18; t++)
        {
            if (api.GetMaterialTextureCount(material, (TextureType)t) > 0)
            {
                useType = (TextureType)t;
                found = true;
                break;
            }
        }
        if (!found) return null;

        AssimpString aiPath = default;
        if (api.GetMaterialTexture(material, useType, 0, ref aiPath, null, null, null, null, null, null) != Return.Success)
            return null;

        var name = ReadAssimpString(ref aiPath);
        Texture? texture = null;

        if (name.StartsWith('*') && int.TryParse(name.AsSpan(1), out int embeddedIdx))
        {
            if (embeddedIdx >= 0 && embeddedIdx < scene->MNumTextures)
                texture = LoadEmbeddedTexture(gl, scene->MTextures[embeddedIdx]);
        }
        else
        {
            var resolved = ResolveTexturePath(modelDir, name);
            if (resolved is not null)
            {
                try { texture = Texture.FromFile(gl, resolved); }
                catch { }
            }
            if (texture is null && scene->MNumTextures > 0)
                texture = FindEmbeddedByName(gl, scene, name);
        }

        if (texture != null) model._textures.Add(texture);
        return texture;
    }

    private static unsafe Texture? FindEmbeddedByName(GL gl, Scene* scene, string referencedName)
    {
        var basename = Path.GetFileName(referencedName.Replace('\\', '/'));
        for (uint i = 0; i < scene->MNumTextures; i++)
        {
            var aiTex = scene->MTextures[i];
            var embeddedName = ReadAssimpString(ref aiTex->MFilename);
            var embeddedBasename = Path.GetFileName(embeddedName.Replace('\\', '/'));
            if (string.Equals(embeddedName, referencedName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(embeddedBasename, basename, StringComparison.OrdinalIgnoreCase) ||
                scene->MNumTextures == 1)
            {
                return LoadEmbeddedTexture(gl, aiTex);
            }
        }
        return null;
    }

    private static unsafe Texture? LoadEmbeddedTexture(GL gl, AiTexture* aiTex)
    {
        int w = (int)aiTex->MWidth;
        int h = (int)aiTex->MHeight;
        try
        {
            if (h == 0) return Texture.FromCompressedBytes(gl, (byte*)aiTex->PcData, w);
            return Texture.FromRawBgra(gl, (byte*)aiTex->PcData, w, h);
        }
        catch
        {
            return null;
        }
    }

    private static unsafe string ReadAssimpString(ref AssimpString s)
    {
        int len = (int)s.Length;
        if (len <= 0) return string.Empty;
        fixed (byte* p = s.Data)
        {
            return System.Text.Encoding.UTF8.GetString(p, len);
        }
    }

    private static string? ResolveTexturePath(string modelDir, string textureRef)
    {
        if (string.IsNullOrWhiteSpace(textureRef)) return null;
        var normalized = textureRef.Replace('\\', '/').Trim();
        var basename = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(basename)) return null;

        var asGiven = Path.IsPathRooted(normalized) ? normalized : Path.Combine(modelDir, normalized);
        if (File.Exists(asGiven)) return asGiven;

        string[] candidates =
        {
            Path.Combine(modelDir, basename),
            Path.Combine(modelDir, "textures", basename),
            Path.Combine(modelDir, "Textures", basename),
            Path.Combine(modelDir, "TEXTURES", basename),
            Path.Combine(modelDir, "tex", basename),
            Path.Combine(modelDir, "maps", basename),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        try
        {
            var match = Directory.EnumerateFiles(modelDir, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), basename, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        catch { }

        return null;
    }

    private static unsafe Mesh BuildMesh(GL gl, AiMesh* m)
    {
        var verts = new Vertex[m->MNumVertices];
        var hasUV = m->MTextureCoords[0] != null;
        for (uint i = 0; i < m->MNumVertices; i++)
        {
            var p = m->MVertices[i];
            var pos = new Vector3(p.X, p.Y, p.Z);
            var n = Vector3.UnitY;
            if (m->MNormals != null)
            {
                var an = m->MNormals[i];
                n = new Vector3(an.X, an.Y, an.Z);
                var len = n.Length();
                if (len > 1e-6f) n /= len; else n = Vector3.UnitY;
            }
            var uv = Vector2.Zero;
            if (hasUV)
            {
                var t = m->MTextureCoords[0][i];
                uv = new Vector2(t.X, t.Y);
            }
            verts[i] = new Vertex(pos, n, uv);
        }

        var indices = new List<uint>((int)(m->MNumFaces * 3));
        for (uint f = 0; f < m->MNumFaces; f++)
        {
            var face = m->MFaces[f];
            if (face.MNumIndices != 3) continue;
            indices.Add(face.MIndices[0]);
            indices.Add(face.MIndices[1]);
            indices.Add(face.MIndices[2]);
        }
        return new Mesh(gl, verts, indices.ToArray());
    }
}
