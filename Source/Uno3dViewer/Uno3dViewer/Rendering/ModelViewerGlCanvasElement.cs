using System;
using System.Numerics;
using Silk.NET.OpenGL;
using Uno.WinUI.Graphics3DGL;

namespace Uno3dViewer.Rendering;

public sealed class ModelViewerGlCanvasElement : GLCanvasElement
{
    private Shader? _shader;
    private Model? _model;
    private Texture? _whiteTexture;
    private string? _pendingPath;

    public Camera Camera { get; } = new();

    public event Action<string>? ModelLoaded;
    public event Action<Exception>? LoadFailed;

    public ModelViewerGlCanvasElement() : base(() => App.MainWindow!)
    {
        Camera.Changed += () => Invalidate();
    }

    public void LoadModel(string path)
    {
        _pendingPath = path;
        Invalidate();
    }

    public void FitToView()
    {
        if (_model is null) return;
        Camera.FitToBounds(_model.BoundsMin, _model.BoundsMax);
    }

    public void SetStandardView(StandardView v)
    {
        if (_model is null) return;
        Camera.SetStandardView(v, _model.BoundsMin, _model.BoundsMax);
    }

    protected override void Init(GL gl)
    {
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Back);
        gl.FrontFace(FrontFaceDirection.Ccw);

        var sl = gl.GetStringS(StringName.ShadingLanguageVersion);
        var ver = sl.Contains("OpenGL ES", StringComparison.InvariantCultureIgnoreCase)
            ? "#version 300 es"
            : "#version 330";

        var vs = $$"""
            {{ver}}
            precision highp float;
            layout(location = 0) in vec3 aPos;
            layout(location = 1) in vec3 aNormal;
            layout(location = 2) in vec2 aUV;
            uniform mat4 uView;
            uniform mat4 uProj;
            out vec3 vNormal;
            out vec2 vUV;
            void main()
            {
                vNormal = aNormal;
                vUV = aUV;
                gl_Position = uProj * uView * vec4(aPos, 1.0);
            }
            """;

        var fs = $$"""
            {{ver}}
            precision highp float;
            in vec3 vNormal;
            in vec2 vUV;
            out vec4 outColor;
            uniform vec3 uLightDir;
            uniform vec3 uBaseColor;
            uniform sampler2D uDiffuse;
            void main()
            {
                vec3 N = normalize(vNormal);
                vec3 L = normalize(-uLightDir);
                float wrap = max(dot(N, L) * 0.5 + 0.5, 0.0);
                vec3 albedo = pow(uBaseColor, vec3(2.2)) * texture(uDiffuse, vUV).rgb;
                vec3 col = albedo * mix(0.85, 1.0, wrap);
                col = pow(col, vec3(1.0 / 2.2));
                outColor = vec4(col, 1.0);
            }
            """;

        _shader = new Shader(gl, vs, fs);
        _shader.Use();
        _shader.SetInt("uDiffuse", 0);

        _whiteTexture = Texture.CreateWhite(gl);

        _model = Model.CreateCube(gl);
        Camera.FitToBounds(_model.BoundsMin, _model.BoundsMax);
    }

    protected override void OnDestroy(GL gl)
    {
        _shader?.Dispose();
        _model?.Dispose();
        _whiteTexture?.Dispose();
        _shader = null;
        _model = null;
        _whiteTexture = null;
    }

    protected override void RenderOverride(GL gl)
    {
        if (_pendingPath is { } pendingPath)
        {
            _pendingPath = null;
            try
            {
                var loaded = Model.LoadFromFile(gl, pendingPath);
                _model?.Dispose();
                _model = loaded;
                Camera.FitToBounds(_model.BoundsMin, _model.BoundsMax);
                ModelLoaded?.Invoke(pendingPath);
            }
            catch (Exception ex)
            {
                LoadFailed?.Invoke(ex);
            }
        }

        gl.ClearColor(0.08f, 0.10f, 0.13f, 1f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_shader is null || _model is null || _whiteTexture is null) return;

        float aspect = (float)(ActualWidth / Math.Max(1.0, ActualHeight));
        _shader.Use();
        _shader.SetMatrix("uView", Camera.GetViewMatrix());
        _shader.SetMatrix("uProj", Camera.GetProjectionMatrix(aspect));
        _shader.SetVec3("uLightDir", Vector3.Normalize(new Vector3(-0.4f, -1f, -0.3f)));

        foreach (var mesh in _model.Meshes)
        {
            _shader.SetVec3("uBaseColor", mesh.DiffuseColor);
            (mesh.DiffuseTexture ?? _whiteTexture).Bind(0);
            mesh.Draw();
        }
    }
}
