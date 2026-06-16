using System;
using SkiaSharp;

namespace Koa.Game;

// Koa renderer — torch-lit neon dungeon. The signature divergence from the other
// demos is the camera: instead of scaling one fixed world to fit the canvas, we
// push Camera2D.Apply (a clamped world->screen translate) and draw the world
// 1:1 under it, culling to the camera's VisibleWorldRect so a multi-screen
// dungeon only ever paints the ~40x25 cells actually on screen. HUD is drawn
// afterwards in screen space (outside the camera transform).
public static class Renderer
{
    // Crypt palette.
    static readonly SKColor WallColor   = new(0x66, 0x33, 0xAA);   // arcane violet stone
    static readonly SKColor WallGlow    = new(0x99, 0x55, 0xEE, 0xA0);
    static readonly SKColor DoorColor   = new(0xFF, 0xCC, 0x44);   // gilded door
    static readonly SKColor ExitColor   = new(0x44, 0xFF, 0xAA);   // way out, beckoning green
    static readonly SKColor HeroColor   = new(0xFF, 0x88, 0x33);   // warrior ember
    static readonly SKColor HeroSkin    = new(0xFF, 0xDD, 0xAA);
    static readonly SKColor BoltColor   = new(0xFF, 0xEE, 0x88);   // glowing bolt
    static readonly SKColor HudColor    = new(0xFF, 0x77, 0x44);   // torch ember (catalog accent)
    static readonly SKColor HealthColor = new(0x55, 0xFF, 0x66);
    static readonly SKColor HealthLow   = new(0xFF, 0x44, 0x33);
    static readonly SKColor KeyColor    = new(0xFF, 0xDD, 0x44);
    static readonly SKColor FoodColor   = new(0xFF, 0x99, 0x55);
    static readonly SKColor PotionColor = new(0xCC, 0x66, 0xFF);
    static readonly SKColor TreasColor  = new(0xFF, 0xEE, 0x66);
    static readonly SKColor GenColor    = new(0xFF, 0x55, 0x22);

    static readonly SKColor BgTop    = new(0x0A, 0x04, 0x10);
    static readonly SKColor BgBottom = new(0x1A, 0x0A, 0x24);

    const string MarqueeText = "KOA  -  WARRIOR NEEDS FOOD BADLY  -  SMASH THE GENERATORS  -  UNO PLATFORM + SKIASHARP 4";

    public static void Render(SKCanvas canvas, GameWorld world, float cw, float ch)
    {
        NeonBackground.Draw(canvas, cw, ch, BgTop, BgBottom);

        if (world.Map is not null)
        {
            // World pass under the clamped camera transform.
            world.Camera.Apply(canvas);
            DrawWorld(canvas, world);
            canvas.Restore();
        }

        DrawHud(canvas, world, cw, ch);
    }

    static void DrawWorld(SKCanvas c, GameWorld world)
    {
        var view = world.Camera.VisibleWorldRect(TileMap.CellSize);
        DrawTiles(c, world, view);
        DrawPickups(c, world, view);
        DrawGenerators(c, world, view);
        DrawEnemies(c, world, view);
        DrawProjectiles(c, world, view);
        if (world.Hero.Alive) DrawHero(c, world.Hero);
        DrawParticles(c, world);
    }

