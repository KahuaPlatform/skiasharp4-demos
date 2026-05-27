using System;
using System.Collections.Generic;
using System.Diagnostics;
using SkiaSharp;

namespace Pohaku.Game;

public static class Renderer
{
    // Retro palette
    private static readonly SKColor RetroGreen = new(0xCC, 0xFF, 0xCC);
    private static readonly SKColor BulletColor = new(0xFF, 0xFF, 0xFF);

    // Vibrant palette
    private static readonly SKColor NeonShipColor = new(0x33, 0xF8, 0xFF);
    private static readonly SKColor NeonAsteroidColor = new(0xFF, 0x33, 0xCC);
    private static readonly SKColor NeonSaucerColor = new(0x66, 0xFF, 0x33);
    private static readonly SKColor NeonBulletColor = new(0xFF, 0xEE, 0x33);
    private static readonly SKColor NeonFlameColor = new(0xFF, 0x66, 0x33);
    private static readonly SKColor NeonHudColor = new(0x33, 0xF8, 0xFF);
    private static readonly SKColor NeonTitleColor = new(0xFF, 0x33, 0xCC);
    private static readonly SKColor NeonBgTop = new(0x08, 0x02, 0x1A);
    private static readonly SKColor NeonBgBottom = new(0x20, 0x04, 0x40);

    private const string MarqueeText = "RUNNING ON UNO PLATFORM AND SKIASHARP 4";
    private const float MarqueeCharHeight = 64f;
    private const float MarqueeCharWidth = 44f;
    private const float MarqueeCharGap = 14f;
    private const float MarqueeSpeed = 220f;

    private static readonly Stopwatch MarqueeClock = Stopwatch.StartNew();

