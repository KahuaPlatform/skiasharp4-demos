using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SkiaSharp;

namespace KahuaNetwork.Engine;

internal sealed class SceneRenderer
{
    public City City { get; }
    public Camera3D Camera { get; }
    public ParticleSystem Particles { get; } = new();
    public WowEffect Wow { get; }

    public float HoveredId { get; set; } = -1;
    public Building? SelectedBuilding { get; private set; }
    public Building? HoveredBuilding { get; private set; }
    public bool ShowGrid { get; set; } = true;
    public double Time { get; private set; }
    public float ViewportWidth => Camera.ViewportWidth;
    public float ViewportHeight => Camera.ViewportHeight;
    public CameraDirector Director { get; }

    public SceneRenderer()
    {
        City = City.Generate();
        Camera = new Camera3D();
        Director = new CameraDirector(Camera);
        Wow = new WowEffect(this);
    }

    public void Resize(float w, float h)
    {
        Camera.ViewportWidth = MathF.Max(2, w);
        Camera.ViewportHeight = MathF.Max(2, h);
    }

    public void Update(float dt)
    {
        Time += dt;
        Director.Update(dt);
        Wow.Update(dt);

        // Ambient sparks
        if (Particles.Count < 1200 && Wow.State == WowState.Idle)
            Particles.EmitAmbient(Vector3.Zero, 700f, 6);

        // Telemetry and risk storms per building
        foreach (var b in City.Buildings)
        {
            // Smoothly relax expand toward 0 unless selected
            float target = b == SelectedBuilding ? 1f : 0f;
            b.ExpandProgress += (target - b.ExpandProgress) * MathF.Min(1f, dt * 4f);
            b.HoverIntensity += ((b == HoveredBuilding ? 1f : 0f) - b.HoverIntensity) * MathF.Min(1f, dt * 6f);

            // Telemetry pulse emit
            double pulse = b.Pulse(Time);
            int emitCount = (int)(pulse * 2 + b.ExpandProgress * 4);
            if (emitCount > 0 && Wow.State == WowState.Idle)
                Particles.EmitTelemetry(b, emitCount);

            // Risk storm (more intense if selected and risky)
            if (b.Risk > 0.55 && Wow.State == WowState.Idle)
            {
                int storm = (int)((b.Risk - 0.5) * 18 + b.ExpandProgress * 12);
                if (storm > 0) Particles.EmitRiskStorm(b, storm);
            }
        }

        Particles.Update(dt);
    }

    public void Render(SKCanvas canvas)
    {
        canvas.Clear(Theme.BackgroundDeep);

        // Background nebula
        DrawSkyGradient(canvas);

        if (Wow.State == WowState.Idle || Wow.State == WowState.Reforming)
        {
            if (ShowGrid) DrawGroundGrid(canvas);
            DrawBuildings(canvas);
            DrawDataStreams(canvas);
        }

        Particles.Render(canvas, Camera);

        Wow.Render(canvas);

        DrawScanlines(canvas);
        DrawVignette(canvas);
    }

    private void DrawSkyGradient(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(ViewportWidth * 0.5f, ViewportHeight * 0.6f),
                MathF.Max(ViewportWidth, ViewportHeight) * 0.8f,
                new[] { new SKColor(0x0A, 0x14, 0x30), Theme.BackgroundDeep },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, ViewportWidth, ViewportHeight, paint);

