using System;
using System.Diagnostics;
using SkiaSharp;

namespace Lua.Game;

// Lua renderer — Tempest-style well shooter. Shared chassis pieces come from
// `Arcade.Common.Chassis` via globally-imported usings. This file owns the
// game-specific draws: 3D well, player claw, climbing enemies, spike trails,
// warp transition, and the radial starfield (which is gameplay-relevant —
// it streaks outward during warp).
public static class Renderer
{
    static readonly SKColor PlayerColor       = new(0xFF, 0xEE, 0x44);
    static readonly SKColor PlayerCockpit     = new(0xFF, 0xFF, 0xFF);
    static readonly SKColor RimColor          = new(0x33, 0xF8, 0xFF);
    static readonly SKColor RimAltColor       = new(0x33, 0x88, 0xFF);
    static readonly SKColor SpokeColor        = new(0x55, 0x55, 0xCC, 0xC0);
    static readonly SKColor InnerRingColor    = new(0x77, 0x44, 0xAA, 0xA0);
    static readonly SKColor BulletColor       = new(0xFF, 0xEE, 0x33);
    static readonly SKColor EnemyBulletColor  = new(0xFF, 0x44, 0x66);
    static readonly SKColor SpikeColor        = new(0x66, 0xFF, 0xAA);
    static readonly SKColor FlipperColor      = new(0xFF, 0x44, 0x66);
    static readonly SKColor TankerColor       = new(0xAA, 0x66, 0xFF);
    static readonly SKColor SpikerColor       = new(0xFF, 0xEE, 0x33);
    static readonly SKColor FuseballColor1    = new(0x55, 0xFF, 0x77);
    static readonly SKColor FuseballColor2    = new(0xFF, 0xFF, 0x55);
    static readonly SKColor HudColor          = new(0x33, 0xF8, 0xFF);

    const string MarqueeText = "LUA · UNO PLATFORM · SKIASHARP 4 · NEON TEMPEST DEMO";

