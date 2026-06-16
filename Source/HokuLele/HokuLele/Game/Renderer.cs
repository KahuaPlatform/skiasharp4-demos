using System;
using System.Diagnostics;
using SkiaSharp;

namespace HokuLele.Game;

/// <summary>
/// All of HokuLele's drawing. Shared chassis pieces (neon paints, glyph font,
/// marquee, HUD text) come from <c>Arcade.Common.Chassis</c> via global usings;
/// this file owns the game-specific draws: the player ship + 6 enemy archetypes
/// (4 hand-drawn vector silhouettes + the Uno brand-mark mothership + the Kahua
/// snowflake), the tractor beam, the trailing captive ship, and the HUD/title.
/// </summary>
public static class Renderer
{
    // --- Game-specific palette ---
    static readonly SKColor NeonPlayerColor      = new(0x33, 0xF8, 0xFF);
    static readonly SKColor NeonPlayerCockpit    = new(0xFF, 0x44, 0x44);
    static readonly SKColor NeonPlayerEngineGlow = new(0xFF, 0xCC, 0x33);
    static readonly SKColor NeonBulletColor      = new(0xFF, 0xEE, 0x33);
    static readonly SKColor NeonEnemyBulletColor = new(0xFF, 0x44, 0x66);
    static readonly SKColor NeonHudColor         = new(0x33, 0xF8, 0xFF);

    record struct EnemyPalette(SKColor Body, SKColor Accent);

    // Per-enemy-kind palette. Index matches Enemy.Kind: 0=drone, 1=wing,
    // 2=captain, 3=boss, 4=mothership, 5=snowflake. Kinds 4 and 5 use specialised
    // brand-mark rendering and ignore this palette.
    static readonly EnemyPalette[] EnemyPalettes =
    {
        new(new(0x99, 0xFF, 0x55), new(0xFF, 0xEE, 0x33)),
        new(new(0xFF, 0x44, 0xAA), new(0x33, 0xCC, 0xFF)),
        new(new(0x33, 0xCC, 0xFF), new(0xFF, 0x44, 0xAA)),
        new(new(0xFF, 0xAA, 0x33), new(0x33, 0xCC, 0xFF)),
        new(new(0xFF, 0xFF, 0xFF), new(0xFF, 0xFF, 0xFF)),
        new(new(0xFF, 0xFF, 0xFF), new(0xFF, 0xFF, 0xFF)),
    };

    // --- Uno Platform mark — the actual logo, compiled from icon_foreground.svg.
    // Used by the kind-4 mothership at stage size.
    static SKPath MakeUnoPath(string d, float tx, float ty)
    {
        var p = SKPath.ParseSvgPathData(d);
        if (tx != 0f || ty != 0f) p.Transform(SKMatrix.CreateTranslation(tx, ty));
        return p;
    }

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

    static readonly SKRect UnoLogoBounds = ComputeUnoLogoBounds();
    static SKRect ComputeUnoLogoBounds()
    {
        var b = UnoLogoPaths[0].path.Bounds;
        for (int i = 1; i < UnoLogoPaths.Length; i++)
            b = SKRect.Union(b, UnoLogoPaths[i].path.Bounds);
        return b;
    }

    // Kahua brand mark — high-res PNG embedded as an assembly resource. Case 5
    // falls back to the stylized snowflake if the embedded asset isn't there.
    static readonly SKImage? KahuaSnowflakeImage = LoadEmbeddedImage("HokuLele.Assets.Icons.kahua_snowflake.png");
    static readonly SKSamplingOptions KahuaSnowflakeSampling = new(SKCubicResampler.Mitchell);
    static readonly SKPaint KahuaGlowPaint = new()
    {
        IsAntialias = true,
        ImageFilter = SKImageFilter.CreateBlur(5f, 5f),
    };

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

    static readonly SKColor KahuaOrange   = new(0xF5, 0x82, 0x20);
    static readonly SKColor KahuaRed      = new(0xE1, 0x14, 0x2A);
    static readonly SKColor SnowflakeCore = new(0xFF, 0xFF, 0xFF);

    const string MarqueeText = "HOKULELE · UNO PLATFORM · SKIASHARP 4 · NEON SHMUP DEMO";

