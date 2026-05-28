using System;
using System.Collections.Generic;
using System.Diagnostics;
using SkiaSharp;

namespace Mahina.Game;

// Lunar-Lander-style vector renderer. Same neon-glow chassis as the rest of the
// repo (background gradient + parallax starfield + marquee + vector glyph font
// + neon paints) with new draws for the lander silhouette, terrain polyline,
// landing pads, thrust flame, HUD readouts.
public static class Renderer
{
    // --- Palette ---
    static readonly SKColor LanderBody     = new(0xCC, 0xEE, 0xFF);  // pale moonlight white
    static readonly SKColor LanderAccent   = new(0xFF, 0xCC, 0x33);  // gold leg/engine accents
    static readonly SKColor TerrainColor   = new(0x88, 0xCC, 0xFF);  // cool cyan-ish lunar surface
    static readonly SKColor PadGoldLow     = new(0xFF, 0xCC, 0x33);  // 2x pads
    static readonly SKColor PadGoldMid     = new(0xFF, 0xAA, 0x33);  // 3x pads
    static readonly SKColor PadGoldHigh    = new(0xFF, 0x55, 0x55);  // 5x pads (hottest = riskiest)
    static readonly SKColor HudColor       = new(0x33, 0xF8, 0xFF);
    static readonly SKColor HudWarnColor   = new(0xFF, 0x55, 0x66);
    static readonly SKColor BgTop          = new(0x05, 0x00, 0x14);
    static readonly SKColor BgBottom       = new(0x18, 0x02, 0x36);