    // Windowed tile loop — only the cells the camera can see, not the whole grid.
    static void DrawTiles(SKCanvas c, GameWorld world, SKRect view)
    {
        var map = world.Map;
        float cs = TileMap.CellSize;

        int c0 = Math.Max(0, (int)MathF.Floor(view.Left / cs));
        int c1 = Math.Min(map.Cols - 1, (int)MathF.Floor(view.Right / cs));
        int r0 = Math.Max(0, (int)MathF.Floor(view.Top / cs));
        int r1 = Math.Min(map.Rows - 1, (int)MathF.Floor(view.Bottom / cs));

        float inner = cs * 0.46f;

        // Wall glow + sharp pass (the neon block+halo idiom from Hahai's maze).
        using (var glow = new SKPaint
        {
            Style = SKPaintStyle.Fill, Color = WallGlow, IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7f),
        })
        using (var sharp = new SKPaint { Style = SKPaintStyle.Fill, Color = WallColor, IsAntialias = true })
        {
            for (int r = r0; r <= r1; r++)
                for (int col = c0; col <= c1; col++)
                {
                    if (map[col, r] != Tile.Wall) continue;
                    var p = map.CellCenter(col, r);
                    c.DrawCircle(p.X, p.Y, inner, glow);
                }
            for (int r = r0; r <= r1; r++)
                for (int col = c0; col <= c1; col++)
                {
                    if (map[col, r] != Tile.Wall) continue;
                    var p = map.CellCenter(col, r);
                    c.DrawRoundRect(new SKRect(p.X - inner, p.Y - inner, p.X + inner, p.Y + inner), 5f, 5f, sharp);
                }
        }