    private static readonly SKPaint MarqueePaint = new()
    {
        Color = RetroGreen,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 3.5f,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private static readonly SKPaint MarqueeNeonHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 11f,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7f),
    };

    private static readonly SKPaint MarqueeNeonSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private static readonly SKPaint NeonStrokeHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 5.5f,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f),
    };

    private static readonly SKPaint NeonStrokeSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2f,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private static readonly SKPaint NeonFillHalo = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f),
    };

    private static readonly SKPaint NeonFillSharp = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

    private static readonly Dictionary<char, SKPath> Glyphs = BuildGlyphs();

    private static readonly SKPath ShipBodyPath = BuildPath(stackalloc SKPoint[]
    {
        new(14, 0), new(-10, -9), new(-6, 0), new(-10, 9),
    }, close: true);

    private static readonly SKPath ShipFlamePath = BuildPath(stackalloc SKPoint[]
    {
        new(-7, -4), new(-14, 0), new(-7, 4),
    }, close: false);

    private static readonly SKPath LifeIconPath = BuildPath(stackalloc SKPoint[]
    {
        new(10, 0), new(-7, -7), new(-4, 0), new(-7, 7),
    }, close: true);

    private static SKPath BuildPath(ReadOnlySpan<SKPoint> points, bool close)
    {
        using var builder = new SKPathBuilder();
        builder.AddPoly(points, close);
        return builder.Detach();
    }

    // Vector font: each glyph is a list of disjoint line segments on a 4x6 grid,
    // baked to pixel coordinates at the marquee character size.
    private static Dictionary<char, SKPath> BuildGlyphs()
    {
        float sx = MarqueeCharWidth / 4f;
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
            ['D'] = G(0,0, 0,6,  0,0, 3,0,  3,0, 4,1,  4,1, 4,5,  4,5, 3,6,  3,6, 0,6),
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
            ['4'] = G(3,0, 0,4,  0,4, 4,4,  3,0, 3,6),
        };
    }

    // --- Vibrant helpers ---

    private static void NeonStroke(SKCanvas c, SKPath path, SKColor color)
    {
        NeonStrokeHalo.Color = color.WithAlpha(0xC0);
        c.DrawPath(path, NeonStrokeHalo);
        NeonStrokeSharp.Color = color;
        c.DrawPath(path, NeonStrokeSharp);
    }

    private static void NeonLine(SKCanvas c, float x1, float y1, float x2, float y2, SKColor color)
    {
        NeonStrokeHalo.Color = color.WithAlpha(0xC0);
        c.DrawLine(x1, y1, x2, y2, NeonStrokeHalo);
        NeonStrokeSharp.Color = color;
        c.DrawLine(x1, y1, x2, y2, NeonStrokeSharp);
    }

    private static void NeonCircleFill(SKCanvas c, float cx, float cy, float r, SKColor color)
    {
        NeonFillHalo.Color = color.WithAlpha(0xB0);
        c.DrawCircle(cx, cy, r * 1.8f, NeonFillHalo);
        NeonFillSharp.Color = color;
        c.DrawCircle(cx, cy, r, NeonFillSharp);
    }

    private static SKColor HsvToRgb(float hue, float sat, float val)
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

    private static void DrawNeonBackground(SKCanvas c, float cw, float ch)
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

    // --- Render entry point ---

    public static void Render(SKCanvas canvas, GameWorld world, float canvasW, float canvasH)
    {
        bool vib = world.VibrantMode;

        if (vib)
        {
            DrawNeonBackground(canvas, canvasW, canvasH);
        }
        else
        {
            canvas.Clear(SKColors.Black);
            using var scan = new SKPaint { Color = new SKColor(255, 255, 255, 6), StrokeWidth = 1, IsAntialias = false };
            for (float y = 0; y < canvasH; y += 3)
            {
                canvas.DrawLine(0, y, canvasW, y, scan);
            }
        }

        // Scale world to canvas
        float sx = canvasW / world.Width;
        float sy = canvasH / world.Height;
        float scale = MathF.Min(sx, sy);
        float ox = (canvasW - world.Width * scale) / 2f;
        float oy = (canvasH - world.Height * scale) / 2f;

        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);

        if (vib)
        {
            DrawWorldVibrant(canvas, world);
        }
        else
        {
            DrawWorldRetro(canvas, world);
        }

        canvas.Restore();

        DrawHud(canvas, world, canvasW, canvasH);
    }

    private static bool ShipVisible(GameWorld world)
    {
        if (!world.Ship.Alive) return false;
        if (world.Ship.InvincibleTime <= 0 || world.Mode != GameMode.Playing) return true;
        return ((int)(world.Ship.InvincibleTime * 10) % 2 == 0);
    }

    private static void DrawWorldRetro(SKCanvas canvas, GameWorld world)
    {
        using var line = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = RetroGreen,
            StrokeWidth = 1.6f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        foreach (var p in world.Particles)
        {
            byte alpha = (byte)Math.Clamp((p.Lifetime / p.MaxLife) * 255f, 0, 255);
            line.Color = RetroGreen.WithAlpha(alpha);
            canvas.DrawCircle(p.Position.X, p.Position.Y, 1.2f, line);
        }
        line.Color = RetroGreen;

        foreach (var a in world.Asteroids)
        {
            DrawAsteroid(canvas, line, a);
        }

        if (world.Saucer != null)
        {
            DrawSaucer(canvas, line, world.Saucer);
        }

        using (var dot = new SKPaint { Color = BulletColor, IsAntialias = true, Style = SKPaintStyle.Fill })
        {
            foreach (var b in world.Bullets)
            {
                canvas.DrawCircle(b.Position.X, b.Position.Y, 2f, dot);
            }
        }

        if (ShipVisible(world))
        {
            DrawShip(canvas, line, world.Ship);
        }
    }

    private static void DrawWorldVibrant(SKCanvas canvas, GameWorld world)
    {
        float hueBase = (float)((MarqueeClock.Elapsed.TotalSeconds * 90f) % 360f);

        // Particles — fade alpha by lifetime, hue cycles
        foreach (var p in world.Particles)
        {
            float lifeT = p.Lifetime / p.MaxLife;
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0, 255);
            float hue = (hueBase + (1f - lifeT) * 240f) % 360f;
            SKColor color = HsvToRgb(hue, 1f, 1f).WithAlpha(alpha);
            NeonCircleFill(canvas, p.Position.X, p.Position.Y, 1.6f, color);
        }

        foreach (var a in world.Asteroids)
        {
            DrawAsteroidVibrant(canvas, a);
        }

        if (world.Saucer != null)
        {
            DrawSaucerVibrant(canvas, world.Saucer);
        }

        // Bullets
        foreach (var b in world.Bullets)
        {
            NeonCircleFill(canvas, b.Position.X, b.Position.Y, 2.5f, NeonBulletColor);
        }

        if (ShipVisible(world))
        {
            canvas.Save();
            canvas.Translate(world.Ship.Position.X, world.Ship.Position.Y);
            canvas.RotateRadians(world.Ship.Rotation);
            NeonStroke(canvas, ShipBodyPath, NeonShipColor);
            if (world.Ship.ThrustOn)
            {
                NeonStroke(canvas, ShipFlamePath, NeonFlameColor);
            }
            canvas.Restore();
        }
    }

    private static void DrawAsteroid(SKCanvas c, SKPaint p, Asteroid a)
    {
        int n = a.Shape.Length;
        Span<SKPoint> points = stackalloc SKPoint[n];
        float step = MathF.Tau / n;
        for (int i = 0; i < n; i++)
        {
            float angle = a.Rotation + i * step;
            float r = a.Shape[i];
            points[i] = new SKPoint(
                a.Position.X + MathF.Cos(angle) * r,
                a.Position.Y + MathF.Sin(angle) * r);
        }

        using var builder = new SKPathBuilder();
        builder.AddPoly(points, close: true);
        using var path = builder.Detach();
        c.DrawPath(path, p);
    }

    private static void DrawShip(SKCanvas c, SKPaint p, Ship ship)
    {
        c.Save();
        c.Translate(ship.Position.X, ship.Position.Y);
        c.RotateRadians(ship.Rotation);

        c.DrawPath(ShipBodyPath, p);
        if (ship.ThrustOn)
        {
            c.DrawPath(ShipFlamePath, p);
        }
        c.Restore();
    }

    private static void DrawSaucer(SKCanvas c, SKPaint p, Saucer s)
    {
        float r = s.Radius;
        c.Save();
        c.Translate(s.Position.X, s.Position.Y);

        Span<SKPoint> body = stackalloc SKPoint[]
        {
            new(-r, 0f),
            new(-r * 0.55f, -r * 0.4f),
            new(r * 0.55f, -r * 0.4f),
            new(r, 0f),
            new(r * 0.55f, r * 0.4f),
            new(-r * 0.55f, r * 0.4f),
        };
        using (var bodyBuilder = new SKPathBuilder())
        {
            bodyBuilder.AddPoly(body, close: true);
            using var bodyPath = bodyBuilder.Detach();
            c.DrawPath(bodyPath, p);
        }

        c.DrawLine(-r, 0f, r, 0f, p);

        Span<SKPoint> dome = stackalloc SKPoint[]
        {
            new(-r * 0.55f, -r * 0.4f),
            new(-r * 0.25f, -r * 0.8f),
            new(r * 0.25f, -r * 0.8f),
            new(r * 0.55f, -r * 0.4f),
        };
        using (var domeBuilder = new SKPathBuilder())
        {
            domeBuilder.AddPoly(dome, close: false);
            using var domePath = domeBuilder.Detach();
            c.DrawPath(domePath, p);
        }

        c.Restore();
    }

    private static void DrawAsteroidVibrant(SKCanvas c, Asteroid a)
    {
        int n = a.Shape.Length;
        Span<SKPoint> points = stackalloc SKPoint[n];
        float step = MathF.Tau / n;
        for (int i = 0; i < n; i++)
        {
            float angle = a.Rotation + i * step;
            float r = a.Shape[i];
            points[i] = new SKPoint(
                a.Position.X + MathF.Cos(angle) * r,
                a.Position.Y + MathF.Sin(angle) * r);
        }
        using var builder = new SKPathBuilder();
        builder.AddPoly(points, close: true);
        using var path = builder.Detach();
        NeonStroke(c, path, NeonAsteroidColor);
    }

    private static void DrawSaucerVibrant(SKCanvas c, Saucer s)
    {
        float r = s.Radius;
        c.Save();
        c.Translate(s.Position.X, s.Position.Y);

        Span<SKPoint> body = stackalloc SKPoint[]
        {
            new(-r, 0f),
            new(-r * 0.55f, -r * 0.4f),
            new(r * 0.55f, -r * 0.4f),
            new(r, 0f),
            new(r * 0.55f, r * 0.4f),
            new(-r * 0.55f, r * 0.4f),
        };
        using (var bodyBuilder = new SKPathBuilder())
        {
            bodyBuilder.AddPoly(body, close: true);
            using var bodyPath = bodyBuilder.Detach();
            NeonStroke(c, bodyPath, NeonSaucerColor);
        }

        NeonLine(c, -r, 0f, r, 0f, NeonSaucerColor);

        Span<SKPoint> dome = stackalloc SKPoint[]
        {
            new(-r * 0.55f, -r * 0.4f),
            new(-r * 0.25f, -r * 0.8f),
            new(r * 0.25f, -r * 0.8f),
            new(r * 0.55f, -r * 0.4f),
        };
        using (var domeBuilder = new SKPathBuilder())
        {
            domeBuilder.AddPoly(dome, close: false);
            using var domePath = domeBuilder.Detach();
            NeonStroke(c, domePath, NeonSaucerColor);
        }

        c.Restore();
    }

    private static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        bool vib = w.VibrantMode;
        SKColor hudColor = vib ? NeonHudColor : RetroGreen;

        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
        DrawHudText(c, $"{w.Score:00000}", 24, 36, SKTextAlign.Left, font, hudColor, vib);

        if (w.HighScore > 0)
        {
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
            DrawHudText(c, $"HI {w.HighScore:00000}", cw / 2f, 28, SKTextAlign.Center, smallFont, hudColor, vib);
        }

        // Lives icons
        if (w.Mode == GameMode.Playing)
        {
            for (int i = 0; i < w.Ship.Lives; i++)
            {
                c.Save();
                c.Translate(28 + i * 20, 60);
                c.RotateDegrees(-90);
                if (vib)
                {
                    NeonStroke(c, LifeIconPath, NeonShipColor);
                }
                else
                {
                    using var line = new SKPaint
                    {
                        Color = RetroGreen,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 1.6f,
                        IsAntialias = true,
                    };
                    c.DrawPath(LifeIconPath, line);
                }
                c.Restore();
            }
        }

        if (w.Mode == GameMode.Demo)
        {
            DrawMarquee(c, cw, ch, vib);
        }

        if (w.Mode == GameMode.Demo && w.ShowAttractText)
        {
            using var bigFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 42);
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
            SKColor titleColor = vib ? NeonTitleColor : RetroGreen;
            DrawHudText(c, "POHAKU", cw / 2f, ch / 2f - 60, SKTextAlign.Center, bigFont, titleColor, vib);
            DrawHudText(c, "PRESS SPACE OR CLICK TO PLAY", cw / 2f, ch / 2f + 10, SKTextAlign.Center, smallFont, hudColor, vib);
            DrawHudText(c, "Arrows / WASD to fly  -  Space to fire  -  H for hyperspace  -  V for vibe", cw / 2f, ch / 2f + 50, SKTextAlign.Center, smallFont, hudColor, vib);
        }

        if (w.Mode == GameMode.GameOver)
        {
            using var bigFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
            DrawHudText(c, "GAME OVER", cw / 2f, ch / 2f, SKTextAlign.Center, bigFont, hudColor, vib);
        }
    }

    private static void DrawHudText(SKCanvas c, string text, float x, float y, SKTextAlign align, SKFont font, SKColor color, bool vibrant)
    {
        if (vibrant)
        {
            NeonFillHalo.Color = color.WithAlpha(0xC0);
            c.DrawText(text, x, y, align, font, NeonFillHalo);
            NeonFillSharp.Color = color;
            c.DrawText(text, x, y, align, font, NeonFillSharp);
        }
        else
        {
            using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
            c.DrawText(text, x, y, align, font, paint);
        }
    }

    private static void DrawMarquee(SKCanvas c, float cw, float ch, bool vibrant)
    {
        float advance = MarqueeCharWidth + MarqueeCharGap;
        float totalW = MarqueeText.Length * advance;
        float loop = totalW + cw;
        double time = MarqueeClock.Elapsed.TotalSeconds;
        float pixelOffset = (float)((time * MarqueeSpeed) % loop);
        float startX = cw - pixelOffset;
        float baselineY = ch * 0.92f;

        // Tilt the marquee plane back around its bottom edge (Star Wars crawl style).
        // In local coords (after Translate below), y=0 is top of glyphs, y=h is bottom (rotation axis).
        const float TiltDegrees = 30f;
        float h = MarqueeCharHeight;
        float tilt = TiltDegrees * MathF.PI / 180f;
        float cosT = MathF.Cos(tilt);
        float sinT = MathF.Sin(tilt);
        float d = 3f * h;
        var perspective = new SKMatrix
        {
            ScaleX = 1f, SkewX = 0f,            TransX = 0f,
            SkewY  = 0f, ScaleY = cosT,         TransY = h * (1f - cosT),
            Persp0 = 0f, Persp1 = -sinT / d,    Persp2 = 1f + h * sinT / d,
        };

        float centerX = cw / 2f;
        c.Save();
        c.Translate(centerX, baselineY - h);
        c.Concat(in perspective);

        float wTop = 1f + h * sinT / d;
        float cullPad = (cw / 2f) * (wTop - 1f) + MarqueeCharWidth;
        for (int i = 0; i < MarqueeText.Length; i++)
        {
            float x = startX + i * advance;
            if (x + MarqueeCharWidth < -cullPad || x > cw + cullPad) continue;
            if (!Glyphs.TryGetValue(MarqueeText[i], out var glyph)) continue;
            c.Save();
            c.Translate(x - centerX, 0f);

            if (vibrant)
            {
                float hue = ((float)time * 75f + i * 18f) % 360f;
                SKColor color = HsvToRgb(hue, 1f, 1f);
                MarqueeNeonHalo.Color = color.WithAlpha(0xC0);
                c.DrawPath(glyph, MarqueeNeonHalo);
                MarqueeNeonSharp.Color = color;
                c.DrawPath(glyph, MarqueeNeonSharp);
            }
            else
            {
                c.DrawPath(glyph, MarqueePaint);
            }

            c.Restore();
        }

        c.Restore();
    }
}
