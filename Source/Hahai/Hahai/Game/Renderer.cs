using System;
using SkiaSharp;

namespace Hahai.Game;

// Hahai renderer — Hawaiian-themed chase game. Honu (sea turtle) eats limu
// (sea grass dots), Lehua flowers act as the power pellets, and four Mo'o
// (water-spirit lizards) pursue. Mechanics mirror Pac-Man but the visual
// vocabulary is shells, petals, and serpentine tails.
public static class Renderer
{
    static readonly SKColor WallColor    = new(0x22, 0x77, 0xFF);   // reef coral cyan-blue
    static readonly SKColor WallGlow     = new(0x55, 0x99, 0xFF, 0xA0);
    static readonly SKColor LimuColor    = new(0x88, 0xFF, 0xAA);   // sea-grass green
    static readonly SKColor LehuaColor   = new(0xFF, 0x66, 0x33);   // red-orange ohia blossom
    static readonly SKColor LehuaCenter  = new(0xFF, 0xEE, 0x66);
    static readonly SKColor DoorColor    = new(0xFF, 0xAA, 0xCC);
    static readonly SKColor HonuShell    = new(0xFF, 0xAA, 0x55);   // warm tortoise-shell amber
    static readonly SKColor HonuShellDk  = new(0xCC, 0x66, 0x22);
    static readonly SKColor HonuSkin     = new(0xFF, 0xDD, 0x99);
    static readonly SKColor FrightColor  = new(0x22, 0x44, 0xFF);
    static readonly SKColor FrightFlash  = new(0xFF, 0xFF, 0xFF);
    static readonly SKColor EyeColor     = new(0xFF, 0xFF, 0xFF);
    static readonly SKColor PupilColor   = new(0x22, 0x22, 0x44);
    static readonly SKColor HudColor     = new(0x33, 0xF8, 0xFF);

