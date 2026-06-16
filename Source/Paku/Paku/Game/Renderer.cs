using SkiaSharp;

namespace Paku.Game;

/// <summary>
/// All of Paku's drawing. Unlike the fixed-Viewbox arcade demos, Paku renders to
/// the full canvas and applies its own camera transform (translate-to-center →
/// scale by zoom → translate by camera) so the 5000×5000 world maps onto the
/// screen. World-space content is drawn inside that transform; the plasma
/// background and HUD are drawn in screen space outside it.
/// </summary>
public static class Renderer
{
    const string MarqueeText = "PAKU · UNO PLATFORM · SKIASHARP 4 · NEON CELL ARENA";

    // Reusable paints — allocated once, color/alpha mutated per draw call (zero
    // per-frame paint allocation). Several carry mask-filter blurs for the glow.
    static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    static readonly SKPaint _haloPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f)
    };
    static readonly SKPaint _bigHaloPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 14f)
    };
    static readonly SKPaint _strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1.5f
    };
    static readonly SKPaint _strokeHaloPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f)
    };
    static readonly SKPaint _plasmaPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };
    static readonly SKPaint _gridPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
        Color = new SKColor(30, 10, 60, 40)
    };

    // HUD fonts
    static readonly SKFont _hudFont      = new(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 28);
    static readonly SKFont _hudSmall     = new(SKTypeface.FromFamilyName("Consolas"), 22);
    static readonly SKFont _hudBig       = new(SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold), 56);
    static readonly SKFont _instrFont    = new(SKTypeface.FromFamilyName("Consolas"), 18);

    /// <summary>
    /// Renders one frame: screen-space plasma backdrop, then the world (grid,
    /// border, spores, particles, cells) under the camera transform, then the
    /// screen-space HUD.
    /// </summary>
    public static void Render(SKCanvas canvas, GameWorld world, float canvasW, float canvasH)
    {
        canvas.Clear(new SKColor(0x03, 0x00, 0x0A));

        // --- Plasma background (screen-space, full canvas) ---
        DrawPlasmaBackground(canvas, canvasW, canvasH, world.TotalTime);

        canvas.Save();

        // --- Camera transform: center the player on screen, zoom, then pan ---
        // Order matters: translate to screen center, scale by zoom, then shift by
        // the camera position so (CameraX,CameraY) lands at the screen center.
        float zoom = world.Zoom;
        float cx = canvasW / 2f;
        float cy = canvasH / 2f;
        canvas.Translate(cx, cy);
        canvas.Scale(zoom, zoom);
        canvas.Translate(-world.CameraX, -world.CameraY);

        // --- World grid ---
        DrawWorldGrid(canvas, world);

        // --- World border ---
        DrawWorldBorder(canvas);

        // --- Spores ---
        DrawSpores(canvas, world);

        // --- Particles ---
        DrawParticles(canvas, world);

        // --- Enemy cells ---
        foreach (var e in world.Enemies)
            DrawCell(canvas, e, world.TotalTime, false);

        // --- Player cell ---
        if (world.Mode != GameMode.GameOver)
            DrawCell(canvas, world.Player, world.TotalTime, true);

        canvas.Restore();

        // --- HUD (screen-space) ---
        DrawHud(canvas, world, canvasW, canvasH);
    }

    // Classic demoscene plasma: three overlapping sine fields summed and mapped to
    // a dark neon palette. Rendered as coarse 24px blocks (not per-pixel) so it's
    // cheap; the blockiness reads as intentional retro texture.
    static void DrawPlasmaBackground(SKCanvas canvas, float w, float h, float time)
    {
        // Low-res plasma rendered into cells for performance
        const int cellSize = 24;
        int cols = (int)(w / cellSize) + 1;
        int rows = (int)(h / cellSize) + 1;

        float t = time * 0.4f;

        for (int iy = 0; iy < rows; iy++)
        {
            float py = iy * cellSize;
            float ny = iy / (float)rows;
            for (int ix = 0; ix < cols; ix++)
            {
                float px = ix * cellSize;
                float nx = ix / (float)cols;

                // Three overlapping sine fields
                float v1 = MathF.Sin(nx * 6f + t * 1.1f) * MathF.Cos(ny * 4f + t * 0.7f);
                float v2 = MathF.Sin(MathF.Sqrt((nx - 0.5f) * (nx - 0.5f) + (ny - 0.5f) * (ny - 0.5f)) * 12f - t * 1.5f);
                float v3 = MathF.Sin(nx * 3f + ny * 5f + t * 0.9f);
                float v = (v1 + v2 + v3) / 3f; // -1..1

                // Map to deep neon colors (keep it dark so cells pop)
                byte r = (byte)(MathF.Max(0, MathF.Sin(v * MathF.PI) * 0.35f + 0.02f) * 255);
                byte g = (byte)(MathF.Max(0, MathF.Sin(v * MathF.PI + 2.1f) * 0.20f + 0.01f) * 255);
                byte b = (byte)(MathF.Max(0, MathF.Sin(v * MathF.PI + 4.2f) * 0.45f + 0.05f) * 255);

                _plasmaPaint.Color = new SKColor(r, g, b, 255);
                canvas.DrawRect(px, py, cellSize, cellSize, _plasmaPaint);
            }
        }
    }

    static void DrawWorldGrid(SKCanvas canvas, GameWorld world)
    {
        float spacing = 200f;
        for (float x = 0; x <= GameWorld.WorldWidth; x += spacing)
            canvas.DrawLine(x, 0, x, GameWorld.WorldHeight, _gridPaint);
        for (float y = 0; y <= GameWorld.WorldHeight; y += spacing)
            canvas.DrawLine(0, y, GameWorld.WorldWidth, y, _gridPaint);
    }

    static void DrawWorldBorder(SKCanvas canvas)
    {
        var rect = new SKRect(0, 0, GameWorld.WorldWidth, GameWorld.WorldHeight);

        // Halo
        _strokeHaloPaint.Color = new SKColor(0x33, 0xF8, 0xFF, 0x50);
        _strokeHaloPaint.StrokeWidth = 6f;
        canvas.DrawRect(rect, _strokeHaloPaint);

        // Sharp
        _strokePaint.Color = new SKColor(0x33, 0xF8, 0xFF, 0xA0);
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawRect(rect, _strokePaint);
    }

    static void DrawSpores(SKCanvas canvas, GameWorld world)
    {
        foreach (var s in world.Spores)
        {
            if (!s.Alive) continue;
            var color = HsvColor.HsvToRgb(s.Hue, 0.9f, 1f);

            // Tiny glow dot
            _haloPaint.Color = color.WithAlpha(0x60);
            canvas.DrawCircle(s.Pos.X, s.Pos.Y, Spore.Radius * 2.5f, _haloPaint);

            _fillPaint.Color = color;
            canvas.DrawCircle(s.Pos.X, s.Pos.Y, Spore.Radius, _fillPaint);
        }
    }

    static void DrawParticles(SKCanvas canvas, GameWorld world)
    {
        foreach (var p in world.Particles)
        {
            if (!p.Alive) continue;
            byte alpha = (byte)(p.Alpha * 220);
            var color = HsvColor.HsvToRgb(p.Hue, 0.8f, 1f).WithAlpha(alpha);

            _haloPaint.Color = color.WithAlpha((byte)(alpha / 2));
            canvas.DrawCircle(p.Pos.X, p.Pos.Y, p.Size * 2.5f, _haloPaint);

            _fillPaint.Color = color;
            canvas.DrawCircle(p.Pos.X, p.Pos.Y, p.Size, _fillPaint);
        }
    }

    const int BlobSegments = 36; // perimeter sample count for the amoeba outline

    /// <summary>
    /// Builds the closed amoeba outline for a cell by sampling
    /// <see cref="Cell.RadiusAt"/> at <see cref="BlobSegments"/> angles, so the
    /// membrane reflects the cell's per-harmonic wobble at the current time.
    /// </summary>
    static SKPath BuildBlobPath(Cell cell, float time)
    {
        var builder = new SKPathBuilder();
        float step = MathF.Tau / BlobSegments;
        for (int i = 0; i <= BlobSegments; i++)
        {
            float a = i * step;
            float r = cell.RadiusAt(a, time);
            float px = cell.Pos.X + MathF.Cos(a) * r;
            float py = cell.Pos.Y + MathF.Sin(a) * r;
            if (i == 0) builder.MoveTo(px, py);
            else        builder.LineTo(px, py);
        }
        builder.Close();
        return builder.Snapshot();
    }

    // Draws one cell as layered passes: big soft outer glow, inner halo, body
    // fill, white nucleus, and a neon membrane stroke — plus role/aim indicators.
    // The player gets brighter alphas than enemies so it always stands out.
    static void DrawCell(SKCanvas canvas, Cell cell, float time, bool isPlayer)
    {
        if (!cell.Alive) return;
        float r = cell.Radius;
        var baseColor = HsvColor.HsvToRgb(cell.Hue, 0.85f, 1f);

        // Build the organic outline once, reuse for all passes. The glow/halo
        // passes redraw it under a scale-about-center transform to enlarge it.
        using var blob = BuildBlobPath(cell, time);

        // Outer glow (big, soft) — scaled-up version of the blob
        canvas.Save();
        canvas.Translate(cell.Pos.X, cell.Pos.Y);
        canvas.Scale(1.7f);
        canvas.Translate(-cell.Pos.X, -cell.Pos.Y);
        byte glowAlpha = (byte)(isPlayer ? 0x50 : 0x30);
        _bigHaloPaint.Color = baseColor.WithAlpha(glowAlpha);
        canvas.DrawPath(blob, _bigHaloPaint);
        canvas.Restore();

        // Inner halo — slightly larger
        canvas.Save();
        canvas.Translate(cell.Pos.X, cell.Pos.Y);
        canvas.Scale(1.15f);
        canvas.Translate(-cell.Pos.X, -cell.Pos.Y);
        _haloPaint.Color = baseColor.WithAlpha(0x70);
        canvas.DrawPath(blob, _haloPaint);
        canvas.Restore();

        // Cell body fill
        byte bodyAlpha = (byte)(isPlayer ? 0xCC : 0x99);
        _fillPaint.Color = baseColor.WithAlpha(bodyAlpha);
        canvas.DrawPath(blob, _fillPaint);

        // Bright inner nucleus (still a circle — organelles are round)
        float nucleusR = r * 0.35f;
        _fillPaint.Color = new SKColor(255, 255, 255, isPlayer ? (byte)0xBB : (byte)0x70);
        canvas.DrawCircle(cell.Pos.X, cell.Pos.Y, nucleusR, _fillPaint);

        // Membrane stroke (neon outline of the blob)
        _strokeHaloPaint.Color = baseColor.WithAlpha(0x80);
        _strokeHaloPaint.StrokeWidth = 4f;
        canvas.DrawPath(blob, _strokeHaloPaint);

        _strokePaint.Color = baseColor;
        _strokePaint.StrokeWidth = 1.5f;
        canvas.DrawPath(blob, _strokePaint);

        // Hunter indicator: three red dots orbiting the cell so aggressors read
        // as dangerous at a glance.
        if (cell.Behavior == CellBehavior.Hunter)
        {
            float dotR = 3f;
            float angle = time * 2f;
            for (int i = 0; i < 3; i++)
            {
                float a = angle + i * MathF.Tau / 3f;
                float dx = MathF.Cos(a) * r * 0.6f;
                float dy = MathF.Sin(a) * r * 0.6f;
                _fillPaint.Color = new SKColor(0xFF, 0x33, 0x33, 0xAA);
                canvas.DrawCircle(cell.Pos.X + dx, cell.Pos.Y + dy, dotR, _fillPaint);
            }
        }

        // Player direction indicator: a short neon stinger pointing along velocity
        // (only when actually moving) to make aim/heading legible.
        if (isPlayer)
        {
            float velLen = cell.Vel.Length;
            if (velLen > 5f)
            {
                float nx = cell.Vel.X / velLen;
                float ny = cell.Vel.Y / velLen;
                float tipX = cell.Pos.X + nx * (r + 8f);
                float tipY = cell.Pos.Y + ny * (r + 8f);

                _strokeHaloPaint.Color = baseColor.WithAlpha(0x90);
                _strokeHaloPaint.StrokeWidth = 3f;
                canvas.DrawLine(cell.Pos.X + nx * r, cell.Pos.Y + ny * r, tipX, tipY, _strokeHaloPaint);

                _strokePaint.Color = baseColor;
                _strokePaint.StrokeWidth = 1.5f;
                canvas.DrawLine(cell.Pos.X + nx * r, cell.Pos.Y + ny * r, tipX, tipY, _strokePaint);
            }
        }
    }

    // Screen-space HUD: live score/best/mass while playing; rainbow title +
    // instructions + scrolling marquee on the attract screen; the game-over panel.
    static void DrawHud(SKCanvas canvas, GameWorld world, float w, float h)
    {
        float margin = 20f;

        if (world.Mode == GameMode.Playing || world.Mode == GameMode.GameOver)
        {
            // Score
            var scoreColor = new SKColor(0xFF, 0xFF, 0xFF);
            HudText.Draw(canvas, $"SCORE  {world.Score}", margin, margin + 30, SKTextAlign.Left, _hudFont, scoreColor);

            // High score
            HudText.Draw(canvas, $"BEST  {world.HighScore}", w - margin, margin + 30, SKTextAlign.Right, _hudSmall, new SKColor(0x88, 0x88, 0xCC));

            // Mass indicator
            HudText.Draw(canvas, $"MASS  {(int)world.Player.Mass}", margin, margin + 65, SKTextAlign.Left, _instrFont, new SKColor(0x66, 0xFF, 0xAA));
        }

        if (world.Mode == GameMode.Attract)
        {
            // Title with rainbow hue cycling
            Marquee.DrawRainbowTitle(canvas, "PAKU", w, h * 0.28f);

            // Subtitle
            HudText.Draw(canvas, "CONSUME  OR  BE  CONSUMED", w / 2, h * 0.42f, SKTextAlign.Center, _hudSmall, new SKColor(0x88, 0xCC, 0xFF));

            // Instructions
            float instrY = h * 0.55f;
            var instrColor = new SKColor(0x99, 0x99, 0xBB);
            HudText.Draw(canvas, "WASD  OR  ARROWS  TO  AIM", w / 2, instrY, SKTextAlign.Center, _instrFont, instrColor);
            HudText.Draw(canvas, "HOLD  SPACE  OR  CLICK  TO  THRUST", w / 2, instrY + 35, SKTextAlign.Center, _instrFont, instrColor);
            HudText.Draw(canvas, "ABSORB  SMALLER  CELLS", w / 2, instrY + 70, SKTextAlign.Center, _instrFont, instrColor);
            HudText.Draw(canvas, "AVOID  LARGER  CELLS", w / 2, instrY + 105, SKTextAlign.Center, _instrFont, instrColor);

            // Blink "press space"
            float blink = MathF.Sin(world.TotalTime * 3f);
            if (blink > 0)
            {
                HudText.Draw(canvas, "PRESS  SPACE  TO  START", w / 2, h * 0.82f, SKTextAlign.Center, _hudFont, new SKColor(0xFF, 0xCC, 0x33));
            }

            // Scrolling marquee
            Marquee.Draw(canvas, MarqueeText, w, h);
        }

        if (world.Mode == GameMode.GameOver)
        {
            float blink = MathF.Sin(world.TotalTime * 4f);
            byte alpha = (byte)(155 + (int)(blink * 100));
            HudText.Draw(canvas, "GAME  OVER", w / 2, h / 2, SKTextAlign.Center, _hudBig, new SKColor(0xFF, 0x33, 0x66, alpha));

            HudText.Draw(canvas, $"FINAL  SCORE  {world.Score}", w / 2, h / 2 + 60, SKTextAlign.Center, _hudFont, new SKColor(0xFF, 0xFF, 0xFF, 0xCC));
        }
    }
}
