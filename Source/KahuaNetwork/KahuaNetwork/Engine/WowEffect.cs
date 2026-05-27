using System;
using System.Collections.Generic;
using System.Numerics;
using SkiaSharp;

namespace KahuaNetwork.Engine;

internal enum WowState
{
    Idle,
    Exploding,
    Topology,
    Reforming,
}

internal sealed class WowEffect
{
    private readonly SceneRenderer _scene;
    private readonly List<TopologyNode> _nodes = new();
    private readonly List<(int a, int b)> _edges = new();
    private readonly Random _rng = new(11);

    public WowState State { get; private set; } = WowState.Idle;
    public float StateTime { get; private set; }
    public float ExplodeDuration { get; set; } = 1.6f;
    public float TopologyHold { get; set; } = 4.5f;
    public float ReformDuration { get; set; } = 1.6f;

    public WowEffect(SceneRenderer scene)
    {
        _scene = scene;
    }

    public void Trigger()
    {
        if (State != WowState.Idle) return;
        StateTime = 0;
        State = WowState.Exploding;
        BuildTopology();
        // Stash original building positions in particles
        _scene.Particles.Clear();
        foreach (var b in _scene.City.Buildings)
        {
            // Spawn one particle per "voxel" representing the building
            int columns = (int)(b.Width / 8f);
            int rows = (int)(b.Height / 10f);
            int depths = (int)(b.Depth / 8f);
            columns = Math.Clamp(columns, 3, 10);
            rows = Math.Clamp(rows, 4, 18);
            depths = Math.Clamp(depths, 3, 10);
            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
            for (int z = 0; z < depths; z++)
            {
                float fx = x / (float)(columns - 1) - 0.5f;
                float fy = y / (float)(rows - 1);
                float fz = z / (float)(depths - 1) - 0.5f;
                var origin = b.GroundCenter + new Vector3(fx * b.Width, fy * b.Height, fz * b.Depth);
                // Pick a topology target
                var target = _nodes[_rng.Next(_nodes.Count)].Position;
                _scene.Particles.Emit(new Particle
                {
                    Position = origin,
                    Target = target,
                    Color = b.BaseColor,
                    Velocity = Vector3.Zero,
                    Life = float.PositiveInfinity,
                    MaxLife = 1,
                    Size = 1.6f,
                    Kind = ParticleKind.Burst,
                    Drag = 1f,
                    Gravity = Vector3.Zero,
                });
            }
        }
    }

    private void BuildTopology()
    {
        _nodes.Clear();
        _edges.Clear();
        // Arrange a ring of nodes overlayed on the camera focus area
        int nodeCount = 14;
        float radius = 360f;
        for (int i = 0; i < nodeCount; i++)
        {
            float a = i / (float)nodeCount * MathF.PI * 2;
            // Stagger heights
            float h = 180f + MathF.Sin(a * 2.0f) * 70f;
            _nodes.Add(new TopologyNode
            {
                Position = new Vector3(MathF.Cos(a) * radius, h, MathF.Sin(a) * radius),
                Color = i % 3 == 0 ? Theme.Cyan : (i % 3 == 1 ? Theme.Magenta : Theme.Lime),
            });
        }
        // Connect each to its neighbors and a few diagonals
        for (int i = 0; i < _nodes.Count; i++)
        {
            _edges.Add((i, (i + 1) % _nodes.Count));
            _edges.Add((i, (i + _nodes.Count / 3) % _nodes.Count));
        }
    }

    public void Update(float dt)
    {
        StateTime += dt;
        switch (State)
        {
            case WowState.Idle: break;
            case WowState.Exploding:
                AnimateExploding(dt);
                if (StateTime >= ExplodeDuration)
                {
                    StateTime = 0;
                    State = WowState.Topology;
                }
                break;
            case WowState.Topology:
                AnimateTopology(dt);
                if (StateTime >= TopologyHold)
                {
                    StateTime = 0;
                    State = WowState.Reforming;
                }
                break;
            case WowState.Reforming:
                AnimateReforming(dt);
                if (StateTime >= ReformDuration)
                {
                    StateTime = 0;
                    State = WowState.Idle;
                    _scene.Particles.Clear();
                }
                break;
        }
    }