    // --- Game-specific starfield (radial; streaks during warp) ---
    // Stays in the game-specific renderer because the streak behavior is tied
    // to GameMode.Warp + WarpProgress — it's gameplay-relevant rendering, not
    // generic chassis.
    struct Star { public float Angle, Radius, Brightness; }
    const int StarCount = 90;
    static Star[]? _stars;
    static readonly Random _starRng = new(13);
    static readonly Stopwatch _starsClock = Stopwatch.StartNew();
    static readonly SKPaint _starPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    static readonly SKPaint _starStreakPaint = new()
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, StrokeCap = SKStrokeCap.Round,
    };

    static void EnsureStars(float worldW, float worldH)
    {
        if (_stars != null) return;
        _stars = new Star[StarCount];
        float maxRadius = MathF.Sqrt(worldW * worldW + worldH * worldH) * 0.6f;
        for (int i = 0; i < _stars.Length; i++)
        {
            double u = _starRng.NextDouble();
            _stars[i].Angle  = (float)(_starRng.NextDouble() * Math.PI * 2);
            _stars[i].Radius = (float)Math.Sqrt(u) * maxRadius;
            _stars[i].Brightness = 0.35f + (float)_starRng.NextDouble() * 0.55f;
        }
    }

    static void DrawStars(SKCanvas canvas, GameWorld world)
    {
        if (_stars is null) return;
        float cx = world.Well.Center.X;
        float cy = world.Well.Center.Y;
        float twinkle = (float)_starsClock.Elapsed.TotalSeconds;

        if (world.Mode == GameMode.Warp)
        {
            float k = 1.0f + world.WarpProgress * 4.0f;
            for (int i = 0; i < _stars.Length; i++)
            {
                var s = _stars[i];
                float rNear = s.Radius * k;
                float rFar  = s.Radius * (k * 1.25f + 18f);
                float dx = MathF.Cos(s.Angle); float dy = MathF.Sin(s.Angle);
                byte a = (byte)(255 * s.Brightness);
                _starStreakPaint.Color = new SKColor(255, 255, 255, a);
                canvas.DrawLine(cx + dx * rNear, cy + dy * rNear,
                                cx + dx * rFar,  cy + dy * rFar, _starStreakPaint);
            }
        }
        else
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                var s = _stars[i];
                float flick = 0.85f + 0.15f * MathF.Sin(twinkle * 1.7f + i * 0.31f);
                byte a = (byte)(255 * s.Brightness * flick);
                _starPaint.Color = new SKColor(255, 255, 255, a);
                float r = s.Brightness > 0.75f ? 1.5f : s.Brightness > 0.55f ? 1.0f : 0.7f;
                canvas.DrawCircle(cx + MathF.Cos(s.Angle) * s.Radius,
                                  cy + MathF.Sin(s.Angle) * s.Radius, r, _starPaint);
            }
        }
    }

    // --- Render entry ---

    public static void Render(SKCanvas canvas, GameWorld world, float canvasW, float canvasH)
    {
        EnsureStars(world.Width, world.Height);
        NeonBackground.Draw(canvas, canvasW, canvasH);

        float scale = MathF.Min(canvasW / world.Width, canvasH / world.Height);
        float ox = (canvasW - world.Width * scale) / 2f;
        float oy = (canvasH - world.Height * scale) / 2f;

        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);
        DrawStars(canvas, world);
        DrawWorld(canvas, world);
        canvas.Restore();

        DrawHud(canvas, world, canvasW, canvasH);
    }

    static void DrawWorld(SKCanvas canvas, GameWorld world)
    {
        PlayfieldBorder.Draw(canvas, world.Width, world.Height, HudColor);

        foreach (var p in world.Particles)
        {
            float lifeT = p.Life / MathF.Max(0.001f, p.MaxLife);
            byte alpha = (byte)Math.Clamp(lifeT * 255f, 0f, 255f);
            var color = new SKColor(p.Color).WithAlpha(alpha);
            NeonDraw.CircleFill(canvas, p.Pos.X, p.Pos.Y, p.Size, color);
        }

        DrawWell(canvas, world);
        DrawSpikes(canvas, world);

        foreach (var b in world.Bullets)
        {
            var p = world.BulletPos(b);
            var color = b.FromPlayer ? BulletColor : EnemyBulletColor;
            float r = b.FromPlayer ? 3.6f : 3.2f;
            NeonDraw.CircleFill(canvas, p.X, p.Y, r, color);
        }

        foreach (var e in world.Enemies)
        {
            if (e.State == EnemyState.Dead) continue;
            DrawEnemy(canvas, world, e);
        }

        if (world.Mode != GameMode.Warp && PlayerVisible(world.Player))
        {
            DrawPlayer(canvas, world);
        }
        else if (world.Mode == GameMode.Warp)
        {
            DrawPlayerWarp(canvas, world);
        }

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

    static void DrawWell(SKCanvas c, GameWorld world)
    {
        var well = world.Well;
        int n = well.RimPoints.Length;
        int segCount = well.SegmentCount;

        for (int i = 0; i < n; i++)
        {
            var a = well.RimPoints[i];
            var b = well.Center;
            NeonDraw.Line(c, a.X, a.Y, b.X, b.Y, SpokeColor, halo: 3.5f, sharp: 1.2f);
        }

        for (int ring = 0; ring < 4; ring++)
        {
            float z = 0.18f + ring * 0.22f;
            var ringColor = InnerRingColor.WithAlpha((byte)(0x70 - ring * 16));
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (!well.Closed && i == n - 1) continue;
                var pa = well.Project(well.RimPoints[i], z);
                var pb = well.Project(well.RimPoints[j], z);
                NeonDraw.Line(c, pa.X, pa.Y, pb.X, pb.Y, ringColor, halo: 2.5f, sharp: 0.9f);
            }
        }

        for (int s = 0; s < segCount; s++)
        {
            var a = well.RimPoints[s];
            var b = well.RimPoints[(s + 1) % n];
            var color = well.IsAlternateSlot(s) ? RimAltColor : RimColor;
            NeonDraw.Line(c, a.X, a.Y, b.X, b.Y, color, halo: 6.5f, sharp: 2.4f);
        }
    }

    static void DrawSpikes(SKCanvas c, GameWorld world)
    {
        var well = world.Well;
        foreach (var s in world.Spikes)
        {
            var mid = well.SegmentMid(s.Segment, s.MinDepth);
            var deep = well.SegmentMid(s.Segment, 1f);
            NeonDraw.Line(c, mid.X, mid.Y, deep.X, deep.Y, SpikeColor, halo: 4.5f, sharp: 1.6f);
            NeonDraw.CircleFill(c, mid.X, mid.Y, 2.4f, SpikeColor);
        }
    }

    static void DrawPlayer(SKCanvas c, GameWorld world)
    {
        var well = world.Well;
        int seg = world.Player.Segment;
        int tgt = world.Player.TargetSegment;
        float t  = world.Player.SegmentT;

        var pos = world.PlayerPos();
        Vec2 dir;
        if (seg == tgt)
        {
            dir = well.SegmentDir(seg);
        }
        else
        {
            var dA = well.SegmentDir(seg);
            var dB = well.SegmentDir(tgt);
            dir = new Vec2(dA.X + (dB.X - dA.X) * t, dA.Y + (dB.Y - dA.Y) * t).Normalized();
        }
        var outward = (seg == tgt)
            ? well.SegmentNormal(seg)
            : new Vec2(
                well.SegmentNormal(seg).X + (well.SegmentNormal(tgt).X - well.SegmentNormal(seg).X) * t,
                well.SegmentNormal(seg).Y + (well.SegmentNormal(tgt).Y - well.SegmentNormal(seg).Y) * t).Normalized();

        DrawClaw(c, pos, dir, outward, world.Player.Invuln > 0);
    }

    static void DrawPlayerWarp(SKCanvas c, GameWorld world)
    {
        var well = world.Well;
        int seg = world.Player.Segment;
        var dir = well.SegmentDir(seg);
        var outward = well.SegmentNormal(seg);
        var pos = world.Well.SegmentMid(seg, world.WarpProgress);
        float persp = 1f / (1f + world.WarpProgress * Well.PerspectiveK);
        pos = new Vec2(pos.X + outward.X * 14f * persp, pos.Y + outward.Y * 14f * persp);
        DrawClaw(c, pos, dir, outward, false, scale: persp);
    }

    static void DrawClaw(SKCanvas c, Vec2 pos, Vec2 dir, Vec2 outward, bool flicker, float scale = 1f)
    {
        if (flicker && ((int)(Marquee.TimeSeconds * 16) & 1) == 0) return;

        float w = 22f * scale;
        float h = 18f * scale;
        float k = 7f  * scale;
        var dx = dir.X; var dy = dir.Y;
        Vec2 P(float a, float b) => new(pos.X + dx * a + outward.X * (-b), pos.Y + dy * a + outward.Y * (-b));

        var p0 = P(-w,        0f);
        var p1 = P(-w * 0.65f, h);
        var p2 = P(-w * 0.30f, k);
        var p3 = P( 0f,        h * 1.1f);
        var p4 = P( w * 0.30f, k);
        var p5 = P( w * 0.65f, h);
        var p6 = P( w,        0f);

        using var b = new SKPathBuilder();
        b.AddPoly(stackalloc SKPoint[]
        {
            new(p0.X, p0.Y), new(p1.X, p1.Y), new(p2.X, p2.Y),
            new(p3.X, p3.Y),
            new(p4.X, p4.Y), new(p5.X, p5.Y), new(p6.X, p6.Y),
        }, close: false);
        using var path = b.Detach();
        NeonDraw.Stroke(c, path, PlayerColor);

        NeonDraw.CircleFill(c, p1.X, p1.Y, 2.2f * scale, PlayerCockpit);
        NeonDraw.CircleFill(c, p5.X, p5.Y, 2.2f * scale, PlayerCockpit);
    }

    static void DrawEnemy(SKCanvas c, GameWorld world, Enemy e)
    {
        var pos = world.EnemyPos(e);
        float persp = 1f / (1f + e.Depth * Well.PerspectiveK);
        float size = 16f * persp;
        if (size < 2f) return;

        switch (e.Kind)
        {
            case EnemyKind.Flipper:  DrawFlipper(c, pos, size, FlipperColor); break;
            case EnemyKind.Tanker:   DrawTanker(c, pos, size, TankerColor);   break;
            case EnemyKind.Spiker:   DrawSpiker(c, pos, size, SpikerColor);   break;
            case EnemyKind.Fuseball: DrawFuseball(c, pos, size, e.Hue);       break;
        }
    }

    static void DrawFlipper(SKCanvas c, Vec2 pos, float size, SKColor color)
    {
        c.Save();
        c.Translate(pos.X, pos.Y);
        using var b = new SKPathBuilder();
        b.MoveTo(-size,      0);
        b.LineTo( 0,        -size * 0.55f);
        b.LineTo( size,      0);
        b.LineTo( 0,         size * 0.55f);
        b.Close();
        b.MoveTo(-size * 0.6f, -size * 0.55f);
        b.LineTo( size * 0.6f,  size * 0.55f);
        b.MoveTo( size * 0.6f, -size * 0.55f);
        b.LineTo(-size * 0.6f,  size * 0.55f);
        using var path = b.Detach();
        NeonDraw.Stroke(c, path, color);
        c.Restore();
    }

    static void DrawTanker(SKCanvas c, Vec2 pos, float size, SKColor color)
    {
        c.Save();
        c.Translate(pos.X, pos.Y);
        using var b = new SKPathBuilder();
        b.AddPoly(stackalloc SKPoint[]
        {
            new(-size,      -size * 0.7f),
            new( size,      -size * 0.7f),
            new( size * 0.4f, 0),
            new( size,       size * 0.7f),
            new(-size,       size * 0.7f),
            new(-size * 0.4f, 0),
        }, close: true);
        using var path = b.Detach();
        NeonDraw.Stroke(c, path, color);
        NeonDraw.CircleFill(c, 0, 0, size * 0.18f, color);
        c.Restore();
    }

    static void DrawSpiker(SKCanvas c, Vec2 pos, float size, SKColor color)
    {
        c.Save();
        c.Translate(pos.X, pos.Y);
        using var b = new SKPathBuilder();
        b.MoveTo(-size,       size * 0.5f);
        b.LineTo(-size * 0.5f, -size * 0.5f);
        b.LineTo( 0,           size * 0.5f);
        b.LineTo( size * 0.5f, -size * 0.5f);
        b.LineTo( size,        size * 0.5f);
        using var path = b.Detach();
        NeonDraw.Stroke(c, path, color);
        c.Restore();
    }

    static void DrawFuseball(SKCanvas c, Vec2 pos, float size, float hue)
    {
        float t = (float)Marquee.TimeSeconds * 8f + hue * 0.05f;
        for (int i = 0; i < 6; i++)
        {
            float ang = i * MathF.PI / 3f + t;
            float r = size * (0.6f + 0.4f * MathF.Sin(t * 2f + i));
            float x1 = pos.X + MathF.Cos(ang) * size * 0.2f;
            float y1 = pos.Y + MathF.Sin(ang) * size * 0.2f;
            float x2 = pos.X + MathF.Cos(ang) * r;
            float y2 = pos.Y + MathF.Sin(ang) * r;
            var color = (i & 1) == 0 ? FuseballColor1 : FuseballColor2;
            NeonDraw.Line(c, x1, y1, x2, y2, color, halo: 4.5f, sharp: 1.4f);
        }
        NeonDraw.CircleFill(c, pos.X, pos.Y, size * 0.3f, FuseballColor1);
    }

    static bool PlayerVisible(Player p)
    {
        if (p.Invuln <= 0) return true;
        return ((int)(p.Invuln * 10f) & 1) == 0;
    }

    static void DrawHud(SKCanvas c, GameWorld w, float cw, float ch)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
        HudText.Draw(c, $"{w.Score:00000}", 24, 36, SKTextAlign.Left, font, HudColor);

        if (w.HighScore > 0)
        {
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
            HudText.Draw(c, $"HI {w.HighScore:00000}", cw / 2f, 28, SKTextAlign.Center, smallFont, HudColor);
        }

        if (w.Mode == GameMode.Playing || w.Mode == GameMode.Warp || w.Mode == GameMode.Attract)
        {
            using var levelFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 22);
            HudText.Draw(c, $"LEVEL {w.Level}", cw - 24, 32, SKTextAlign.Right, levelFont, HudColor);

            using var zapFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);
            HudText.Draw(c, $"SUPER ZAPPER  x{w.Player.SuperZapperUsesLeft}", cw - 24, 56, SKTextAlign.Right, zapFont, HudColor);

            for (int i = 0; i < w.LivesLeft; i++)
            {
                c.Save();
                c.Translate(36f + i * 36f, ch - 32f);
                c.Scale(0.55f);
                DrawClaw(c, Vec2.Zero, new Vec2(1, 0), new Vec2(0, -1), false);
                c.Restore();
            }

            if (w.PlacardTimer > 0 && !string.IsNullOrEmpty(w.PlacardText))
            {
                using var placardFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 44);
                HudText.Draw(c, w.PlacardText, cw / 2f, ch * 0.20f, SKTextAlign.Center, placardFont, HudColor);
            }
        }

        if (w.Mode == GameMode.Attract)
        {
            using var attractFont = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 20);
            HudText.Draw(c, "ATTRACT  -  PRESS ANY KEY", cw / 2f, ch - 32f, SKTextAlign.Center, attractFont, HudColor);
        }

        if (w.Mode == GameMode.Title)
        {
            DrawTitle(c, cw, ch);
            Marquee.Draw(c, MarqueeText, cw, ch);
        }
        else if (w.Mode == GameMode.GameOver)
        {
            using var bigFont   = new SKFont(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
            using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
            HudText.Draw(c, "GAME OVER",              cw / 2f, ch / 2f,        SKTextAlign.Center, bigFont,   HudColor);
            HudText.Draw(c, $"FINAL SCORE  {w.Score:00000}", cw / 2f, ch / 2f + 50f, SKTextAlign.Center, smallFont, HudColor);
            HudText.Draw(c, $"YOU REACHED LEVEL {w.Level}", cw / 2f, ch / 2f + 80f, SKTextAlign.Center, smallFont, HudColor);
            HudText.Draw(c, "PRESS SPACE TO PLAY AGAIN",     cw / 2f, ch / 2f + 130f, SKTextAlign.Center, smallFont, HudColor);
        }
    }

    static void DrawTitle(SKCanvas c, float cw, float ch)
    {
        Marquee.DrawRainbowTitle(c, "LUA", cw, ch * 0.18f);

        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 22);
        using var instrFont = new SKFont(SKTypeface.FromFamilyName("Consolas"), 18);
        HudText.Draw(c, "TEMPEST-STYLE VECTOR DEMO", cw / 2f, ch * 0.18f + GlyphFont.CharHeight + 28f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "PRESS SPACE OR CLICK TO START", cw / 2f, ch * 0.42f, SKTextAlign.Center, smallFont, HudColor);
        HudText.Draw(c, "Left / Right or A / D  -  rotate around rim",          cw / 2f, ch * 0.48f, SKTextAlign.Center, instrFont, HudColor);
        HudText.Draw(c, "Space  -  fire    Z  -  super zapper (2 per level)",   cw / 2f, ch * 0.52f, SKTextAlign.Center, instrFont, HudColor);
    }
}
