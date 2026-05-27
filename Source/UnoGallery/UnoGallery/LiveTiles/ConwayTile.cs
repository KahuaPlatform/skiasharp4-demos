using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Conway's Game of Life on a 56×56 toroidal grid stepped at ~12 Hz.
/// Renders by writing the grid into an <see cref="SKBitmap"/> and drawing
/// the bitmap with nearest-neighbour upsampling — one <c>DrawImage</c>
/// call per frame instead of one <c>DrawRect</c> per alive cell.
/// Reseeds after 600 generations so the visual stays alive when patterns
/// stagnate into still-lifes or oscillators.
/// </summary>
public sealed class ConwayTile : ILiveTile
{
    const int W = 56;
    const int H = 56;
    const float StepInterval = 0.083f;

    static readonly SKSamplingOptions Sampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    byte[] _cur = new byte[W * H];
    byte[] _next = new byte[W * H];
    readonly SKBitmap _bmp = new(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Premul));
    int _generation;
    readonly Lock _lock = new();
    readonly Thread? _worker;
    volatile bool _alive = true;

    // WASM has no Thread support — step inline from Draw, gated at StepInterval.
    readonly bool _runInline;
    float _inlineLastStep;

    uint _alivePack;
    uint _deadPack;

    public string Caption => "Conway";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(18, 22, 32),
        new SKColor(80, 130, 200),
        new SKColor(200, 230, 255),
        new SKColor(255, 255, 255));

    public ConwayTile()
    {
        _alivePack = PackColor(Palette[2]);
        _deadPack = PackColor(Palette[0]);
        var rng = new Random(11);
        for (int i = 0; i < _cur.Length; i++)
            _cur[i] = (byte)(rng.NextDouble() < 0.32 ? 1 : 0);

        _runInline = OperatingSystem.IsBrowser();
        if (!_runInline)
        {
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "Conway-Worker",
                Priority = ThreadPriority.BelowNormal,
            };
            _worker.Start();
        }
    }

    void WorkerLoop()
    {
        // Step at ~12 generations / sec on the worker, leaving the UI thread free.
        while (_alive)
        {
            try
            {
                lock (_lock) Step();
                Thread.Sleep((int)(StepInterval * 1000f));
            }
            catch { Thread.Sleep(100); }
        }
    }

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        if (_runInline && t - _inlineLastStep >= StepInterval)
        {
            _inlineLastStep = t;
            lock (_lock) Step();
        }

        lock (_lock)
        {
            unsafe
            {
                uint* pixels = (uint*)_bmp.GetPixels();
                for (int i = 0; i < _cur.Length; i++)
                    pixels[i] = _cur[i] != 0 ? _alivePack : _deadPack;
            }
        }

        using var img = SKImage.FromBitmap(_bmp);
        canvas.DrawImage(img, dest, Sampling);
    }

    void Step()
    {
        _generation++;
        if (_generation > 600)
        {
            var rng = new Random(_generation);
            for (int i = 0; i < _cur.Length; i++)
                _cur[i] = (byte)(rng.NextDouble() < 0.32 ? 1 : 0);
            _generation = 0;
            return;
        }

        for (int y = 0; y < H; y++)
        {
            int ym = (y - 1 + H) % H;
            int yp = (y + 1) % H;
            for (int x = 0; x < W; x++)
            {
                int xm = (x - 1 + W) % W;
                int xp = (x + 1) % W;
                int n = _cur[ym * W + xm] + _cur[ym * W + x] + _cur[ym * W + xp]
                      + _cur[y  * W + xm]                    + _cur[y  * W + xp]
                      + _cur[yp * W + xm] + _cur[yp * W + x] + _cur[yp * W + xp];

                bool alive = _cur[y * W + x] != 0;
                _next[y * W + x] = (byte)((alive && (n == 2 || n == 3)) || (!alive && n == 3) ? 1 : 0);
            }
        }

        (_cur, _next) = (_next, _cur);
    }

    static uint PackColor(SKColor c)
        => 0xFF000000u | ((uint)c.Blue << 16) | ((uint)c.Green << 8) | c.Red;
}
