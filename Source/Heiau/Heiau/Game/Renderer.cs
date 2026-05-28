using System;
using SkiaSharp;

namespace Heiau.Game;

// Heiau renderer — Star-Castle-style ring shooter. All shared chassis pieces
// (neon paints, vector glyph font, marquee, gradients, playfield border, hud
// text helpers) come from `Arcade.Common.Chassis` via globally-imported usings.
// This file owns the game-specific draws: rings as arc segments, central
// turret, Asteroids ship, Sparx, and the HUD layout.
public static class Renderer
{
    // --- Game-specific palette ---
    static readonly SKColor ShipColor       = new(0x33, 0xF8, 0xFF);
    static readonly SKColor ShipAccent      = new(0xFF, 0xCC, 0x33);
    static readonly SKColor PlayerBullet    = new(0xFF, 0xEE, 0x33);
    static readonly SKColor TurretBullet    = new(0xFF, 0x44, 0x66);
    static readonly SKColor TurretCore      = new(0xFF, 0xAA, 0x33);
    static readonly SKColor TurretRimColor  = new(0xFF, 0xEE, 0xAA);
    static readonly SKColor HudColor        = new(0x33, 0xF8, 0xFF);
    static readonly SKColor HudWarnColor    = new(0xFF, 0x55, 0x66);

    const string MarqueeText = "HEIAU · UNO PLATFORM · SKIASHARP 4 · NEON STAR CASTLE";

