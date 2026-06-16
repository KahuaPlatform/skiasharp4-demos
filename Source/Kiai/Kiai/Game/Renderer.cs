using System;
using SkiaSharp;
using Arcade.Common;
using Arcade.Common.Chassis;

namespace Kiai.Game;

// Draws Kia'i. Render order (per DESIGN):
//   1. NeonBackground gradient (over the ambient starfield the BackgroundSurface
//      already painted behind us).
//   2. The radar/scanner strip across the top (ship-centred, wrapping).
//   3. The scrolling, wrapping world — terrain + entities. Because the world is
//      a torus, entities are NOT drawn under a single camera transform (an affine
//      Translate can't replicate a sprite across the seam). Instead each sprite is
//      drawn once per on-screen replica yielded by Camera2D.ForEachVisibleX, with
//      screen Y from Camera2D.ToScreenY. Terrain uses SeamlessTerrain's screen-X
//      strip walk, so its silhouette is continuous across the seam for free.
//   4. HUD (score, hi-score, lives, smart bombs, wave, humanoid count).
//   5. Title / Attract overlay — scrolling Marquee + rainbow vector title.
//
// Neon-only look: every shape goes through the shared NeonDraw / VectorShapes /
// HudText / Marquee chassis helpers. No retro/vibrant toggle.
public static class Renderer
{
    // Palette (the catalog accent is guardian sky-cyan).
    static readonly SKColor ShipColor     = new(0x44, 0xCC, 0xFF);
    static readonly SKColor FlameColor    = new(0xFF, 0x88, 0x33);
    static readonly SKColor BulletColor   = new(0xFF, 0xEE, 0x55);
    static readonly SKColor EnemyBullet   = new(0xFF, 0x55, 0x55);
    static readonly SKColor HumanoidColor = new(0xEE, 0xEE, 0xFF);
    static readonly SKColor LanderColor   = new(0x55, 0xFF, 0x66);
    static readonly SKColor MutantColor   = new(0xFF, 0x44, 0xCC);
    static readonly SKColor BaiterColor   = new(0xFF, 0xEE, 0x33);
    static readonly SKColor BomberColor   = new(0xFF, 0x88, 0x22);
    static readonly SKColor PodColor      = new(0x88, 0xCC, 0xFF);
    static readonly SKColor SwarmerColor  = new(0xCC, 0x66, 0xFF);
    static readonly SKColor TerrainColor  = new(0x33, 0x99, 0xDD);
    static readonly SKColor HudColor      = new(0x44, 0xCC, 0xFF);
    static readonly SKColor RadarFrame    = new(0x33, 0x77, 0xAA);

    static readonly SKColor BgTop    = new(0x03, 0x05, 0x18);
    static readonly SKColor BgBottom = new(0x0A, 0x02, 0x2A);

    // Cached origin-centred vector silhouettes (built once, drawn many).
    static readonly SKPath ShipBody = VectorShapes.Poly(stackalloc SKPoint[]
    {
        new(14, 0), new(-10, -8), new(-5, 0), new(-10, 8),
    }, close: true);
    static readonly SKPath ShipFlame = VectorShapes.Poly(stackalloc SKPoint[]
    {
        new(-6, -4), new(-15, 0), new(-6, 4),
    }, close: false);
    static readonly SKPath HumanoidBody = VectorShapes.Poly(stackalloc SKPoint[]
    {
        new(0, -7), new(0, 2), new(-4, 7), new(0, 2), new(4, 7), new(0, 2), new(-4, -3), new(4, -3),
    }, close: false);
    static readonly SKPath LanderBody = VectorShapes.Poly(stackalloc SKPoint[]
    {
        new(-12, 0), new(-6, -8), new(6, -8), new(12, 0), new(6, 6), new(-6, 6),
    }, close: true);
    static readonly SKPath LanderLegs = VectorShapes.Poly(stackalloc SKPoint[]
    {
        new(-6, 6), new(-9, 12), new(6, 6), new(9, 12),
    }, close: false);

    static readonly SKFont HudFont      = new(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 26);
    static readonly SKFont HudSmallFont = new(SKTypeface.FromFamilyName("Consolas"), 18);

