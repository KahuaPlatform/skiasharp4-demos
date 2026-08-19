using System;
using SkiaSharp;

namespace Eli.Game;

// Eli renderer — a lamp-lit cross-section of packed earth. Framing works exactly
// as Koa's: push Camera2D.Apply (a clamped world->screen transform), draw the
// world 1:1 under it, cull to VisibleWorldRect, restore, then HUD in screen space.
//
// One deliberate divergence from Koa's tile pass. Koa draws each WALL cell as a
// blurred circle plus a sharp round-rect, which is cheap because walls are the
// minority of its grid. Here dirt is the overwhelming majority — ~640 visible
// cells would mean ~640 blurred fills a frame, the wrong shape for the wasm frame
// budget. So dirt is run-length-encoded per row into flat rects (matte earth is
// not a glowing element), and the neon signature goes on the TUNNEL OUTLINES:
// every excavated face that borders dirt is stroked with the halo+sharp pass.
public static class Renderer
{
    // Four strata, shallowest first. Dark and desaturated, deliberately clear of
    // the rock-fall warning red and the amber accent.
    static readonly SKColor[] StratumFill =
    {
        new(0x8A, 0x5A, 0x2B),   // warm topsoil ochre
        new(0x7A, 0x4A, 0x38),   // red-brown clay
        new(0x63, 0x48, 0x2E),   // deep loam
        new(0x4A, 0x3A, 0x55),   // slate-violet shale
    };

    // Brightened stratum hues used for the glowing excavated outline.
    static readonly SKColor[] StratumEdge =
    {
        new(0xE0, 0xA0, 0x55),
        new(0xD8, 0x8A, 0x70),
        new(0xC0, 0x96, 0x60),
        new(0x9A, 0x82, 0xC8),
    };

    static readonly SKColor RockColor  = new(0x39, 0x33, 0x4A);   // bedrock
    static readonly SKColor RockEdge   = new(0x6E, 0x66, 0x8E);
    static readonly SKColor SkyGlow    = new(0x33, 0x55, 0x88, 0x50);

    static readonly SKColor DiggerBody = new(0xFF, 0xAA, 0x33);   // dirt amber (accent)
    static readonly SKColor DiggerLamp = new(0xFF, 0xEE, 0xBB);
    static readonly SKColor HarpoonCol = new(0xCC, 0xEE, 0xFF);
    static readonly SKColor WarnRed    = new(0xFF, 0x3B, 0x22);   // rock-fall telegraph ONLY
    static readonly SKColor BoulderCol = new(0x9A, 0x7A, 0x55);
    static readonly SKColor HudColor   = new(0xFF, 0xAA, 0x33);   // accent

    static readonly SKColor BgTop    = new(0x0B, 0x06, 0x03);
    static readonly SKColor BgBottom = new(0x24, 0x14, 0x08);

    const string MarqueeText =
        "ELI  -  DIG DEEP  -  PUMP THEM TILL THEY POP  -  MIND THE FALLING ROCK  -  UNO PLATFORM + SKIASHARP 4";

    public static void Render(SKCanvas canvas, GameWorld world, float cw, float ch)
    {
        NeonBackground.Draw(canvas, cw, ch, BgTop, BgBottom);

        if (world.Field is not null)
        {
            world.Camera.Apply(canvas);
            DrawWorld(canvas, world);
            canvas.Restore();
        }

        DrawHud(canvas, world, cw, ch);
    }

    static void DrawWorld(SKCanvas c, GameWorld world)
    {
        var view = world.Camera.VisibleWorldRect(Field.CellSize);
        DrawTerrain(c, world, view);
        DrawBoulders(c, world, view);
        DrawEnemies(c, world, view);
        DrawHarpoon(c, world);
        if (world.Digger.Alive && world.Digger.RespawnTimer <= 0f) DrawDigger(c, world.Digger);
        DrawParticles(c, world);
    }

    // --- Terrain ------------------------------------------------------------
    static void DrawTerrain(SKCanvas c, GameWorld world, SKRect view)
    {
        var field = world.Field;
        float cs = Field.CellSize;

        int c0 = Math.Max(0, (int)MathF.Floor(view.Left / cs));
        int c1 = Math.Min(field.Cols - 1, (int)MathF.Floor(view.Right / cs));
        int r0 = Math.Max(0, (int)MathF.Floor(view.Top / cs));
        int r1 = Math.Min(field.Rows - 1, (int)MathF.Floor(view.Bottom / cs));

        using var fill = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };

