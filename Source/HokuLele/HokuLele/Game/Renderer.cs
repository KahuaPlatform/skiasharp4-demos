using System.Diagnostics;
using SkiaSharp;

namespace HokuLele.Game;

public static class Renderer
{
    // --- Neon palette ---
    static readonly SKColor NeonPlayerColor       = new(0x33, 0xF8, 0xFF);  // cyan body
    static readonly SKColor NeonPlayerCockpit     = new(0xFF, 0x44, 0x44);  // red cockpit
    static readonly SKColor NeonPlayerEngineGlow  = new(0xFF, 0xCC, 0x33);  // gold engine
    static readonly SKColor NeonBulletColor       = new(0xFF, 0xEE, 0x33);  // player bullets — bright yellow
    static readonly SKColor NeonEnemyBulletColor  = new(0xFF, 0x44, 0x66);  // enemy bullets — hot red, easy to read at speed
    static readonly SKColor NeonHudColor          = new(0x33, 0xF8, 0xFF);
    static readonly SKColor NeonBgTop             = new(0x08, 0x02, 0x1A);
    static readonly SKColor NeonBgBottom          = new(0x20, 0x04, 0x40);

    record struct EnemyPalette(SKColor Body, SKColor Accent);

    // Per-enemy-kind palette. Index matches Enemy.Kind:
    //   0 = drone, 1 = wing, 2 = captain, 3 = boss, 4 = mothership, 5 = snowflake
    // Kinds 4 and 5 use specialised color sets (MothershipPetalColors / Kahua*).
    static readonly EnemyPalette[] EnemyPalettes =
    {
        new(new(0x99, 0xFF, 0x55), new(0xFF, 0xEE, 0x33)),  // drone      — acid green + neon yellow
        new(new(0xFF, 0x44, 0xAA), new(0x33, 0xCC, 0xFF)),  // wing       — hot pink + electric cyan
        new(new(0x33, 0xCC, 0xFF), new(0xFF, 0x44, 0xAA)),  // captain    — electric cyan + magenta
        new(new(0xFF, 0xAA, 0x33), new(0x33, 0xCC, 0xFF)),  // boss       — neon orange + cyan
        new(new(0xFF, 0xFF, 0xFF), new(0xFF, 0xFF, 0xFF)),  // mothership — palette ignored
        new(new(0xFF, 0xFF, 0xFF), new(0xFF, 0xFF, 0xFF)),  // snowflake  — palette ignored
    };

    // Uno Platform mark — the actual logo, compiled from the SVG path data in
    // Assets/Icons/icon_foreground.svg. Used by the kind-4 mothership at stage size.
    static SKPath MakeUnoPath(string d, float tx, float ty)
    {
        var p = SKPath.ParseSvgPathData(d);
        if (tx != 0f || ty != 0f) p.Transform(SKMatrix.CreateTranslation(tx, ty));
        return p;
    }

