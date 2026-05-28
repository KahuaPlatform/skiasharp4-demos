using System;
using System.Collections.Generic;
using System.Diagnostics;
using SkiaSharp;

namespace Heiau.Game;

// Star-Castle-style vector renderer. Same neon-glow chassis as Lua / HokuLele /
// Mahina: background gradient (BackgroundSurface handles full window), marquee,
// vector glyph font. Game-specific draws: 3 concentric segmented rings, central
// turret with a tracking barrel, Asteroids-style player ship, bullets, HUD.
public static class Renderer
{
    static readonly SKColor ShipColor       = new(0x33, 0xF8, 0xFF);
    static readonly SKColor ShipAccent      = new(0xFF, 0xCC, 0x33);
    static readonly SKColor PlayerBullet    = new(0xFF, 0xEE, 0x33);
    static readonly SKColor TurretBullet    = new(0xFF, 0x44, 0x66);
    static readonly SKColor TurretCore      = new(0xFF, 0xAA, 0x33);
    static readonly SKColor TurretRimColor  = new(0xFF, 0xEE, 0xAA);
    static readonly SKColor HudColor        = new(0x33, 0xF8, 0xFF);
    static readonly SKColor HudWarnColor    = new(0xFF, 0x55, 0x66);
    static readonly SKColor BgTop           = new(0x05, 0x00, 0x14);
    static readonly SKColor BgBottom        = new(0x18, 0x02, 0x36);

    const string MarqueeText = "HEIAU · UNO PLATFORM · SKIASHARP 4 · NEON STAR CASTLE";
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
    static readonly SKPaint RingArcPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
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
        DrawRings(canvas, world);
        if (world.Turret.Alive) DrawTurret(canvas, world);

        // Particles
        foreach (var p in world.Particles)
        {
            float lifeT = p.Life / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonCircleFill(canvas, p.Pos.X, p.Pos.Y, p.Size, color);
        }

        // Bullets
        foreach (var b in world.Bullets)
        {
            var color = b.FromPlayer ? PlayerBullet : TurretBullet;
            float r = b.FromPlayer ? 3.4f : 3.0f;
            NeonCircleFill(canvas, b.Position.X, b.Position.Y, r, color);
        }

        // Sparx — pulsing diamond/cross with hue-cycling glow.
        foreach (var sp in world.Sparks)
        {
            DrawSpark(canvas, sp);
        }

        if (world.Ship.Alive && ShipVisible(world.Ship)) DrawShip(canvas, world.Ship);