    // --- Vector silhouettes for the player + four hand-drawn enemy archetypes ---
    static readonly SKPath PlayerBodyPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -16),
        new(2, -8), new(3, -2),
        new(11, 6), new(7, 8), new(4, 5),
        new(3, 10), new(-3, 10),
        new(-4, 5), new(-7, 8), new(-11, 6),
        new(-3, -2), new(-2, -8),
    }, close: true);

    static readonly SKPath PlayerCockpitPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -6), new(2, 2), new(-2, 2),
    }, close: true);

    static readonly SKPath DroneBodyPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -10),
        new(3, -4),
        new(9, 4), new(4, 3),
        new(3, 7), new(-3, 7),
        new(-4, 3), new(-9, 4),
        new(-3, -4),
    }, close: true);

    static readonly SKPath WingBodyPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -8),
        new(5, -5), new(13, -1), new(11, 3),
        new(6, 2), new(4, 8), new(-4, 8),
        new(-6, 2), new(-11, 3), new(-13, -1),
        new(-5, -5),
    }, close: true);

    static readonly SKPath CaptainBodyPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -11),
        new(5, -9), new(8, -3),
        new(14, 1), new(10, 5),
        new(5, 3), new(3, 8), new(-3, 8),
        new(-5, 3), new(-10, 5),
        new(-14, 1), new(-8, -3),
        new(-5, -9),
    }, close: true);

    static readonly SKPath CaptainAntennaePath = MakeCaptainAntennae();
    static SKPath MakeCaptainAntennae()
    {
        using var b = new SKPathBuilder();
        b.MoveTo(-2.5f, -11f); b.LineTo(-5f, -16f);
        b.MoveTo( 2.5f, -11f); b.LineTo( 5f, -16f);
        return b.Detach();
    }

    static readonly SKPath BossBodyPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -13),
        new(7, -11), new(11, -6),
        new(17, -1), new(15, 5), new(11, 4),
        new(8, 4), new(5, 11),
        new(2, 8), new(-2, 8),
        new(-5, 11), new(-8, 4), new(-11, 4),
        new(-15, 5), new(-17, -1),
        new(-11, -6), new(-7, -11),
    }, close: true);

    static readonly SKPath SnowflakeSpikePath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -6),
        new(1.5f, -8), new(2.5f, -12),
        new(2, -16), new(0.5f, -13),
        new(0, -12),
        new(-0.5f, -13), new(-2, -16),
        new(-2.5f, -12), new(-1.5f, -8),
    }, close: true);

    static SKPath BuildPath(ReadOnlySpan<SKPoint> points, bool close)
    {
        using var b = new SKPathBuilder();
        b.AddPoly(points, close);
        return b.Detach();
    }

    // --- Game-specific starfield (vertical scrolling, top-down feel) ---
    struct Star { public float X, Y, Speed, Brightness; }
    const int StarCount = 110;
    static Star[]? _stars;
    static readonly Random _starRng = new(13);
    static readonly Stopwatch _starsClock = Stopwatch.StartNew();
    static double _starsLastT;
    static readonly SKPaint _starPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    static void EnsureStars(float worldW, float worldH)
    {
        if (_stars != null) return;
        _stars = new Star[StarCount];
        for (int i = 0; i < _stars.Length; i++)
        {
            double r = _starRng.NextDouble();
            int layer = r < 0.50 ? 0 : r < 0.85 ? 1 : 2;
            _stars[i].X = (float)_starRng.NextDouble() * worldW;
            _stars[i].Y = (float)_starRng.NextDouble() * worldH;
            _stars[i].Speed = layer switch { 0 => 25f, 1 => 65f, _ => 130f };
            _stars[i].Brightness = layer switch
            {
                0 => 0.28f + (float)_starRng.NextDouble() * 0.15f,
                1 => 0.55f + (float)_starRng.NextDouble() * 0.20f,
                _ => 0.85f + (float)_starRng.NextDouble() * 0.15f,
            };
        }
    }

    static void UpdateStars(float worldW, float worldH)
    {
        EnsureStars(worldW, worldH);
        double now = _starsClock.Elapsed.TotalSeconds;
        float dt = MathF.Min(0.1f, (float)(now - _starsLastT));
        _starsLastT = now;
        for (int i = 0; i < _stars!.Length; i++)
        {
            _stars[i].Y += _stars[i].Speed * dt;
            if (_stars[i].Y > worldH + 5f)
            {
                _stars[i].X = (float)_starRng.NextDouble() * worldW;
                _stars[i].Y = -5f;
            }
        }
    }

    static void DrawStars(SKCanvas canvas)
    {
        if (_stars is null) return;
        for (int i = 0; i < _stars.Length; i++)
        {
            var s = _stars[i];
            byte a = (byte)(255 * s.Brightness);
            _starPaint.Color = new SKColor(255, 255, 255, a);
            float r = s.Brightness > 0.85f ? 1.8f : s.Brightness > 0.55f ? 1.3f : 0.9f;
            canvas.DrawCircle(s.X, s.Y, r, _starPaint);
        }
    }

    // --- Render entry point ---

    /// <summary>Renders one frame: background, world entities, then the screen-space HUD/title.</summary>
    public static void Render(SKCanvas canvas, GameWorld world, float canvasW, float canvasH)
    {
        UpdateStars(world.Width, world.Height);
        NeonBackground.Draw(canvas, canvasW, canvasH);

        float scale = MathF.Min(canvasW / world.Width, canvasH / world.Height);
        float ox = (canvasW - world.Width * scale) / 2f;
        float oy = (canvasH - world.Height * scale) / 2f;

        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);
        DrawStars(canvas);
        DrawWorld(canvas, world);
        canvas.Restore();

        DrawHud(canvas, world, canvasW, canvasH);
    }

    static void DrawWorld(SKCanvas canvas, GameWorld world)
    {
        PlayfieldBorder.Draw(canvas, world.Width, world.Height, NeonHudColor);

        foreach (var p in world.Particles)
        {
            float lifeT = p.Lifetime / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonDraw.CircleFill(canvas, p.Position.X, p.Position.Y, 1.8f, color);
        }

        using (var popupFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 20))
        {
            foreach (var sp in world.ScorePopups)
            {
                float lifeT = sp.Lifetime / MathF.Max(0.001f, sp.MaxLife);
                byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
                var color = new SKColor(sp.Color).WithAlpha(alpha);
                NeonPaints.FillSharp.Color = color;
                canvas.DrawText($"+{sp.Value}", sp.Position.X, sp.Position.Y, SKTextAlign.Center, popupFont, NeonPaints.FillSharp);
            }
        }

        foreach (var b in world.Bullets)
        {
            var color = b.FromPlayer ? NeonBulletColor : NeonEnemyBulletColor;
            NeonDraw.CircleFill(canvas, b.Position.X, b.Position.Y, b.Radius, color);
        }

        foreach (var e in world.Enemies)
            if (e.Alive && e.State == EnemyState.BeamActive)
                DrawTractorBeam(canvas, e, world.Height);

        foreach (var e in world.Enemies)
            DrawEnemy(canvas, e);

        foreach (var e in world.Enemies)
            if (e.Alive && e.HasCaptive) DrawCaptive(canvas, e);

        if (world.Player.Alive && PlayerVisible(world.Player))
        {
            DrawPlayer(canvas, world.Player.Position.X, world.Player.Position.Y);
            if (world.Player.HasWingman)
                DrawPlayer(canvas, world.Player.Position.X + world.Player.WingmanOffsetX, world.Player.Position.Y);
        }
    }

    static void DrawTractorBeam(SKCanvas canvas, Enemy boss, float worldH)
    {
        float bx = boss.Position.X;
        float by = boss.Position.Y + 8f;
        const float topHW = 16f;
        const float botHW = 72f;
        float botY = worldH - 50f;

        using var builder = new SKPathBuilder();
        builder.AddPoly(stackalloc SKPoint[]
        {
            new(bx - topHW, by),
            new(bx + topHW, by),
            new(bx + botHW, botY),
            new(bx - botHW, botY),
        }, close: true);
        using var path = builder.Detach();

        using var fill = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(bx, by), new SKPoint(bx, botY),
                new[]
                {
                    new SKColor(0xFF, 0xCC, 0x33, 0xC0),
                    new SKColor(0xFF, 0xAA, 0x88, 0x35),
                },
                SKShaderTileMode.Clamp),
        };
        canvas.DrawPath(path, fill);

        using var mouth = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            IsAntialias = true,
            Color = new SKColor(0xFF, 0xFF, 0x99, 0xE0),
        };
        canvas.DrawLine(bx - topHW, by, bx + topHW, by, mouth);
    }

    static void DrawCaptive(SKCanvas canvas, Enemy boss)
    {
        canvas.Save();
        canvas.Translate(boss.Position.X, boss.Position.Y + 22f);
        canvas.Scale(0.6f);
        canvas.RotateDegrees(180f);
        NeonDraw.Stroke(canvas, PlayerBodyPath, NeonPlayerColor);
        NeonPaints.FillSharp.Color = NeonPlayerCockpit;
        canvas.DrawPath(PlayerCockpitPath, NeonPaints.FillSharp);
        canvas.Restore();
    }

    static void DrawPlayer(SKCanvas canvas, float x, float y)
    {
        canvas.Save();
        canvas.Translate(x, y);
        NeonDraw.Stroke(canvas, PlayerBodyPath, NeonPlayerColor);
        NeonPaints.FillSharp.Color = NeonPlayerCockpit;
        canvas.DrawPath(PlayerCockpitPath, NeonPaints.FillSharp);
        NeonDraw.CircleFill(canvas, -7f, 8f, 1.2f, NeonPlayerEngineGlow);
        NeonDraw.CircleFill(canvas,  7f, 8f, 1.2f, NeonPlayerEngineGlow);
        canvas.Restore();
    }

    static void DrawEnemy(SKCanvas canvas, Enemy enemy)
    {
        int kind = Math.Clamp(enemy.Kind, 0, EnemyPalettes.Length - 1);
        canvas.Save();
        canvas.Translate(enemy.Position.X, enemy.Position.Y);
        if (kind < 4) canvas.RotateRadians(enemy.Rotation);
        DrawEnemyShape(canvas, kind);
        canvas.Restore();
    }

    static void DrawStylizedSnowflake(SKCanvas canvas)
    {
        for (int spike = 0; spike < 8; spike++)
        {
            canvas.Save();
            canvas.RotateDegrees(spike * 45f);
            NeonDraw.Stroke(canvas, SnowflakeSpikePath, KahuaOrange);
            canvas.Restore();
        }
        NeonPaints.FillSharp.Color = SnowflakeCore;
        canvas.DrawCircle(0f, 0f, 5f, NeonPaints.FillSharp);
        NeonPaints.FillSharp.Color = KahuaOrange;
        canvas.DrawCircle(-2.4f, -2f, 0.9f, NeonPaints.FillSharp);
        canvas.DrawCircle( 2.4f, -2f, 0.9f, NeonPaints.FillSharp);
        canvas.DrawCircle(-2.4f,  2f, 0.9f, NeonPaints.FillSharp);
        canvas.DrawCircle( 2.4f,  2f, 0.9f, NeonPaints.FillSharp);
        NeonPaints.FillSharp.Color = KahuaRed;
        canvas.DrawCircle(0f, 0f, 0.9f, NeonPaints.FillSharp);
    }

    static void DrawEnemyShape(SKCanvas canvas, int kind)
    {
        var p = EnemyPalettes[kind];
        switch (kind)
        {
            case 0:
                NeonDraw.Stroke(canvas, DroneBodyPath, p.Body);
                NeonDraw.CircleFill(canvas, 0f, -3f, 1.4f, p.Accent);
                break;
            case 1:
                NeonDraw.Stroke(canvas, WingBodyPath, p.Body);
                NeonDraw.CircleFill(canvas, -13f, -1f, 1.6f, p.Accent);
                NeonDraw.CircleFill(canvas,  13f, -1f, 1.6f, p.Accent);
                break;
            case 2:
                NeonDraw.Stroke(canvas, CaptainBodyPath,     p.Body);
                NeonDraw.Stroke(canvas, CaptainAntennaePath, p.Accent);
                NeonDraw.CircleFill(canvas, 0f, -2f, 1.5f, p.Accent);
                break;
            case 3:
                NeonDraw.Stroke(canvas, BossBodyPath, p.Body);
                NeonDraw.CircleFill(canvas,   0f,  0f, 2.5f, p.Accent);
                NeonDraw.CircleFill(canvas, -16f, -1f, 1.4f, p.Accent);
                NeonDraw.CircleFill(canvas,  16f, -1f, 1.4f, p.Accent);
                break;
            case 4:
            {
                const float TargetSize = 32f;
                float s = TargetSize / MathF.Max(UnoLogoBounds.Width, UnoLogoBounds.Height);
                canvas.Save();
                canvas.Scale(s);
                canvas.Translate(-UnoLogoBounds.MidX, -UnoLogoBounds.MidY);
                // Halo pass: blurred fills for each colored arc. Black accent dots
                // are skipped — blurring black just darkens the surrounding pixels.
                foreach (var (path, fill) in UnoLogoPaths)
                {
                    if (fill == SKColors.Black) continue;
                    NeonPaints.FillHalo.Color = fill.WithAlpha(0xC0);
                    canvas.DrawPath(path, NeonPaints.FillHalo);
                }
                foreach (var (path, fill) in UnoLogoPaths)
                {
                    NeonPaints.FillSharp.Color = fill;
                    canvas.DrawPath(path, NeonPaints.FillSharp);
                }
                canvas.Restore();
                break;
            }
            case 5:
                if (KahuaSnowflakeImage is not null)
                {
                    const float TargetSize = 38f;
                    float scale = TargetSize / MathF.Max(KahuaSnowflakeImage.Width, KahuaSnowflakeImage.Height);
                    float w = KahuaSnowflakeImage.Width  * scale;
                    float h = KahuaSnowflakeImage.Height * scale;
                    var rect = new SKRect(-w / 2f, -h / 2f, w / 2f, h / 2f);
                    canvas.DrawImage(KahuaSnowflakeImage, rect, KahuaSnowflakeSampling, KahuaGlowPaint);
                    canvas.DrawImage(KahuaSnowflakeImage, rect, KahuaSnowflakeSampling);
                }
                else
                {
                    DrawStylizedSnowflake(canvas);
                }
                break;
        }
    }

    static bool PlayerVisible(Player p)
    {
        if (p.InvincibleTime <= 0) return true;
        return ((int)(p.InvincibleTime * 10) % 2 == 0);
    }

    // --- HUD ---

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
        HudText.Draw(c, $"{w.Score:00000}", 24, 36, SKTextAlign.Left, font, NeonHudColor);

        if (w.HighScore > 0)
        {
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
            HudText.Draw(c, $"HI {w.HighScore:00000}", cw / 2f, 28, SKTextAlign.Center, smallFont, NeonHudColor);
        }

        if (w.Mode == GameMode.Playing)
        {
            using var stageFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 22);
            string stageLabel = w.IsChallengeStage ? "CHALLENGE" : $"STAGE {w.Stage}";
            HudText.Draw(c, stageLabel, cw - 24, 32, SKTextAlign.Right, stageFont, NeonHudColor);

            if (!w.BulletCapEnabled)
            {
                using var cheatFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);
                HudText.Draw(c, "CHEAT: NO BULLET CAP", 24, 64, SKTextAlign.Left, cheatFont, new SKColor(0xFF, 0xCC, 0x33));
            }

            for (int i = 0; i < w.Player.Lives; i++)
            {
                c.Save();
                c.Translate(28f + i * 30f, ch - 28f);
                c.Scale(0.85f);
                NeonDraw.Stroke(c, PlayerBodyPath, NeonPlayerColor);
                NeonPaints.FillSharp.Color = NeonPlayerCockpit;
                c.DrawPath(PlayerCockpitPath, NeonPaints.FillSharp);
                c.Restore();
            }

            if (w.WaveState == WaveState.Placard && !string.IsNullOrEmpty(w.PlacardText))
            {
                using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 44);
                SKColor color = w.PlacardText.Contains("CHALLENG") || w.PlacardText.Contains("BONUS")
                    ? new SKColor(0xFF, 0xAA, 0x33)
                    : NeonHudColor;
                HudText.Draw(c, w.PlacardText, cw / 2f, ch * 0.42f, SKTextAlign.Center, placardFont, color);
            }
        }

        if (w.Mode == GameMode.Attract)
        {
            using var attractFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 20);
            HudText.Draw(c, "ATTRACT  -  PRESS ANY KEY", cw / 2f, ch - 32f, SKTextAlign.Center, attractFont, NeonHudColor);
        }

        if (w.Mode == GameMode.Title)
        {
            DrawTitle(c, cw, ch);
            Marquee.Draw(c, MarqueeText, cw, ch);
        }
        else if (w.Mode == GameMode.GameOver)
        {
            using var bigFont   = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
            HudText.Draw(c, "GAME OVER",              cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   NeonHudColor);
            HudText.Draw(c, $"FINAL SCORE  {w.Score:00000}", cw / 2f, ch / 2f + 50f, SKTextAlign.Center, smallFont, NeonHudColor);
            HudText.Draw(c, "PRESS SPACE TO PLAY AGAIN",     cw / 2f, ch / 2f + 90f, SKTextAlign.Center, smallFont, NeonHudColor);
        }
    }

    static void DrawTitle(SKCanvas c, float cw, float ch)
    {
        Marquee.DrawRainbowTitle(c, "HOKULELE", cw, ch * 0.28f);

        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
        HudText.Draw(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.50f, SKTextAlign.Center, smallFont, NeonHudColor);
        HudText.Draw(c, "Arrows or A/D to move  -  Space to fire",  cw / 2f, ch * 0.55f, SKTextAlign.Center, smallFont, NeonHudColor);
    }
}