    static readonly (SKPath path, SKColor fill)[] UnoLogoPaths =
    {
        // Four interlocking colored arcs
        (MakeUnoPath("M 34.758,38.865 H 34.746 C 31.892,38.86 29.342,36.882 26.152,33.692 l -6.93,-6.873 2.166,-2.188 6.937,6.88 c 3.075,3.074 4.876,4.272 6.427,4.275 h 0.005 c 1.567,0 3.467,-1.262 6.558,-4.353 l 3.541,-3.587 c 1.784,-1.784 2.57,-3.34 2.408,-4.762 -0.13,-1.156 -0.894,-2.397 -2.401,-3.904 L 44.83,19.146 C 43.202,17.414 41.211,15.483 39.131,14.414 38.745,12.437 37.48,10.881 37.3,10.596 c 3.803,0.559 7.197,3.703 9.758,6.424 2.788,2.794 5.803,7.176 -0.018,12.996 l -3.54,3.588 c -3.251,3.25 -5.844,5.261 -8.742,5.261", 0f, 0f), new SKColor(0x7A, 0x67, 0xF8)),
        (MakeUnoPath("m 25.399,28.608 6.492,-6.562 c 3.076,-3.076 4.274,-4.877 4.276,-6.428 0.004,-1.567 -1.257,-3.469 -4.352,-6.563 L 28.228,5.515 C 24.58,1.867 22.369,2.699 19.561,5.507 L 19.528,5.54 c -1.54,1.448 -3.237,3.182 -4.346,5.01 -1.031,0.073 -2.361,0.424 -3.997,1.518 0.906,-3.397 3.737,-6.422 6.216,-8.755 2.794,-2.789 7.177,-5.804 12.997,0.017 l 3.588,3.54 c 3.255,3.256 5.266,5.851 5.26,8.754 -0.005,2.854 -1.982,5.404 -5.172,8.594 l -6.489,6.559 z", 0f, 0f), new SKColor(0xF8, 0x59, 0x77)),
        (MakeUnoPath("M 12.522,38.707 C 8.939,37.946 5.746,34.972 3.308,32.382 2.035,31.106 0.321,29.13 0.042,26.663 c -0.274,-2.414 0.8,-4.795 3.283,-7.278 l 3.542,-3.588 c 3.25,-3.25 5.843,-5.261 8.74,-5.261 h 0.013 c 2.854,0.005 5.404,1.983 8.593,5.172 l 7.046,6.976 -2.165,2.19 -7.053,-6.983 c -3.076,-3.076 -4.876,-4.273 -6.427,-4.276 h -0.006 c -1.566,0 -3.466,1.261 -6.557,4.352 L 5.51,21.555 c -1.784,1.784 -2.57,3.34 -2.409,4.762 0.131,1.156 0.894,2.396 2.402,3.904 l 0.033,0.034 c 1.55,1.649 3.43,3.479 5.401,4.573 0.168,1.739 1.2,3.297 1.585,3.88", 0f, 0f), new SKColor(0x15, 0x9B, 0xFF)),
        (MakeUnoPath("m 26.32,49.827 c -1.925,0 -4.114,-0.886 -6.557,-3.33 l -3.588,-3.54 C 9.167,35.949 9.151,32.546 16.086,25.61 l 6.802,-6.872 2.193,2.162 -6.812,6.882 c -3.076,3.076 -4.273,4.877 -4.276,6.427 -0.003,1.568 1.258,3.47 4.352,6.563 l 3.588,3.541 c 3.646,3.647 5.858,2.816 8.666,0.008 l 0.034,-0.033 c 1.654,-1.555 3.5,-3.46 4.593,-5.437 1.661,-0.14 2.9,-0.841 3.835,-1.438 -0.8,3.537 -3.738,6.69 -6.302,9.102 -1.62,1.618 -3.777,3.312 -6.439,3.312", 0f, 0f), new SKColor(0x67, 0xE5, 0xAD)),
        // Four small black diamond accents at the arc junctions
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

    // Kahua brand mark — high-res PNG embedded as an assembly resource (see csproj
    // <EmbeddedResource Include="Assets\Icons\kahua_snowflake.png" />). Loaded once
    // at type-init; case 5 falls back to the stylized snowflake if load fails.
    static readonly SKImage? KahuaSnowflakeImage = LoadEmbeddedImage("HokuLele.Assets.Icons.kahua_snowflake.png");
    static readonly SKSamplingOptions KahuaSnowflakeSampling = new(SKCubicResampler.Mitchell);
    // Glow halo for the embedded Kahua PNG — ImageFilter blur the image's own colors
    // outward so the brand-mark enemy reads as the same neon-glow language as the
    // hand-drawn vector enemies (which use NeonStrokeHalo for the same effect).
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
        catch
        {
            return null;
        }
    }

