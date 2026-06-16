using System;
using SkiaSharp;

namespace Kanapi.Game;

/// <summary>
/// All of Kanapi's drawing. Shared chassis (neon paints, glyph font, marquee,
/// gradient backdrop, playfield border, HUD text) comes from
/// <c>Arcade.Common.Chassis</c>; this file owns the game-specific draws: the
/// HP-aware mushroom field, centipede chains (head + body + eyes), the animated
/// spider, the player blaster, bullets, and the HUD/title.
/// </summary>
public static class Renderer
{
    static readonly SKColor MushroomColor    = new(0x66, 0xFF, 0xAA);
    static readonly SKColor MushroomDamaged  = new(0xFF, 0xCC, 0x66);
    static readonly SKColor MushroomPoisoned = new(0xFF, 0x44, 0x88);
    static readonly SKColor CentipedeHead    = new(0xFF, 0xEE, 0x44);
    static readonly SKColor CentipedeBody    = new(0x99, 0xFF, 0x55);
    static readonly SKColor CentipedePoisoned = new(0xFF, 0x55, 0x88);
    static readonly SKColor SpiderColor      = new(0xFF, 0x66, 0xFF);
    static readonly SKColor PlayerColor      = new(0x33, 0xF8, 0xFF);
    static readonly SKColor PlayerAccent     = new(0xFF, 0xCC, 0x33);
    static readonly SKColor BulletColor      = new(0xFF, 0xEE, 0x33);
    static readonly SKColor HudColor         = new(0x33, 0xF8, 0xFF);
    static readonly SKColor PlayerZoneTint   = new(0x22, 0x33, 0x88, 0x40);

    const string MarqueeText = "KANAPI · UNO PLATFORM · SKIASHARP 4 · NEON CENTIPEDE";

    /// <summary>Renders one frame: background + player-zone tint, mushrooms, entities, then the HUD.</summary>
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

        // Player zone tint — subtle band so the "shooter zone" reads as distinct.
        using (var zonePaint = new SKPaint { Color = PlayerZoneTint, IsAntialias = false, Style = SKPaintStyle.Fill })
        {
            c.DrawRect(0, GameWorld.PlayerZoneTop, world.Width, world.Height - GameWorld.PlayerZoneTop, zonePaint);
        }

        DrawMushrooms(c, world);
        DrawCentipedes(c, world);
        DrawSpiders(c, world);
        DrawBullets(c, world);

        foreach (var p in world.Particles)
        {
            float lifeT = p.Life / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonDraw.CircleFill(c, p.Pos.X, p.Pos.Y, p.Size, color);
        }

        if (world.Player.Alive && PlayerVisible(world.Player)) DrawPlayer(c, world.Player);