    private void AnimateExploding(float dt)
    {
        // Move each particle toward its target with easing
        float t = MathF.Min(1f, StateTime / ExplodeDuration);
        float ease = 1f - MathF.Pow(1f - t, 3f);
        var snap = _scene.Particles.Snapshot();
        for (int i = 0; i < snap.Count; i++)
        {
            var p = snap[i];
            // Mark particle pos via velocity-less direct lerp from saved origin
            // Hack: we don't store origin; treat current pos as origin if velocity is zero
            // Better: store origin in Gravity? Let's just push toward target
            var toTarget = p.Target - p.Position;
            p.Position += toTarget * MathF.Min(1f, dt * 6f * ease);
            // Indexer not available; need mutable list — adjust via reflection-less approach
            _scene.Particles.Replace(i, p);
        }
    }

    private void AnimateTopology(float dt)
    {
        // Particles orbit gently around their target node
        var snap = _scene.Particles.Snapshot();
        for (int i = 0; i < snap.Count; i++)
        {
            var p = snap[i];
            var toTarget = p.Target - p.Position;
            var len = toTarget.Length();
            if (len > 4f)
                p.Position += Vector3.Normalize(toTarget) * dt * 30f;
            // Add gentle tangential motion
            var ortho = new Vector3(-toTarget.Z, 0, toTarget.X);
            if (ortho.Length() > 0.1f)
                p.Position += Vector3.Normalize(ortho) * dt * 5f;
            _scene.Particles.Replace(i, p);
        }
    }

    private void AnimateReforming(float dt)
    {
        // Reassemble — fly back to nearest building origin
        var snap = _scene.Particles.Snapshot();
        for (int i = 0; i < snap.Count; i++)
        {
            var p = snap[i];
            // Pick (deterministically) a building based on index
            var b = _scene.City.Buildings[i % _scene.City.Buildings.Count];
            float fy = (i * 0.137f) % 1f;
            float fx = ((i * 0.213f) % 1f) - 0.5f;
            float fz = ((i * 0.317f) % 1f) - 0.5f;
            var dest = b.GroundCenter + new Vector3(fx * b.Width, fy * b.Height, fz * b.Depth);
            var to = dest - p.Position;
            p.Position += to * MathF.Min(1f, dt * 8f);
            _scene.Particles.Replace(i, p);
        }
    }

    public void Render(SKCanvas canvas)
    {
        if (State == WowState.Topology || (State == WowState.Exploding && StateTime > ExplodeDuration * 0.6f))
        {
            DrawTopologyEdges(canvas);
            DrawTopologyNodes(canvas);
            DrawTopologyTitle(canvas);
        }
    }

    private void DrawTopologyEdges(SKCanvas canvas)
    {
        foreach (var (a, b) in _edges)
        {
            if (!_scene.Camera.Project(_nodes[a].Position, out var sa, out _)) continue;
            if (!_scene.Camera.Project(_nodes[b].Position, out var sb, out _)) continue;
            using var glow = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 6f,
                Color = Theme.Cyan.WithAlpha(50),
                BlendMode = SKBlendMode.Plus,
            };
            canvas.DrawLine(sa, sb, glow);
            using var line = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.4f,
                Color = Theme.Cyan.WithAlpha(220),
                BlendMode = SKBlendMode.Plus,
            };
            canvas.DrawLine(sa, sb, line);
        }
    }

    private void DrawTopologyNodes(SKCanvas canvas)
    {
        foreach (var n in _nodes)
        {
            if (!_scene.Camera.Project(n.Position, out var s, out var d)) continue;
            float size = (1f - MathF.Min(0.95f, d)) * 18f + 6f;
            using var glow = new SKPaint
            {
                IsAntialias = true,
                Color = n.Color.WithAlpha(120),
                BlendMode = SKBlendMode.Plus,
            };
            canvas.DrawCircle(s, size * 2.5f, glow);
            using var core = new SKPaint
            {
                IsAntialias = true,
                Color = n.Color,
            };
            canvas.DrawCircle(s, size * 0.6f, core);
        }
    }

    private void DrawTopologyTitle(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = Theme.Cyan,
        };
        using var font = new SKFont(SKTypeface.Default, 28f);
        var text = "// THE KAHUA NETWORK · LIVE TOPOLOGY //";
        float w = font.MeasureText(text);
        canvas.DrawText(text, (_scene.ViewportWidth - w) / 2f, 70f, SKTextAlign.Left, font, paint);
    }
}

internal sealed class TopologyNode
{
    public Vector3 Position;
    public SKColor Color;
}
