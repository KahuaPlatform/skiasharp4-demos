using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Cellular falling-sand simulation. Each cell is either empty or one
/// of a few "sand colours"; on every step, each non-empty cell tries to
/// fall straight down, then down-left, then down-right. New sand
/// spawns at the top from a slowly oscillating emitter, and the pile
/// is periodically nudged (rows pruned from the bottom) so it never
/// fills the tile permanently.
/// </summary>
public sealed class FallingSandTile : ILiveTile
{
    const int W = 64;
    const int H = 80;
    const float StepInterval = 0.03f;  // ~33 steps/s for visibly falling motion
    static readonly SKSamplingOptions Sampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    // 0 = empty, otherwise palette index (1..3)
    byte[] _grid = new byte[W * H];
    byte[] _next = new byte[W * H];
    readonly SKBitmap _bmp = new(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul));
    readonly Random _rng = new(91);
    float _lastStep = float.NegativeInfinity;
    int _stepCount;

    public string Caption => "Sand";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(8, 6, 14),
        new SKColor(255, 180, 60),
        new SKColor(255, 110, 130),
        new SKColor(120, 220, 255));

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        while (t - _lastStep > StepInterval)
        {
            Step();
            _lastStep += StepInterval;
            if (_lastStep < t - 0.5f) _lastStep = t; // resync after a stall
        }

        // Render grid as small bitmap, scale up with nearest-neighbour for the
        // pixelated "falling-pixel" aesthetic.
        unsafe
        {
            uint* pixels = (uint*)_bmp.GetPixels();
            for (int i = 0; i < _grid.Length; i++)
            {
                byte v = _grid[i];
                pixels[i] = v == 0 ? PackColor(Palette[0]) : PackColor(Palette[v]);
            }
        }

        using var img = SKImage.FromBitmap(_bmp);
        canvas.DrawImage(img, dest, Sampling);
    }

    void Step()
    {
        _stepCount++;

        // Spawn new sand at the top — emitter sweeps left/right slowly.
        float emitterPhase = _stepCount * 0.04f;
        int emitterX = W / 2 + (int)(MathF.Sin(emitterPhase) * (W * 0.30f));
        for (int dx = -2; dx <= 2; dx++)
        {
            int x = emitterX + dx;
            if ((uint)x >= W) continue;
            if (_rng.NextDouble() < 0.55) _grid[x] = (byte)(1 + _rng.Next(Palette.Length - 1));
        }

        // Step physics: copy current grid into _next as the destination, then
        // for each cell in current, decide its new resting position.
        Array.Clear(_next);

        // Walk bottom-up so each row's choices don't displace itself.
        for (int y = H - 1; y >= 0; y--)
        {
            for (int x = 0; x < W; x++)
            {
                byte cell = _grid[y * W + x];
                if (cell == 0) continue;

                // Already settled on the bottom row → stay.
                if (y == H - 1)
                {
                    if (_next[y * W + x] == 0) _next[y * W + x] = cell;
                    else PutNearby(cell, x, y);
                    continue;
                }

                int below = (y + 1) * W + x;
                if (_grid[below] == 0 && _next[below] == 0)
                {
                    _next[below] = cell;
                    continue;
                }

                // Try diagonals — randomise order so piles don't drift sideways.
                int first = _rng.NextDouble() < 0.5 ? -1 : 1;
                int second = -first;
                if (TryDiagonal(cell, x, y, first)) continue;
                if (TryDiagonal(cell, x, y, second)) continue;

                // Couldn't fall — stay put.
                if (_next[y * W + x] == 0) _next[y * W + x] = cell;
                else PutNearby(cell, x, y);
            }
        }

        // Settle: occasionally nibble bottom rows so the tile never clogs.
        if (_stepCount % 200 == 0)
        {
            for (int x = 0; x < W; x++) _next[(H - 1) * W + x] = 0;
        }

        (_grid, _next) = (_next, _grid);
    }

    bool TryDiagonal(byte cell, int x, int y, int dx)
    {
        int nx = x + dx;
        if ((uint)nx >= W) return false;
        int target = (y + 1) * W + nx;
        if (_grid[target] == 0 && _next[target] == 0)
        {
            _next[target] = cell;
            return true;
        }
        return false;
    }

    void PutNearby(byte cell, int x, int y)
    {
        // Best-effort: try the cell itself, then immediate neighbours. Avoids
        // losing sand entirely in pathological collisions.
        if (_next[y * W + x] == 0) { _next[y * W + x] = cell; return; }
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((uint)nx >= W || (uint)ny >= H) continue;
                int idx = ny * W + nx;
                if (_next[idx] == 0) { _next[idx] = cell; return; }
            }
    }

    static uint PackColor(SKColor c)
        => 0xFF000000u | ((uint)c.Blue << 16) | ((uint)c.Green << 8) | c.Red;
}
