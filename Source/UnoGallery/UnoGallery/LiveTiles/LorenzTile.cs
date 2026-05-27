#pragma warning disable CS0618 // SKPath obsolete in v4 — accept warning, runs on both versions
using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// The Lorenz attractor — classic chaotic 3D trajectory:
///   dx/dt = σ (y − x)
///   dy/dt = x (ρ − z) − y
///   dz/dt = x y − β z
/// with σ=10, ρ=28, β=8/3. Integrates a fixed number of substeps each frame
/// so the simulation runs at a consistent rate independent of vsync.
///
/// Renders as a fading trail of the most recent <see cref="TrailLength"/>
/// 3D points, projected to 2D with a slowly rotating view so the iconic
/// butterfly shape tumbles in space. Always alive, never repeats.
/// </summary>
public sealed class LorenzTile : ILiveTile
{
    const int TrailLength = 1400;
    const float Sigma = 10f;
    const float Rho = 28f;
    const float Beta = 8f / 3f;
    const float StepSize = 0.0035f;    // simulation dt per substep
    const int StepsPerFrame = 8;

    readonly float[] _xs = new float[TrailLength];
    readonly float[] _ys = new float[TrailLength];
    readonly float[] _zs = new float[TrailLength];
    int _head;                          // newest point index
    int _filled;                        // number of valid points in the ring

    float _x = 1f, _y = 1f, _z = 1f;

    public string Caption => "Lorenz";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(6, 8, 22),
        new SKColor(80, 130, 255),
        new SKColor(255, 130, 200),
        new SKColor(255, 230, 150));

    public LorenzTile()
    {
        // Spin up the trail so the very first frame isn't sparse.
        for (int i = 0; i < TrailLength; i++) Advance(1);
    }

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        Advance(StepsPerFrame);

        using var bg = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bg);

        // The attractor lives roughly in x:[-25,25], y:[-30,30], z:[0,50].
        // We project (x, z) and apply a slow Y-axis rotation by mixing x and y.
        float yaw = t * 0.20f;
        float cy = MathF.Cos(yaw), sy = MathF.Sin(yaw);

        float cx = dest.MidX;
        float cyScreen = dest.MidY;
        float scale = MathF.Min(dest.Width, dest.Height) / 60f; // fits attractor bounds

        // Bucket the trail into age bands. One path + one paint per band
        // so 1400 segments draw in ~10 GPU calls instead of 1400 paint allocations.
        const int Buckets = 10;
        var paths = new SKPath[Buckets];
        for (int i = 0; i < Buckets; i++) paths[i] = new SKPath();

        int n = _filled;
        for (int i = 0; i < n - 1; i++)
        {
            int idxA = (_head - n + 1 + i + TrailLength) % TrailLength;
            int idxB = (idxA + 1) % TrailLength;

            float xa = _xs[idxA] * cy - _ys[idxA] * sy;
            float xb = _xs[idxB] * cy - _ys[idxB] * sy;
            float za = _zs[idxA] - 25f;
            float zb = _zs[idxB] - 25f;

            float ax = cx + xa * scale;
            float ay = cyScreen - za * scale;
            float bx = cx + xb * scale;
            float by = cyScreen - zb * scale;

            float age = i / (float)(n - 1);
            int bucket = Math.Min(Buckets - 1, (int)(age * Buckets));
            paths[bucket].MoveTo(ax, ay);
            paths[bucket].LineTo(bx, by);
        }

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        };
        for (int i = 0; i < Buckets; i++)
        {
            if (paths[i].IsEmpty) { paths[i].Dispose(); continue; }
            float age = (i + 0.5f) / Buckets;
            float alpha = age * age;
            byte a = (byte)Math.Clamp(alpha * 255f, 0f, 255f);
            paint.Color = LerpPalette(age).WithAlpha(a);
            paint.StrokeWidth = 0.8f + age * 1.6f;
            canvas.DrawPath(paths[i], paint);
            paths[i].Dispose();
        }
    }

    void Advance(int steps)
    {
        for (int s = 0; s < steps; s++)
        {
            float dx = Sigma * (_y - _x);
            float dy = _x * (Rho - _z) - _y;
            float dz = _x * _y - Beta * _z;

            _x += dx * StepSize;
            _y += dy * StepSize;
            _z += dz * StepSize;

            _head = (_head + 1) % TrailLength;
            _xs[_head] = _x;
            _ys[_head] = _y;
            _zs[_head] = _z;
            if (_filled < TrailLength) _filled++;
        }
    }

    SKColor LerpPalette(float t)
    {
        t = Math.Clamp(t, 0f, 0.999f);
        int span = Palette.Length - 1;
        float scaled = t * (span - 1);
        int idx = 1 + (int)scaled;
        float f = scaled - (int)scaled;
        var a = Palette[idx];
        var b = Palette[Math.Min(idx + 1, Palette.Length - 1)];
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * f),
            (byte)(a.Green + (b.Green - a.Green) * f),
            (byte)(a.Blue + (b.Blue - a.Blue) * f));
    }
}
