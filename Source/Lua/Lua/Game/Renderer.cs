using System;
using System.Collections.Generic;
using System.Diagnostics;
using SkiaSharp;

namespace Lua.Game;

// Tempest-style vector renderer. Reuses the neon-glow chassis from Pohaku/HokuLele
// (background gradient + starfield + marquee + vector glyph font + neon paints)
// and draws the new gameplay elements: 3D well, player claw on rim, climbing
// enemies, segment-bound bullets, spike trails, warp transition.
public static class Renderer
{
    // --- Neon palette ---
    static readonly SKColor PlayerColor       = new(0xFF, 0xEE, 0x44);  // yellow claw — Tempest classic
    static readonly SKColor PlayerCockpit     = new(0xFF, 0xFF, 0xFF);
    static readonly SKColor RimColor          = new(0x33, 0xF8, 0xFF);  // cyan well rim
    static readonly SKColor RimAltColor       = new(0x33, 0x88, 0xFF);  // alt-segment blue
    static readonly SKColor SpokeColor        = new(0x55, 0x55, 0xCC, 0xC0);
    static readonly SKColor InnerRingColor    = new(0x77, 0x44, 0xAA, 0xA0);
    static readonly SKColor BulletColor       = new(0xFF, 0xEE, 0x33);
    static readonly SKColor EnemyBulletColor  = new(0xFF, 0x44, 0x66);
    static readonly SKColor SpikeColor        = new(0x66, 0xFF, 0xAA);
    static readonly SKColor FlipperColor      = new(0xFF, 0x44, 0x66);
    static readonly SKColor TankerColor       = new(0xAA, 0x66, 0xFF);
    static readonly SKColor SpikerColor       = new(0xFF, 0xEE, 0x33);
    static readonly SKColor FuseballColor1    = new(0x55, 0xFF, 0x77);
    static readonly SKColor FuseballColor2    = new(0xFF, 0xFF, 0x55);
    static readonly SKColor HudColor          = new(0x33, 0xF8, 0xFF);
    static readonly SKColor BgTop             = new(0x05, 0x00, 0x14);
    static readonly SKColor BgBottom          = new(0x18, 0x02, 0x36);
    static readonly SKColor WarpColor         = new(0x33, 0xF8, 0xFF, 0xA0);

    // --- Brand marks shown on the title screen ---
    static readonly (SKPath path, SKColor fill)[] UnoLogoPaths =
    {
        (MakeUnoPath("M 34.758,38.865 H 34.746 C 31.892,38.86 29.342,36.882 26.152,33.692 l -6.93,-6.873 2.166,-2.188 6.937,6.88 c 3.075,3.074 4.876,4.272 6.427,4.275 h 0.005 c 1.567,0 3.467,-1.262 6.558,-4.353 l 3.541,-3.587 c 1.784,-1.784 2.57,-3.34 2.408,-4.762 -0.13,-1.156 -0.894,-2.397 -2.401,-3.904 L 44.83,19.146 C 43.202,17.414 41.211,15.483 39.131,14.414 38.745,12.437 37.48,10.881 37.3,10.596 c 3.803,0.559 7.197,3.703 9.758,6.424 2.788,2.794 5.803,7.176 -0.018,12.996 l -3.54,3.588 c -3.251,3.25 -5.844,5.261 -8.742,5.261", 0f, 0f), new SKColor(0x7A, 0x67, 0xF8)),
        (MakeUnoPath("m 25.399,28.608 6.492,-6.562 c 3.076,-3.076 4.274,-4.877 4.276,-6.428 0.004,-1.567 -1.257,-3.469 -4.352,-6.563 L 28.228,5.515 C 24.58,1.867 22.369,2.699 19.561,5.507 L 19.528,5.54 c -1.54,1.448 -3.237,3.182 -4.346,5.01 -1.031,0.073 -2.361,0.424 -3.997,1.518 0.906,-3.397 3.737,-6.422 6.216,-8.755 2.794,-2.789 7.177,-5.804 12.997,0.017 l 3.588,3.54 c 3.255,3.256 5.266,5.851 5.26,8.754 -0.005,2.854 -1.982,5.404 -5.172,8.594 l -6.489,6.559 z", 0f, 0f), new SKColor(0xF8, 0x59, 0x77)),
        (MakeUnoPath("M 12.522,38.707 C 8.939,37.946 5.746,34.972 3.308,32.382 2.035,31.106 0.321,29.13 0.042,26.663 c -0.274,-2.414 0.8,-4.795 3.283,-7.278 l 3.542,-3.588 c 3.25,-3.25 5.843,-5.261 8.74,-5.261 h 0.013 c 2.854,0.005 5.404,1.983 8.593,5.172 l 7.046,6.976 -2.165,2.19 -7.053,-6.983 c -3.076,-3.076 -4.876,-4.273 -6.427,-4.276 h -0.006 c -1.566,0 -3.466,1.261 -6.557,4.352 L 5.51,21.555 c -1.784,1.784 -2.57,3.34 -2.409,4.762 0.131,1.156 0.894,2.396 2.402,3.904 l 0.033,0.034 c 1.55,1.649 3.43,3.479 5.401,4.573 0.168,1.739 1.2,3.297 1.585,3.88", 0f, 0f), new SKColor(0x15, 0x9B, 0xFF)),
        (MakeUnoPath("m 26.32,49.827 c -1.925,0 -4.114,-0.886 -6.557,-3.33 l -3.588,-3.54 C 9.167,35.949 9.151,32.546 16.086,25.61 l 6.802,-6.872 2.193,2.162 -6.812,6.882 c -3.076,3.076 -4.273,4.877 -4.276,6.427 -0.003,1.568 1.258,3.47 4.352,6.563 l 3.588,3.541 c 3.646,3.647 5.858,2.816 8.666,0.008 l 0.034,-0.033 c 1.654,-1.555 3.5,-3.46 4.593,-5.437 1.661,-0.14 2.9,-0.841 3.835,-1.438 -0.8,3.537 -3.738,6.69 -6.302,9.102 -1.62,1.618 -3.777,3.312 -6.439,3.312", 0f, 0f), new SKColor(0x67, 0xE5, 0xAD)),
        (MakeUnoPath("M 1.738,0.156 3.927,2.323 2.347,3.919 0.101,1.81 Z", 21.154f, 18.577f), SKColors.Black),
        (MakeUnoPath("M 2.201,0.066 3.855,1.703 1.69,3.894 0.093,2.311 Z", 27.404f, 20.981f), SKColors.Black),
        (MakeUnoPath("M 2.398,0.044 3.994,1.624 1.886,3.869 0.232,2.232 Z", 18.99f,  24.587f), SKColors.Black),
        (MakeUnoPath("M 1.736,0.023 3.981,2.132 2.344,3.786 0.156,1.619 Z", 25.24f,  26.99f ), SKColors.Black),
    };
    static SKPath MakeUnoPath(string d, float tx, float ty)
    {
        var p = SKPath.ParseSvgPathData(d);
        if (tx != 0f || ty != 0f) p.Transform(SKMatrix.CreateTranslation(tx, ty));
        return p;
    }
    static readonly SKRect UnoLogoBounds = ComputeUnoLogoBounds();
    static SKRect ComputeUnoLogoBounds()
    {
        var b = UnoLogoPaths[0].path.Bounds;
        for (int i = 1; i < UnoLogoPaths.Length; i++)
            b = SKRect.Union(b, UnoLogoPaths[i].path.Bounds);
        return b;
    }

