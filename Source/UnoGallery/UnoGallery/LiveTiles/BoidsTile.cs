#pragma warning disable CS0618 // SKPath mutable API is obsolete in SkiaSharp 4 — accept warning for cross-version compat
using System.Numerics;
using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Flocking boids — Reynolds' classic separation / alignment / cohesion
/// with a toroidal world wrap so the flock keeps moving. Drawn as small
/// arrowheads pointing along velocity. Lightweight: 50 boids, O(N²) per
/// frame, well under a millisecond on any modern CPU.
/// </summary>
public sealed class BoidsTile : ILiveTile
{
    const int Count = 50;
    const float WorldSize = 200f;
    const float MaxSpeed = 50f;
    const float SeparationRange = 14f;
    const float NeighborRange = 35f;

    struct Boid
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public int ColorIdx;
    }

    readonly Boid[] _boids = new Boid[Count];
    readonly Lock _lock = new();
    readonly Thread? _worker;
    volatile bool _alive = true;

    // WASM has no Thread support — step inline from Draw using a dt derived from t.
    readonly bool _runInline;
    float _inlineLast;

    public string Caption => "Boids";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(8, 12, 24),
        new SKColor(255, 110, 110),
        new SKColor(120, 230, 200),
        new SKColor(255, 220, 120));

    public BoidsTile()
    {
        var rng = new Random(5);
        for (int i = 0; i < Count; i++)
        {
            float a = (float)(rng.NextDouble() * Math.Tau);
            _boids[i].Pos = new Vector2((float)rng.NextDouble() * WorldSize, (float)rng.NextDouble() * WorldSize);
            _boids[i].Vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 25f;
            _boids[i].ColorIdx = 1 + rng.Next(Palette.Length - 1);
        }

        _runInline = OperatingSystem.IsBrowser();
        if (!_runInline)
        {
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "Boids-Worker",
                Priority = ThreadPriority.BelowNormal,
            };
            _worker.Start();
        }
    }

    void WorkerLoop()
    {
        const float TargetDt = 1f / 60f;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float last = (float)sw.Elapsed.TotalSeconds;
        while (_alive)
        {
            try
            {
                float now = (float)sw.Elapsed.TotalSeconds;
                float dt = MathF.Min(0.05f, now - last);
                last = now;
                lock (_lock) Step(dt);
                Thread.Sleep((int)(TargetDt * 1000f));
            }
            catch { Thread.Sleep(50); }
        }
    }

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        if (_runInline)
        {
            float dt = MathF.Min(0.05f, MathF.Max(0f, t - _inlineLast));
            _inlineLast = t;
            lock (_lock) Step(dt);
        }

        using var bgPaint = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bgPaint);

        float sx = dest.Width / WorldSize;
        float sy = dest.Height / WorldSize;
        float arrowR = MathF.Max(2f, MathF.Min(dest.Width, dest.Height) / 70f);

        // Build one path per palette colour, batch into a few DrawPath calls.
        int colors = Palette.Length;
        var paths = new SKPath[colors];
        for (int i = 0; i < colors; i++) paths[i] = new SKPath();

        // Snapshot positions under lock so a worker mutation can't tear arrow geometry.
        Span<(Vector2 pos, Vector2 vel, int color)> snap = stackalloc (Vector2, Vector2, int)[Count];
        lock (_lock)
        {
            for (int i = 0; i < Count; i++)
                snap[i] = (_boids[i].Pos, _boids[i].Vel, _boids[i].ColorIdx);
        }

        for (int i = 0; i < Count; i++)
        {
            var (pos, vel, color) = snap[i];
            float px = dest.Left + pos.X * sx;
            float py = dest.Top + pos.Y * sy;
            float angle = MathF.Atan2(vel.Y, vel.X);
            float c = MathF.Cos(angle), s = MathF.Sin(angle);

            var p = paths[color];
            p.MoveTo(px + c * arrowR, py + s * arrowR);
            p.LineTo(px - c * arrowR * 0.7f + s * arrowR * 0.5f, py - s * arrowR * 0.7f - c * arrowR * 0.5f);
            p.LineTo(px - c * arrowR * 0.7f - s * arrowR * 0.5f, py - s * arrowR * 0.7f + c * arrowR * 0.5f);
            p.Close();
        }

        using var paint = new SKPaint { IsAntialias = true };
        for (int i = 0; i < colors; i++)
        {
            if (paths[i].IsEmpty) { paths[i].Dispose(); continue; }
            paint.Color = Palette[i];
            canvas.DrawPath(paths[i], paint);
            paths[i].Dispose();
        }
    }

    void Step(float dt)
    {
        for (int i = 0; i < Count; i++)
        {
            Vector2 sep = Vector2.Zero;
            Vector2 alignSum = Vector2.Zero;
            Vector2 cohSum = Vector2.Zero;
            int neighbors = 0;

            for (int j = 0; j < Count; j++)
            {
                if (i == j) continue;
                Vector2 d = _boids[j].Pos - _boids[i].Pos;
                // Shortest distance on a torus
                if (d.X > WorldSize * 0.5f) d.X -= WorldSize;
                else if (d.X < -WorldSize * 0.5f) d.X += WorldSize;
                if (d.Y > WorldSize * 0.5f) d.Y -= WorldSize;
                else if (d.Y < -WorldSize * 0.5f) d.Y += WorldSize;

                float dist = d.Length();
                if (dist < 0.001f) continue;

                if (dist < SeparationRange)
                    sep -= d / dist;

                if (dist < NeighborRange)
                {
                    alignSum += _boids[j].Vel;
                    cohSum += d;
                    neighbors++;
                }
            }

            Vector2 force = sep * 80f;
            if (neighbors > 0)
            {
                force += (alignSum / neighbors - _boids[i].Vel) * 1.2f;
                force += (cohSum / neighbors) * 0.6f;
            }

            _boids[i].Vel += force * dt;
            float sp = _boids[i].Vel.Length();
            if (sp > MaxSpeed) _boids[i].Vel = _boids[i].Vel / sp * MaxSpeed;
            else if (sp < 5f && sp > 0.001f) _boids[i].Vel = _boids[i].Vel / sp * 5f;

            _boids[i].Pos += _boids[i].Vel * dt;
            // Toroidal wrap
            if (_boids[i].Pos.X < 0) _boids[i].Pos.X += WorldSize;
            else if (_boids[i].Pos.X >= WorldSize) _boids[i].Pos.X -= WorldSize;
            if (_boids[i].Pos.Y < 0) _boids[i].Pos.Y += WorldSize;
            else if (_boids[i].Pos.Y >= WorldSize) _boids[i].Pos.Y -= WorldSize;
        }
    }
}