        // Distant aurora glow
        using var aurora = new SKPaint
        {
            BlendMode = SKBlendMode.Plus,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, ViewportHeight * 0.4f),
                new SKPoint(ViewportWidth, ViewportHeight * 0.55f),
                new[]
                {
                    Theme.Magenta.WithAlpha(0),
                    Theme.Magenta.WithAlpha(25),
                    Theme.Cyan.WithAlpha(35),
                    Theme.Violet.WithAlpha(25),
                    Theme.Cyan.WithAlpha(0),
                },
                new[] { 0f, 0.25f, 0.5f, 0.75f, 1f },
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, ViewportHeight * 0.35f, ViewportWidth, ViewportHeight * 0.35f, aurora);
    }

    private void DrawGroundGrid(SKCanvas canvas)
    {
        // Animated procedural grid drawn in world XZ plane
        const float gridStep = 100f;
        const int range = 12;
        var farColor = Theme.GridFar;
        var nearColor = Theme.GridNear;

        using var linePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            BlendMode = SKBlendMode.Plus,
        };

        float anim = (float)((Time * 12) % gridStep);

        for (int i = -range; i <= range; i++)
        {
            float zw = i * gridStep + anim;
            DrawWorldLine(canvas, linePaint,
                new Vector3(-range * gridStep, 0, zw),
                new Vector3(range * gridStep, 0, zw),
                FadeAlpha(farColor, nearColor, MathF.Abs(zw) / (range * gridStep)));
        }
        for (int i = -range; i <= range; i++)
        {
            float xw = i * gridStep;
            DrawWorldLine(canvas, linePaint,
                new Vector3(xw, 0, -range * gridStep),
                new Vector3(xw, 0, range * gridStep),
                FadeAlpha(farColor, nearColor, MathF.Abs(xw) / (range * gridStep)));
        }
    }

    private SKColor FadeAlpha(SKColor far, SKColor near, float t)
    {
        t = MathF.Min(1, t);
        var c = Theme.Lerp(near, far, t);
        return c.WithAlpha((byte)(200 * (1 - t * 0.7f)));
    }

    private void DrawWorldLine(SKCanvas canvas, SKPaint paint, Vector3 a, Vector3 b, SKColor color)
    {
        if (!Camera.Project(a, out var sa, out _)) return;
        if (!Camera.Project(b, out var sb, out _)) return;
        paint.Color = color;
        canvas.DrawLine(sa, sb, paint);
    }

    private void DrawBuildings(SKCanvas canvas)
    {
        // Project, depth sort back-to-front
        var sorted = new List<(Building b, float d)>(City.Buildings.Count);
        foreach (var b in City.Buildings)
        {
            if (!Camera.Project(b.GroundCenter, out _, out var d)) continue;
            sorted.Add((b, d));
        }
        sorted.Sort((x, y) => y.d.CompareTo(x.d));

        foreach (var (b, _) in sorted)
            DrawBuilding(canvas, b);
    }

    private void DrawBuilding(SKCanvas canvas, Building b)
    {
        float halfW = b.Width * 0.5f * (1f + b.ExpandProgress * 0.4f);
        float halfD = b.Depth * 0.5f * (1f + b.ExpandProgress * 0.4f);
        float topY = b.Height * (1f + b.ExpandProgress * 1.2f);

        // 8 corners
        Span<Vector3> corners = stackalloc Vector3[8];
        corners[0] = b.GroundCenter + new Vector3(-halfW, 0, -halfD);
        corners[1] = b.GroundCenter + new Vector3(halfW, 0, -halfD);
        corners[2] = b.GroundCenter + new Vector3(halfW, 0, halfD);
        corners[3] = b.GroundCenter + new Vector3(-halfW, 0, halfD);
        corners[4] = b.GroundCenter + new Vector3(-halfW, topY, -halfD);
        corners[5] = b.GroundCenter + new Vector3(halfW, topY, -halfD);
        corners[6] = b.GroundCenter + new Vector3(halfW, topY, halfD);
        corners[7] = b.GroundCenter + new Vector3(-halfW, topY, halfD);

        Span<SKPoint> proj = stackalloc SKPoint[8];
        Span<bool> ok = stackalloc bool[8];
        for (int i = 0; i < 8; i++)
            ok[i] = Camera.Project(corners[i], out proj[i], out _);

        // Ground halo
        if (ok[0] && ok[1] && ok[2] && ok[3])
        {
            var centerScreen = new SKPoint(
                (proj[0].X + proj[1].X + proj[2].X + proj[3].X) * 0.25f,
                (proj[0].Y + proj[1].Y + proj[2].Y + proj[3].Y) * 0.25f);
            float haloRadius = MathF.Max(
                MathF.Abs(proj[0].X - proj[2].X),
                MathF.Abs(proj[1].X - proj[3].X)) * 0.9f;
            using var halo = new SKPaint
            {
                IsAntialias = true,
                BlendMode = SKBlendMode.Plus,
                Shader = SKShader.CreateRadialGradient(
                    centerScreen, haloRadius,
                    new[]
                    {
                        b.BaseColor.WithAlpha((byte)(140 * (0.5 + b.HoverIntensity * 0.5))),
                        b.BaseColor.WithAlpha(0),
                    },
                    new[] { 0f, 1f },
                    SKShaderTileMode.Clamp),
            };
            canvas.DrawCircle(centerScreen, haloRadius, halo);
        }

        // Side faces — fill with gradient (top brighter)
        var pulse = (float)b.Pulse(Time);
        var fillColor = b.BaseColor;
        // Glassmorphism-style fill
        DrawFace(canvas, proj, ok, 0, 1, 5, 4, fillColor, pulse, b);
        DrawFace(canvas, proj, ok, 1, 2, 6, 5, fillColor, pulse, b);
        DrawFace(canvas, proj, ok, 2, 3, 7, 6, fillColor, pulse, b);
        DrawFace(canvas, proj, ok, 3, 0, 4, 7, fillColor, pulse, b);

        // Top cap
        if (ok[4] && ok[5] && ok[6] && ok[7])
        {
            using var topPath = new SKPath();
            topPath.MoveTo(proj[4]);
            topPath.LineTo(proj[5]);
            topPath.LineTo(proj[6]);
            topPath.LineTo(proj[7]);
            topPath.Close();
            using var topFill = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = fillColor.WithAlpha((byte)(180 + pulse * 50)),
                BlendMode = SKBlendMode.Plus,
            };
            canvas.DrawPath(topPath, topFill);

            using var topStroke = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                Color = fillColor.WithAlpha(255),
                BlendMode = SKBlendMode.Plus,
            };
            canvas.DrawPath(topPath, topStroke);
        }

        // Edges (wireframe)
        DrawEdge(canvas, proj, ok, 0, 1, fillColor, 0.35f);
        DrawEdge(canvas, proj, ok, 1, 2, fillColor, 0.35f);
        DrawEdge(canvas, proj, ok, 2, 3, fillColor, 0.35f);
        DrawEdge(canvas, proj, ok, 3, 0, fillColor, 0.35f);
        DrawEdge(canvas, proj, ok, 0, 4, fillColor, 0.8f);
        DrawEdge(canvas, proj, ok, 1, 5, fillColor, 0.8f);
        DrawEdge(canvas, proj, ok, 2, 6, fillColor, 0.8f);
        DrawEdge(canvas, proj, ok, 3, 7, fillColor, 0.8f);
        DrawEdge(canvas, proj, ok, 4, 5, fillColor, 1.0f);
        DrawEdge(canvas, proj, ok, 5, 6, fillColor, 1.0f);
        DrawEdge(canvas, proj, ok, 6, 7, fillColor, 1.0f);
        DrawEdge(canvas, proj, ok, 7, 4, fillColor, 1.0f);

        // Selected outline
        if (b.ExpandProgress > 0.05f)
        {
            DrawSelectionHalo(canvas, proj, ok, b);
        }

        // Apex backlog marker
        if (b.Risk > 0.4 && Camera.Project(b.ApexCenter + new Vector3(0, 18, 0), out var apex, out _))
        {
            var riskColor = Theme.RiskColor(b.Risk);
            using var p1 = new SKPaint
            {
                IsAntialias = true,
                Color = riskColor.WithAlpha(90),
                BlendMode = SKBlendMode.Plus,
            };
            canvas.DrawCircle(apex, 12f + pulse * 4f, p1);
            using var p2 = new SKPaint
            {
                IsAntialias = true,
                Color = riskColor.WithAlpha(255),
            };
            canvas.DrawCircle(apex, 3f, p2);
        }

        // Role tag floating above the building
        DrawRoleTag(canvas, b);
    }

    private void DrawRoleTag(SKCanvas canvas, Building b)
    {
        var tagWorld = b.ApexCenter + new Vector3(0, 30 + b.ExpandProgress * 12, 0);
        if (!Camera.Project(tagWorld, out var s, out var depth)) return;
        // Hide tiny far tags
        float scale = 1f - MathF.Min(0.95f, depth);
        if (scale < 0.05f) return;
        // Don't show role tag when in topology mode
        if (Wow.State != WowState.Idle) return;

        string tag = b.Role.Tag();
        using var font = new SKFont(SKTypeface.Default, 9f + scale * 3f) { Embolden = true };
        float tw = font.MeasureText(tag) + 10;
        float th = 14 + scale * 3f;
        var rect = new SKRect(s.X - tw / 2f, s.Y - th / 2f, s.X + tw / 2f, s.Y + th / 2f);
        using var fill = new SKPaint
        {
            IsAntialias = true,
            Color = b.BaseColor.WithAlpha(70),
        };
        canvas.DrawRoundRect(rect, 3, 3, fill);
        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.8f,
            Color = b.BaseColor,
            BlendMode = SKBlendMode.Plus,
        };
        canvas.DrawRoundRect(rect, 3, 3, stroke);
        using var paint = new SKPaint { IsAntialias = true, Color = b.BaseColor };
        canvas.DrawText(tag, s.X - tw / 2f + 5, s.Y + 4, SKTextAlign.Left, font, paint);
    }

    private static void DrawFace(SKCanvas canvas, Span<SKPoint> proj, Span<bool> ok,
        int a, int b, int c, int d, SKColor color, float pulse, Building bldg)
    {
        if (!ok[a] || !ok[b] || !ok[c] || !ok[d]) return;
        using var path = new SKPath();
        path.MoveTo(proj[a]);
        path.LineTo(proj[b]);
        path.LineTo(proj[c]);
        path.LineTo(proj[d]);
        path.Close();

        // Vertical gradient from base (semi-transparent dark) to top (color pulse)
        using var fill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                proj[a],
                proj[d], // top point
                new[]
                {
                    color.WithAlpha((byte)(40 + pulse * 30)),
                    color.WithAlpha((byte)(120 + pulse * 80 + bldg.HoverIntensity * 40)),
                },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp),
            BlendMode = SKBlendMode.Plus,
        };
        canvas.DrawPath(path, fill);

        // Window light streaks
        DrawWindowStreaks(canvas, proj, ok, a, b, c, d, color, bldg, pulse);
    }

    private static void DrawWindowStreaks(SKCanvas canvas, Span<SKPoint> proj, Span<bool> ok,
        int a, int b, int c, int d, SKColor color, Building bldg, float pulse)
    {
        // Draw horizontal streaks interpolating between bottom edge (a-b) and top edge (d-c)
        if (!ok[a] || !ok[b] || !ok[c] || !ok[d]) return;
        int floors = (int)(bldg.Height / 14f);
        floors = Math.Clamp(floors, 4, 30);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = color.WithAlpha((byte)(110 + pulse * 80)),
            BlendMode = SKBlendMode.Plus,
        };
        for (int f = 1; f <= floors; f++)
        {
            float t = f / (float)(floors + 1);
            var p1 = new SKPoint(
                proj[a].X + (proj[d].X - proj[a].X) * t,
                proj[a].Y + (proj[d].Y - proj[a].Y) * t);
            var p2 = new SKPoint(
                proj[b].X + (proj[c].X - proj[b].X) * t,
                proj[b].Y + (proj[c].Y - proj[b].Y) * t);
            // Vary brightness like animated windows
            byte alpha = (byte)(40 + ((f * 73 + (int)(bldg.TelemetryPhase * 100)) % 180) * (0.5f + pulse * 0.5f));
            paint.Color = color.WithAlpha(alpha);
            canvas.DrawLine(p1, p2, paint);
        }
    }

    private static void DrawEdge(SKCanvas canvas, Span<SKPoint> proj, Span<bool> ok,
        int a, int b, SKColor color, float intensity)
    {
        if (!ok[a] || !ok[b]) return;
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f,
            Color = color.WithAlpha((byte)(220 * intensity)),
            BlendMode = SKBlendMode.Plus,
        };
        canvas.DrawLine(proj[a], proj[b], paint);

        using var glow = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4f,
            Color = color.WithAlpha((byte)(60 * intensity)),
            BlendMode = SKBlendMode.Plus,
        };
        canvas.DrawLine(proj[a], proj[b], glow);
    }

    private static void DrawSelectionHalo(SKCanvas canvas, Span<SKPoint> proj, Span<bool> ok, Building b)
    {
        // Find max screen Y span (base) and min (top)
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            if (!ok[i]) return;
            minX = MathF.Min(minX, proj[i].X);
            maxX = MathF.Max(maxX, proj[i].X);
            minY = MathF.Min(minY, proj[i].Y);
            maxY = MathF.Max(maxY, proj[i].Y);
        }
        var rect = new SKRect(minX - 10, minY - 10, maxX + 10, maxY + 10);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = Theme.Cyan.WithAlpha((byte)(220 * b.ExpandProgress)),
            BlendMode = SKBlendMode.Plus,
            PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0),
        };
        canvas.DrawRoundRect(rect, 6, 6, paint);
    }

    private void DrawDataStreams(SKCanvas canvas)
    {
        foreach (var s in City.DataStreams)
            s.Render(canvas, Camera, Time);
    }

    private void DrawScanlines(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(18),
            BlendMode = SKBlendMode.Multiply,
        };
        for (float y = 0; y < ViewportHeight; y += 3f)
        {
            canvas.DrawRect(0, y, ViewportWidth, 1f, paint);
        }
    }

    private void DrawVignette(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(ViewportWidth * 0.5f, ViewportHeight * 0.5f),
                MathF.Max(ViewportWidth, ViewportHeight) * 0.7f,
                new[] { SKColors.Transparent, new SKColor(0, 0, 0, 200) },
                new[] { 0.55f, 1f },
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(0, 0, ViewportWidth, ViewportHeight, paint);
    }

    public Building? PickBuilding(SKPoint screen)
    {
        // Hit-test against each building's projected silhouette bounding box.
        // Among buildings that contain the click point, pick the one with the
        // smallest center depth (frontmost).
        Building? best = null;
        float bestDepth = float.PositiveInfinity;
        Span<Vector3> corners = stackalloc Vector3[8];
        Span<SKPoint> proj = stackalloc SKPoint[8];

        foreach (var b in City.Buildings)
        {
            float halfW = b.Width * 0.5f * (1f + b.ExpandProgress * 0.4f);
            float halfD = b.Depth * 0.5f * (1f + b.ExpandProgress * 0.4f);
            float topY = b.Height * (1f + b.ExpandProgress * 1.2f);

            corners[0] = b.GroundCenter + new Vector3(-halfW, 0, -halfD);
            corners[1] = b.GroundCenter + new Vector3(halfW, 0, -halfD);
            corners[2] = b.GroundCenter + new Vector3(halfW, 0, halfD);
            corners[3] = b.GroundCenter + new Vector3(-halfW, 0, halfD);
            corners[4] = b.GroundCenter + new Vector3(-halfW, topY, -halfD);
            corners[5] = b.GroundCenter + new Vector3(halfW, topY, -halfD);
            corners[6] = b.GroundCenter + new Vector3(halfW, topY, halfD);
            corners[7] = b.GroundCenter + new Vector3(-halfW, topY, halfD);

            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            int valid = 0;
            for (int i = 0; i < 8; i++)
            {
                if (!Camera.Project(corners[i], out proj[i], out _)) continue;
                valid++;
                if (proj[i].X < minX) minX = proj[i].X;
                if (proj[i].X > maxX) maxX = proj[i].X;
                if (proj[i].Y < minY) minY = proj[i].Y;
                if (proj[i].Y > maxY) maxY = proj[i].Y;
            }
            if (valid < 4) continue;

            const float pad = 4f;
            if (screen.X < minX - pad || screen.X > maxX + pad ||
                screen.Y < minY - pad || screen.Y > maxY + pad) continue;

            // Use depth of building center to break ties: prefer frontmost
            if (!Camera.Project(b.GroundCenter + new Vector3(0, b.Height * 0.5f, 0),
                                out _, out float centerDepth))
                continue;
            if (centerDepth < bestDepth)
            {
                bestDepth = centerDepth;
                best = b;
            }
        }
        return best;
    }

    public void SetHover(SKPoint? screen)
    {
        HoveredBuilding = screen.HasValue ? PickBuilding(screen.Value) : null;
    }

    public void SelectAt(SKPoint screen)
    {
        var b = PickBuilding(screen);
        Select(b);
    }

    public void Select(Building? b)
    {
        if (SelectedBuilding == b) return;
        SelectedBuilding = b;
        if (b != null)
        {
            // Eruption of celebration/risk particles
            Particles.EmitBurst(b.GroundCenter + new Vector3(0, b.Height * 0.5f, 0),
                Theme.RiskColor(b.Risk), 80, 70f, 2f, 1.8f);
            Director.FocusOn(b);
        }
        else
        {
            Director.ResumeIdle();
        }
    }

    public void TriggerGlobalView()
    {
        Select(null);
        Wow.Trigger();
    }
}