    static readonly SKImage? KahuaSnowflakeImage = LoadEmbeddedImage("Lua.Assets.Icons.kahua_snowflake.png");
    static readonly SKSamplingOptions KahuaSnowflakeSampling = new(SKCubicResampler.Mitchell);
    static SKImage? LoadEmbeddedImage(string resourceName)
    {
        try
        {
            using var stream = typeof(Renderer).Assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return null;
            return SKImage.FromEncodedData(stream);
        }
        catch { return null; }
    }

    // --- Marquee + vector glyph font (reused from Pohaku/HokuLele chassis) ---
    const string MarqueeText = "LUA · UNO PLATFORM · SKIASHARP 4 · NEON TEMPEST DEMO";
    const float MarqueeCharHeight = 56f;
    const float MarqueeCharWidth  = 40f;
    const float MarqueeCharGap    = 12f;
    const float MarqueeSpeed      = 200f;
    static readonly Stopwatch MarqueeClock = Stopwatch.StartNew();

    static readonly SKPaint MarqueeNeonHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 11f, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7f),
    };
    static readonly SKPaint MarqueeNeonSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
    };
    static readonly SKPaint NeonStrokeHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 5.5f, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f),
    };
    static readonly SKPaint NeonStrokeSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
    };
    static readonly SKPaint NeonFillHalo = new()
    {
        Style = SKPaintStyle.Fill, IsAntialias = true,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f),
    };
    static readonly SKPaint NeonFillSharp = new()
    {
        Style = SKPaintStyle.Fill, IsAntialias = true,
    };

    static readonly Dictionary<char, SKPath> Glyphs = BuildGlyphs();

    static Dictionary<char, SKPath> BuildGlyphs()
    {
        float sx = MarqueeCharWidth  / 4f;
        float sy = MarqueeCharHeight / 6f;
        SKPath G(params float[] coords)
        {
            using var b = new SKPathBuilder();
            for (int i = 0; i + 3 < coords.Length; i += 4)
            {
                b.MoveTo(coords[i] * sx, coords[i + 1] * sy);
                b.LineTo(coords[i + 2] * sx, coords[i + 3] * sy);
            }
            return b.Detach();
        }
        return new Dictionary<char, SKPath>
        {
            ['A'] = G(0,6, 2,0,  2,0, 4,6,  1,4, 3,4),
            ['B'] = G(0,0, 0,6,  0,0, 3,0,  3,0, 4,1,  4,1, 4,2,  4,2, 3,3,  0,3, 3,3,  3,3, 4,4,  4,4, 4,5,  4,5, 3,6,  3,6, 0,6),
            ['C'] = G(4,1, 3,0,  3,0, 1,0,  1,0, 0,1,  0,1, 0,5,  0,5, 1,6,  1,6, 3,6,  3,6, 4,5),
            ['D'] = G(0,0, 0,6,  0,0, 3,0,  3,0, 4,1,  4,1, 4,5,  4,5, 3,6,  3,6, 0,6),
            ['E'] = G(0,0, 0,6,  0,0, 4,0,  0,3, 3,3,  0,6, 4,6),
            ['F'] = G(0,0, 0,6,  0,0, 4,0,  0,3, 3,3),
            ['G'] = G(4,1, 3,0,  3,0, 1,0,  1,0, 0,1,  0,1, 0,5,  0,5, 1,6,  1,6, 3,6,  3,6, 4,5,  4,5, 4,3,  4,3, 2,3),
            ['H'] = G(0,0, 0,6,  4,0, 4,6,  0,3, 4,3),
            ['I'] = G(1,0, 3,0,  2,0, 2,6,  1,6, 3,6),
            ['K'] = G(0,0, 0,6,  0,3, 4,0,  0,3, 4,6),
            ['L'] = G(0,0, 0,6,  0,6, 4,6),
            ['M'] = G(0,6, 0,0,  0,0, 2,3,  2,3, 4,0,  4,0, 4,6),
            ['N'] = G(0,6, 0,0,  0,0, 4,6,  4,6, 4,0),
            ['O'] = G(1,0, 3,0,  3,0, 4,1,  4,1, 4,5,  4,5, 3,6,  3,6, 1,6,  1,6, 0,5,  0,5, 0,1,  0,1, 1,0),
            ['P'] = G(0,0, 0,6,  0,0, 3,0,  3,0, 4,1,  4,1, 4,2,  4,2, 3,3,  3,3, 0,3),
            ['R'] = G(0,0, 0,6,  0,0, 3,0,  3,0, 4,1,  4,1, 4,2,  4,2, 3,3,  3,3, 0,3,  2,3, 4,6),
            ['S'] = G(4,1, 3,0,  3,0, 1,0,  1,0, 0,1,  0,1, 0,2,  0,2, 1,3,  1,3, 3,3,  3,3, 4,4,  4,4, 4,5,  4,5, 3,6,  3,6, 1,6,  1,6, 0,5),
            ['T'] = G(0,0, 4,0,  2,0, 2,6),
            ['U'] = G(0,0, 0,5,  0,5, 1,6,  1,6, 3,6,  3,6, 4,5,  4,5, 4,0),
            ['V'] = G(0,0, 2,6,  2,6, 4,0),
            ['W'] = G(0,0, 1,6,  1,6, 2,2,  2,2, 3,6,  3,6, 4,0),
            ['Y'] = G(0,0, 2,3,  4,0, 2,3,  2,3, 2,6),
            ['·'] = G(1.7f,3, 2.3f,3),
            ['4'] = G(3,0, 0,4,  0,4, 4,4,  3,0, 3,6),
        };
    }

    // --- Helpers ---

    static void NeonStroke(SKCanvas c, SKPath path, SKColor color)
    {
        NeonStrokeHalo.Color = color.WithAlpha(0xC0);
        c.DrawPath(path, NeonStrokeHalo);
        NeonStrokeSharp.Color = color;
        c.DrawPath(path, NeonStrokeSharp);
    }

    static void NeonLine(SKCanvas c, float x1, float y1, float x2, float y2, SKColor color, float halo = 5.5f, float sharp = 2f)
    {
        NeonStrokeHalo.StrokeWidth = halo;
        NeonStrokeHalo.Color = color.WithAlpha(0xC0);
        c.DrawLine(x1, y1, x2, y2, NeonStrokeHalo);
        NeonStrokeSharp.StrokeWidth = sharp;
        NeonStrokeSharp.Color = color;
        c.DrawLine(x1, y1, x2, y2, NeonStrokeSharp);
        // Reset to default widths after.
        NeonStrokeHalo.StrokeWidth = 5.5f;
        NeonStrokeSharp.StrokeWidth = 2f;
    }

    static void NeonCircleFill(SKCanvas c, float cx, float cy, float r, SKColor color)
    {
        NeonFillHalo.Color = color.WithAlpha(0xB0);
        c.DrawCircle(cx, cy, r * 1.8f, NeonFillHalo);
        NeonFillSharp.Color = color;
        c.DrawCircle(cx, cy, r, NeonFillSharp);
    }

    static SKColor HsvToRgb(float hue, float sat, float val)
    {
        hue = ((hue % 360f) + 360f) % 360f;
        float c = val * sat;
        float x = c * (1f - MathF.Abs((hue / 60f) % 2f - 1f));
        float m = val - c;
        float r, g, b;
        switch ((int)(hue / 60f) % 6)
        {
            case 0: r = c; g = x; b = 0; break;
            case 1: r = x; g = c; b = 0; break;
            case 2: r = 0; g = c; b = x; break;
            case 3: r = 0; g = x; b = c; break;
            case 4: r = x; g = 0; b = c; break;
            default: r = c; g = 0; b = x; break;
        }
        return new SKColor(
            (byte)MathF.Round((r + m) * 255f),
            (byte)MathF.Round((g + m) * 255f),
            (byte)MathF.Round((b + m) * 255f));
    }

    static void DrawNeonBackground(SKCanvas c, float cw, float ch)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, ch),
                new[] { BgTop, BgBottom },
                SKShaderTileMode.Clamp),
        };
        c.DrawRect(0, 0, cw, ch, paint);
    }

    // --- Starfield ----------------------------------------------------
    // Tempest's playfield is otherwise empty black space — adding scrolling
    // top-to-bottom stars looked wrong because there is no "down" direction
    // here. Instead each star is given a fixed *radial offset* from the well
    // center; they sit still during normal play (just twinkle slightly), then
    // streak outward from center during the warp transition, giving the
    // camera-flying-down-the-tube feel.
    struct Star { public float Angle, Radius, Brightness; }
    const int StarCount = 90;
    static Star[]? _stars;
    static readonly Random _starRng = new(13);
    static readonly Stopwatch _starsClock = Stopwatch.StartNew();
    static readonly SKPaint _starPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    static readonly SKPaint _starStreakPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, StrokeCap = SKStrokeCap.Round,
    };

    static void EnsureStars(float worldW, float worldH)
    {
        if (_stars != null) return;
        _stars = new Star[StarCount];
        float maxRadius = MathF.Sqrt(worldW * worldW + worldH * worldH) * 0.6f;
        for (int i = 0; i < _stars.Length; i++)
        {
            // Distribute uniformly by area so stars are denser nearer the rim.
            double u = _starRng.NextDouble();
            _stars[i].Angle  = (float)(_starRng.NextDouble() * Math.PI * 2);
            _stars[i].Radius = (float)Math.Sqrt(u) * maxRadius;
            _stars[i].Brightness = 0.35f + (float)_starRng.NextDouble() * 0.55f;
        }
    }

    static void DrawStars(SKCanvas canvas, GameWorld world)
    {
        if (_stars is null) return;
        float cx = world.Well.Center.X;
        float cy = world.Well.Center.Y;
        float twinkle = (float)_starsClock.Elapsed.TotalSeconds;

        if (world.Mode == GameMode.Warp)
        {
            // Each star streaks outward from well center, accelerating with WarpProgress.
            float k = 1.0f + world.WarpProgress * 4.0f;
            for (int i = 0; i < _stars.Length; i++)
            {
                var s = _stars[i];
                float rNear = s.Radius * k;
                float rFar  = s.Radius * (k * 1.25f + 18f);
                float dx = MathF.Cos(s.Angle); float dy = MathF.Sin(s.Angle);
                byte a = (byte)(255 * s.Brightness);
                _starStreakPaint.Color = new SKColor(255, 255, 255, a);
                canvas.DrawLine(cx + dx * rNear, cy + dy * rNear,
                                cx + dx * rFar,  cy + dy * rFar, _starStreakPaint);
            }
        }
        else
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                var s = _stars[i];
                // Mild twinkle so the background isn't dead.
                float flick = 0.85f + 0.15f * MathF.Sin(twinkle * 1.7f + i * 0.31f);
                byte a = (byte)(255 * s.Brightness * flick);
                _starPaint.Color = new SKColor(255, 255, 255, a);
                float r = s.Brightness > 0.75f ? 1.5f : s.Brightness > 0.55f ? 1.0f : 0.7f;
                canvas.DrawCircle(cx + MathF.Cos(s.Angle) * s.Radius,
                                  cy + MathF.Sin(s.Angle) * s.Radius, r, _starPaint);
            }
        }
    }

    // --- Render entry point ---

    public static void Render(SKCanvas canvas, GameWorld world, float canvasW, float canvasH)
    {
        EnsureStars(world.Width, world.Height);
        DrawNeonBackground(canvas, canvasW, canvasH);

        float scale = MathF.Min(canvasW / world.Width, canvasH / world.Height);
        float ox = (canvasW - world.Width * scale) / 2f;
        float oy = (canvasH - world.Height * scale) / 2f;

        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);
        DrawStars(canvas, world);
        DrawWorld(canvas, world);
        canvas.Restore();

        DrawHud(canvas, world, canvasW, canvasH);
    }

    static void DrawWorld(SKCanvas canvas, GameWorld world)
    {
        // Particles
        foreach (var p in world.Particles)
        {
            float lifeT = p.Life / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonCircleFill(canvas, p.Pos.X, p.Pos.Y, p.Size, color);
        }

        // Well — drawn before bullets/enemies so they layer over it.
        DrawWell(canvas, world);

        // Spikes — drawn as a line along the segment direction from spike depth->1.
        DrawSpikes(canvas, world);

        // Bullets along their segment column.
        foreach (var b in world.Bullets)
        {
            var p = world.BulletPos(b);
            var color = b.FromPlayer ? BulletColor : EnemyBulletColor;
            float r = b.FromPlayer ? 3.6f : 3.2f;
            NeonCircleFill(canvas, p.X, p.Y, r, color);
        }

        foreach (var e in world.Enemies)
        {
            if (e.State == EnemyState.Dead) continue;
            DrawEnemy(canvas, world, e);
        }

        // Player claw on rim (skipped during warp; warp draws own player below).
        if (world.Mode != GameMode.Warp && PlayerVisible(world.Player))
        {
            DrawPlayer(canvas, world);
        }
        else if (world.Mode == GameMode.Warp)
        {
            DrawPlayerWarp(canvas, world);
        }

        // Popups
        using (var popupFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 20))
        {
            foreach (var sp in world.Popups)
            {
                float lifeT = sp.Life / MathF.Max(0.001f, sp.MaxLife);
                byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
                var color = new SKColor(sp.Color).WithAlpha(alpha);
                NeonFillSharp.Color = color;
                canvas.DrawText($"+{sp.Value}", sp.Pos.X, sp.Pos.Y, SKTextAlign.Center, popupFont, NeonFillSharp);
            }
        }
    }

    // Well: draw the rim outline + spokes from each rim vertex to the vanishing point.
    // Alternate-color segments + a few faint inner rings give the 3D well its shape.
    static void DrawWell(SKCanvas c, GameWorld world)
    {
        var well = world.Well;
        int n = well.RimPoints.Length;
        int segCount = well.SegmentCount;

        // 1) Spokes from each rim vertex to the vanishing point (Center).
        for (int i = 0; i < n; i++)
        {
            var a = well.RimPoints[i];
            var b = well.Center;
            NeonLine(c, a.X, a.Y, b.X, b.Y, SpokeColor, halo: 3.5f, sharp: 1.2f);
        }

        // 2) Inner rings at a few depths give the tunnel a sense of depth.
        for (int ring = 0; ring < 4; ring++)
        {
            float z = 0.18f + ring * 0.22f;
            var ringColor = InnerRingColor.WithAlpha((byte)(0x70 - ring * 16));
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (!well.Closed && i == n - 1) continue;
                var pa = well.Project(well.RimPoints[i], z);
                var pb = well.Project(well.RimPoints[j], z);
                NeonLine(c, pa.X, pa.Y, pb.X, pb.Y, ringColor, halo: 2.5f, sharp: 0.9f);
            }
        }

        // 3) Rim itself, with alternating segment colors for retro contrast.
        for (int s = 0; s < segCount; s++)
        {
            var a = well.RimPoints[s];
            var b = well.RimPoints[(s + 1) % n];
            var color = well.IsAlternateSlot(s) ? RimAltColor : RimColor;
            NeonLine(c, a.X, a.Y, b.X, b.Y, color, halo: 6.5f, sharp: 2.4f);
        }
    }

    static void DrawSpikes(SKCanvas c, GameWorld world)
    {
        var well = world.Well;
        foreach (var s in world.Spikes)
        {
            var mid = well.SegmentMid(s.Segment, s.MinDepth);
            var deep = well.SegmentMid(s.Segment, 1f);
            // Spike line from current top toward the vanishing point.
            NeonLine(c, mid.X, mid.Y, deep.X, deep.Y, SpikeColor, halo: 4.5f, sharp: 1.6f);
            // A small barb at the leading tip.
            NeonCircleFill(c, mid.X, mid.Y, 2.4f, SpikeColor);
        }
    }

    // Player claw — a yellow open "M" that straddles the rim segment with two
    // claws hooking outward. Scale shrinks based on segment length so it always
    // fits.
    static void DrawPlayer(SKCanvas c, GameWorld world)
    {
        var well = world.Well;
        int seg = world.Player.Segment;
        int tgt = world.Player.TargetSegment;
        float t  = world.Player.SegmentT;

        var pos = world.PlayerPos();
        Vec2 dir;
        if (seg == tgt)
        {
            dir = well.SegmentDir(seg);
        }
        else
        {
            var dA = well.SegmentDir(seg);
            var dB = well.SegmentDir(tgt);
            dir = new Vec2(dA.X + (dB.X - dA.X) * t, dA.Y + (dB.Y - dA.Y) * t).Normalized();
        }
        var outward = (seg == tgt)
            ? well.SegmentNormal(seg)
            : new Vec2(
                well.SegmentNormal(seg).X + (well.SegmentNormal(tgt).X - well.SegmentNormal(seg).X) * t,
                well.SegmentNormal(seg).Y + (well.SegmentNormal(tgt).Y - well.SegmentNormal(seg).Y) * t).Normalized();

        DrawClaw(c, pos, dir, outward, world.Player.Invuln > 0);
    }

    static void DrawPlayerWarp(SKCanvas c, GameWorld world)
    {
        var well = world.Well;
        int seg = world.Player.Segment;
        var dir = well.SegmentDir(seg);
        var outward = well.SegmentNormal(seg);
        var pos = world.Well.SegmentMid(seg, world.WarpProgress);
        // Add the outward offset, scaled to perspective at this depth.
        float persp = 1f / (1f + world.WarpProgress * Well.PerspectiveK);
        pos = new Vec2(pos.X + outward.X * 14f * persp, pos.Y + outward.Y * 14f * persp);
        DrawClaw(c, pos, dir, outward, false, scale: persp);
    }

    static void DrawClaw(SKCanvas c, Vec2 pos, Vec2 dir, Vec2 outward, bool flicker, float scale = 1f)
    {
        if (flicker && ((int)(MarqueeClock.Elapsed.TotalSeconds * 16) & 1) == 0) return;

        // Build claw shape: an M-like outline with two claws hooking outward.
        // Tempest's "blaster" is iconic: a wide V/M straddling the segment.
        float w = 22f * scale; // half-width along segment dir
        float h = 18f * scale; // claw height outward
        float k = 7f  * scale; // inner notch height

        var dx = dir.X; var dy = dir.Y;
        var nx = -outward.X; var ny = -outward.Y; // INWARD toward well
        Vec2 P(float a, float b) => new(pos.X + dx * a + outward.X * (-b), pos.Y + dy * a + outward.Y * (-b));

        // Two claws — outer V on each side, meeting at center-top.
        var p0 = P(-w,        0f);     // outer left tip on rim
        var p1 = P(-w * 0.65f, h);     // upper left claw
        var p2 = P(-w * 0.30f, k);     // notch left
        var p3 = P( 0f,        h * 1.1f); // center peak
        var p4 = P( w * 0.30f, k);     // notch right
        var p5 = P( w * 0.65f, h);     // upper right claw
        var p6 = P( w,        0f);     // outer right tip on rim

        using var b = new SKPathBuilder();
        b.AddPoly(stackalloc SKPoint[]
        {
            new(p0.X, p0.Y), new(p1.X, p1.Y), new(p2.X, p2.Y),
            new(p3.X, p3.Y),
            new(p4.X, p4.Y), new(p5.X, p5.Y), new(p6.X, p6.Y),
        }, close: false);
        using var path = b.Detach();
        NeonStroke(c, path, PlayerColor);

        // Bright dots at claw tips (the firing points).
        NeonCircleFill(c, p1.X, p1.Y, 2.2f * scale, PlayerCockpit);
        NeonCircleFill(c, p5.X, p5.Y, 2.2f * scale, PlayerCockpit);
    }

    // Enemies are drawn at their projected position with their kind-specific shape.
    // Shapes scale with depth (smaller far away) — uses the perspective factor.
    static void DrawEnemy(SKCanvas c, GameWorld world, Enemy e)
    {
        var well = world.Well;
        var pos = world.EnemyPos(e);
        float persp = 1f / (1f + e.Depth * Well.PerspectiveK);
        float size = 16f * persp;
        if (size < 2f) return;

        switch (e.Kind)
        {
            case EnemyKind.Flipper:  DrawFlipper(c, pos, size, FlipperColor); break;
            case EnemyKind.Tanker:   DrawTanker(c, pos, size, TankerColor);   break;
            case EnemyKind.Spiker:   DrawSpiker(c, pos, size, SpikerColor);   break;
            case EnemyKind.Fuseball: DrawFuseball(c, pos, size, e.Hue);       break;
        }
    }

    // Flipper: angular "bowtie" — two triangles meeting at center, with a small
    // central diamond. Tempest's flipper is iconic. Drawn axis-aligned for clarity.
    static void DrawFlipper(SKCanvas c, Vec2 pos, float size, SKColor color)
    {
        c.Save();
        c.Translate(pos.X, pos.Y);
        using var b = new SKPathBuilder();
        b.MoveTo(-size,      0);
        b.LineTo( 0,        -size * 0.55f);
        b.LineTo( size,      0);
        b.LineTo( 0,         size * 0.55f);
        b.Close();
        b.MoveTo(-size * 0.6f, -size * 0.55f);
        b.LineTo( size * 0.6f,  size * 0.55f);
        b.MoveTo( size * 0.6f, -size * 0.55f);
        b.LineTo(-size * 0.6f,  size * 0.55f);
        using var path = b.Detach();
        NeonStroke(c, path, color);
        c.Restore();
    }

    // Tanker: open box / "I" shape — clearly distinct from Flipper.
    static void DrawTanker(SKCanvas c, Vec2 pos, float size, SKColor color)
    {
        c.Save();
        c.Translate(pos.X, pos.Y);
        using var b = new SKPathBuilder();
        b.AddPoly(stackalloc SKPoint[]
        {
            new(-size,      -size * 0.7f),
            new( size,      -size * 0.7f),
            new( size * 0.4f, 0),
            new( size,       size * 0.7f),
            new(-size,       size * 0.7f),
            new(-size * 0.4f, 0),
        }, close: true);
        using var path = b.Detach();
        NeonStroke(c, path, color);
        // Central dot to read as "carrying something".
        NeonCircleFill(c, 0, 0, size * 0.18f, color);
        c.Restore();
    }

    // Spiker: zig-zag yellow "M" — Tempest's spike-leaving enemy.
    static void DrawSpiker(SKCanvas c, Vec2 pos, float size, SKColor color)
    {
        c.Save();
        c.Translate(pos.X, pos.Y);
        using var b = new SKPathBuilder();
        b.MoveTo(-size,       size * 0.5f);
        b.LineTo(-size * 0.5f, -size * 0.5f);
        b.LineTo( 0,           size * 0.5f);
        b.LineTo( size * 0.5f, -size * 0.5f);
        b.LineTo( size,        size * 0.5f);
        using var path = b.Detach();
        NeonStroke(c, path, color);
        c.Restore();
    }

    // Fuseball: animated green/yellow energy ball with crackling rays.
    static void DrawFuseball(SKCanvas c, Vec2 pos, float size, float hue)
    {
        float t = (float)MarqueeClock.Elapsed.TotalSeconds * 8f + hue * 0.05f;
        for (int i = 0; i < 6; i++)
        {
            float ang = i * MathF.PI / 3f + t;
            float r = size * (0.6f + 0.4f * MathF.Sin(t * 2f + i));
            float x1 = pos.X + MathF.Cos(ang) * size * 0.2f;
            float y1 = pos.Y + MathF.Sin(ang) * size * 0.2f;
            float x2 = pos.X + MathF.Cos(ang) * r;
            float y2 = pos.Y + MathF.Sin(ang) * r;
            var color = (i & 1) == 0 ? FuseballColor1 : FuseballColor2;
            NeonLine(c, x1, y1, x2, y2, color, halo: 4.5f, sharp: 1.4f);
        }
        NeonCircleFill(c, pos.X, pos.Y, size * 0.3f, FuseballColor1);
    }

    static bool PlayerVisible(Player p)
    {
        if (p.Invuln <= 0) return true;
        return ((int)(p.Invuln * 10f) & 1) == 0;
    }

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
        DrawHudText(c, $"{w.Score:00000}", 24, 36, SKTextAlign.Left, font, HudColor);

        if (w.HighScore > 0)
        {
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
            DrawHudText(c, $"HI {w.HighScore:00000}", cw / 2f, 28, SKTextAlign.Center, smallFont, HudColor);
        }

        if (w.Mode == GameMode.Playing || w.Mode == GameMode.Warp || w.Mode == GameMode.Attract)
        {
            using var levelFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 22);
            DrawHudText(c, $"LEVEL {w.Level}", cw - 24, 32, SKTextAlign.Right, levelFont, HudColor);

            using var zapFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);
            DrawHudText(c, $"SUPER ZAPPER  x{w.Player.SuperZapperUsesLeft}", cw - 24, 56, SKTextAlign.Right, zapFont, HudColor);

            // Lives indicator — small claws bottom-left.
            for (int i = 0; i < w.LivesLeft; i++)
            {
                c.Save();
                c.Translate(36f + i * 36f, ch - 32f);
                c.Scale(0.55f);
                DrawClaw(c, Vec2.Zero, new Vec2(1, 0), new Vec2(0, -1), false);
                c.Restore();
            }

            if (w.PlacardTimer > 0 && !string.IsNullOrEmpty(w.PlacardText))
            {
                using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 44);
                DrawHudText(c, w.PlacardText, cw / 2f, ch * 0.20f, SKTextAlign.Center, placardFont, HudColor);
            }
        }

        if (w.Mode == GameMode.Attract)
        {
            using var attractFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 20);
            DrawHudText(c, "ATTRACT  -  PRESS ANY KEY", cw / 2f, ch - 32f, SKTextAlign.Center, attractFont, HudColor);
        }

        if (w.Mode == GameMode.Title)
        {
            DrawTitle(c, w, cw, ch);
            DrawMarquee(c, cw, ch);
        }
        else if (w.Mode == GameMode.GameOver)
        {
            using var bigFont   = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
            DrawHudText(c, "GAME OVER",              cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   HudColor);
            DrawHudText(c, $"FINAL SCORE  {w.Score:00000}", cw / 2f, ch / 2f + 50f, SKTextAlign.Center, smallFont, HudColor);
            DrawHudText(c, $"YOU REACHED LEVEL {w.Level}", cw / 2f, ch / 2f + 80f, SKTextAlign.Center, smallFont, HudColor);
            DrawHudText(c, "PRESS SPACE TO PLAY AGAIN",     cw / 2f, ch / 2f + 130f, SKTextAlign.Center, smallFont, HudColor);
        }
    }

    static void DrawHudText(SKCanvas c, string text, float x, float y, SKTextAlign align, SKFont font, SKColor color)
    {
        NeonFillHalo.Color = color.WithAlpha(0xC0);
        c.DrawText(text, x, y, align, font, NeonFillHalo);
        NeonFillSharp.Color = color;
        c.DrawText(text, x, y, align, font, NeonFillSharp);
    }

    // Title screen: rainbow vector "LUA", subtitle, branding row (Uno + Kahua).
    static void DrawTitle(SKCanvas c, GameWorld w, float cw, float ch)
    {
        const string title = "LUA";
        float advance = MarqueeCharWidth + MarqueeCharGap;
        float titleW = title.Length * advance - MarqueeCharGap;

        c.Save();
        c.Translate((cw - titleW) / 2f, ch * 0.18f);
        float time = (float)MarqueeClock.Elapsed.TotalSeconds;
        for (int i = 0; i < title.Length; i++)
        {
            if (!Glyphs.TryGetValue(title[i], out var glyph)) continue;
            float hue = (time * 60f + i * 22f) % 360f;
            SKColor color = HsvToRgb(hue, 1f, 1f);
            c.Save();
            c.Translate(i * advance, 0f);
            MarqueeNeonHalo.Color = color.WithAlpha(0xC0);
            c.DrawPath(glyph, MarqueeNeonHalo);
            MarqueeNeonSharp.Color = color;
            c.DrawPath(glyph, MarqueeNeonSharp);
            c.Restore();
        }
        c.Restore();

        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
        using var instrFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        DrawHudText(c, "TEMPEST-STYLE VECTOR DEMO", cw / 2f, ch * 0.18f + MarqueeCharHeight + 28f, SKTextAlign.Center, smallFont, HudColor);
        DrawHudText(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.42f, SKTextAlign.Center, smallFont, HudColor);
        DrawHudText(c, "Left / Right or A / D  -  rotate around rim",          cw / 2f, ch * 0.48f, SKTextAlign.Center, instrFont, HudColor);
        DrawHudText(c, "Space  -  fire    Z  -  super zapper (2 per level)",   cw / 2f, ch * 0.52f, SKTextAlign.Center, instrFont, HudColor);
    }

    static void DrawUnoLogo(SKCanvas c)
    {
        c.Save();
        c.Translate(-UnoLogoBounds.MidX, -UnoLogoBounds.MidY);
        foreach (var (path, fill) in UnoLogoPaths)
        {
            NeonFillSharp.Color = fill;
            c.DrawPath(path, NeonFillSharp);
        }
        c.Restore();
    }

    static void DrawMarquee(SKCanvas c, float cw, float ch)
    {
        float advance = MarqueeCharWidth + MarqueeCharGap;
        float totalW  = MarqueeText.Length * advance;
        float loop    = totalW + cw;
        double time   = MarqueeClock.Elapsed.TotalSeconds;
        float pixelOffset = (float)((time * MarqueeSpeed) % loop);
        float startX    = cw - pixelOffset;
        float baselineY = ch * 0.92f;

        const float TiltDegrees = 30f;
        float h    = MarqueeCharHeight;
        float tilt = TiltDegrees * MathF.PI / 180f;
        float cosT = MathF.Cos(tilt);
        float sinT = MathF.Sin(tilt);
        float d    = 3f * h;
        var perspective = new SKMatrix
        {
            ScaleX = 1f, SkewX = 0f,         TransX = 0f,
            SkewY  = 0f, ScaleY = cosT,      TransY = h * (1f - cosT),
            Persp0 = 0f, Persp1 = -sinT / d, Persp2 = 1f + h * sinT / d,
        };

        float centerX = cw / 2f;
        c.Save();
        c.Translate(centerX, baselineY - h);
        c.Concat(in perspective);

        float wTop    = 1f + h * sinT / d;
        float cullPad = (cw / 2f) * (wTop - 1f) + MarqueeCharWidth;
        for (int i = 0; i < MarqueeText.Length; i++)
        {
            float x = startX + i * advance;
            if (x + MarqueeCharWidth < -cullPad || x > cw + cullPad) continue;
            if (!Glyphs.TryGetValue(MarqueeText[i], out var glyph)) continue;

            c.Save();
            c.Translate(x - centerX, 0f);
            float hue = ((float)time * 75f + i * 18f) % 360f;
            SKColor color = HsvToRgb(hue, 1f, 1f);
            MarqueeNeonHalo.Color = color.WithAlpha(0xC0);
            c.DrawPath(glyph, MarqueeNeonHalo);
            MarqueeNeonSharp.Color = color;
            c.DrawPath(glyph, MarqueeNeonSharp);
            c.Restore();
        }
        c.Restore();
    }
}
