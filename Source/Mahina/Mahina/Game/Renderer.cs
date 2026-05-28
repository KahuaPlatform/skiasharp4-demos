using System;
using SkiaSharp;

namespace Mahina.Game;

// Mahina renderer — Lunar-Lander vector physics game. Shared chassis pieces
// (neon paints, vector glyph font, marquee, gradients, playfield border, HUD
// text helpers) come from `Arcade.Common.Chassis` via globally-imported usings.
// This file owns the game-specific draws: terrain polyline, landing pads, LM
// silhouette, HUD readouts.
public static class Renderer
{
    static readonly SKColor LanderBody     = new(0xCC, 0xEE, 0xFF);
    static readonly SKColor LanderAccent   = new(0xFF, 0xCC, 0x33);
    static readonly SKColor TerrainColor   = new(0x88, 0xCC, 0xFF);
    static readonly SKColor PadGoldLow     = new(0xFF, 0xCC, 0x33);
    static readonly SKColor PadGoldMid     = new(0xFF, 0xAA, 0x33);
    static readonly SKColor PadGoldHigh    = new(0xFF, 0x55, 0x55);
    static readonly SKColor HudColor       = new(0x33, 0xF8, 0xFF);
    static readonly SKColor HudWarnColor   = new(0xFF, 0x55, 0x66);

    const string MarqueeText = "MAHINA · UNO PLATFORM · SKIASHARP 4 · NEON LUNAR LANDER";

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
        DrawTerrain(canvas, world);

        foreach (var p in world.Particles)
        {
            float lifeT = p.Life / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonDraw.CircleFill(canvas, p.Pos.X, p.Pos.Y, p.Size, color);
        }

        if (world.Lander.Alive) DrawLander(canvas, world);

