using System.Numerics;
using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Particles drifting through a curl-of-Perlin-noise vector field. The
/// curl of a scalar field is divergence-free, so the particles never
/// pile up or disperse — they swirl like smoke. Animated by adding
/// time to the noise input.
///
/// We approximate Perlin via 2D value-noise with bilinear interpolation
/// (no precomputed permutation table), and the curl via central
/// differences. Cheap and good enough for visual flow.
/// </summary>
public sealed class CurlNoiseTile : ILiveTile
{
    const int ParticleCount = 360;
    const float NoiseScale = 1.6f;     // how zoomed in to the noise field
    const float Speed = 50f;
    const float TrailFade = 18;        // alpha of the per-frame fade rect (0..255)
    const float TimeWarp = 0.20f;      // how fast the field evolves

    struct Particle { public Vector2 Pos; public int ColorIdx; }
    readonly Particle[] _particles = new Particle[ParticleCount];
    SKBitmap? _canvas;
    int _canvasW, _canvasH;
    readonly Lock _lock = new();
    readonly Thread _worker;
    volatile bool _alive = true;
    readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

    public string Caption => "CurlNoise";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(4, 6, 18),
        new SKColor(120, 200, 255),
        new SKColor(255, 130, 220),
        new SKColor(255, 230, 150));

    public CurlNoiseTile()
    {
        var rng = new Random(31);
        for (int i = 0; i < ParticleCount; i++)
        {
            _particles[i].Pos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
            _particles[i].ColorIdx = 1 + rng.Next(Palette.Length - 1);
        }

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "CurlNoise-Worker",
            Priority = ThreadPriority.BelowNormal,
        };
        _worker.Start();
    }

    void WorkerLoop()
    {
        float last = (float)_sw.Elapsed.TotalSeconds;
        while (_alive)
        {
            try
            {
                float now = (float)_sw.Elapsed.TotalSeconds;
                float dt = MathF.Min(0.05f, now - last);
                last = now;

                lock (_lock)
                {
                    for (int i = 0; i < ParticleCount; i++)
                    {
                        ref var p = ref _particles[i];
                        const float h = 0.01f;
                        float n_yp = Noise(p.Pos.X, p.Pos.Y + h, now * TimeWarp);
                        float n_yn = Noise(p.Pos.X, p.Pos.Y - h, now * TimeWarp);
                        float n_xp = Noise(p.Pos.X + h, p.Pos.Y, now * TimeWarp);
                        float n_xn = Noise(p.Pos.X - h, p.Pos.Y, now * TimeWarp);

                        var velocity = new Vector2(
                            (n_yp - n_yn) / (2f * h),
                            -(n_xp - n_xn) / (2f * h));
                        // Use a fixed scale factor — we don't know canvas dims here, but
                        // the speed-to-canvas ratio is computed against a typical edge.
                        p.Pos += velocity * (Speed / 256f) * dt;

                        if (p.Pos.X < 0) p.Pos.X += 1f; else if (p.Pos.X > 1f) p.Pos.X -= 1f;
                        if (p.Pos.Y < 0) p.Pos.Y += 1f; else if (p.Pos.Y > 1f) p.Pos.Y -= 1f;
                    }
                }

                Thread.Sleep(16); // ~60 Hz particle updates
            }
            catch { Thread.Sleep(50); }
        }
    }

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        // (Re)create the offscreen accumulator if the tile size changes.
        int targetW = (int)MathF.Ceiling(dest.Width);
        int targetH = (int)MathF.Ceiling(dest.Height);
        if (_canvas is null || _canvasW != targetW || _canvasH != targetH)
        {
            _canvas?.Dispose();
            _canvas = new SKBitmap(new SKImageInfo(targetW, targetH, SKColorType.Rgba8888, SKAlphaType.Premul));
            _canvasW = targetW;
            _canvasH = targetH;
            using var c = new SKCanvas(_canvas);
            c.Clear(Palette[0]);
        }

        // Snapshot particle positions under lock; the worker updates them on its own clock.
        int colors = Palette.Length;
        var buckets = new List<SKPoint>[colors];
        for (int i = 0; i < colors; i++) buckets[i] = new List<SKPoint>(ParticleCount / colors + 4);

        lock (_lock)
        {
            for (int i = 0; i < ParticleCount; i++)
            {
                var p = _particles[i];
                buckets[p.ColorIdx].Add(new SKPoint(p.Pos.X * _canvasW, p.Pos.Y * _canvasH));
            }
        }

        using (var workCanvas = new SKCanvas(_canvas))
        {
            // Trail fade: dark overlay each frame.
            using (var fade = new SKPaint { Color = Palette[0].WithAlpha((byte)TrailFade) })
            {
                workCanvas.DrawRect(0, 0, _canvasW, _canvasH, fade);
            }

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeWidth = 2.8f,
            };
            for (int i = 0; i < colors; i++)
            {
                if (buckets[i].Count == 0) continue;
                paint.Color = Palette[i].WithAlpha(220);
                workCanvas.DrawPoints(SKPointMode.Points, buckets[i].ToArray(), paint);
            }
        }

        using var img = SKImage.FromBitmap(_canvas);
        canvas.DrawImage(img, dest);
    }

    // 2D value noise with bilinear interpolation, slowly evolving in z.
    // Quick + acceptable for visual flow; not Perlin-quality but cheap.
    static float Noise(float x, float y, float z)
    {
        x *= NoiseScale;
        y *= NoiseScale;
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;

        float n00 = Hash(xi, yi, z);
        float n10 = Hash(xi + 1, yi, z);
        float n01 = Hash(xi, yi + 1, z);
        float n11 = Hash(xi + 1, yi + 1, z);

        // Smoothstep weights so the field doesn't have visible grid creases.
        float sx = fx * fx * (3f - 2f * fx);
        float sy = fy * fy * (3f - 2f * fy);

        float ix0 = n00 + (n10 - n00) * sx;
        float ix1 = n01 + (n11 - n01) * sx;
        return ix0 + (ix1 - ix0) * sy;
    }

    static float Hash(int x, int y, float z)
    {
        // Slide the integer hash by z to animate; sin gives smooth temporal drift.
        float s = MathF.Sin((x * 127.1f + y * 311.7f) + z * 4.0f) * 43758.5453f;
        return (s - MathF.Floor(s)) * 2f - 1f;
    }
}
