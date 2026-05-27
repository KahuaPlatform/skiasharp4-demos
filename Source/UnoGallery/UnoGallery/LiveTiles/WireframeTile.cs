using System.Numerics;
using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Cycles through a pool of 3D wireframe shapes — cube, tetrahedron,
/// octahedron, icosahedron, stellated octahedron, tesseract (4D hypercube
/// projected through W then 3D), one-sheet hyperboloid lattice, and a
/// (2,3) torus knot. Each shape rotates continuously and dwells for ~6 s
/// before a smoothstep alpha cross-fade into the next.
///
/// Hidden-surface treatment is purely depth-based: each edge is
/// brighter / thicker / fuller-alpha when its midpoint sits closer to
/// the camera, dimmer / thinner / more transparent as it recedes into
/// the screen. No face geometry needed — every shape is just vertices
/// and edges. Reads as a true 3D object thanks to the depth gradient.
/// </summary>
public sealed class WireframeTile : ILiveTile
{
    const float ShapeDwell = 6f;
    const float ShapeFade = 0.8f;

    static readonly WireMesh[] Shapes =
    {
        Cube(),
        Tetrahedron(),
        Octahedron(),
        Icosahedron(),
        StellatedOctahedron(),
        Tesseract(),
        Hyperboloid(),
        TorusKnot(),
    };