    const string MarqueeText = "HAHAI  -  HONU VS MO'O  -  NEON CHASE  -  UNO PLATFORM + SKIASHARP 4";

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
        DrawMaze(c, world.Arena);
        DrawPellets(c, world);
        if (world.Pac.Alive) DrawHonu(c, world.Pac);
        DrawMoos(c, world);
        DrawParticles(c, world);
        DrawScorePopups(c, world);
    }

    // Draw walls as a glow stroke + sharp stroke per-cell. For each wall cell
    // we draw a small rounded square; adjacent wall cells stitch visually
    // because their halos overlap.
    static void DrawMaze(SKCanvas c, Arena arena)
    {
        float cs = Arena.CellSize;
        float halo = cs * 0.45f;
        float inner = cs * 0.28f;

        // First pass: glow halo for every wall cell.
        using (var glowPaint = new SKPaint
        {
            Style       = SKPaintStyle.Fill,
            Color       = WallGlow,
            IsAntialias = true,
            MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f),
        })
        {
            for (int r = 0; r < Arena.Rows; r++)
                for (int col = 0; col < Arena.Cols; col++)
                    if (arena.Tiles[col, r] == Tile.Wall)
                    {
                        var p = Arena.CellCenter(col, r);
                        c.DrawCircle(p.X, p.Y, halo, glowPaint);
                    }
        }

        // Second pass: solid sharp wall blocks. Use rounded rects so corridors
        // look like the classic maze tubes.
        using (var sharp = new SKPaint
        {
            Style       = SKPaintStyle.Fill,
            Color       = WallColor,
            IsAntialias = true,
        })
        {
            for (int r = 0; r < Arena.Rows; r++)
                for (int col = 0; col < Arena.Cols; col++)
                {
                    if (arena.Tiles[col, r] != Tile.Wall) continue;
                    var p = Arena.CellCenter(col, r);
                    c.DrawRoundRect(new SKRect(p.X - inner, p.Y - inner, p.X + inner, p.Y + inner), inner * 0.4f, inner * 0.4f, sharp);
                }
        }

        // Ghost door: pink bar.
        using (var doorPaint = new SKPaint
        {
            Style       = SKPaintStyle.Fill,
            Color       = DoorColor,
            IsAntialias = true,
        })
        {
            for (int r = 0; r < Arena.Rows; r++)
                for (int col = 0; col < Arena.Cols; col++)
                    if (arena.Tiles[col, r] == Tile.GhostDoor)
                    {
                        var p = Arena.CellCenter(col, r);
                        c.DrawRect(new SKRect(p.X - cs * 0.45f, p.Y - cs * 0.08f, p.X + cs * 0.45f, p.Y + cs * 0.08f), doorPaint);
                    }
        }
    }

    static void DrawPellets(SKCanvas c, GameWorld world)
    {
        var arena = world.Arena;
        for (int r = 0; r < Arena.Rows; r++)
            for (int col = 0; col < Arena.Cols; col++)
            {
                if (arena.Pellets[col, r])
                {
                    // Limu — small green sea-grass blip.
                    var p = Arena.CellCenter(col, r);
                    NeonDraw.CircleFill(c, p.X, p.Y, 1.8f, LimuColor);
                }
                else if (arena.PowerDot[col, r])
                {
                    var p = Arena.CellCenter(col, r);
                    float pulse = 0.85f + 0.15f * MathF.Sin((float)Environment.TickCount * 0.006f + col + r);
                    DrawLehua(c, p.X, p.Y, 7f * pulse);
                }
            }
    }

    // Lehua (ohia) flower: five round petals around a glowing pistil. Drawn at
    // power-pellet positions, slowly pulsing so the eye is drawn to them.
    static void DrawLehua(SKCanvas c, float cx, float cy, float r)
    {
        float petalR = r * 0.55f;
        float petalOff = r * 0.7f;
        // Halo behind the whole flower.
        using (var halo = new SKPaint
        {
            Style       = SKPaintStyle.Fill,
            Color       = LehuaColor.WithAlpha(0xA0),
            IsAntialias = true,
            MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f),
        })
        {
            c.DrawCircle(cx, cy, r, halo);
        }
        using var petal = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = LehuaColor };
        for (int i = 0; i < 5; i++)
        {
            float a = i * MathF.PI * 2f / 5f - MathF.PI / 2f;
            float px = cx + MathF.Cos(a) * petalOff;
            float py = cy + MathF.Sin(a) * petalOff;
            c.DrawCircle(px, py, petalR, petal);
        }
        using var pistil = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = LehuaCenter };
        c.DrawCircle(cx, cy, r * 0.32f, pistil);
    }

    // Honu — sea turtle. Round shell with hexagonal scute pattern, four
    // flippers angled outward, head poking forward in the moving direction
    // with a small mouth that opens and closes as it eats limu.
    static void DrawHonu(SKCanvas c, Pac pac)
    {
        float r = Arena.CellSize * 0.42f;
        var center = new SKPoint(pac.Position.X, pac.Position.Y);
        var (fx, fy) = HeadingVector(pac.Dir);

        // Glow halo behind the shell.
        using (var halo = new SKPaint
        {
            Style       = SKPaintStyle.Fill,
            Color       = HonuShell.WithAlpha(0xA0),
            IsAntialias = true,
            MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f),
        })
        {
            c.DrawCircle(center, r * 1.1f, halo);
        }

        // Four flippers — short ovals offset diagonally from the shell. Drawn
        // first so the shell sits on top.
        using (var flipper = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = HonuSkin })
        {
            float fOff = r * 0.55f;
            float fR   = r * 0.30f;
            float[] xs = { -fOff,  fOff, -fOff, fOff };
            float[] ys = { -fOff, -fOff,  fOff, fOff };
            for (int i = 0; i < 4; i++)
                c.DrawCircle(center.X + xs[i], center.Y + ys[i], fR, flipper);
        }

        // Head — small circle in front of the shell, in the moving direction.
        float headR = r * 0.32f;
        float headDist = r * 0.85f;
        float hx = center.X + fx * headDist;
        float hy = center.Y + fy * headDist;
        using (var head = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = HonuSkin })
        {
            c.DrawCircle(hx, hy, headR, head);
        }

        // Mouth on the head: a thin dark wedge that opens with mouth phase.
        float open = MathF.Sin(pac.MouthPhase * MathF.PI);
        if (open > 0.15f)
        {
            using var mouth = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x20, 0x10, 0x10) };
            float mouthW = headR * 0.7f;
            float mouthH = headR * 0.45f * open;
            // Rotate the small mouth ellipse to face direction of travel.
            c.Save();
            c.Translate(hx, hy);
            c.RotateDegrees(FacingDegrees(pac.Dir));
            c.DrawOval(new SKRect(0f, -mouthH, mouthW, mouthH), mouth);
            c.Restore();
        }

        // Shell — main amber body.
        using (var shell = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = HonuShell })
        {
            c.DrawCircle(center, r, shell);
        }

        // Scute pattern — 6 small darker hexagonal-ish dots inside the shell.
        using (var scute = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = HonuShellDk })
        {
            float scuteR = r * 0.18f;
            float scuteOff = r * 0.45f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * MathF.PI * 2f / 6f;
                c.DrawCircle(center.X + MathF.Cos(a) * scuteOff, center.Y + MathF.Sin(a) * scuteOff, scuteR, scute);
            }
            c.DrawCircle(center.X, center.Y, scuteR * 0.7f, scute);
        }
    }

    static (float fx, float fy) HeadingVector(Direction d) => d switch
    {
        Direction.Right => ( 1f,  0f),
        Direction.Left  => (-1f,  0f),
        Direction.Up    => ( 0f, -1f),
        Direction.Down  => ( 0f,  1f),
        _               => ( 1f,  0f),
    };

    static float FacingDegrees(Direction d) => d switch
    {
        Direction.Right => 0f,
        Direction.Down  => 90f,
        Direction.Left  => 180f,
        Direction.Up    => 270f,
        _               => 0f,
    };

    static void DrawMoos(SKCanvas c, GameWorld world)
    {
        foreach (var g in world.Ghosts) DrawMoo(c, g, world.PowerTimer);
    }

    // Mo'o — Hawaiian water-spirit lizard. Elongated oval body oriented in the
    // direction of motion with a tapering tail behind, eyes on the head end,
    // four little legs sticking out, all glowing in the spirit's kind color.
    static void DrawMoo(SKCanvas c, Ghost g, float powerTimer)
    {
        float r = Arena.CellSize * 0.42f;
        var pos = new SKPoint(g.Position.X, g.Position.Y);

        if (g.State == GhostState.Eaten)
        {
            // Devoured mo'o — just the spirit eyes drift back to the lua (cave).
            DrawSpiritEyes(c, pos, r, g.Dir);
            return;
        }

        SKColor body;
        if (g.State == GhostState.Frightened)
        {
            bool flash = powerTimer < 2f && ((int)(powerTimer * 5f) % 2 == 0);
            body = flash ? FrightFlash : FrightColor;
        }
        else
        {
            body = new SKColor(GameWorld.GhostBaseColor(g.Kind));
        }

        var (fx, fy) = HeadingVector(g.Dir);
        float facing = FacingDegrees(g.Dir);

        // Halo behind the whole body.
        using (var halo = new SKPaint
        {
            Style       = SKPaintStyle.Fill,
            Color       = body.WithAlpha(0x80),
            IsAntialias = true,
            MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7f),
        })
        {
            c.DrawCircle(pos, r * 1.15f, halo);
        }

        // Slithering tail — two diminishing circles trailing behind the body
        // along the direction-of-motion axis. Subtle phase-based wobble for life.
        using (var tail = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = body.WithAlpha(0xC8) })
        {
            float wobble = MathF.Sin((float)Environment.TickCount * 0.012f + g.Col * 0.5f) * r * 0.25f;
            // Perpendicular axis for wobble.
            float px = -fy, py = fx;
            float t1x = pos.X - fx * r * 0.85f + px * wobble * 0.6f;
            float t1y = pos.Y - fy * r * 0.85f + py * wobble * 0.6f;
            float t2x = pos.X - fx * r * 1.55f - px * wobble * 0.5f;
            float t2y = pos.Y - fy * r * 1.55f - py * wobble * 0.5f;
            c.DrawCircle(t1x, t1y, r * 0.45f, tail);
            c.DrawCircle(t2x, t2y, r * 0.25f, tail.Color.WithAlpha(0x88) is var ca ? new SKPaint { IsAntialias = true, Color = ca, Style = SKPaintStyle.Fill } : tail);
        }

        // Four tiny legs sticking out diagonally from the body center.
        using (var leg = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = body.WithAlpha(0xE0) })
        {
            c.Save();
            c.Translate(pos.X, pos.Y);
            c.RotateDegrees(facing);
            float lOff = r * 0.45f;
            float lR   = r * 0.18f;
            c.DrawCircle(-lOff * 0.5f, -lOff, lR, leg);
            c.DrawCircle( lOff * 0.5f, -lOff, lR, leg);
            c.DrawCircle(-lOff * 0.5f,  lOff, lR, leg);
            c.DrawCircle( lOff * 0.5f,  lOff, lR, leg);
            c.Restore();
        }

        // Main body — elongated oval (long in motion direction).
        using (var bodyPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = body })
        {
            c.Save();
            c.Translate(pos.X, pos.Y);
            c.RotateDegrees(facing);
            c.DrawOval(new SKRect(-r * 1.05f, -r * 0.75f, r * 1.05f, r * 0.75f), bodyPaint);
            c.Restore();
        }

        // Spine ridge — darker line along the back.
        using (var ridge = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = body.WithAlpha(0xFF) })
        {
            // dark version: blend toward black by halving channels.
            ridge.Color = new SKColor((byte)(body.Red / 2), (byte)(body.Green / 2), (byte)(body.Blue / 2));
            c.Save();
            c.Translate(pos.X, pos.Y);
            c.RotateDegrees(facing);
            c.DrawLine(-r * 0.9f, 0f, r * 0.85f, 0f, ridge);
            c.Restore();
        }

        // Eyes on the head end (forward along facing axis).
        if (g.State == GhostState.Frightened)
            DrawFrightFace(c, pos, r, fx, fy);
        else
            DrawMooEyes(c, pos, r, fx, fy);
    }

    static void DrawMooEyes(SKCanvas c, SKPoint pos, float r, float fx, float fy)
    {
        // Head is forward along (fx, fy); two eyes offset perpendicularly.
        float headFwd = r * 0.55f;
        float eyeSpread = r * 0.30f;
        float px = -fy, py = fx; // perpendicular axis
        float cxA = pos.X + fx * headFwd + px * eyeSpread;
        float cyA = pos.Y + fy * headFwd + py * eyeSpread;
        float cxB = pos.X + fx * headFwd - px * eyeSpread;
        float cyB = pos.Y + fy * headFwd - py * eyeSpread;
        float eyeR  = r * 0.20f;
        float pupilR = r * 0.10f;

        using var eyePaint   = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = EyeColor };
        using var pupilPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = PupilColor };

        c.DrawCircle(cxA, cyA, eyeR, eyePaint);
        c.DrawCircle(cxB, cyB, eyeR, eyePaint);
        c.DrawCircle(cxA + fx * eyeR * 0.4f, cyA + fy * eyeR * 0.4f, pupilR, pupilPaint);
        c.DrawCircle(cxB + fx * eyeR * 0.4f, cyB + fy * eyeR * 0.4f, pupilR, pupilPaint);
    }

    static void DrawSpiritEyes(SKCanvas c, SKPoint pos, float r, Direction dir)
    {
        var (fx, fy) = HeadingVector(dir);
        DrawMooEyes(c, pos, r, fx, fy);
    }

    // Frightened mo'o — small wide eyes near the head and a wavy mouth across
    // the back. Coords are computed in the rotated frame so the mouth orients
    // with the body direction.
    static void DrawFrightFace(SKCanvas c, SKPoint pos, float r, float fx, float fy)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = new SKColor(0xFF, 0xFF, 0xCC) };
        float headFwd = r * 0.55f;
        float px = -fy, py = fx;
        float eyeOff = r * 0.25f;
        float eyeR = r * 0.09f;
        c.DrawCircle(pos.X + fx * headFwd + px * eyeOff, pos.Y + fy * headFwd + py * eyeOff, eyeR, paint);
        c.DrawCircle(pos.X + fx * headFwd - px * eyeOff, pos.Y + fy * headFwd - py * eyeOff, eyeR, paint);
        // Zigzag mouth running across the back perpendicular to motion.
        using var pb = new SKPathBuilder();
        float cx = pos.X - fx * r * 0.10f;
        float cy = pos.Y - fy * r * 0.10f;
        float span = r * 0.6f;
        pb.MoveTo(cx - px * span, cy - py * span);
        for (int i = 1; i <= 5; i++)
        {
            float t = (i / 5f) * 2f - 1f; // -1..+1
            float jx = ((i & 1) == 0 ? -fx : fx) * 3f;
            float jy = ((i & 1) == 0 ? -fy : fy) * 3f;
            pb.LineTo(cx + px * span * t + jx, cy + py * span * t + jy);
        }
        using var path = pb.Detach();
        c.DrawPath(path, paint);
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

    static void DrawScorePopups(SKCanvas c, GameWorld world)
    {
        using var f = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 14);
        foreach (var sp in world.ScorePopups)
        {
            float lifeT = sp.Life / MathF.Max(0.001f, sp.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(sp.Color).WithAlpha(alpha);
            NeonPaints.FillSharp.Color = color;
            c.DrawText($"+{sp.Value}", sp.Pos.X, sp.Pos.Y, SKTextAlign.Center, f, NeonPaints.FillSharp);
        }
    }

    static void DrawHud(SKCanvas c, GameWorld world, float cw, float ch)
    {
        using var font      = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 22);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);
        using var bigFont   = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
        using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 38);

        HudText.Draw(c, $"SCORE {world.Score:00000}", 24, 36, SKTextAlign.Left, font, HudColor);
        if (world.HighScore > 0)
            HudText.Draw(c, $"HI {world.HighScore:00000}", cw / 2f, 30, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, $"LEVEL {world.Level}", cw - 24, 36, SKTextAlign.Right, font, HudColor);

        // Lives icons (mini honu shells) in the bottom-left.
        for (int i = 0; i < world.Lives; i++)
        {
            float lx = 28 + i * 28f;
            float ly = ch - 28f;
            DrawHonuIcon(c, lx, ly, 10f);
        }

        if (world.PlacardTimer > 0 && !string.IsNullOrEmpty(world.PlacardText))
        {
            HudText.Draw(c, world.PlacardText, cw / 2f, ch * 0.5f, SKTextAlign.Center, placardFont, HonuShell);
        }

        switch (world.Mode)
        {
            case GameMode.Title:    DrawTitleOverlay(c, cw, ch, smallFont); break;
            case GameMode.Attract:  HudText.Draw(c, "ATTRACT  -  PRESS ANY KEY", cw / 2f, ch - 26f, SKTextAlign.Center, smallFont, HudColor); break;
            case GameMode.GameOver:
                HudText.Draw(c, "GAME OVER",                       cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   HudColor);
                HudText.Draw(c, $"FINAL SCORE  {world.Score:00000}", cw / 2f, ch / 2f + 50f, SKTextAlign.Center, smallFont, HudColor);
                HudText.Draw(c, "PRESS SPACE TO PLAY AGAIN",       cw / 2f, ch / 2f + 90f,  SKTextAlign.Center, smallFont, HudColor);
                break;
        }

        // Marquee is a title/attract/over flourish — hide it while actually
        // playing so it doesn't compete with the maze.
        if (world.Mode != GameMode.Playing)
            Marquee.Draw(c, MarqueeText, cw, ch, baselineFraction: 0.985f);
    }

    // Lives indicator: a tiny honu shell — a colored disc with three darker
    // scute dots so it reads as a turtle and not a generic ball.
    static void DrawHonuIcon(SKCanvas c, float cx, float cy, float r)
    {
        using var shell = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = HonuShell };
        c.DrawCircle(cx, cy, r, shell);
        using var scute = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = HonuShellDk };
        c.DrawCircle(cx,           cy - r * 0.4f, r * 0.22f, scute);
        c.DrawCircle(cx - r * 0.4f, cy + r * 0.2f, r * 0.22f, scute);
        c.DrawCircle(cx + r * 0.4f, cy + r * 0.2f, r * 0.22f, scute);
    }

    static void DrawTitleOverlay(SKCanvas c, float cw, float ch, SKFont smallFont)
    {
        Marquee.DrawRainbowTitle(c, "HAHAI", cw, ch * 0.12f);
        HudText.Draw(c, "HONU vs MO'O",                     cw / 2f, ch * 0.18f + GlyphFont.CharHeight + 28f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "PRESS SPACE OR CLICK TO START",    cw / 2f, ch * 0.50f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Arrows or WASD  -  steer the honu", cw / 2f, ch * 0.56f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Eat limu  -  dodge the mo'o",      cw / 2f, ch * 0.60f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Lehua flowers flip them edible",   cw / 2f, ch * 0.64f, SKTextAlign.Center, smallFont, HudColor);
    }
}