        using var popupFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 16);
        foreach (var sp in world.Popups)
        {
            float lifeT = sp.Life / MathF.Max(0.001f, sp.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(sp.Color).WithAlpha(alpha);
            NeonPaints.FillSharp.Color = color;
            c.DrawText($"+{sp.Value}", sp.Pos.X, sp.Pos.Y, SKTextAlign.Center, popupFont, NeonPaints.FillSharp);
        }
    }

    // Mushrooms: 4 HP, shown as a small cross/cluster that loses petals per hit.
    static void DrawMushrooms(SKCanvas c, GameWorld world)
    {
        foreach (var m in world.Grid.AllMushrooms())
        {
            var p = MushroomGrid.CellCenter(m.Col, m.Row);
            SKColor col = m.Poisoned ? MushroomPoisoned
                        : m.Health <= 2 ? MushroomDamaged
                        : MushroomColor;
            float cap = 7f;
            float stem = 3.5f;
            // Cap (top arc) — larger when fully alive, smaller as it loses HP.
            float capR = cap * (0.5f + 0.125f * m.Health);
            NeonDraw.CircleFill(c, p.X, p.Y - 2f, capR, col);
            // Stem
            NeonDraw.CircleFill(c, p.X, p.Y + 2f, stem, col.WithAlpha(0xC0));
            // Dots on the cap (more dots = healthier)
            int dots = Math.Max(0, m.Health - 1);
            for (int d = 0; d < dots; d++)
            {
                float a = d * MathF.PI * 2 / 3f + (m.Col + m.Row) * 0.7f;
                NeonDraw.CircleFill(c, p.X + MathF.Cos(a) * capR * 0.55f, p.Y - 2f + MathF.Sin(a) * capR * 0.45f, 0.9f, col.WithAlpha(0xFF));
            }
        }
    }

    static void DrawCentipedes(SKCanvas c, GameWorld world)
    {
        foreach (var chain in world.Chains)
        {
            for (int i = chain.Segments.Count - 1; i >= 0; i--)
            {
                var s = chain.Segments[i];
                SKColor col = s.Poisoned ? CentipedePoisoned : (s.IsHead ? CentipedeHead : CentipedeBody);
                float r = s.IsHead ? 9f : 7.5f;
                NeonDraw.CircleFill(c, s.Position.X, s.Position.Y, r, col);
                // Center dot for "eyes" on head segments.
                if (s.IsHead)
                {
                    NeonDraw.CircleFill(c, s.Position.X - 2.5f, s.Position.Y - 1.5f, 1.4f, new SKColor(0x10, 0x10, 0x18));
                    NeonDraw.CircleFill(c, s.Position.X + 2.5f, s.Position.Y - 1.5f, 1.4f, new SKColor(0x10, 0x10, 0x18));
                }
            }
        }
    }

    static void DrawSpiders(SKCanvas c, GameWorld world)
    {
        foreach (var s in world.Spiders)
        {
            // Body
            NeonDraw.CircleFill(c, s.Position.X, s.Position.Y, 7f, SpiderColor);
            // 8 legs as short lines
            for (int i = 0; i < 8; i++)
            {
                float ang = (i / 8f) * MathF.PI * 2f + (float)Marquee.TimeSeconds * 6f;
                float lx = MathF.Cos(ang);
                float ly = MathF.Sin(ang);
                NeonDraw.Line(c,
                    s.Position.X + lx * 4f, s.Position.Y + ly * 4f,
                    s.Position.X + lx * 12f, s.Position.Y + ly * 12f,
                    SpiderColor, halo: 3f, sharp: 1.2f);
            }
            // Eyes
            NeonDraw.CircleFill(c, s.Position.X - 2f, s.Position.Y - 1.5f, 1.0f, new SKColor(0x20, 0x10, 0x20));
            NeonDraw.CircleFill(c, s.Position.X + 2f, s.Position.Y - 1.5f, 1.0f, new SKColor(0x20, 0x10, 0x20));
        }
    }

    static void DrawBullets(SKCanvas c, GameWorld world)
    {
        foreach (var b in world.Bullets)
            NeonDraw.CircleFill(c, b.Position.X, b.Position.Y, 3.2f, BulletColor);
    }

    static void DrawPlayer(SKCanvas c, Player p)
    {
        c.Save();
        c.Translate(p.Position.X, p.Position.Y);
        using var body = new SKPathBuilder();
        body.AddPoly(stackalloc SKPoint[]
        {
            new( 0f, -11f),
            new( 9f,   8f),
            new( 0f,   4f),
            new(-9f,   8f),
        }, close: true);
        using var path = body.Detach();
        NeonDraw.Stroke(c, path, PlayerColor);
        NeonDraw.CircleFill(c, 0f, -3f, 1.6f, PlayerAccent);
        c.Restore();
    }

    static readonly SKPoint[] LivesShipPoly =
    {
        new( 0f, -11f), new( 9f,  8f), new( 0f,  4f), new(-9f,  8f),
    };

    static bool PlayerVisible(Player p)
    {
        if (p.Invuln <= 0) return true;
        return ((int)(p.Invuln * 12f) & 1) == 0;
    }

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 24);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);

        HudText.Draw(c, $"SCORE {w.Score:00000}", 16, 28, SKTextAlign.Left, font, HudColor);
        if (w.HighScore > 0)
            HudText.Draw(c, $"HI {w.HighScore:00000}", cw / 2f, 24, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, $"LEVEL {w.Level}", cw - 16, 28, SKTextAlign.Right, font, HudColor);

        if (w.Mode is GameMode.Playing or GameMode.Attract)
        {
            // Lives indicator — small triangles bottom-left.
            using var lifeBody = new SKPathBuilder();
            lifeBody.AddPoly(LivesShipPoly, close: true);
            using var lifePath = lifeBody.Detach();
            for (int i = 0; i < Math.Min(w.LivesLeft, 6); i++)
            {
                c.Save();
                c.Translate(20f + i * 22f, ch - 16f);
                c.Scale(0.55f);
                NeonDraw.Stroke(c, lifePath, PlayerColor);
                c.Restore();
            }
        }

        if (w.PlacardTimer > 0 && !string.IsNullOrEmpty(w.PlacardText))
        {
            using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 36);
            HudText.Draw(c, w.PlacardText, cw / 2f, ch * 0.30f, SKTextAlign.Center, placardFont, HudColor);
        }

        if (w.Mode == GameMode.Attract)
            HudText.Draw(c, "ATTRACT  -  PRESS ANY KEY", cw / 2f, ch - 16f, SKTextAlign.Center, smallFont, HudColor);

        if (w.Mode == GameMode.Title)
        {
            DrawTitle(c, cw, ch);
            Marquee.Draw(c, MarqueeText, cw, ch);
        }
        else if (w.Mode == GameMode.GameOver)
        {
            using var bigFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 50);
            HudText.Draw(c, "GAME OVER",                    cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   HudColor);
            HudText.Draw(c, $"FINAL SCORE  {w.Score:00000}", cw / 2f, ch / 2f + 46f,  SKTextAlign.Center, smallFont, HudColor);
            HudText.Draw(c, $"YOU REACHED LEVEL {w.Level}",  cw / 2f, ch / 2f + 70f,  SKTextAlign.Center, smallFont, HudColor);
            HudText.Draw(c, "PRESS SPACE TO PLAY AGAIN",     cw / 2f, ch / 2f + 110f, SKTextAlign.Center, smallFont, HudColor);
        }
    }

    static void DrawTitle(SKCanvas c, float cw, float ch)
    {
        Marquee.DrawRainbowTitle(c, "KANAPI", cw, ch * 0.18f);

        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
        using var instrFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        HudText.Draw(c, "NEON CENTIPEDE", cw / 2f, ch * 0.18f + GlyphFont.CharHeight + 28f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.46f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Arrows or WASD  -  move",  cw / 2f, ch * 0.54f, SKTextAlign.Center, instrFont, HudColor);
        HudText.Draw(c, "Space  -  fire",           cw / 2f, ch * 0.58f, SKTextAlign.Center, instrFont, HudColor);
        HudText.Draw(c, "Chip through the mushrooms, snipe the centipede before it reaches you", cw / 2f, ch * 0.66f, SKTextAlign.Center, instrFont, HudColor);
    }
}