    public string Caption => "Wireframe";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(4, 6, 18),
        new SKColor(70, 110, 200),
        new SKColor(160, 220, 255),
        new SKColor(255, 240, 200));

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        using var bg = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bg);

        int idx = (int)(t / ShapeDwell) % Shapes.Length;
        var current = Shapes[idx];
        float intoCycle = t - idx * ShapeDwell;
        float fadeStart = ShapeDwell - ShapeFade;

        if (intoCycle > fadeStart)
        {
            var next = Shapes[(idx + 1) % Shapes.Length];
            float fade = Math.Clamp((intoCycle - fadeStart) / ShapeFade, 0f, 1f);
            float eased = fade * fade * (3f - 2f * fade);
            DrawShape(canvas, dest, current, t, 1f - eased);
            DrawShape(canvas, dest, next, t, eased);
        }
        else
        {
            DrawShape(canvas, dest, current, t, 1f);
        }
    }

    void DrawShape(SKCanvas canvas, SKRect dest, WireMesh mesh, float t, float alphaMul)
    {
        float yaw = t * 0.6f;
        float pitch = 0.35f + MathF.Sin(t * 0.21f) * 0.45f;
        float cy = MathF.Cos(yaw), sy = MathF.Sin(yaw);
        float cx = MathF.Cos(pitch), sx = MathF.Sin(pitch);

        Span<Vector3> rot = mesh.Vertices.Length <= 256
            ? stackalloc Vector3[mesh.Vertices.Length]
            : new Vector3[mesh.Vertices.Length];
        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            var p = mesh.Vertices[i];
            var p1 = new Vector3(p.X * cy + p.Z * sy, p.Y, -p.X * sy + p.Z * cy);
            rot[i] = new Vector3(p1.X, p1.Y * cx - p1.Z * sx, p1.Y * sx + p1.Z * cx);
        }

        const float CamD = 4f;
        float scale = MathF.Min(dest.Width, dest.Height) * 0.30f;
        Span<SKPoint> screen = mesh.Vertices.Length <= 256
            ? stackalloc SKPoint[mesh.Vertices.Length]
            : new SKPoint[mesh.Vertices.Length];
        for (int i = 0; i < rot.Length; i++)
        {
            float depth = CamD - rot[i].Z;
            float pers = CamD / MathF.Max(0.1f, depth);
            screen[i] = new SKPoint(
                dest.MidX + rot[i].X * scale * pers,
                dest.MidY - rot[i].Y * scale * pers);
        }

        // Back-to-front edge order so closer edges paint over farther ones.
        Span<int> order = mesh.Edges.Length <= 256
            ? stackalloc int[mesh.Edges.Length]
            : new int[mesh.Edges.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = 1; i < order.Length; i++)
        {
            int k = order[i];
            float kz = MidZ(rot, mesh.Edges[k]);
            int j = i - 1;
            while (j >= 0 && MidZ(rot, mesh.Edges[order[j]]) > kz) { order[j + 1] = order[j]; j--; }
            order[j + 1] = k;
        }

        // Bucket edges into N depth bands. Each band gets one path + one paint,
        // so a 200-edge shape draws in ~8 GPU calls instead of 200.
        const int Buckets = 8;
        var paths = new SKPath[Buckets];
        for (int i = 0; i < Buckets; i++) paths[i] = new SKPath();

        for (int oi = 0; oi < order.Length; oi++)
        {
            var (a, b) = mesh.Edges[order[oi]];
            float midZ = (rot[a].Z + rot[b].Z) * 0.5f;
            float depth01 = Math.Clamp((midZ + 1.5f) / 3.0f, 0f, 1f);
            int bucket = Math.Min(Buckets - 1, (int)(depth01 * Buckets));

            paths[bucket].MoveTo(screen[a]);
            paths[bucket].LineTo(screen[b]);
        }

        // One paint reused across all buckets — mutate Color/StrokeWidth per draw.
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        };
        for (int i = 0; i < Buckets; i++)
        {
            if (paths[i].IsEmpty) { paths[i].Dispose(); continue; }
            float bucketCenter = (i + 0.5f) / Buckets;
            byte alpha = (byte)(Lerp(60f, 255f, bucketCenter) * alphaMul);
            paint.Color = LerpPalette(0.35f + bucketCenter * 0.6f).WithAlpha(alpha);
            paint.StrokeWidth = Lerp(0.7f, 2.0f, bucketCenter);
            canvas.DrawPath(paths[i], paint);
            paths[i].Dispose();
        }
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    static float MidZ(ReadOnlySpan<Vector3> v, (int a, int b) e) => (v[e.a].Z + v[e.b].Z) * 0.5f;

    SKColor LerpPalette(float t)
    {
        t = Math.Clamp(t, 0f, 0.999f);
        float scaled = t * (Palette.Length - 1);
        int idx = (int)scaled;
        float f = scaled - idx;
        var a = Palette[idx];
        var b = Palette[Math.Min(idx + 1, Palette.Length - 1)];
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * f),
            (byte)(a.Green + (b.Green - a.Green) * f),
            (byte)(a.Blue + (b.Blue - a.Blue) * f));
    }

    // ----------- mesh data -----------

    readonly struct WireMesh
    {
        public readonly Vector3[] Vertices;
        public readonly (int a, int b)[] Edges;
        public WireMesh(Vector3[] v, (int, int)[] e) { Vertices = v; Edges = e; }
    }

    /// <summary>Derive unique edge pairs from face vertex loops.</summary>
    static (int, int)[] EdgesFromFaces(int[][] faces)
    {
        var set = new HashSet<(int, int)>();
        foreach (var face in faces)
        {
            for (int i = 0; i < face.Length; i++)
            {
                int a = face[i], b = face[(i + 1) % face.Length];
                set.Add(a < b ? (a, b) : (b, a));
            }
        }
        return set.ToArray();
    }

    static WireMesh Cube()
    {
        var v = new[]
        {
            new Vector3(-1, -1, -1), new Vector3( 1, -1, -1),
            new Vector3( 1,  1, -1), new Vector3(-1,  1, -1),
            new Vector3(-1, -1,  1), new Vector3( 1, -1,  1),
            new Vector3( 1,  1,  1), new Vector3(-1,  1,  1),
        };
        var faces = new[]
        {
            new[] {0,3,2,1}, new[] {4,5,6,7},
            new[] {0,4,7,3}, new[] {1,2,6,5},
            new[] {0,1,5,4}, new[] {3,7,6,2},
        };
        return new WireMesh(v, EdgesFromFaces(faces));
    }

    static WireMesh Tetrahedron()
    {
        var v = new[]
        {
            new Vector3( 1,  1,  1), new Vector3( 1, -1, -1),
            new Vector3(-1,  1, -1), new Vector3(-1, -1,  1),
        };
        var faces = new[] { new[] {0,1,2}, new[] {0,3,1}, new[] {0,2,3}, new[] {1,3,2} };
        return new WireMesh(v, EdgesFromFaces(faces));
    }

    static WireMesh Octahedron()
    {
        var v = new[]
        {
            new Vector3( 1,0,0), new Vector3(-1,0,0),
            new Vector3(0, 1,0), new Vector3(0,-1,0),
            new Vector3(0,0, 1), new Vector3(0,0,-1),
        };
        var faces = new[]
        {
            new[] {0,2,4}, new[] {2,1,4}, new[] {1,3,4}, new[] {3,0,4},
            new[] {2,0,5}, new[] {1,2,5}, new[] {3,1,5}, new[] {0,3,5},
        };
        return new WireMesh(v, EdgesFromFaces(faces));
    }

    static WireMesh Icosahedron()
    {
        float phi = (1f + MathF.Sqrt(5f)) * 0.5f;
        var raw = new[]
        {
            new Vector3(-1,  phi, 0), new Vector3( 1,  phi, 0),
            new Vector3(-1, -phi, 0), new Vector3( 1, -phi, 0),
            new Vector3(0, -1,  phi), new Vector3(0,  1,  phi),
            new Vector3(0, -1, -phi), new Vector3(0,  1, -phi),
            new Vector3( phi, 0, -1), new Vector3( phi, 0,  1),
            new Vector3(-phi, 0, -1), new Vector3(-phi, 0,  1),
        };
        var v = new Vector3[raw.Length];
        for (int i = 0; i < raw.Length; i++) v[i] = Vector3.Normalize(raw[i]) * 1.1f;
        var faces = new[]
        {
            new[] {0,11,5}, new[] {0,5,1}, new[] {0,1,7}, new[] {0,7,10}, new[] {0,10,11},
            new[] {1,5,9}, new[] {5,11,4}, new[] {11,10,2}, new[] {10,7,6}, new[] {7,1,8},
            new[] {3,9,4}, new[] {3,4,2}, new[] {3,2,6}, new[] {3,6,8}, new[] {3,8,9},
            new[] {4,9,5}, new[] {2,4,11}, new[] {6,2,10}, new[] {8,6,7}, new[] {9,8,1},
        };
        return new WireMesh(v, EdgesFromFaces(faces));
    }

    static WireMesh StellatedOctahedron()
    {
        var v = new[]
        {
            new Vector3( 1,  1,  1), new Vector3( 1, -1, -1),
            new Vector3(-1,  1, -1), new Vector3(-1, -1,  1),
            new Vector3(-1, -1, -1), new Vector3(-1,  1,  1),
            new Vector3( 1, -1,  1), new Vector3( 1,  1, -1),
        };
        var faces = new[]
        {
            new[] {0,1,2}, new[] {0,3,1}, new[] {0,2,3}, new[] {1,3,2},
            new[] {4,5,6}, new[] {4,7,5}, new[] {4,6,7}, new[] {5,7,6},
        };
        return new WireMesh(v, EdgesFromFaces(faces));
    }

    /// <summary>Tesseract — 4D hypercube projected through W to 3D.</summary>
    static WireMesh Tesseract()
    {
        // 16 4D vertices at (±1, ±1, ±1, ±1).
        var v4 = new (float X, float Y, float Z, float W)[16];
        for (int i = 0; i < 16; i++)
        {
            v4[i] = (
                (i & 1) == 0 ? -1 : 1,
                (i & 2) == 0 ? -1 : 1,
                (i & 4) == 0 ? -1 : 1,
                (i & 8) == 0 ? -1 : 1);
        }

        // Pre-rotate in the XW plane so the 4D structure isn't flat-projected.
        // A constant skew yields a recognisable hypercube silhouette; the
        // outer rotation (Y/X pitch in DrawShape) keeps it moving.
        const float xwAngle = 0.55f;
        float cw = MathF.Cos(xwAngle), sw = MathF.Sin(xwAngle);

        // 4D → 3D via inverse W projection: scale = 2 / (2 - w).
        var v3 = new Vector3[16];
        for (int i = 0; i < 16; i++)
        {
            var p = v4[i];
            float xr = p.X * cw - p.W * sw;
            float wr = p.X * sw + p.W * cw;
            float s = 2f / (2f - wr);
            v3[i] = new Vector3(xr * s * 0.8f, p.Y * s * 0.8f, p.Z * s * 0.8f);
        }

        // Edges: vertices that differ in exactly one bit.
        var edges = new List<(int, int)>();
        for (int i = 0; i < 16; i++)
            for (int j = i + 1; j < 16; j++)
                if (BitOperations.PopCount((uint)(i ^ j)) == 1)
                    edges.Add((i, j));
        return new WireMesh(v3, edges.ToArray());
    }

    /// <summary>One-sheet hyperboloid lattice: r = cosh(v), y = sinh(v).</summary>
    static WireMesh Hyperboloid()
    {
        const int U = 14;   // around the y-axis
        const int V = 8;    // up the y-axis
        var v = new Vector3[U * V];
        for (int j = 0; j < V; j++)
        {
            float vp = (j / (float)(V - 1)) * 1.6f - 0.8f;
            float r = MathF.Cosh(vp);
            float y = MathF.Sinh(vp);
            for (int i = 0; i < U; i++)
            {
                float u = i * MathF.Tau / U;
                v[j * U + i] = new Vector3(r * MathF.Cos(u) * 0.7f, y * 0.9f, r * MathF.Sin(u) * 0.7f);
            }
        }

        var edges = new List<(int, int)>();
        for (int j = 0; j < V; j++)
            for (int i = 0; i < U; i++)
            {
                int a = j * U + i;
                int b = j * U + ((i + 1) % U);
                edges.Add((a, b));                              // horizontal ring
                if (j + 1 < V) edges.Add((a, (j + 1) * U + i)); // vertical meridian
            }
        return new WireMesh(v, edges.ToArray());
    }

    /// <summary>Trefoil torus knot (p=2, q=3) sampled as a closed polyline.</summary>
    static WireMesh TorusKnot()
    {
        const int Samples = 220;
        const float R = 0.85f;
        const float r = 0.32f;
        const int p = 2;
        const int q = 3;

        var v = new Vector3[Samples];
        for (int i = 0; i < Samples; i++)
        {
            float t = i * MathF.Tau / Samples;
            float cosqt = MathF.Cos(q * t);
            v[i] = new Vector3(
                (R + r * MathF.Cos(p * t)) * cosqt,
                r * MathF.Sin(p * t),
                (R + r * MathF.Cos(p * t)) * MathF.Sin(q * t));
        }
        var edges = new (int, int)[Samples];
        for (int i = 0; i < Samples; i++) edges[i] = (i, (i + 1) % Samples);
        return new WireMesh(v, edges);
    }
}
