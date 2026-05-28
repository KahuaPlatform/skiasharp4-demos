using System;
using SkiaSharp;

namespace Alaloa.Game;

// Alaloa renderer — Tron-Light-Cycles-style. Shared chassis comes from
// `Arcade.Common.Chassis` via global usings. This file owns the
// game-specific draws: arena grid backdrop, neon trails (polyline per cycle),
// cycle heads, HUD scoreboard.
public static class Renderer
{
    static readonly SKColor GridLineColor = new(0x22, 0x44, 0x66, 0x40);
    static readonly SKColor HudColor      = new(0x33, 0xF8, 0xFF);

    const string MarqueeText = "ALALOA · UNO PLATFORM · SKIASHARP 4 · NEON LIGHT CYCLES";

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

    static void DrawWorld(SKCanvas c, GameWorld world)
    {
        PlayfieldBorder.Draw(c, world.Width, world.Height, HudColor);
        DrawArenaGrid(c, world);
        DrawTrails(c, world);
        DrawCycles(c, world);
        DrawParticles(c, world);
    }

    // Subtle 10-cell-spaced grid lines so the player has a sense of scale.
    static void DrawArenaGrid(SKCanvas c, GameWorld world)
    {
        using var gridPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = GridLineColor,
            StrokeWidth = 1f,
            IsAntialias = false,
        };
        float step = Arena.CellSize * 10f;
        for (float x = step; x < world.Width; x += step)
            c.DrawLine(x, 0, x, world.Height, gridPaint);
        for (float y = step; y < world.Height; y += step)
            c.DrawLine(0, y, world.Width, y, gridPaint);
    }

    static void DrawTrails(SKCanvas c, GameWorld world)
    {
        foreach (var cy in world.Cycles)
        {
            if (cy.Trail.Count == 0) continue;
            var color = new SKColor(cy.Color);
            using var b = new SKPathBuilder();
            b.MoveTo(cy.Trail[0].X, cy.Trail[0].Y);
            for (int i = 1; i < cy.Trail.Count; i++)
                b.LineTo(cy.Trail[i].X, cy.Trail[i].Y);
            // Connect the last corner to the cycle's current position so the
            // live tail extends right under the head.
            if (cy.Alive) b.LineTo(cy.Position.X, cy.Position.Y);
            using var path = b.Detach();
            NeonPaints.StrokeHalo.StrokeWidth = 6f;
            NeonPaints.StrokeHalo.Color = color.WithAlpha(0xB0);
            c.DrawPath(path, NeonPaints.StrokeHalo);
            NeonPaints.StrokeSharp.StrokeWidth = 2.2f;
            NeonPaints.StrokeSharp.Color = color;
            c.DrawPath(path, NeonPaints.StrokeSharp);
            NeonPaints.StrokeHalo.StrokeWidth  = NeonPaints.DefaultStrokeHaloWidth;
            NeonPaints.StrokeSharp.StrokeWidth = NeonPaints.DefaultStrokeSharpWidth;
        }
    }

    static readonly SKPoint[] CycleBodyPoly =
    {
        new( 9f,  0f),
        new(-4f,  5f),
        new(-2f,  0f),
        new(-4f, -5f),
    };
    static void DrawCycles(SKCanvas c, GameWorld world)
    {
        using var bodyBuilder = new SKPathBuilder();
        bodyBuilder.AddPoly(CycleBodyPoly, close: true);
        using var bodyPath = bodyBuilder.Detach();

        foreach (var cy in world.Cycles)
        {
            if (!cy.Alive) continue;
            var color = new SKColor(cy.Color);
            c.Save();
            c.Translate(cy.Position.X, cy.Position.Y);
            float angle = cy.Dir switch
            {
                Direction.Up    => -MathF.PI / 2f,
                Direction.Right => 0,
                Direction.Down  =>  MathF.PI / 2f,
                Direction.Left  =>  MathF.PI,
                _               => 0,
            };
            c.RotateRadians(angle);
            NeonDraw.Stroke(c, bodyPath, color);
            // Bright nose dot
            NeonDraw.CircleFill(c, 7f, 0f, 1.6f, new SKColor(0xFF, 0xFF, 0xFF));
            c.Restore();
        }
    }

    static void DrawParticles(SKCanvas c, GameWorld world)
    {
        foreach (var p in world.Particles)
        {
            float lifeT = p.Life / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonDraw.CircleFill(c, p.Pos.X, p.Pos.Y, p.Size, color);
        }
    }

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 24);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);

        // Scoreboard: 4 chips across the top, one per cycle.
        for (int i = 0; i < 4; i++)
        {
            var col = new SKColor(GameWorld.CycleColors[i]);
            float x = 16f + i * 110f;
            HudText.Draw(c, $"P{i+1}  {w.MatchScores[i]}", x, 28f, SKTextAlign.Left, font, col);
        }

        HudText.Draw(c, $"ROUND {w.Round}", cw - 16f, 28f, SKTextAlign.Right, font, HudColor);
        if (w.HighScore > 0)
            HudText.Draw(c, $"BEST {w.HighScore}", cw / 2f, 24f, SKTextAlign.Center, smallFont, HudColor);

        if (w.PlacardTimer > 0 && !string.IsNullOrEmpty(w.PlacardText))
        {
            using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 44);
            SKColor color = w.PlacardText.Contains("WIN") || w.PlacardText.Contains("ROUND")
                ? HudColor
                : new SKColor(0xFF, 0x55, 0x66);
            HudText.Draw(c, w.PlacardText, cw / 2f, ch * 0.32f, SKTextAlign.Center, placardFont, color);
        }

        if (w.Mode == GameMode.Attract)
            HudText.Draw(c, "ATTRACT  -  PRESS ANY KEY", cw / 2f, ch - 18f, SKTextAlign.Center, smallFont, HudColor);

        if (w.Mode == GameMode.Title)
        {
            DrawTitle(c, cw, ch);
            Marquee.Draw(c, MarqueeText, cw, ch);
        }
        else if (w.Mode == GameMode.GameOver)
        {
            using var bigFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
            bool playerWon = w.MatchScores[0] >= GameWorld.StartingMatchScore;
            HudText.Draw(c, playerWon ? "YOU WIN" : "GAME OVER",
                cw / 2f, ch / 2f, SKTextAlign.Center, bigFont, HudColor);
            HudText.Draw(c, $"FINAL  P1 {w.MatchScores[0]}   P2 {w.MatchScores[1]}   P3 {w.MatchScores[2]}   P4 {w.MatchScores[3]}",
                cw / 2f, ch / 2f + 50f, SKTextAlign.Center, smallFont, HudColor);
            HudText.Draw(c, "PRESS SPACE TO PLAY AGAIN", cw / 2f, ch / 2f + 90f, SKTextAlign.Center, smallFont, HudColor);
        }
    }

    static void DrawTitle(SKCanvas c, float cw, float ch)
    {
        Marquee.DrawRainbowTitle(c, "ALALOA", cw, ch * 0.18f);

        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
        using var instrFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        HudText.Draw(c, "NEON LIGHT CYCLES", cw / 2f, ch * 0.18f + GlyphFont.CharHeight + 28f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.46f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Arrows or WASD  -  turn (90 degree)", cw / 2f, ch * 0.54f, SKTextAlign.Center, instrFont, HudColor);
        HudText.Draw(c, "First to 5 round wins takes the match", cw / 2f, ch * 0.58f, SKTextAlign.Center, instrFont, HudColor);
        HudText.Draw(c, "Touch any trail or the arena edge and your cycle dies", cw / 2f, ch * 0.65f, SKTextAlign.Center, instrFont, HudColor);
    }
}