    public static void Render(SKCanvas canvas, GameWorld world, float canvasW, float canvasH)
    {
        // 1) Background gradient.
        NeonBackground.Draw(canvas, canvasW, canvasH, BgTop, BgBottom);

        var cam = world.Camera;

        // 2) Radar strip (drawn in canvas space, before the world; entities plot
        //    over it via blips below the frame).
        DrawRadar(canvas, world);

        // 3) The scrolling, wrapping world.
        DrawTerrain(canvas, world);
        DrawEntities(canvas, world);

        // Screen flash (smart bomb / planet loss).
        if (world.ScreenFlash > 0f)
        {
            byte a = (byte)Math.Clamp(world.ScreenFlash * 255f, 0, 180);
            using var flash = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, a) };
            canvas.DrawRect(0, 0, canvasW, canvasH, flash);
        }

        // 4) HUD.
        DrawHud(canvas, world, canvasW, canvasH);

        // 5) Title / Attract overlay.
        if (world.Mode == GameMode.Title || world.Mode == GameMode.Attract)
            DrawTitle(canvas, world, canvasW, canvasH);
        else if (world.Mode == GameMode.GameOver)
            HudText.Draw(canvas, "GAME OVER", canvasW / 2f, canvasH / 2f, SKTextAlign.Center,
                         new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56), HudColor);
    }

    // --- Terrain --------------------------------------------------------------

    static void DrawTerrain(SKCanvas c, GameWorld world)
    {
        // SeamlessTerrain walks screen X and lets HeightAt wrap, so the strip is
        // continuous across the seam with zero special-casing. Stroke the surface
        // silhouette in neon.
        using var strip = world.Terrain.Field.BuildVisibleStrip(world.Camera, world.ViewW, stepPx: 6f);
        NeonDraw.Stroke(c, strip, TerrainColor);
    }

    // --- Entities (seam-replicated) -------------------------------------------

    static void DrawEntities(SKCanvas c, GameWorld world)
    {
        var cam = world.Camera;

        // Particles (thrust trail, explosions, mines).
        foreach (var p in world.Particles)
        {
            float life = p.MaxLife > 0 ? p.Lifetime / p.MaxLife : 0f;
            byte alpha = (byte)Math.Clamp(life * 255f, 0, 255);
            SKColor col = p.Color != 0u ? new SKColor(p.Color) : new SKColor(0xFF, 0xCC, 0x66);
            col = col.WithAlpha(p.IsMine ? (byte)0xFF : alpha);
            float r = p.IsMine ? 4f : 1.6f;
            float sy = cam.ToScreenY(p.Position.Y);
            cam.ForEachVisibleX(p.Position.X, 8f, sx => NeonDraw.CircleFill(c, sx, sy, r, col));
        }

        // Humanoids.
        foreach (var h in world.Humanoids)
        {
            if (!h.Alive) continue;
            float sy = cam.ToScreenY(h.Position.Y);
            cam.ForEachVisibleX(h.Position.X, 16f, sx =>
                VectorShapes.DrawAt(c, HumanoidBody, sx, sy, 0f, 1f, HumanoidColor));
        }

        // Bullets.
        foreach (var b in world.Bullets)
        {
            if (!b.Alive) continue;
            SKColor col = b.FromShip ? BulletColor : EnemyBullet;
            float sy = cam.ToScreenY(b.Position.Y);
            cam.ForEachVisibleX(b.Position.X, 8f, sx => NeonDraw.CircleFill(c, sx, sy, 2.6f, col));
        }

        // Landers (+ legs).
        foreach (var l in world.Landers)
        {
            if (!l.Alive) continue;
            float sy = cam.ToScreenY(l.Position.Y);
            cam.ForEachVisibleX(l.Position.X, 20f, sx =>
            {
                VectorShapes.DrawAt(c, LanderBody, sx, sy, 0f, 1f, LanderColor);
                VectorShapes.DrawAt(c, LanderLegs, sx, sy, 0f, 1f, LanderColor);
            });
        }

        DrawBlobEntities(c, cam, world.Mutants,  MutantColor,  12f, 7);
        DrawBlobEntities(c, cam, world.Baiters,  BaiterColor,  12f, 6);
        DrawBlobEntities(c, cam, world.Bombers,  BomberColor,  14f, 8);
        DrawBlobEntities(c, cam, world.Pods,     PodColor,     15f, 9);
        DrawBlobEntities(c, cam, world.Swarmers, SwarmerColor, 8f,  5);

        // Ship (skip flicker frames while invincible).
        if (ShipVisible(world))
        {
            var ship = world.Ship;
            float sy = cam.ToScreenY(ship.Position.Y);
            float rot = ship.FacingSign < 0 ? 180f : 0f;   // flip the nose with facing
            cam.ForEachVisibleX(ship.Position.X, 24f, sx =>
            {
                VectorShapes.DrawAt(c, ShipBody, sx, sy, rot, 1f, ShipColor);
                if (ship.ThrustingAny)
                    VectorShapes.DrawAt(c, ShipFlame, sx, sy, rot, 1f, FlameColor);
            });
        }
    }

    // Generic neon-circle draw for the "blob" enemies (mutant/baiter/bomber/pod/
    // swarmer) — a glowing ring sized to the entity radius, seam-replicated.
    static void DrawBlobEntities<T>(SKCanvas c, Camera2D cam, System.Collections.Generic.List<T> list,
                                    SKColor color, float radius, int _) where T : Entity
    {
        foreach (var e in list)
        {
            if (!e.Alive) continue;
            float sy = cam.ToScreenY(e.Position.Y);
            cam.ForEachVisibleX(e.Position.X, radius + 6f, sx => NeonDraw.CircleFill(c, sx, sy, radius * 0.5f, color));
        }
    }

    static bool ShipVisible(GameWorld world)
    {
        var s = world.Ship;
        if (!s.Alive) return false;
        if (s.InvincibleTime <= 0f || world.Mode != GameMode.Playing) return true;
        return ((int)(s.InvincibleTime * 10) % 2) == 0;
    }

    // --- Radar ----------------------------------------------------------------

    static void DrawRadar(SKCanvas c, GameWorld world)
    {
        var radar = world.Radar;
        // Faint terrain silhouette, then blips, then frame + ship caret.
        radar.DrawTerrain(c, world.Terrain.HeightAt, RadarFrame, samples: 110);

        foreach (var h in world.Humanoids)
            if (h.Alive && h.State != HumanoidState.Dead)
                radar.DrawBlip(c, h.Position.X, h.Position.Y, 1.6f, HumanoidColor);
        foreach (var l in world.Landers)  if (l.Alive) radar.DrawBlip(c, l.Position.X, l.Position.Y, 2f, LanderColor);
        foreach (var m in world.Mutants)  if (m.Alive) radar.DrawBlip(c, m.Position.X, m.Position.Y, 2f, MutantColor);
        foreach (var b in world.Baiters)  if (b.Alive) radar.DrawBlip(c, b.Position.X, b.Position.Y, 2f, BaiterColor);
        foreach (var b in world.Bombers)  if (b.Alive) radar.DrawBlip(c, b.Position.X, b.Position.Y, 2f, BomberColor);
        foreach (var p in world.Pods)     if (p.Alive) radar.DrawBlip(c, p.Position.X, p.Position.Y, 2f, PodColor);

        radar.DrawFrame(c, RadarFrame, ShipColor, world.Ship.FacingSign);
    }

    // --- HUD ------------------------------------------------------------------

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        HudText.Draw(c, $"{w.Score:000000}", 18, 70, SKTextAlign.Left, HudFont, HudColor);
        if (w.HighScore > 0)
            HudText.Draw(c, $"HI {w.HighScore:000000}", cw / 2f, 66, SKTextAlign.Center, HudSmallFont, HudColor);

        if (w.Mode == GameMode.Playing || w.Mode == GameMode.Attract)
        {
            HudText.Draw(c, $"WAVE {w.Wave}", cw - 18, 66, SKTextAlign.Right, HudSmallFont, HudColor);
            HudText.Draw(c, $"LIVES {Math.Max(0, w.Ship.Lives)}   BOMBS {Math.Max(0, w.Ship.SmartBombs)}   HUMANS {w.HumanoidsRemaining}",
                         18, ch - 16, SKTextAlign.Left, HudSmallFont, HudColor);
        }
    }

    // --- Title / Attract ------------------------------------------------------

    static void DrawTitle(SKCanvas c, GameWorld w, float cw, float ch)
    {
        Marquee.DrawRainbowTitle(c, "KIA'I", cw, ch * 0.22f);
        Marquee.Draw(c, "RUNNING ON UNO PLATFORM AND SKIASHARP 4", cw, ch);

        if (w.Mode == GameMode.Title && w.ShowAttractText)
        {
            HudText.Draw(c, "PRESS SPACE OR CLICK TO PATROL", cw / 2f, ch / 2f + 20, SKTextAlign.Center, HudSmallFont, HudColor);
            HudText.Draw(c, "ARROWS / WASD THRUST  -  SPACE FIRE  -  B BOMB  -  H HYPERSPACE",
                         cw / 2f, ch / 2f + 52, SKTextAlign.Center, HudSmallFont, HudColor);
        }
        if (w.Mode == GameMode.Attract)
            HudText.Draw(c, "ATTRACT - PRESS ANY KEY", cw / 2f, ch / 2f + 20, SKTextAlign.Center, HudSmallFont, HudColor);
    }
}