    // Cached ring-segment SKPaint (kept separate from NeonPaints so the ring
    // stroke width can vary per-segment without disturbing other neon draws).
    static readonly SKPaint RingArcPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
    };

    // --- Render entry point ---

    public static void Render(SKCanvas canvas, GameWorld world, float canvasW, float canvasH)
    {
        NeonBackground.Draw(canvas, canvasW, canvasH);

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
        PlayfieldBorder.Draw(canvas, world.Width, world.Height, HudColor);
        DrawRings(canvas, world);
        if (world.Turret.Alive) DrawTurret(canvas, world);

        foreach (var p in world.Particles)
        {
            float lifeT = p.Life / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonDraw.CircleFill(canvas, p.Pos.X, p.Pos.Y, p.Size, color);
        }

        foreach (var b in world.Bullets)
        {
            var color = b.FromPlayer ? PlayerBullet : TurretBullet;
            float r = b.FromPlayer ? 3.4f : 3.0f;
            NeonDraw.CircleFill(canvas, b.Position.X, b.Position.Y, r, color);
        }

        foreach (var sp in world.Sparks) DrawSpark(canvas, sp);

        if (world.Ship.Alive && ShipVisible(world.Ship)) DrawShip(canvas, world.Ship);

        using var popupFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 18);
        foreach (var pop in world.Popups)
        {
            float lifeT = pop.Life / MathF.Max(0.001f, pop.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(pop.Color).WithAlpha(alpha);
            NeonPaints.FillSharp.Color = color;
            canvas.DrawText($"+{pop.Value}", pop.Pos.X, pop.Pos.Y, SKTextAlign.Center, popupFont, NeonPaints.FillSharp);
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

                float healthT = (float)ring.Health[s] / MathF.Max(1, ring.MaxHealth);
                float flash   = ring.HitFlash[s];
                float alphaMul = 0.45f + 0.55f * healthT + flash * 0.4f;
                float widthMul = 0.55f + 0.45f * healthT;

                float centerAng = ring.Rotation + segWidth * s;
                float startAngDeg = centerAng * 180f / MathF.PI - arcHalfDeg;
                float sweepDeg = arcHalfDeg * 2f;
                var bounds = new SKRect(
                    center.X - ring.Radius, center.Y - ring.Radius,
                    center.X + ring.Radius, center.Y + ring.Radius);

                float t = (float)Marquee.TimeSeconds;
                float hue = (ring.SegmentColorHue + t * 12f + s * 3f) % 360f;
                SKColor color = HsvColor.HsvToRgb(hue, 0.85f, 1f);

                using var arcBuilder = new SKPathBuilder();
                arcBuilder.AddArc(bounds, startAngDeg, sweepDeg);
                using var arcPath = arcBuilder.Detach();

                byte haloAlpha  = (byte)Math.Clamp(0xB0 * alphaMul, 0, 255);
                byte sharpAlpha = (byte)Math.Clamp(255 * MathF.Min(1f, alphaMul), 0, 255);

                RingArcPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f);
                RingArcPaint.StrokeWidth = 8f * widthMul;
                RingArcPaint.Color = color.WithAlpha(haloAlpha);
                c.DrawPath(arcPath, RingArcPaint);
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

        float pulse = 0.85f + 0.15f * MathF.Sin((float)Marquee.TimeSeconds * 4f);
        NeonDraw.CircleFill(c, 0, 0, 16f * pulse, TurretCore);

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
        NeonDraw.Stroke(c, hexPath, TurretRimColor);

        c.RotateRadians(t.BarrelAngle);
        NeonPaints.StrokeHalo.StrokeWidth = 7f;
        NeonPaints.StrokeHalo.Color = TurretCore.WithAlpha(0xC0);
        c.DrawLine(0, 0, 32f, 0, NeonPaints.StrokeHalo);
        NeonPaints.StrokeSharp.StrokeWidth = 2.5f;
        NeonPaints.StrokeSharp.Color = TurretCore;
        c.DrawLine(0, 0, 32f, 0, NeonPaints.StrokeSharp);
        NeonPaints.StrokeHalo.StrokeWidth  = NeonPaints.DefaultStrokeHaloWidth;
        NeonPaints.StrokeSharp.StrokeWidth = NeonPaints.DefaultStrokeSharpWidth;

        c.Restore();
    }

    static void DrawSpark(SKCanvas c, Spark sp)
    {
        SKColor col = HsvColor.HsvToRgb(sp.Hue, 0.85f, 1f);
        float pulse = 0.85f + 0.15f * MathF.Sin((float)Marquee.TimeSeconds * 10f + sp.Hue * 0.05f);
        float r = 7f * pulse;
        c.Save();
        c.Translate(sp.Position.X, sp.Position.Y);
        using var diamond = new SKPathBuilder();
        diamond.AddPoly(stackalloc SKPoint[]
        {
            new( 0f, -r),
            new( r,  0f),
            new( 0f,  r),
            new(-r,  0f),
        }, close: true);
        using var path = diamond.Detach();
        NeonDraw.Stroke(c, path, col);
        NeonDraw.CircleFill(c, 0f, 0f, 2.2f, col);
        c.Restore();
    }

    static void DrawShip(SKCanvas c, Ship s)
    {
        c.Save();
        c.Translate(s.Position.X, s.Position.Y);
        c.RotateRadians(s.AngleRadians);
        using var body = new SKPathBuilder();
        body.AddPoly(stackalloc SKPoint[]
        {
            new( 14f,  0f),
            new(-10f,  9f),
            new( -5f,  0f),
            new(-10f, -9f),
        }, close: true);
        using var bodyPath = body.Detach();
        NeonDraw.Stroke(c, bodyPath, ShipColor);
        NeonDraw.CircleFill(c, 1f, 0f, 2f, ShipAccent);
        if (s.Thrusting)
        {
            float flicker = 0.8f + 0.3f * MathF.Sin((float)Marquee.TimeSeconds * 50f);
            using var flame = new SKPathBuilder();
            flame.AddPoly(stackalloc SKPoint[]
            {
                new(-5f,  4f),
                new(-14f - 10f * flicker, 0f),
                new(-5f, -4f),
            }, close: true);
            using var flamePath = flame.Detach();
            NeonPaints.FillHalo.Color = new SKColor(0xFF, 0xCC, 0x33, 0xA0);
            c.DrawPath(flamePath, NeonPaints.FillHalo);
            NeonPaints.FillSharp.Color = new SKColor(0xFF, 0xEE, 0x88);
            c.DrawPath(flamePath, NeonPaints.FillSharp);
        }
        c.Restore();
    }

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);

        HudText.Draw(c, $"SCORE {w.Score:00000}", 24, 36, SKTextAlign.Left, font, HudColor);
        if (w.HighScore > 0)
            HudText.Draw(c, $"HI {w.HighScore:00000}", cw / 2f, 30, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, $"LEVEL {w.Level}", cw - 24, 36, SKTextAlign.Right, font, HudColor);

        if (w.Mode is GameMode.Playing or GameMode.Attract)
            DrawLivesIndicator(c, w, 24, 60);

        if (w.PlacardTimer > 0 && !string.IsNullOrEmpty(w.PlacardText))
        {
            using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 40);
            SKColor color = w.PlacardText.Contains("CRASH") || w.PlacardText.Contains("OVER") ? HudWarnColor : HudColor;
            HudText.Draw(c, w.PlacardText, cw / 2f, ch * 0.10f + 4f, SKTextAlign.Center, placardFont, color);
        }

        if (w.Mode == GameMode.Attract)
            HudText.Draw(c, "ATTRACT  -  PRESS ANY KEY", cw / 2f, ch - 28f, SKTextAlign.Center, smallFont, HudColor);

        if (w.Mode == GameMode.Title)
        {
            DrawTitle(c, cw, ch);
            Marquee.Draw(c, MarqueeText, cw, ch);
        }
        else if (w.Mode == GameMode.GameOver)
        {
            using var bigFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
            HudText.Draw(c, "GAME OVER",                       cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   HudColor);
            HudText.Draw(c, $"FINAL SCORE  {w.Score:00000}",   cw / 2f, ch / 2f + 50f,  SKTextAlign.Center, smallFont, HudColor);
            HudText.Draw(c, $"YOU REACHED LEVEL {w.Level}",    cw / 2f, ch / 2f + 80f,  SKTextAlign.Center, smallFont, HudColor);
            HudText.Draw(c, "PRESS SPACE TO PLAY AGAIN",       cw / 2f, ch / 2f + 130f, SKTextAlign.Center, smallFont, HudColor);
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
            NeonDraw.Stroke(c, bodyPath, ShipColor);
            c.Restore();
        }
    }

    static void DrawTitle(SKCanvas c, float cw, float ch)
    {
        Marquee.DrawRainbowTitle(c, "HEIAU", cw, ch * 0.18f);

        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
        using var instrFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        HudText.Draw(c, "NEON STAR CASTLE", cw / 2f, ch * 0.18f + GlyphFont.CharHeight + 28f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.48f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Left / Right or A / D  -  rotate",     cw / 2f, ch * 0.55f, SKTextAlign.Center, instrFont, HudColor);
        HudText.Draw(c, "Up / W  -  thrust    Space  -  fire",  cw / 2f, ch * 0.59f, SKTextAlign.Center, instrFont, HudColor);
        HudText.Draw(c, "Break through the three rotating rings to destroy the central pohaku", cw / 2f, ch * 0.66f, SKTextAlign.Center, instrFont, HudColor);
    }
}