        using var popupFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 20);
        foreach (var sp in world.Popups)
        {
            float lifeT = sp.Life / MathF.Max(0.001f, sp.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(sp.Color).WithAlpha(alpha);
            NeonPaints.FillSharp.Color = color;
            canvas.DrawText($"+{sp.Value}", sp.Pos.X, sp.Pos.Y, SKTextAlign.Center, popupFont, NeonPaints.FillSharp);
        }
    }

    static void DrawTerrain(SKCanvas c, GameWorld world)
    {
        var t = world.Terrain;
        if (t.Points.Length < 2) return;

        // Filled subterrain — closes the polyline back along the bottom of the world.
        using var b = new SKPathBuilder();
        b.MoveTo(t.Points[0].X, t.Points[0].Y);
        for (int i = 1; i < t.Points.Length; i++) b.LineTo(t.Points[i].X, t.Points[i].Y);
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

        // Stroke the surface line only (not the closing edges).
        using var line = new SKPathBuilder();
        line.MoveTo(t.Points[0].X, t.Points[0].Y);
        for (int i = 1; i < t.Points.Length; i++) line.LineTo(t.Points[i].X, t.Points[i].Y);
        using var linePath = line.Detach();
        NeonPaints.StrokeHalo.StrokeWidth = 5f;
        NeonPaints.StrokeHalo.Color = TerrainColor.WithAlpha(0xA0);
        c.DrawPath(linePath, NeonPaints.StrokeHalo);
        NeonPaints.StrokeSharp.StrokeWidth = 1.6f;
        NeonPaints.StrokeSharp.Color = TerrainColor;
        c.DrawPath(linePath, NeonPaints.StrokeSharp);
        NeonPaints.StrokeHalo.StrokeWidth  = NeonPaints.DefaultStrokeHaloWidth;
        NeonPaints.StrokeSharp.StrokeWidth = NeonPaints.DefaultStrokeSharpWidth;

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
            NeonDraw.Line(c, pad.X0, pad.Y, pad.X1, pad.Y, color, halo: 8f, sharp: 3.4f);
            string label = $"x{pad.Multiplier}";
            float lx = (pad.X0 + pad.X1) * 0.5f;
            float ly = pad.Y - 10f;
            HudText.Draw(c, label, lx, ly, SKTextAlign.Center, padFont, color);
        }
    }

    static void DrawLander(SKCanvas canvas, GameWorld world)
    {
        var l = world.Lander;
        canvas.Save();
        canvas.Translate(l.Position.X, l.Position.Y);
        canvas.RotateRadians(l.AngleRadians);

        if (l.Thrusting && l.FuelKg > 0f)
        {
            float flicker = 0.9f + 0.2f * MathF.Sin((float)Marquee.TimeSeconds * 60f);
            float baseY = 12f;
            float tipY  = 12f + 26f * flicker;
            using var flame = new SKPathBuilder();
            flame.MoveTo(-5f, baseY);
            flame.LineTo( 0f, tipY);
            flame.LineTo( 5f, baseY);
            flame.Close();
            using var flamePath = flame.Detach();
            NeonPaints.FillHalo.Color = new SKColor(0xFF, 0xCC, 0x33, 0xA0);
            canvas.DrawPath(flamePath, NeonPaints.FillHalo);
            NeonPaints.FillSharp.Color = new SKColor(0xFF, 0xEE, 0x88);
            canvas.DrawPath(flamePath, NeonPaints.FillSharp);
        }

        // Descent stage
        using (var descent = new SKPathBuilder())
        {
            descent.AddPoly(stackalloc SKPoint[]
            {
                new(-10f, 0f), new( 10f, 0f),
                new(  8f, 10f), new( -8f, 10f),
            }, close: true);
            using var p = descent.Detach();
            NeonDraw.Stroke(canvas, p, LanderBody);
        }

        // Ascent stage
        using (var ascent = new SKPathBuilder())
        {
            ascent.AddPoly(stackalloc SKPoint[]
            {
                new(-6f, -10f), new( 6f, -10f),
                new( 9f,  -4f), new( 9f,   0f),
                new(-9f,   0f), new(-9f,  -4f),
            }, close: true);
            using var p = ascent.Detach();
            NeonDraw.Stroke(canvas, p, LanderBody);
        }

        // Viewport
        using (var win = new SKPathBuilder())
        {
            win.AddPoly(stackalloc SKPoint[]
            {
                new(-2f, -8f), new( 2f, -8f), new( 0f, -4f),
            }, close: true);
            using var p = win.Detach();
            NeonPaints.FillSharp.Color = LanderAccent;
            canvas.DrawPath(p, NeonPaints.FillSharp);
        }

        // Engine bell
        using (var bell = new SKPathBuilder())
        {
            bell.AddPoly(stackalloc SKPoint[]
            {
                new(-3f, 10f), new( 3f, 10f),
                new( 4f, 14f), new(-4f, 14f),
            }, close: true);
            using var p = bell.Detach();
            NeonDraw.Stroke(canvas, p, LanderAccent);
        }

        // Landing legs + foot pads
        NeonDraw.Line(canvas, -8f, 10f, -14f, 18f, LanderAccent, halo: 4f, sharp: 1.4f);
        NeonDraw.Line(canvas,  8f, 10f,  14f, 18f, LanderAccent, halo: 4f, sharp: 1.4f);
        NeonDraw.Line(canvas, -3f, 10f,  -6f, 17f, LanderAccent, halo: 4f, sharp: 1.4f);
        NeonDraw.Line(canvas,  3f, 10f,   6f, 17f, LanderAccent, halo: 4f, sharp: 1.4f);
        NeonDraw.CircleFill(canvas, -14f, 18f, 1.5f, LanderAccent);
        NeonDraw.CircleFill(canvas,  14f, 18f, 1.5f, LanderAccent);

        canvas.Restore();
    }

    // --- HUD ---

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);

        HudText.Draw(c, $"SCORE {w.Score:00000}", 24, 36, SKTextAlign.Left, font, HudColor);
        if (w.HighScore > 0)
            HudText.Draw(c, $"HI {w.HighScore:00000}", cw / 2f, 30, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, $"LEVEL {w.Level}", cw - 24, 36, SKTextAlign.Right, font, HudColor);

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
            HudText.Draw(c, w.PlacardText, cw / 2f, ch * 0.50f, SKTextAlign.Center, placardFont, color);

            if (w.Mode == GameMode.Landed)
            {
                HudText.Draw(c, $"+{50 * w.LastLandingMultiplier} pad   +{w.LastLandingFuelBonus:0} fuel   =  {w.LastLandingScore}",
                    cw / 2f, ch * 0.50f + 40f, SKTextAlign.Center, smallFont, HudColor);
            }
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
        NeonDraw.Stroke(c, path, LanderBody);
    }

    static void DrawFlightReadouts(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var f = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        float altitude = MathF.Max(0, TerrainBuilder.HeightAt(w.Terrain, w.Lander.Position.X) - w.Lander.Position.Y);
        float vx = w.Lander.Velocity.X;
        float vy = w.Lander.Velocity.Y;
        SKColor vyColor = MathF.Abs(vy) > GameWorld.MaxLandVerticalSpd   ? HudWarnColor : HudColor;
        SKColor vxColor = MathF.Abs(vx) > GameWorld.MaxLandHorizontalSpd ? HudWarnColor : HudColor;
        HudText.Draw(c, $"ALT  {altitude,5:0}", cw - 24, 86,  SKTextAlign.Right, f, HudColor);
        HudText.Draw(c, $"VY  {vy,5:+0;-0; 0}", cw - 24, 106, SKTextAlign.Right, f, vyColor);
        HudText.Draw(c, $"VX  {vx,5:+0;-0; 0}", cw - 24, 126, SKTextAlign.Right, f, vxColor);
    }

    static void DrawFuelGauge(SKCanvas c, GameWorld w, float cw, float ch)
    {
        const float gaugeW = 220f;
        const float gaugeH = 14f;
        float x = 24f;
        float y = ch - 36f;
        float pct = MathF.Max(0f, MathF.Min(1f, w.Lander.FuelKg / GameWorld.StartingFuel));
        SKColor color = pct < 0.15f ? HudWarnColor : pct < 0.35f ? new SKColor(0xFF, 0xCC, 0x33) : HudColor;

        using var outlinePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f,
            IsAntialias = true,
            Color = HudColor.WithAlpha(0xB0),
        };
        c.DrawRect(x, y, gaugeW, gaugeH, outlinePaint);

        NeonPaints.FillHalo.Color = color.WithAlpha(0xB0);
        c.DrawRect(x, y, gaugeW * pct, gaugeH, NeonPaints.FillHalo);
        NeonPaints.FillSharp.Color = color;
        c.DrawRect(x, y, gaugeW * pct, gaugeH, NeonPaints.FillSharp);

        using var label = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 16);
        HudText.Draw(c, $"FUEL  {w.Lander.FuelKg:0}", x, y - 6f, SKTextAlign.Left, label, color);
    }

    static void DrawTitle(SKCanvas c, float cw, float ch)
    {
        Marquee.DrawRainbowTitle(c, "MAHINA", cw, ch * 0.20f);

        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
        using var instrFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        HudText.Draw(c, "NEON LUNAR LANDER", cw / 2f, ch * 0.20f + GlyphFont.CharHeight + 28f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.46f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Left / Right or A / D  -  rotate",     cw / 2f, ch * 0.54f, SKTextAlign.Center, instrFont, HudColor);
        HudText.Draw(c, "Up / W / Space  -  fire main thruster", cw / 2f, ch * 0.58f, SKTextAlign.Center, instrFont, HudColor);
        HudText.Draw(c, "Touch down gently on a pad — wider pads are safer, narrow ones score more", cw / 2f, ch * 0.65f, SKTextAlign.Center, instrFont, HudColor);
    }
}