    const string MarqueeText = "MAHINA · UNO PLATFORM · SKIASHARP 4 · NEON LUNAR LANDER";
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
                new SKPoint(0, 0), new SKPoint(0, ch),
                new[] { BgTop, BgBottom }, SKShaderTileMode.Clamp),
        };
        c.DrawRect(0, 0, cw, ch, paint);
    }

    // Thin neon border around the world's logical playfield. Same idea as Lua/
    // HokuLele — defines the gameplay rectangle when the BackgroundSurface stars
    // bleed into the side bars.
    static void DrawPlayfieldBorder(SKCanvas c, GameWorld world)
    {
        var rect = new SKRect(0, 0, world.Width, world.Height);
        NeonStrokeHalo.StrokeWidth = 6f;
        NeonStrokeHalo.Color = HudColor.WithAlpha(0x80);
        c.DrawRect(rect, NeonStrokeHalo);
        NeonStrokeSharp.StrokeWidth = 1.4f;
        NeonStrokeSharp.Color = HudColor.WithAlpha(0xC0);
        c.DrawRect(rect, NeonStrokeSharp);
        NeonStrokeHalo.StrokeWidth = 5.5f;
        NeonStrokeSharp.StrokeWidth = 2f;
    }

    // --- Render entry point ---

    public static void Render(SKCanvas canvas, GameWorld world, float canvasW, float canvasH)
    {
        DrawNeonBackground(canvas, canvasW, canvasH);

        float scale = MathF.Min(canvasW / world.Width, canvasH / world.Height);
        float ox = (canvasW - world.Width * scale) / 2f;
        float oy = (canvasH - world.Height * scale) / 2f;

        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);
        DrawWorld(canvas, world);
        canvas.Restore();

        DrawHud(canvas, world, canvasW, canvasH);
    }

    static void DrawWorld(SKCanvas canvas, GameWorld world)
    {
        DrawPlayfieldBorder(canvas, world);
        DrawTerrain(canvas, world);

        foreach (var p in world.Particles)
        {
            float lifeT = p.Life / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonCircleFill(canvas, p.Pos.X, p.Pos.Y, p.Size, color);
        }

        if (world.Lander.Alive) DrawLander(canvas, world);

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

    static void DrawTerrain(SKCanvas c, GameWorld world)
    {
        var t = world.Terrain;
        if (t.Points.Length < 2) return;

        // Terrain polyline. Halo + sharp pair, separately so neon glow comes through.
        using var b = new SKPathBuilder();
        b.MoveTo(t.Points[0].X, t.Points[0].Y);
        for (int i = 1; i < t.Points.Length; i++) b.LineTo(t.Points[i].X, t.Points[i].Y);
        // Close back along the bottom so we can also fill the subterrain dark.
        b.LineTo(world.Width, world.Height + 10f);
        b.LineTo(0, world.Height + 10f);
        b.Close();
        using var path = b.Detach();
        using var fill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x02, 0x06, 0x18, 0xE0),
        };
        c.DrawPath(path, fill);

        // Stroke the surface line, not the closing.
        using var line = new SKPathBuilder();
        line.MoveTo(t.Points[0].X, t.Points[0].Y);
        for (int i = 1; i < t.Points.Length; i++) line.LineTo(t.Points[i].X, t.Points[i].Y);
        using var linePath = line.Detach();
        NeonStrokeHalo.StrokeWidth = 5f;
        NeonStrokeHalo.Color = TerrainColor.WithAlpha(0xA0);
        c.DrawPath(linePath, NeonStrokeHalo);
        NeonStrokeSharp.StrokeWidth = 1.6f;
        NeonStrokeSharp.Color = TerrainColor;
        c.DrawPath(linePath, NeonStrokeSharp);
        NeonStrokeHalo.StrokeWidth = 5.5f;
        NeonStrokeSharp.StrokeWidth = 2f;

        // Pads on top of terrain, with brighter color and multiplier labels.
        using var padFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 18);
        foreach (var pad in t.Pads)
        {
            var color = pad.Multiplier switch
            {
                5 => PadGoldHigh,
                3 => PadGoldMid,
                _ => PadGoldLow,
            };
            NeonLine(c, pad.X0, pad.Y, pad.X1, pad.Y, color, halo: 8f, sharp: 3.4f);
            // Multiplier label slightly above the pad.
            string label = $"x{pad.Multiplier}";
            float lx = (pad.X0 + pad.X1) * 0.5f;
            float ly = pad.Y - 10f;
            NeonFillHalo.Color = color.WithAlpha(0xC0);
            c.DrawText(label, lx, ly, SKTextAlign.Center, padFont, NeonFillHalo);
            NeonFillSharp.Color = color;
            c.DrawText(label, lx, ly, SKTextAlign.Center, padFont, NeonFillSharp);
        }
    }

    // Lunar Module silhouette: ascent stage (octagonal cabin with triangle window)
    // on top, descent stage (wide trapezoid) below, four splayed landing legs, and
    // an engine bell. Drawn at world coords, rotated to ship's angle.
    static void DrawLander(SKCanvas canvas, GameWorld world)
    {
        var l = world.Lander;
        canvas.Save();
        canvas.Translate(l.Position.X, l.Position.Y);
        canvas.RotateRadians(l.AngleRadians);

        // Thrust flame is drawn before the ship so it sits behind the engine bell.
        if (l.Thrusting && l.FuelKg > 0f)
        {
            float flicker = 0.9f + 0.2f * MathF.Sin((float)MarqueeClock.Elapsed.TotalSeconds * 60f);
            float baseY = 12f;
            float tipY  = 12f + 26f * flicker;
            using var flame = new SKPathBuilder();
            flame.MoveTo(-5f, baseY);
            flame.LineTo( 0f, tipY);
            flame.LineTo( 5f, baseY);
            flame.Close();
            using var flamePath = flame.Detach();
            NeonFillHalo.Color = new SKColor(0xFF, 0xCC, 0x33, 0xA0);
            canvas.DrawPath(flamePath, NeonFillHalo);
            NeonFillSharp.Color = new SKColor(0xFF, 0xEE, 0x88);
            canvas.DrawPath(flamePath, NeonFillSharp);
        }

        // Descent stage (wide trapezoid)
        using (var descent = new SKPathBuilder())
        {
            descent.AddPoly(stackalloc SKPoint[]
            {
                new(-10f, 0f),
                new( 10f, 0f),
                new(  8f, 10f),
                new( -8f, 10f),
            }, close: true);
            using var path = descent.Detach();
            NeonStroke(canvas, path, LanderBody);
        }

        // Ascent stage (octagonal cabin)
        using (var ascent = new SKPathBuilder())
        {
            ascent.AddPoly(stackalloc SKPoint[]
            {
                new(-6f, -10f),
                new( 6f, -10f),
                new( 9f,  -4f),
                new( 9f,   0f),
                new(-9f,   0f),
                new(-9f,  -4f),
            }, close: true);
            using var path = ascent.Detach();
            NeonStroke(canvas, path, LanderBody);
        }

        // Triangle viewport on the cabin face
        using (var win = new SKPathBuilder())
        {
            win.AddPoly(stackalloc SKPoint[]
            {
                new(-2f, -8f),
                new( 2f, -8f),
                new( 0f, -4f),
            }, close: true);
            using var path = win.Detach();
            NeonFillSharp.Color = LanderAccent;
            canvas.DrawPath(path, NeonFillSharp);
        }

        // Engine bell underneath
        using (var bell = new SKPathBuilder())
        {
            bell.AddPoly(stackalloc SKPoint[]
            {
                new(-3f, 10f),
                new( 3f, 10f),
                new( 4f, 14f),
                new(-4f, 14f),
            }, close: true);
            using var path = bell.Detach();
            NeonStroke(canvas, path, LanderAccent);
        }

        // Four landing legs splaying out from the descent stage corners.
        NeonLine(canvas, -8f, 10f, -14f, 18f, LanderAccent, halo: 4f, sharp: 1.4f);
        NeonLine(canvas,  8f, 10f,  14f, 18f, LanderAccent, halo: 4f, sharp: 1.4f);
        NeonLine(canvas, -3f, 10f,  -6f, 17f, LanderAccent, halo: 4f, sharp: 1.4f);
        NeonLine(canvas,  3f, 10f,   6f, 17f, LanderAccent, halo: 4f, sharp: 1.4f);
        // Foot pads at the ends of the outer legs.
        NeonCircleFill(canvas, -14f, 18f, 1.5f, LanderAccent);
        NeonCircleFill(canvas,  14f, 18f, 1.5f, LanderAccent);

        canvas.Restore();
    }

    // --- HUD -----------------------------------------------------------------

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        // Score / hi-score / level along the top.
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        using var miniFont  = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);

        DrawHudText(c, $"SCORE {w.Score:00000}", 24, 36, SKTextAlign.Left, font, HudColor);
        if (w.HighScore > 0)
            DrawHudText(c, $"HI {w.HighScore:00000}", cw / 2f, 30, SKTextAlign.Center, smallFont, HudColor);
        DrawHudText(c, $"LEVEL {w.Level}", cw - 24, 36, SKTextAlign.Right, font, HudColor);

        if (w.Mode is GameMode.Playing or GameMode.Landed or GameMode.Crashed or GameMode.Attract)
        {
            DrawLivesIndicator(c, w, 24, 60);
            DrawFlightReadouts(c, w, cw, ch);
            DrawFuelGauge(c, w, cw, ch);
        }

        if (w.PlacardTimer > 0 && !string.IsNullOrEmpty(w.PlacardText))
        {
            using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 44);
            SKColor color = w.PlacardText.Contains("CRASH") ? HudWarnColor : HudColor;
            DrawHudText(c, w.PlacardText, cw / 2f, ch * 0.50f, SKTextAlign.Center, placardFont, color);

            if (w.Mode == GameMode.Landed)
            {
                DrawHudText(c, $"+{50 * w.LastLandingMultiplier} pad   +{w.LastLandingFuelBonus:0} fuel   =  {w.LastLandingScore}",
                    cw / 2f, ch * 0.50f + 40f, SKTextAlign.Center, smallFont, HudColor);
            }
        }

        if (w.Mode == GameMode.Attract)
            DrawHudText(c, "ATTRACT  -  PRESS ANY KEY", cw / 2f, ch - 28f, SKTextAlign.Center, smallFont, HudColor);
        if (w.Mode == GameMode.Title)
        {
            DrawTitle(c, cw, ch);
            DrawMarquee(c, cw, ch);
        }
        else if (w.Mode == GameMode.GameOver)
        {
            using var bigFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
            DrawHudText(c, "GAME OVER",              cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   HudColor);
            DrawHudText(c, $"FINAL SCORE  {w.Score:00000}", cw / 2f, ch / 2f + 50f, SKTextAlign.Center, smallFont, HudColor);
            DrawHudText(c, $"YOU REACHED LEVEL {w.Level}", cw / 2f, ch / 2f + 80f, SKTextAlign.Center, smallFont, HudColor);
            DrawHudText(c, "PRESS SPACE TO PLAY AGAIN", cw / 2f, ch / 2f + 130f, SKTextAlign.Center, smallFont, HudColor);
        }
    }

    static void DrawLivesIndicator(SKCanvas c, GameWorld w, float x, float y)
    {
        for (int i = 0; i < Math.Min(w.LivesLeft, 6); i++)
        {
            c.Save();
            c.Translate(x + 14f + i * 24f, y + 8f);
            c.Scale(0.45f);
            DrawLanderSilhouetteSimple(c);
            c.Restore();
        }
    }

    static void DrawLanderSilhouetteSimple(SKCanvas c)
    {
        // Compact LM icon for lives indicator — just the silhouette outline.
        using var p = new SKPathBuilder();
        p.AddPoly(stackalloc SKPoint[]
        {
            new(-6f, -10f), new( 6f, -10f),
            new( 9f,  -4f), new( 9f,   0f),
            new(10f,  0f),  new( 8f, 10f),
            new(-8f, 10f),  new(-10f, 0f),
            new(-9f,  0f),  new(-9f, -4f),
        }, close: true);
        using var path = p.Detach();
        NeonStroke(c, path, LanderBody);
    }

    static void DrawFlightReadouts(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var f = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        float altitude = MathF.Max(0, TerrainBuilder.HeightAt(w.Terrain, w.Lander.Position.X) - w.Lander.Position.Y);
        float vx = w.Lander.Velocity.X;
        float vy = w.Lander.Velocity.Y;

        SKColor vyColor = MathF.Abs(vy) > GameWorld.MaxLandVerticalSpd ? HudWarnColor : HudColor;
        SKColor vxColor = MathF.Abs(vx) > GameWorld.MaxLandHorizontalSpd ? HudWarnColor : HudColor;

        // Right-side readouts.
        DrawHudText(c, $"ALT  {altitude,5:0}", cw - 24, 86,  SKTextAlign.Right, f, HudColor);
        DrawHudText(c, $"VY  {vy,5:+0;-0; 0}", cw - 24, 106, SKTextAlign.Right, f, vyColor);
        DrawHudText(c, $"VX  {vx,5:+0;-0; 0}", cw - 24, 126, SKTextAlign.Right, f, vxColor);
    }

    static void DrawFuelGauge(SKCanvas c, GameWorld w, float cw, float ch)
    {
        const float gaugeW = 220f;
        const float gaugeH = 14f;
        float x = 24f;
        float y = ch - 36f;
        float pct = MathF.Max(0f, MathF.Min(1f, w.Lander.FuelKg / GameWorld.StartingFuel));
        SKColor color = pct < 0.15f ? HudWarnColor : pct < 0.35f ? new SKColor(0xFF, 0xCC, 0x33) : HudColor;

        // Outline
        using var outlinePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f,
            IsAntialias = true,
            Color = HudColor.WithAlpha(0xB0),
        };
        c.DrawRect(x, y, gaugeW, gaugeH, outlinePaint);

        // Filled bar
        NeonFillHalo.Color = color.WithAlpha(0xB0);
        c.DrawRect(x, y, gaugeW * pct, gaugeH, NeonFillHalo);
        NeonFillSharp.Color = color;
        c.DrawRect(x, y, gaugeW * pct, gaugeH, NeonFillSharp);

        using var label = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 16);
        DrawHudText(c, $"FUEL  {w.Lander.FuelKg:0}", x, y - 6f, SKTextAlign.Left, label, color);
    }

    static void DrawHudText(SKCanvas c, string text, float x, float y, SKTextAlign align, SKFont font, SKColor color)
    {
        NeonFillHalo.Color = color.WithAlpha(0xC0);
        c.DrawText(text, x, y, align, font, NeonFillHalo);
        NeonFillSharp.Color = color;
        c.DrawText(text, x, y, align, font, NeonFillSharp);
    }

    static void DrawTitle(SKCanvas c, float cw, float ch)
    {
        const string title = "MAHINA";
        float advance = MarqueeCharWidth + MarqueeCharGap;
        float titleW = title.Length * advance - MarqueeCharGap;

        c.Save();
        c.Translate((cw - titleW) / 2f, ch * 0.20f);
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
        DrawHudText(c, "NEON LUNAR LANDER", cw / 2f, ch * 0.20f + MarqueeCharHeight + 28f, SKTextAlign.Center, smallFont, HudColor);
        DrawHudText(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.46f, SKTextAlign.Center, smallFont, HudColor);
        DrawHudText(c, "Left / Right or A / D  -  rotate",     cw / 2f, ch * 0.54f, SKTextAlign.Center, instrFont, HudColor);
        DrawHudText(c, "Up / W / Space  -  fire main thruster", cw / 2f, ch * 0.58f, SKTextAlign.Center, instrFont, HudColor);
        DrawHudText(c, "Touch down gently on a pad — wider pads are safer, narrow ones score more", cw / 2f, ch * 0.65f, SKTextAlign.Center, instrFont, HudColor);
    }

    static void DrawMarquee(SKCanvas c, float cw, float ch)
    {
        float advance = MarqueeCharWidth + MarqueeCharGap;
        float totalW  = MarqueeText.Length * advance;
        float loop    = totalW + cw;
        double time   = MarqueeClock.Elapsed.TotalSeconds;
        float pixelOffset = (float)((time * MarqueeSpeed) % loop);
        float startX    = cw - pixelOffset;
        float baselineY = ch * 0.93f;

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