        // Popups
        using var popupFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 18);
        foreach (var sp in world.Popups)
        {
            float lifeT = sp.Life / MathF.Max(0.001f, sp.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(sp.Color).WithAlpha(alpha);
            NeonFillSharp.Color = color;
            canvas.DrawText($"+{sp.Value}", sp.Pos.X, sp.Pos.Y, SKTextAlign.Center, popupFont, NeonFillSharp);
        }
    }

    static bool ShipVisible(Ship s) =>
        s.Invuln <= 0 || (((int)(s.Invuln * 12f)) & 1) == 0;

    static void DrawRings(SKCanvas c, GameWorld world)
    {
        var center = world.Center;
        foreach (var ring in world.Rings)
        {
            int segs = ring.Segments;
            float segWidth = MathF.Tau / segs;
            float arcHalfDeg = (RingGeometry.SegmentHalfArc * 0.85f) * 180f / MathF.PI;
            for (int s = 0; s < segs; s++)
            {
                if (!ring.IsAlive(s)) continue;

                // Damage fade — segments at lower HP draw dimmer + thinner so the
                // player can see what they've weakened. healthT = 1 at full HP,
                // 1/MaxHealth at 1 HP.
                float healthT = (float)ring.Health[s] / MathF.Max(1, ring.MaxHealth);
                float flash   = ring.HitFlash[s];
                float alphaMul   = 0.45f + 0.55f * healthT + flash * 0.4f;
                float widthMul   = 0.55f + 0.45f * healthT;

                float centerAng = ring.Rotation + segWidth * s;
                float startAngDeg = centerAng * 180f / MathF.PI - arcHalfDeg;
                float sweepDeg = arcHalfDeg * 2f;
                var bounds = new SKRect(
                    center.X - ring.Radius, center.Y - ring.Radius,
                    center.X + ring.Radius, center.Y + ring.Radius);

                float t = (float)MarqueeClock.Elapsed.TotalSeconds;
                float hue = (ring.SegmentColorHue + t * 12f + s * 3f) % 360f;
                SKColor color = HsvToRgb(hue, 0.85f, 1f);

                using var arcBuilder = new SKPathBuilder();
                arcBuilder.AddArc(bounds, startAngDeg, sweepDeg);
                using var arcPath = arcBuilder.Detach();

                byte haloAlpha  = (byte)Math.Clamp(0xB0 * alphaMul, 0, 255);
                byte sharpAlpha = (byte)Math.Clamp(255 * MathF.Min(1f, alphaMul), 0, 255);

                // Halo arc
                RingArcPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f);
                RingArcPaint.StrokeWidth = 8f * widthMul;
                RingArcPaint.Color = color.WithAlpha(haloAlpha);
                c.DrawPath(arcPath, RingArcPaint);
                // Sharp arc
                RingArcPaint.MaskFilter = null;
                RingArcPaint.StrokeWidth = 2.6f * widthMul;
                RingArcPaint.Color = color.WithAlpha(sharpAlpha);
                c.DrawPath(arcPath, RingArcPaint);
            }
        }
    }

    static void DrawTurret(SKCanvas c, GameWorld world)
    {
        var t = world.Turret;
        c.Save();
        c.Translate(t.Position.X, t.Position.Y);

        // Pulsing core
        float pulse = 0.85f + 0.15f * MathF.Sin((float)MarqueeClock.Elapsed.TotalSeconds * 4f);
        NeonCircleFill(c, 0, 0, 16f * pulse, TurretCore);

        // Outer pohaku (stone) ring — a small hexagonal silhouette
        using var hex = new SKPathBuilder();
        for (int i = 0; i < 6; i++)
        {
            float ang = MathF.Tau * i / 6f + t.Spin;
            float x = MathF.Cos(ang) * GameWorld.TurretRadius;
            float y = MathF.Sin(ang) * GameWorld.TurretRadius;
            if (i == 0) hex.MoveTo(x, y);
            else        hex.LineTo(x, y);
        }
        hex.Close();
        using var hexPath = hex.Detach();
        NeonStroke(c, hexPath, TurretRimColor);

        // Barrel
        c.RotateRadians(t.BarrelAngle);
        NeonStrokeHalo.StrokeWidth = 7f;
        NeonStrokeHalo.Color = TurretCore.WithAlpha(0xC0);
        c.DrawLine(0, 0, 32f, 0, NeonStrokeHalo);
        NeonStrokeSharp.StrokeWidth = 2.5f;
        NeonStrokeSharp.Color = TurretCore;
        c.DrawLine(0, 0, 32f, 0, NeonStrokeSharp);
        NeonStrokeHalo.StrokeWidth = 5.5f;
        NeonStrokeSharp.StrokeWidth = 2f;

        c.Restore();
    }

    static void DrawSpark(SKCanvas c, Spark sp)
    {
        SKColor col = HsvToRgb(sp.Hue, 0.85f, 1f);
        float pulse = 0.85f + 0.15f * MathF.Sin((float)MarqueeClock.Elapsed.TotalSeconds * 10f + sp.Hue * 0.05f);
        float r = 7f * pulse;
        c.Save();
        c.Translate(sp.Position.X, sp.Position.Y);
        // Diamond outline
        using var diamond = new SKPathBuilder();
        diamond.AddPoly(stackalloc SKPoint[]
        {
            new( 0f, -r),
            new( r,  0f),
            new( 0f,  r),
            new(-r,  0f),
        }, close: true);
        using var path = diamond.Detach();
        NeonStroke(c, path, col);
        // Bright core
        NeonCircleFill(c, 0f, 0f, 2.2f, col);
        c.Restore();
    }

    static void DrawShip(SKCanvas c, Ship s)
    {
        c.Save();
        c.Translate(s.Position.X, s.Position.Y);
        c.RotateRadians(s.AngleRadians);
        // Vector ship body — a triangular wedge pointing along +X (rotated by Angle).
        using var body = new SKPathBuilder();
        body.AddPoly(stackalloc SKPoint[]
        {
            new( 14f,  0f),
            new(-10f,  9f),
            new( -5f,  0f),
            new(-10f, -9f),
        }, close: true);
        using var bodyPath = body.Detach();
        NeonStroke(c, bodyPath, ShipColor);
        // Cockpit dot
        NeonCircleFill(c, 1f, 0f, 2f, ShipAccent);
        // Thrust flame (drawn behind ship via x axis offset)
        if (s.Thrusting)
        {
            float flicker = 0.8f + 0.3f * MathF.Sin((float)MarqueeClock.Elapsed.TotalSeconds * 50f);
            using var flame = new SKPathBuilder();
            flame.AddPoly(stackalloc SKPoint[]
            {
                new(-5f,  4f),
                new(-14f - 10f * flicker, 0f),
                new(-5f, -4f),
            }, close: true);
            using var flamePath = flame.Detach();
            NeonFillHalo.Color = new SKColor(0xFF, 0xCC, 0x33, 0xA0);
            c.DrawPath(flamePath, NeonFillHalo);
            NeonFillSharp.Color = new SKColor(0xFF, 0xEE, 0x88);
            c.DrawPath(flamePath, NeonFillSharp);
        }
        c.Restore();
    }

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);

        DrawHudText(c, $"SCORE {w.Score:00000}", 24, 36, SKTextAlign.Left, font, HudColor);
        if (w.HighScore > 0)
            DrawHudText(c, $"HI {w.HighScore:00000}", cw / 2f, 30, SKTextAlign.Center, smallFont, HudColor);
        DrawHudText(c, $"LEVEL {w.Level}", cw - 24, 36, SKTextAlign.Right, font, HudColor);

        if (w.Mode is GameMode.Playing or GameMode.Attract)
            DrawLivesIndicator(c, w, 24, 60);

        if (w.PlacardTimer > 0 && !string.IsNullOrEmpty(w.PlacardText))
        {
            using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 40);
            SKColor color = w.PlacardText.Contains("CRASH") || w.PlacardText.Contains("OVER") ? HudWarnColor : HudColor;
            DrawHudText(c, w.PlacardText, cw / 2f, ch * 0.10f + 4f, SKTextAlign.Center, placardFont, color);
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
            DrawHudText(c, "GAME OVER",                       cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   HudColor);
            DrawHudText(c, $"FINAL SCORE  {w.Score:00000}",   cw / 2f, ch / 2f + 50f,  SKTextAlign.Center, smallFont, HudColor);
            DrawHudText(c, $"YOU REACHED LEVEL {w.Level}",    cw / 2f, ch / 2f + 80f,  SKTextAlign.Center, smallFont, HudColor);
            DrawHudText(c, "PRESS SPACE TO PLAY AGAIN",       cw / 2f, ch / 2f + 130f, SKTextAlign.Center, smallFont, HudColor);
        }
    }

    static readonly SKPoint[] LivesShipPoly =
    {
        new( 14f,  0f),
        new(-10f,  9f),
        new( -5f,  0f),
        new(-10f, -9f),
    };
    static void DrawLivesIndicator(SKCanvas c, GameWorld w, float x, float y)
    {
        using var body = new SKPathBuilder();
        body.AddPoly(LivesShipPoly, close: true);
        using var bodyPath = body.Detach();
        for (int i = 0; i < Math.Min(w.LivesLeft, 6); i++)
        {
            c.Save();
            c.Translate(x + 14f + i * 26f, y + 12f);
            c.RotateRadians(-MathF.PI / 2f);
            c.Scale(0.55f);
            NeonStroke(c, bodyPath, ShipColor);
            c.Restore();
        }
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
        const string title = "HEIAU";
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
        DrawHudText(c, "NEON STAR CASTLE", cw / 2f, ch * 0.18f + MarqueeCharHeight + 28f, SKTextAlign.Center, smallFont, HudColor);
        DrawHudText(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.48f, SKTextAlign.Center, smallFont, HudColor);
        DrawHudText(c, "Left / Right or A / D  -  rotate",     cw / 2f, ch * 0.55f, SKTextAlign.Center, instrFont, HudColor);
        DrawHudText(c, "Up / W  -  thrust    Space  -  fire",  cw / 2f, ch * 0.59f, SKTextAlign.Center, instrFont, HudColor);
        DrawHudText(c, "Break through the three rotating rings to destroy the central pohaku", cw / 2f, ch * 0.66f, SKTextAlign.Center, instrFont, HudColor);
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
