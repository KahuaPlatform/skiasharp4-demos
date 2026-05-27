using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Gray-Scott reaction-diffusion: two species A and B evolving on a 2D
/// grid by the coupled PDE
///   ∂A/∂t = Da ∇²A − A·B² + f·(1 − A)
///   ∂B/∂t = Db ∇²B + A·B² − (f + k)·B
/// with f and k chosen for the "mitosis" regime — soft expanding spots
/// that split when they collide. Eight substeps per frame keep the
/// simulation evolving fast enough to read as live.
///
/// Renders the B concentration through a palette gradient into a small
/// bitmap, upsampled to the tile.
/// </summary>
public sealed class ReactionDiffusionTile : ILiveTile
{
    const int W = 96;
    const int H = 96;
    const int SubstepsPerFrame = 8;
    const float Da = 1.0f;
    const float Db = 0.5f;
    const float Feed = 0.0545f;
    const float Kill = 0.062f;

    static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    float[] _a = new float[W * H];
    float[] _b = new float[W * H];
    float[] _a2 = new float[W * H];
    float[] _b2 = new float[W * H];
    readonly SKBitmap _bmp = new(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul));
    int _stepsTotal;
    readonly Lock _lock = new();
    readonly Thread _worker;
    volatile bool _alive = true;

    public string Caption => "Reaction";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(6, 14, 24),
        new SKColor(40, 110, 180),
        new SKColor(255, 180, 80),
        new SKColor(255, 250, 220));

    public ReactionDiffusionTile()
    {
        Seed();
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "RD-Worker",
            Priority = ThreadPriority.BelowNormal,
        };
        _worker.Start();
    }

    void WorkerLoop()
    {
        // Step continuously; sleep just enough between bursts to let other
        // worker threads breathe and the UI thread not contend.
        while (_alive)
        {
            try
            {
                lock (_lock)
                {
                    for (int s = 0; s < SubstepsPerFrame; s++) Step();
                    if (_stepsTotal > 12000)
                    {
                        Seed();
                        _stepsTotal = 0;
                    }
                }
                Thread.Sleep(8); // ~120 batches/sec ceiling
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RD worker] {ex.Message}");
                Thread.Sleep(100);
            }
        }
    }

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        lock (_lock)
        {
            // Render B concentration to bitmap under lock so the worker can't
            // mutate _b mid-read. Snapshot is short — a 9216-element scan.
            unsafe
            {
                uint* pixels = (uint*)_bmp.GetPixels();
                for (int i = 0; i < _b.Length; i++)
                {
                    float v = Math.Clamp(_b[i] * 3.0f, 0f, 1f);
                    var col = LerpPalette(v);
                    pixels[i] = ((uint)0xFFu << 24) | ((uint)col.Blue << 16) | ((uint)col.Green << 8) | col.Red;
                }
            }
        }

        using var img = SKImage.FromBitmap(_bmp);
        canvas.DrawImage(img, dest, Sampling);
    }

    void Seed()
    {
        Array.Fill(_a, 1f);
        Array.Fill(_b, 0f);

        // Drop a few small B blobs to kick the system off — single-seed Gray-Scott
        // grows beautifully but takes a while to reach the whole canvas.
        var rng = new Random(_stepsTotal);
        int seedCount = 3 + rng.Next(4);
        for (int n = 0; n < seedCount; n++)
        {
            int cx = 10 + rng.Next(W - 20);
            int cy = 10 + rng.Next(H - 20);
            int r = 3 + rng.Next(4);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r * r) continue;
                    int x = cx + dx, y = cy + dy;
                    if ((uint)x < W && (uint)y < H)
                    {
                        _a[y * W + x] = 0.5f;
                        _b[y * W + x] = 0.25f;
                    }
                }
        }
    }

    void Step()
    {
        _stepsTotal++;
        // Discrete Laplacian via the 9-point stencil (more isotropic than 5-point).
        for (int y = 0; y < H; y++)
        {
            int ym = (y - 1 + H) % H;
            int yp = (y + 1) % H;
            for (int x = 0; x < W; x++)
            {
                int xm = (x - 1 + W) % W;
                int xp = (x + 1) % W;

                int i = y * W + x;
                float a = _a[i];
                float b = _b[i];

                float lapA =
                      _a[ym * W + xm] * 0.05f + _a[ym * W + x] * 0.20f + _a[ym * W + xp] * 0.05f
                    + _a[y  * W + xm] * 0.20f + a              * -1.0f + _a[y  * W + xp] * 0.20f
                    + _a[yp * W + xm] * 0.05f + _a[yp * W + x] * 0.20f + _a[yp * W + xp] * 0.05f;

                float lapB =
                      _b[ym * W + xm] * 0.05f + _b[ym * W + x] * 0.20f + _b[ym * W + xp] * 0.05f
                    + _b[y  * W + xm] * 0.20f + b              * -1.0f + _b[y  * W + xp] * 0.20f
                    + _b[yp * W + xm] * 0.05f + _b[yp * W + x] * 0.20f + _b[yp * W + xp] * 0.05f;

                float abb = a * b * b;
                _a2[i] = a + (Da * lapA - abb + Feed * (1f - a));
                _b2[i] = b + (Db * lapB + abb - (Feed + Kill) * b);
            }
        }

        (_a, _a2) = (_a2, _a);
        (_b, _b2) = (_b2, _b);
    }

    SKColor LerpPalette(float t)
    {
        t = Math.Clamp(t, 0f, 0.999f);
        float scaled = t * (Palette.Length - 1);
        int idx = (int)scaled;
        float f = scaled - idx;
        var a = Palette[idx];
        var b = Palette[Math.Min(idx + 1, Palette.Length - 1)];
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * f),
            (byte)(a.Green + (b.Green - a.Green) * f),
            (byte)(a.Blue + (b.Blue - a.Blue) * f));
    }
}