        // Doors (gilded bars) and exits (glowing portals).
        using var door = new SKPaint { Style = SKPaintStyle.Fill, Color = DoorColor, IsAntialias = true };
        for (int r = r0; r <= r1; r++)
            for (int col = c0; col <= c1; col++)
            {
                var t = map[col, r];
                if (t == Tile.Door)
                {
                    var p = map.CellCenter(col, r);
                    c.DrawRoundRect(new SKRect(p.X - inner, p.Y - inner * 0.55f, p.X + inner, p.Y + inner * 0.55f), 4f, 4f, door);
                }
                else if (t == Tile.Exit)
                {
                    var p = map.CellCenter(col, r);
                    float pulse = 0.7f + 0.3f * MathF.Sin((float)Marquee.TimeSeconds * 3f + col + r);
                    NeonDraw.CircleFill(c, p.X, p.Y, cs * 0.3f * pulse, ExitColor);
                }
            }
    }

    static bool InView(SKRect view, Vec2 p, float r) =>
        p.X + r >= view.Left && p.X - r <= view.Right && p.Y + r >= view.Top && p.Y - r <= view.Bottom;

    static void DrawPickups(SKCanvas c, GameWorld world, SKRect view)
    {
        foreach (var p in world.Pickups)
        {
            if (!p.Alive || !InView(view, p.Pos, p.Radius)) continue;
            switch (p.Kind)
            {
                case PickupKind.Key:
                    NeonDraw.CircleFill(c, p.Pos.X, p.Pos.Y - 3f, 4f, KeyColor);
                    NeonDraw.Line(c, p.Pos.X, p.Pos.Y, p.Pos.X, p.Pos.Y + 9f, KeyColor);
                    NeonDraw.Line(c, p.Pos.X, p.Pos.Y + 9f, p.Pos.X + 5f, p.Pos.Y + 9f, KeyColor);
                    break;
                case PickupKind.Food:
                    NeonDraw.CircleFill(c, p.Pos.X, p.Pos.Y, 6f, FoodColor);
                    break;
                case PickupKind.Potion:
                    NeonDraw.CircleFill(c, p.Pos.X, p.Pos.Y + 2f, 5f, PotionColor);
                    NeonDraw.Line(c, p.Pos.X, p.Pos.Y - 6f, p.Pos.X, p.Pos.Y, PotionColor);
                    break;
                case PickupKind.Treasure:
                    DrawDiamond(c, p.Pos.X, p.Pos.Y, 7f, TreasColor);
                    break;
            }
        }
    }

    static void DrawDiamond(SKCanvas c, float x, float y, float r, SKColor color)
    {
        Span<SKPoint> pts = stackalloc SKPoint[4]
        {
            new(x, y - r), new(x + r, y), new(x, y + r), new(x - r, y),
        };
        using var path = VectorShapes.Poly(pts, close: true);
        NeonDraw.Stroke(c, path, color);
    }

    static void DrawGenerators(SKCanvas c, GameWorld world, SKRect view)
    {
        foreach (var g in world.Generators)
        {
            if (!g.Alive || !InView(view, g.Pos, g.Radius)) continue;
            int lvl = Math.Clamp(g.Level, 1, 3);
            // Colour telegraphs the level (and how dangerous its spawn is):
            // 1 = green (Grunts), 2 = amber (Ghosts), 3 = red (Demons).
            var col = new SKColor(lvl >= 3 ? 0xFFFF4455u : lvl == 2 ? 0xFFFFAA33u : 0xFF66FF88u);
            float pulse = 0.8f + 0.2f * MathF.Sin((float)Marquee.TimeSeconds * 6f + g.Col);
            // A jagged spawner core...
            NeonDraw.CircleFill(c, g.Pos.X, g.Pos.Y, g.Radius * 0.4f * pulse, col);
            // ...wrapped in one rotating ring per remaining level, so the ring
            // count reads as "hits left" and visibly drops as you damage it.
            for (int k = 0; k < lvl; k++)
            {
                float rr = g.Radius * (0.55f + 0.22f * k);
                using var ring = VectorShapes.Blob(new Random(g.Col * 73 + g.Row * 131 + k * 17), rr, 7, 0.35f);
                VectorShapes.DrawAt(c, ring, g.Pos.X, g.Pos.Y, (float)Marquee.TimeSeconds * 40f * (k % 2 == 0 ? 1f : -1f), 1f, col);
            }
        }
    }

    static void DrawEnemies(SKCanvas c, GameWorld world, SKRect view)
    {
        foreach (var e in world.Enemies)
        {
            if (!e.Alive || !InView(view, e.Pos, e.Radius)) continue;
            var color = new SKColor(GameWorld.EnemyColor(e.Kind));
            switch (e.Kind)
            {
                case EnemyKind.Ghost:
                    // Spectral wisp: a soft glowing disc that bobs.
                    NeonDraw.CircleFill(c, e.Pos.X, e.Pos.Y, e.Radius * 0.8f, color);
                    break;
                case EnemyKind.Demon:
                    // Bulky horned brute.
                    NeonDraw.CircleFill(c, e.Pos.X, e.Pos.Y, e.Radius * 0.7f, color);
                    NeonDraw.Line(c, e.Pos.X - e.Radius * 0.5f, e.Pos.Y - e.Radius * 0.5f, e.Pos.X - e.Radius, e.Pos.Y - e.Radius, color);
                    NeonDraw.Line(c, e.Pos.X + e.Radius * 0.5f, e.Pos.Y - e.Radius * 0.5f, e.Pos.X + e.Radius, e.Pos.Y - e.Radius, color);
                    break;
                default: // Grunt — a small lurching humanoid blob.
                    NeonDraw.CircleFill(c, e.Pos.X, e.Pos.Y, e.Radius * 0.7f, color);
                    break;
            }
        }
    }

    static void DrawProjectiles(SKCanvas c, GameWorld world, SKRect view)
    {
        foreach (var p in world.Projectiles)
        {
            if (!p.Alive || !InView(view, p.Pos, p.Radius)) continue;
            NeonDraw.CircleFill(c, p.Pos.X, p.Pos.Y, p.Radius, BoltColor);
        }
    }

    // The warrior — an amber disc with a small head/shield poking in the aim
    // direction so its facing reads at a glance.
    //
    // Facing is applied by rotating the canvas about the BODY CENTRE (hero.Pos)
    // and drawing the silhouette in body-local space, where the head sits at a
    // fixed +X offset. Pivoting the transform at the centre means changing
    // direction turns the whole warrior in place; the body never orbits the eye.
    // (The previous draw positioned the eye from an un-rotated world offset, so
    // the disc + eye read as the eye swinging the body around rather than the
    // sprite turning on the spot.)
    static void DrawHero(SKCanvas c, Hero hero)
    {
        float r = hero.Radius;
        var d = hero.AimDir;
        if (d.X == 0f && d.Y == 0f) d = new Vec2(1f, 0f);
        d = d.Normalized();

        // Aim direction -> degrees (0 = +X), matching SKCanvas.RotateDegrees.
        float angleDeg = MathF.Atan2(d.Y, d.X) * 180f / MathF.PI;

        c.Save();
        c.Translate(hero.Pos.X, hero.Pos.Y); // pivot at the body centre
        c.RotateDegrees(angleDeg);

        // Body centred on the (now translated) origin, and the head/shield as a
        // fixed local offset along +X. Both are drawn in local space, so the
        // rotation spins the pair about the centre as a single sprite.
        NeonDraw.CircleFill(c, 0f, 0f, r, HeroColor);
        NeonDraw.CircleFill(c, r * 0.7f, 0f, r * 0.4f, HeroSkin);

        c.Restore();
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

    // --- HUD (screen space) -------------------------------------------------
    static void DrawHud(SKCanvas c, GameWorld world, float cw, float ch)
    {
        using var font      = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 22);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);
        using var bigFont   = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);

        // Draining health bar — the signature readout.
        float maxHp = MathF.Max(1f, world.Hero.Stats.MaxHealth);
        float fill = world.Hero.Health / maxHp;
        var barColor = fill < 0.2f
            ? (((int)(Marquee.TimeSeconds * 6) & 1) == 0 ? HealthLow : HealthColor) // flash when critical
            : HealthColor;
        HudText.Draw(c, "HEALTH", 24, 30, SKTextAlign.Left, smallFont, HudColor);
        HudText.Bar(c, 24, 38, 240, 18, fill, barColor);

        HudText.Draw(c, $"SCORE {world.Score:000000}", cw / 2f, 34, SKTextAlign.Center, font, HudColor);
        if (world.HighScore > 0)
            HudText.Draw(c, $"HI {world.HighScore:000000}", cw / 2f, 56, SKTextAlign.Center, smallFont, HudColor);

        HudText.Draw(c, $"LEVEL {world.Level}", cw - 24, 30, SKTextAlign.Right, font, HudColor);
        HudText.Draw(c, $"KEYS {world.Hero.Keys}   POTIONS {world.Hero.Potions}", cw - 24, 54, SKTextAlign.Right, smallFont, HudColor);

        switch (world.Mode)
        {
            case GameMode.Title:    DrawTitleOverlay(c, cw, ch, smallFont); break;
            case GameMode.Attract:  HudText.Draw(c, "ATTRACT  -  PRESS SPACE", cw / 2f, ch - 26f, SKTextAlign.Center, smallFont, HudColor); break;
            case GameMode.GameOver:
                HudText.Draw(c, "GAME OVER",                          cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   HudColor);
                HudText.Draw(c, $"FINAL SCORE  {world.Score:000000}", cw / 2f, ch / 2f + 50f,  SKTextAlign.Center, smallFont, HudColor);
                HudText.Draw(c, "PRESS SPACE TO RAID AGAIN",          cw / 2f, ch / 2f + 90f,  SKTextAlign.Center, smallFont, HudColor);
                break;
        }

        if (world.Mode != GameMode.Playing)
            Marquee.Draw(c, MarqueeText, cw, ch, baselineFraction: 0.985f);
    }

    static void DrawTitleOverlay(SKCanvas c, float cw, float ch, SKFont smallFont)
    {
        Marquee.DrawRainbowTitle(c, "KOA", cw, ch * 0.14f);
        HudText.Draw(c, "GAUNTLET IN NEON",                       cw / 2f, ch * 0.16f + GlyphFont.CharHeight + 30f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "PRESS SPACE OR CLICK TO RAID",           cw / 2f, ch * 0.52f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Arrows or WASD  -  move 8 ways",         cw / 2f, ch * 0.58f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Space  -  fire     Q/E  -  smite potion", cw / 2f, ch * 0.62f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Smash generators, grab food, reach the exit", cw / 2f, ch * 0.66f, SKTextAlign.Center, smallFont, HudColor);
    }
}
