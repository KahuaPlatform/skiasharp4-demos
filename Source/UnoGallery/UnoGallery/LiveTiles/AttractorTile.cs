using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Clifford strange attractor — the iteration
///   x' = sin(a·y) + c·cos(a·x)
///   y' = sin(b·x) + d·cos(b·y)
/// produces wildly different gorgeous patterns from tiny changes in (a,b,c,d).
/// Most random parameter tuples are uninteresting, so we curate a list of
/// known-good ones and cycle every <see cref="DwellSeconds"/>, easing
/// smoothly between them so the shape morphs rather than cutting.
///
/// Each frame we accumulate density into a small off-screen bitmap by
/// iterating ~8000 points, then upsample that to the tile. The density
/// map effectively averages a few seconds of iteration which gives the
/// soft luminous look these attractors are known for.
/// </summary>
public sealed class AttractorTile : ILiveTile
{
    const int DensityRes = 128;
    const int IterationsPerFrame = 8000;
    const float DensityDecay = 0.94f;    // per-frame multiplier so the trail fades smoothly
    const float DwellSeconds = 10f;
    const float MorphSeconds = 2.5f;

    // Curated Clifford (a, b, c, d) tuples that produce visually distinct,
    // dense, attractive shapes. Found by manual exploration.
    static readonly (float a, float b, float c, float d)[] Presets =
    {
        (-1.7f,  1.8f, -1.9f, -0.4f),
        (-1.4f,  1.6f,  1.0f,  0.7f),
        ( 1.5f, -1.8f,  1.6f,  0.9f),
        (-1.7f,  1.3f, -0.1f, -1.2f),
        (-1.8f, -2.0f, -0.5f, -0.9f),
        ( 1.7f,  1.7f,  0.6f,  1.2f),
        (-1.32f,-1.65f, 0.74f, 1.81f),
        (-1.24f,-1.25f, -1.81f,-1.91f),
        ( 1.6f, -0.6f, -1.2f,  1.6f),
        (-1.9f,  1.6f,  1.7f,  0.8f),
        ( 1.8f,  0.9f,  1.2f, -1.1f),
        (-1.5f, -1.8f,  1.6f,  0.9f),
    };

    readonly float[] _density = new float[DensityRes * DensityRes];
    readonly SKBitmap _bmp = new(new SKImageInfo(DensityRes, DensityRes, SKColorType.Rgba8888, SKAlphaType.Premul));
    static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);
    readonly Lock _lock = new();
    readonly Thread? _worker;
    volatile bool _alive = true;
    readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

    // WASM has no Thread support — step inline from Draw, using Draw's t for parameter morphing.
    readonly bool _runInline;

    float _x = 0.1f, _y = 0.1f;
    int _currentIdx;

    public string Caption => "Attractor";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(6, 4, 18),
        new SKColor(60, 80, 200),
        new SKColor(255, 130, 220),
        new SKColor(255, 230, 180));

    public AttractorTile()
    {
        _runInline = OperatingSystem.IsBrowser();
        if (!_runInline)
        {
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "Attractor-Worker",
                Priority = ThreadPriority.BelowNormal,
            };
            _worker.Start();
        }
    }

    void WorkerLoop()
    {
        while (_alive)
        {
            try
            {
                StepBatch((float)_sw.Elapsed.TotalSeconds);
                Thread.Sleep(16); // ~60 batches/sec
            }
            catch { Thread.Sleep(100); }
        }
    }

    void StepBatch(float t)
    {
        var (a, b, c, d) = CurrentParams(t);
        lock (_lock)
        {
            for (int i = 0; i < _density.Length; i++) _density[i] *= DensityDecay;

            const float Range = 2f;
            for (int i = 0; i < IterationsPerFrame; i++)
            {
                float nx = MathF.Sin(a * _y) + c * MathF.Cos(a * _x);
                float ny = MathF.Sin(b * _x) + d * MathF.Cos(b * _y);
                _x = nx; _y = ny;

                int dx = (int)((_x + Range) / (2f * Range) * DensityRes);
                int dy = (int)((_y + Range) / (2f * Range) * DensityRes);
                if ((uint)dx < DensityRes && (uint)dy < DensityRes)
                    _density[dy * DensityRes + dx] += 1f;
            }
        }
    }

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        if (_runInline) StepBatch(t);

        // Snapshot peak + color-map the density into the bitmap. Lock briefly.
        lock (_lock)
        {
            float peak = 0.001f;
            for (int i = 0; i < _density.Length; i++)
                if (_density[i] > peak) peak = _density[i];

            unsafe
            {
                uint* pixels = (uint*)_bmp.GetPixels();
                for (int i = 0; i < _density.Length; i++)
                {
                    float n = MathF.Min(_density[i] / peak, 1f);
                    float u = MathF.Pow(n, 0.4f);
                    var col = LerpPalette(u);
                    pixels[i] = PackRgba(col, (byte)(u * 255));
                }
            }
        }

        using var bg = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bg);
        using var img = SKImage.FromBitmap(_bmp);
        canvas.DrawImage(img, dest, Sampling);
    }

    (float a, float b, float c, float d) CurrentParams(float t)
    {
        float elapsed = t;
        float cycle = DwellSeconds;
        int idx = (int)(elapsed / cycle) % Presets.Length;
        if (idx != _currentIdx) _currentIdx = idx;

        var cur = Presets[idx];
        // Within the cycle, smoothly morph into the NEXT preset over the
        // final MorphSeconds so the shape never has a hard cut.
        float intoCycle = elapsed - idx * cycle;
        if (intoCycle > cycle - MorphSeconds)
        {
            var next = Presets[(idx + 1) % Presets.Length];
            float morph = (intoCycle - (cycle - MorphSeconds)) / MorphSeconds;
            morph = Smoothstep(morph);
            return (
                cur.a + (next.a - cur.a) * morph,
                cur.b + (next.b - cur.b) * morph,
                cur.c + (next.c - cur.c) * morph,
                cur.d + (next.d - cur.d) * morph);
        }
        return cur;
    }

    static float Smoothstep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
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

    static uint PackRgba(SKColor c, byte alpha)
        => ((uint)alpha << 24) | ((uint)c.Blue << 16) | ((uint)c.Green << 8) | c.Red;
}