        // Pass 1 — run-length fills. One rect per contiguous run of same-material
        // cells in a row, instead of one draw per cell.
        for (int r = r0; r <= r1; r++)
        {
            int run = c0;
            while (run <= c1)
            {
                var t = field[run, r];
                if (t == Tile.Tunnel || t == Tile.Sky) { run++; continue; }

                int end = run;
                while (end + 1 <= c1 && field[end + 1, r] == t) end++;

                fill.Color = t == Tile.Rock ? RockColor : StratumFill[Field.StratumAt(r)];
                c.DrawRect(new SKRect(run * cs, r * cs, (end + 1) * cs, (r + 1) * cs), fill);
                run = end + 1;
            }
        }

        // Pass 2 — the glow. Every excavated face that borders solid material gets
        // the halo+sharp neon stroke; that outline IS the visual signature here.
        for (int r = r0; r <= r1; r++)
            for (int col = c0; col <= c1; col++)
            {
                if (field[col, r] != Tile.Tunnel) continue;

                float x0 = col * cs, y0 = r * cs, x1 = x0 + cs, y1 = y0 + cs;
                var edge = StratumEdge[Field.StratumAt(r)];

                if (IsSolid(field, col, r - 1)) StrokeFace(c, x0, y0, x1, y0, field, col, r - 1, edge);
                if (IsSolid(field, col, r + 1)) StrokeFace(c, x0, y1, x1, y1, field, col, r + 1, edge);
                if (IsSolid(field, col - 1, r)) StrokeFace(c, x0, y0, x0, y1, field, col - 1, r, edge);
                if (IsSolid(field, col + 1, r)) StrokeFace(c, x1, y0, x1, y1, field, col + 1, r, edge);
            }