    // Kahua-snowflake palette — used by the kind-5 "snowflake" enemy (8 radial spikes
    // around a dotted white core, inspired by the Kahua logo).
    static readonly SKColor KahuaOrange = new(0xF5, 0x82, 0x20);
    static readonly SKColor KahuaRed    = new(0xE1, 0x14, 0x2A);
    static readonly SKColor SnowflakeCore = new(0xFF, 0xFF, 0xFF);

    const string MarqueeText = "HOKULELE · UNO PLATFORM · SKIASHARP 4 · NEON SHMUP DEMO";
    const float MarqueeCharHeight = 56f;
    const float MarqueeCharWidth  = 40f;
    const float MarqueeCharGap    = 12f;
    const float MarqueeSpeed      = 200f;

    static readonly Stopwatch MarqueeClock = Stopwatch.StartNew();

    // --- Paints (one of each kind, mutated per draw) ---

    static readonly SKPaint MarqueeNeonHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 11f,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7f),
    };

    static readonly SKPaint MarqueeNeonSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    static readonly SKPaint NeonStrokeHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 5.5f,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f),
    };

    static readonly SKPaint NeonStrokeSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    static readonly SKPaint NeonFillHalo = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f),
    };

    static readonly SKPaint NeonFillSharp = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

    // --- Vector silhouettes for the player + four enemy archetypes ---
    // All shapes are drawn pointing "up" (negative Y) and centred at (0,0).
    // Renderer.Translate() positions them; no rotation is applied.

    // Player: sharp nose, slender body, swept wings, twin engine tails.
    static readonly SKPath PlayerBodyPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -16),
        new(2, -8), new(3, -2),
        new(11, 6), new(7, 8), new(4, 5),
        new(3, 10), new(-3, 10),
        new(-4, 5), new(-7, 8), new(-11, 6),
        new(-3, -2), new(-2, -8),
    }, close: true);

    // Optional inner cockpit detail drawn over the body in player color
    static readonly SKPath PlayerCockpitPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -6), new(2, 2), new(-2, 2),
    }, close: true);

    // Drone (kind 0): slender dart, swept-back wings.
    static readonly SKPath DroneBodyPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -10),
        new(3, -4),
        new(9, 4), new(4, 3),
        new(3, 7), new(-3, 7),
        new(-4, 3), new(-9, 4),
        new(-3, -4),
    }, close: true);

    // Wing (kind 1): wide crab/manta with hooked tips and a notched body.
    static readonly SKPath WingBodyPath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -8),
        new(5, -5), new(13, -1), new(11, 3),
        new(6, 2), new(4, 8), new(-4, 8),
        new(-6, 2), new(-11, 3), new(-13, -1),
        new(-5, -5),
    }, close: true);

    // Captain (kind 2): hex body with two-stage swept wings.
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

    // Captain antenna prongs — drawn separately as two short strokes above the body.
    static readonly SKPath CaptainAntennaePath = MakeCaptainAntennae();
    static SKPath MakeCaptainAntennae()
    {
        using var b = new SKPathBuilder();
        b.MoveTo(-2.5f, -11f); b.LineTo(-5f, -16f);
        b.MoveTo( 2.5f, -11f); b.LineTo( 5f, -16f);
        return b.Detach();
    }

    // Boss (kind 3): wide hex-octagon mothership, split outer wing tips, central
    // bay slot underneath as a structural mass tell.
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

    // (Kind 4 mothership is rendered from the Uno SVG paths above — no
    // stylized vector path needed here.)

    // Snowflake (kind 5): one Y-forked spike pointing up; drawn eight times at
    // 45° intervals in Kahua orange. A white core with small orange accent dots
    // sits in the centre. Inspired by the Kahua brand mark's radial geometry.
    static readonly SKPath SnowflakeSpikePath = BuildPath(stackalloc SKPoint[]
    {
        new(0, -6),
        new(1.5f, -8), new(2.5f, -12),
        new(2, -16), new(0.5f, -13),
        new(0, -12),
        new(-0.5f, -13), new(-2, -16),
        new(-2.5f, -12), new(-1.5f, -8),
    }, close: true);

    // --- Vector font for the marquee + title ---
    static readonly Dictionary<char, SKPath> Glyphs = BuildGlyphs();

    static SKPath BuildPath(ReadOnlySpan<SKPoint> points, bool close)
    {
        using var b = new SKPathBuilder();
        b.AddPoly(points, close);
        return b.Detach();
    }

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

    // --- Neon primitive helpers (mirrors the pattern from Pohaku) ---

    static void NeonStroke(SKCanvas c, SKPath path, SKColor color)
    {
        NeonStrokeHalo.Color = color.WithAlpha(0xC0);
        c.DrawPath(path, NeonStrokeHalo);
        NeonStrokeSharp.Color = color;
        c.DrawPath(path, NeonStrokeSharp);
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

    // Thin neon border around the world's logical playfield. Defines where
    // gameplay begins so the ambient backdrop in the side bars doesn't visually
    // merge with the playfield. Drawn in world coords (0,0)..(Width,Height).
    static void DrawPlayfieldBorder(SKCanvas c, GameWorld world)
    {
        var rect = new SKRect(0, 0, world.Width, world.Height);
        NeonStrokeHalo.StrokeWidth = 6f;
        NeonStrokeHalo.Color = NeonHudColor.WithAlpha(0x80);
        c.DrawRect(rect, NeonStrokeHalo);
        NeonStrokeSharp.StrokeWidth = 1.4f;
        NeonStrokeSharp.Color = NeonHudColor.WithAlpha(0xC0);
        c.DrawRect(rect, NeonStrokeSharp);
        NeonStrokeHalo.StrokeWidth = 5.5f;
        NeonStrokeSharp.StrokeWidth = 2f;
    }

    static void DrawNeonBackground(SKCanvas c, float cw, float ch)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, ch),
                new[] { NeonBgTop, NeonBgBottom },
                SKShaderTileMode.Clamp),
        };
        c.DrawRect(0, 0, cw, ch, paint);
    }

    // --- Parallax starfield --------------------------------------------------
    // Drawn inside the world transform so star positions are in world coords.
    // Three speed/brightness layers create depth; stars scrolling top-to-bottom
    // gives a Galaga-style "falling through space" feel without any sample data.

    struct Star { public float X, Y, Speed, Brightness; }
    const int StarCount = 110;
    static Star[]? _stars;
    static readonly Random _starRng = new(13);
    static readonly System.Diagnostics.Stopwatch _starsClock = System.Diagnostics.Stopwatch.StartNew();
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
            _stars[i].Speed      = layer switch { 0 => 25f, 1 => 65f, _ => 130f };
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

    public static void Render(SKCanvas canvas, GameWorld world, float canvasW, float canvasH)
    {
        UpdateStars(world.Width, world.Height);
        DrawNeonBackground(canvas, canvasW, canvasH);

        // Letterbox the virtual world onto the canvas, preserving aspect.
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
        DrawPlayfieldBorder(canvas, world);

        foreach (var p in world.Particles)
        {
            float lifeT = p.Lifetime / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonCircleFill(canvas, p.Position.X, p.Position.Y, 1.8f, color);
        }

        // Score popups (+200 text floating upward and fading)
        using (var popupFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 20))
        {
            foreach (var sp in world.ScorePopups)
            {
                float lifeT = sp.Lifetime / MathF.Max(0.001f, sp.MaxLife);
                byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
                var color = new SKColor(sp.Color).WithAlpha(alpha);
                NeonFillSharp.Color = color;
                canvas.DrawText($"+{sp.Value}", sp.Position.X, sp.Position.Y, SKTextAlign.Center, popupFont, NeonFillSharp);
            }
        }

        foreach (var b in world.Bullets)
        {
            var color = b.FromPlayer ? NeonBulletColor : NeonEnemyBulletColor;
            NeonCircleFill(canvas, b.Position.X, b.Position.Y, b.Radius, color);
        }

        // Tractor beams render under enemies so the boss visibly emits the beam downward.
        foreach (var e in world.Enemies)
        {
            if (e.Alive && e.State == EnemyState.BeamActive)
                DrawTractorBeam(canvas, e, world.Height);
        }

        foreach (var e in world.Enemies)
        {
            DrawEnemy(canvas, e);
        }

        // Captive trailing the boss (after enemies so it sits visually attached).
        foreach (var e in world.Enemies)
        {
            if (e.Alive && e.HasCaptive) DrawCaptive(canvas, e);
        }

        if (world.Player.Alive && PlayerVisible(world.Player))
        {
            DrawPlayer(canvas, world.Player.Position.X, world.Player.Position.Y);
            if (world.Player.HasWingman)
                DrawPlayer(canvas, world.Player.Position.X + world.Player.WingmanOffsetX, world.Player.Position.Y);
        }
    }

    // Tractor beam: yellow-to-amber trapezoid widening from boss down to the bottom of
    // the playfield, with a bright stroke at the beam mouth. Coordinates mirror
    // GameWorld.BeamTopHalfWidth / BeamBottomHalfWidth so the visual matches the hit test.
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
                new SKPoint(bx, by),
                new SKPoint(bx, botY),
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

    // Captive ship: small upside-down player silhouette attached below the boss.
    static void DrawCaptive(SKCanvas canvas, Enemy boss)
    {
        canvas.Save();
        canvas.Translate(boss.Position.X, boss.Position.Y + 22f);
        canvas.Scale(0.6f);
        canvas.RotateDegrees(180f);  // captured — drawn upside down
        NeonStroke(canvas, PlayerBodyPath, NeonPlayerColor);
        NeonFillSharp.Color = NeonPlayerCockpit;
        canvas.DrawPath(PlayerCockpitPath, NeonFillSharp);
        canvas.Restore();
    }

    static void DrawPlayer(SKCanvas canvas, float x, float y)
    {
        canvas.Save();
        canvas.Translate(x, y);
        NeonStroke(canvas, PlayerBodyPath, NeonPlayerColor);
        // Red cockpit chevron + a pair of gold engine-glow dots at the rear-tail tips.
        NeonFillSharp.Color = NeonPlayerCockpit;
        canvas.DrawPath(PlayerCockpitPath, NeonFillSharp);
        NeonCircleFill(canvas, -7f, 8f, 1.2f, NeonPlayerEngineGlow);
        NeonCircleFill(canvas,  7f, 8f, 1.2f, NeonPlayerEngineGlow);
        canvas.Restore();
    }

    static void DrawEnemy(SKCanvas canvas, Enemy enemy)
    {
        int kind = Math.Clamp(enemy.Kind, 0, EnemyPalettes.Length - 1);

        canvas.Save();
        canvas.Translate(enemy.Position.X, enemy.Position.Y);
        // Kinds 0-3 rotate to face along their motion direction (banking into curves);
        // kinds 4-5 are real brand marks and stay upright regardless of trajectory.
        if (kind < 4) canvas.RotateRadians(enemy.Rotation);
        DrawEnemyShape(canvas, kind);
        canvas.Restore();
    }

    // Fallback rendering used by kind 5 only when the embedded Kahua PNG fails to load.
    static void DrawStylizedSnowflake(SKCanvas canvas)
    {
        for (int spike = 0; spike < 8; spike++)
        {
            canvas.Save();
            canvas.RotateDegrees(spike * 45f);
            NeonStroke(canvas, SnowflakeSpikePath, KahuaOrange);
            canvas.Restore();
        }
        NeonFillSharp.Color = SnowflakeCore;
        canvas.DrawCircle(0f, 0f, 5f, NeonFillSharp);
        NeonFillSharp.Color = KahuaOrange;
        canvas.DrawCircle(-2.4f, -2f, 0.9f, NeonFillSharp);
        canvas.DrawCircle( 2.4f, -2f, 0.9f, NeonFillSharp);
        canvas.DrawCircle(-2.4f,  2f, 0.9f, NeonFillSharp);
        canvas.DrawCircle( 2.4f,  2f, 0.9f, NeonFillSharp);
        NeonFillSharp.Color = KahuaRed;
        canvas.DrawCircle(0f, 0f, 0.9f, NeonFillSharp);
    }

    // Body + accent geometry per kind. Shared by world rendering and the title-screen demo.
    static void DrawEnemyShape(SKCanvas canvas, int kind)
    {
        var p = EnemyPalettes[kind];
        switch (kind)
        {
            case 0: // drone — body + center accent dot
                NeonStroke(canvas, DroneBodyPath, p.Body);
                NeonCircleFill(canvas, 0f, -3f, 1.4f, p.Accent);
                break;
            case 1: // wing — body + accent dots at the hooked wing tips
                NeonStroke(canvas, WingBodyPath, p.Body);
                NeonCircleFill(canvas, -13f, -1f, 1.6f, p.Accent);
                NeonCircleFill(canvas,  13f, -1f, 1.6f, p.Accent);
                break;
            case 2: // captain — body + accent-colored antennae + center dot
                NeonStroke(canvas, CaptainBodyPath,     p.Body);
                NeonStroke(canvas, CaptainAntennaePath, p.Accent);
                NeonCircleFill(canvas, 0f, -2f, 1.5f, p.Accent);
                break;
            case 3: // boss — body + central tractor port + accent wing-tip dots
                NeonStroke(canvas, BossBodyPath, p.Body);
                NeonCircleFill(canvas,   0f,  0f, 2.5f, p.Accent);
                NeonCircleFill(canvas, -16f, -1f, 1.4f, p.Accent);
                NeonCircleFill(canvas,  16f, -1f, 1.4f, p.Accent);
                break;
            case 4: // Uno Platform mark — rendered from the actual SVG paths in Assets/Icons/icon_foreground.svg
            {
                const float TargetSize = 32f;
                float s = TargetSize / MathF.Max(UnoLogoBounds.Width, UnoLogoBounds.Height);
                canvas.Save();
                canvas.Scale(s);
                canvas.Translate(-UnoLogoBounds.MidX, -UnoLogoBounds.MidY);
                // Halo pass: draw each colored arc through NeonFillHalo (a blurred fill).
                // Skip the black accent dots — blurring black just darkens the surrounding
                // pixels and looks like grime against the dark background.
                foreach (var (path, fill) in UnoLogoPaths)
                {
                    if (fill == SKColors.Black) continue;
                    NeonFillHalo.Color = fill.WithAlpha(0xC0);
                    canvas.DrawPath(path, NeonFillHalo);
                }
                foreach (var (path, fill) in UnoLogoPaths)
                {
                    NeonFillSharp.Color = fill;
                    canvas.DrawPath(path, NeonFillSharp);
                }
                canvas.Restore();
                break;
            }
            case 5: // Kahua brand mark — rendered from embedded high-res PNG when available
                if (KahuaSnowflakeImage is not null)
                {
                    const float TargetSize = 38f;
                    float scale = TargetSize / MathF.Max(KahuaSnowflakeImage.Width, KahuaSnowflakeImage.Height);
                    float w = KahuaSnowflakeImage.Width  * scale;
                    float h = KahuaSnowflakeImage.Height * scale;
                    var rect = new SKRect(-w / 2f, -h / 2f, w / 2f, h / 2f);
                    // Halo: image with a blur ImageFilter. The image's own orange/red
                    // colors bleed outward, matching the neon-stroke glow of the other
                    // enemies. Then draw the sharp image on top.
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

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
        DrawHudText(c, $"{w.Score:00000}", 24, 36, SKTextAlign.Left, font, NeonHudColor);

        if (w.HighScore > 0)
        {
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
            DrawHudText(c, $"HI {w.HighScore:00000}", cw / 2f, 28, SKTextAlign.Center, smallFont, NeonHudColor);
        }

        if (w.Mode == GameMode.Playing)
        {
            using var stageFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 22);
            string stageLabel = w.IsChallengeStage ? "CHALLENGE" : $"STAGE {w.Stage}";
            DrawHudText(c, stageLabel, cw - 24, 32, SKTextAlign.Right, stageFont, NeonHudColor);

            if (!w.BulletCapEnabled)
            {
                using var cheatFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);
                DrawHudText(c, "CHEAT: NO BULLET CAP", 24, 64, SKTextAlign.Left, cheatFont, new SKColor(0xFF, 0xCC, 0x33));
            }

            // Lives indicator — small ship icons in the bottom-left.
            for (int i = 0; i < w.Player.Lives; i++)
            {
                c.Save();
                c.Translate(28f + i * 30f, ch - 28f);
                c.Scale(0.85f);
                NeonStroke(c, PlayerBodyPath, NeonPlayerColor);
                NeonFillSharp.Color = NeonPlayerCockpit;
                c.DrawPath(PlayerCockpitPath, NeonFillSharp);
                c.Restore();
            }

            // Between-stage placard: large centered announce text.
            if (w.WaveState == WaveState.Placard && !string.IsNullOrEmpty(w.PlacardText))
            {
                using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 44);
                SKColor color = w.PlacardText.Contains("CHALLENG") || w.PlacardText.Contains("BONUS")
                    ? new SKColor(0xFF, 0xAA, 0x33)
                    : NeonHudColor;
                DrawHudText(c, w.PlacardText, cw / 2f, ch * 0.42f, SKTextAlign.Center, placardFont, color);
            }
        }

        if (w.Mode == GameMode.Attract)
        {
            using var attractFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 20);
            DrawHudText(c, "ATTRACT  -  PRESS ANY KEY", cw / 2f, ch - 32f, SKTextAlign.Center, attractFont, NeonHudColor);
        }

        if (w.Mode == GameMode.Title)
        {
            DrawTitle(c, cw, ch);
            DrawMarquee(c, cw, ch);
        }
        else if (w.Mode == GameMode.GameOver)
        {
            using var bigFont   = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
            DrawHudText(c, "GAME OVER",              cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   NeonHudColor);
            DrawHudText(c, $"FINAL SCORE  {w.Score:00000}", cw / 2f, ch / 2f + 50f, SKTextAlign.Center, smallFont, NeonHudColor);
            DrawHudText(c, "PRESS SPACE TO PLAY AGAIN",     cw / 2f, ch / 2f + 90f, SKTextAlign.Center, smallFont, NeonHudColor);
        }
    }

    static void DrawHudText(SKCanvas c, string text, float x, float y, SKTextAlign align, SKFont font, SKColor color)
    {
        NeonFillHalo.Color = color.WithAlpha(0xC0);
        c.DrawText(text, x, y, align, font, NeonFillHalo);
        NeonFillSharp.Color = color;
        c.DrawText(text, x, y, align, font, NeonFillSharp);
    }

    // Title text rendered with the vector glyph font, centred horizontally,
    // upper third of the canvas. Subtitle uses Skia's text rendering below it.
    static void DrawTitle(SKCanvas c, float cw, float ch)
    {
        const string title = "HOKULELE";
        float advance = MarqueeCharWidth + MarqueeCharGap;
        float titleW = title.Length * advance - MarqueeCharGap;

        c.Save();
        c.Translate((cw - titleW) / 2f, ch * 0.28f);

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
        DrawHudText(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.50f, SKTextAlign.Center, smallFont, NeonHudColor);
        DrawHudText(c, "Arrows or A/D to move  -  Space to fire",  cw / 2f, ch * 0.55f, SKTextAlign.Center, smallFont, NeonHudColor);
    }

    // Bottom-of-screen scroller with a perspective tilt back toward the horizon —
    // ported verbatim from the Pohaku marquee implementation.
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