        // A soft wash along the sky/dirt boundary so the surface reads as daylight.
        if (r0 <= Field.SkyRows)
        {
            using var sky = new SKPaint
            {
                Style = SKPaintStyle.Fill, Color = SkyGlow, IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 14f),
            };
            c.DrawRect(new SKRect(c0 * cs, 0f, (c1 + 1) * cs, Field.SkyRows * cs), sky);
        }
    }

    // Which neighbours an excavated face should be outlined against. Sky counts:
    // it is solid to movement, so where the player has dug open the top row the
    // sky/ground boundary gets the same glowing edge as any other tunnel wall.
    static bool IsSolid(Field f, int col, int row)
    {
        if (!f.InBounds(col, row)) return true;
        var t = f[col, row];
        return t == Tile.Dirt || t == Tile.Rock || t == Tile.Sky;
    }

    // Stroke one excavated face, tinted by whichever material is on the far side.
    static void StrokeFace(SKCanvas c, float x0, float y0, float x1, float y1,
                           Field f, int nCol, int nRow, SKColor dirtEdge)
    {
        var color = (f.InBounds(nCol, nRow) && f[nCol, nRow] == Tile.Rock) ? RockEdge : dirtEdge;
        NeonDraw.Line(c, x0, y0, x1, y1, color, halo: 6f, sharp: 1.6f);
    }

    static bool InView(SKRect view, Vec2 p, float r) =>
        p.X + r >= view.Left && p.X - r <= view.Right && p.Y + r >= view.Top && p.Y - r <= view.Bottom;

    // --- Boulders -----------------------------------------------------------
    static void DrawBoulders(SKCanvas c, GameWorld world, SKRect view)
    {
        foreach (var b in world.Boulders)
        {
            if (!b.Alive || !InView(view, b.Pos, b.Radius * 2f)) continue;

            // Deterministic silhouette per boulder, as Koa seeds its generator rings.
            using var shape = VectorShapes.Blob(new Random(b.Col * 73 + b.Row * 131), b.Radius, 9, 0.28f);

            float rot = 0f;
            var color = BoulderCol;

            switch (b.State)
            {
                case BoulderState.Wobbling:
                    // The telegraph: shake and flash the warning red. This window is
                    // the player's only chance to get out from underneath.
                    rot = MathF.Sin((float)Marquee.TimeSeconds * 18f * MathF.Tau) * 6f;
                    color = (((int)(Marquee.TimeSeconds * 12) & 1) == 0) ? WarnRed : BoulderCol;
                    break;

                case BoulderState.Falling:
                    // A short motion streak so a fast drop still reads.
                    NeonDraw.Line(c, b.Pos.X, b.Pos.Y - b.Radius * 1.6f,
                                     b.Pos.X, b.Pos.Y, BoulderCol.WithAlpha(0x66), halo: 10f, sharp: 3f);
                    break;

                case BoulderState.Shattering:
                    float t = MathF.Max(0f, b.StateTimer / GameWorld.BoulderShatterTime);
                    color = BoulderCol.WithAlpha((byte)(t * 255f));
                    break;
            }

            VectorShapes.DrawAt(c, shape, b.Pos.X, b.Pos.Y, rot, 1f, color);
        }
    }

    // --- Enemies ------------------------------------------------------------
    static void DrawEnemies(SKCanvas c, GameWorld world, SKRect view)
    {
        foreach (var e in world.Enemies)
        {
            if (!e.Alive || !InView(view, e.Pos, e.Radius * 3f)) continue;

            var color = new SKColor(GameWorld.EnemyColor(e.Kind));
            // Inflation swells the body — at full pumps it is nearly twice the size,
            // which is the whole read on "one more and it pops".
            float swell = 1f + 0.22f * e.Inflation;
            float r = e.Radius * swell;

            if (e.Mode == EnemyMode.Phasing)
            {
                // Flattened wisp phasing through the dirt: squashed along its travel
                // axis and translucent, so it reads as "inside the earth, untouchable".
                c.Save();
                c.Translate(e.Pos.X, e.Pos.Y);
                c.Scale(0.6f, 1.15f);
                NeonDraw.CircleFill(c, 0f, 0f, r * 0.85f, color.WithAlpha(0x88));
                c.Restore();
                continue;
            }

            switch (e.Kind)
            {
                case EnemyKind.Nohu:
                    // Scaled, spiky and squat.
                    using (var spikes = VectorShapes.Blob(new Random(e.GetHashCode() & 0xFFFF), r * 1.15f, 9, 0.3f))
                        VectorShapes.DrawAt(c, spikes, e.Pos.X, e.Pos.Y, 0f, 1f, color);
                    NeonDraw.CircleFill(c, e.Pos.X, e.Pos.Y, r * 0.6f, color);
                    break;

                default: // Uhane — a round spirit with goggle eyes.
                    NeonDraw.CircleFill(c, e.Pos.X, e.Pos.Y, r * 0.85f, color);
                    NeonDraw.CircleFill(c, e.Pos.X - r * 0.3f, e.Pos.Y - r * 0.15f, r * 0.2f, DiggerLamp);
                    NeonDraw.CircleFill(c, e.Pos.X + r * 0.3f, e.Pos.Y - r * 0.15f, r * 0.2f, DiggerLamp);
                    break;
            }
        }
    }

    // --- Harpoon ------------------------------------------------------------
    static void DrawHarpoon(SKCanvas c, GameWorld world)
    {
        ref readonly var h = ref world.Harpoon;
        if (h.State == HarpoonState.Idle) return;

        var tip = h.State == HarpoonState.Attached && h.Victim is { } v ? v.Pos : h.Tip;
        NeonDraw.Line(c, h.Origin.X, h.Origin.Y, tip.X, tip.Y, HarpoonCol, halo: 7f, sharp: 2.2f);
        NeonDraw.CircleFill(c, tip.X, tip.Y, 3.5f, HarpoonCol);
    }

    // --- Digger -------------------------------------------------------------
    //
    // Pivot at the BODY CENTRE and draw the silhouette in body-local space (the
    // Koa hero idiom), so changing facing turns the whole sprite in place rather
    // than swinging the lamp around the body.
    static void DrawDigger(SKCanvas c, Digger d)
    {
        float r = d.Radius;

        c.Save();
        c.Translate(d.Pos.X, d.Pos.Y);
        c.RotateDegrees(Facings.ToDegrees(d.Facing));

        NeonDraw.CircleFill(c, 0f, 0f, r, DiggerBody);
        // The drill snout, pointing along +X in local space.
        NeonDraw.Line(c, r * 0.5f, 0f, r * 1.5f, 0f, DiggerBody, halo: 8f, sharp: 3f);
        // Helmet lamp — brighter while actually cutting earth.
        NeonDraw.CircleFill(c, r * 0.35f, -r * 0.35f, r * 0.28f,
                            d.Digging ? DiggerLamp : DiggerLamp.WithAlpha(0xAA));

        c.Restore();
    }

    static void DrawParticles(SKCanvas c, GameWorld world)
    {
        foreach (var p in world.Particles)
        {
            float lifeT = p.Life / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            NeonDraw.CircleFill(c, p.Pos.X, p.Pos.Y, p.Size, new SKColor(p.Color).WithAlpha(alpha));
        }
    }

    // --- HUD (screen space) -------------------------------------------------
    static void DrawHud(SKCanvas c, GameWorld world, float cw, float ch)
    {
        using var font      = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 22);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);
        using var bigFont   = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);

        HudText.Draw(c, $"SCORE {world.Score:000000}", cw / 2f, 34, SKTextAlign.Center, font, HudColor);
        if (world.HighScore > 0)
            HudText.Draw(c, $"HI {world.HighScore:000000}", cw / 2f, 56, SKTextAlign.Center, smallFont, HudColor);

        HudText.Draw(c, $"LEVEL {world.Level}", cw - 24, 30, SKTextAlign.Right, font, HudColor);
        HudText.Draw(c, $"DEPTH {world.Field?.StratumAtWorld(world.Digger.Pos.Y) + 1}",
                     cw - 24, 54, SKTextAlign.Right, smallFont, HudColor);

        // Lives as digger pips.
        HudText.Draw(c, "LIVES", 24, 30, SKTextAlign.Left, smallFont, HudColor);
        for (int i = 0; i < world.Lives; i++)
            NeonDraw.CircleFill(c, 30f + i * 22f, 48f, 7f, DiggerBody);

        // The pump gauge — only while something is on the hook. Third consumer of
        // the shared HudText.Bar after Koa's health clock.
        if (world.Harpoon.State == HarpoonState.Attached && world.Harpoon.Victim is { } v)
        {
            float fill = Math.Clamp(v.Inflation / v.PumpsToBurst, 0f, 1f);
            HudText.Draw(c, "PUMP", 24, 84, SKTextAlign.Left, smallFont, HudColor);
            HudText.Bar(c, 24, 92, 200, 16, fill, fill > 0.75f ? WarnRed : HudColor);
        }

        if (world.LevelClearFlash > 0f && world.Mode == GameMode.Playing)
            HudText.Draw(c, "FIELD CLEARED", cw / 2f, ch * 0.4f, SKTextAlign.Center, font, HudColor);

        switch (world.Mode)
        {
            case GameMode.Title:
                DrawTitleOverlay(c, cw, ch, smallFont);
                break;
            case GameMode.Attract:
                HudText.Draw(c, "ATTRACT  -  PRESS SPACE", cw / 2f, ch - 26f, SKTextAlign.Center, smallFont, HudColor);
                break;
            case GameMode.GameOver:
                HudText.Draw(c, "GAME OVER",                          cw / 2f, ch / 2f,       SKTextAlign.Center, bigFont,   HudColor);
                HudText.Draw(c, $"FINAL SCORE  {world.Score:000000}", cw / 2f, ch / 2f + 50f, SKTextAlign.Center, smallFont, HudColor);
                HudText.Draw(c, "PRESS SPACE TO DIG AGAIN",           cw / 2f, ch / 2f + 90f, SKTextAlign.Center, smallFont, HudColor);
                break;
        }

        if (world.Mode != GameMode.Playing)
            Marquee.Draw(c, MarqueeText, cw, ch, baselineFraction: 0.985f);
    }

    static void DrawTitleOverlay(SKCanvas c, float cw, float ch, SKFont smallFont)
    {
        Marquee.DrawRainbowTitle(c, "ELI", cw, ch * 0.14f);
        HudText.Draw(c, "DIG DUG IN NEON",                          cw / 2f, ch * 0.16f + GlyphFont.CharHeight + 30f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "PRESS SPACE OR CLICK TO DIG",               cw / 2f, ch * 0.52f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Arrows or WASD  -  tunnel 4 ways",          cw / 2f, ch * 0.58f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Space  -  fire harpoon, then hold to pump", cw / 2f, ch * 0.62f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Stand still to pump  -  moving lets go",    cw / 2f, ch * 0.66f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Dig under a boulder to drop it on them",    cw / 2f, ch * 0.70f, SKTextAlign.Center, smallFont, HudColor);
    }
}
