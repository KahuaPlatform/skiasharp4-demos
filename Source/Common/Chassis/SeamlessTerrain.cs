using System;
using SkiaSharp;

namespace Arcade.Common.Chassis;

// A periodic 1-D height field for horizontally-scrolling, X-wrapping worlds
// (Kia'i's planet surface). The whole point of this piece is a terrain whose
// seam is *mathematically invisible*: because every component sinusoid has a
// period that is an exact integer divisor of the world width, the height and
// the slope at x == 0 are identical to those at x == WorldWidth. There is no
// special-casing at the seam — you sample HeightAt(worldX) for any x (the input
// is wrapped first) and the silhouette is continuous, on both sides of the loop.
//
// How the periodicity works: a term cos(2*pi * harmonic * x / WorldWidth) has
// period WorldWidth / harmonic, so it completes exactly `harmonic` whole cycles
// across the world. Summing several such terms (with integer harmonics like
// 3, 7, 13, 23) gives a function that is genuinely periodic with period
// WorldWidth — f(x) == f(x + WorldWidth) and f'(x) == f'(x + WorldWidth) — so
// the terrain tiles around the torus with neither a height step nor a kink.
//
// Coordinate convention matches the rest of the chassis: world Y grows downward
// (screen space), so a *larger* HeightAt means the ground is *lower* on screen.
// BaselineY is where the average terrain sits; Amplitude is the peak-to-trough
// half-range about that baseline.
public sealed class SeamlessTerrain
{
    // One sinusoidal component of the height field. Period == WorldWidth /
    // Harmonic, so Harmonic must be a positive integer for seamlessness.
    readonly struct Harmonic
    {
        public readonly float K;       // angular frequency: 2*pi*harmonic / WorldWidth
        public readonly float Amp;      // contribution amplitude (world units)
        public readonly float Phase;    // phase offset (radians) — does not affect periodicity
        public Harmonic(float k, float amp, float phase) { K = k; Amp = amp; Phase = phase; }
    }

    // The torus circumference along X. Heights repeat with this period.
    public float WorldWidth { get; }

    // The world Y the terrain oscillates about (mean ground line). Larger == lower.
    public float BaselineY { get; }

    // Peak-to-trough half range about BaselineY, i.e. the terrain spans roughly
    // [BaselineY - Amplitude, BaselineY + Amplitude].
    public float Amplitude { get; }

    readonly Harmonic[] _harmonics;
    readonly float _ampScale;   // normaliser so the summed terms hit ~+/-Amplitude

    // A few big rolling shapes (3, 7) textured by finer ripples (13, 23). These
    // are integer cycle-counts across the world, which is what makes the seam
    // invisible. Held as a static array (not stackalloc) so it can default the
    // ReadOnlySpan parameter without escaping a local stack buffer.
    static readonly int[] DefaultHarmonics = { 3, 7, 13, 23 };

    // Build a seamless terrain over [0, worldWidth). `harmonics` are the integer
    // cycle-counts of each component across the world — they MUST be integers for
    // the seam to be invisible (the constructor rounds defensively). Each harmonic
    // gets a deterministic per-term amplitude weight and phase from `rng` (pass a
    // seeded Random for a reproducible planet). amplitude is the peak half-range
    // about baselineY.
    //
    // Sensible defaults: harmonics = {3, 7, 13, 23} gives a rolling landscape with
    // a few big hills (the 3 and 7 terms) textured by finer ripples (13, 23).
    public SeamlessTerrain(float worldWidth, float baselineY, float amplitude,
                           Random rng, ReadOnlySpan<int> harmonics = default)
    {
        WorldWidth = worldWidth <= 0f ? 1f : worldWidth;
        BaselineY  = baselineY;
        Amplitude  = amplitude;

        if (harmonics.IsEmpty) harmonics = DefaultHarmonics;

        _harmonics = new Harmonic[harmonics.Length];
        float ampSum = 0f;
        for (int i = 0; i < harmonics.Length; i++)
        {
            // Defensive: force a positive integer harmonic. Period = WorldWidth / h.
            int h = Math.Max(1, harmonics[i]);
            float k = MathF.Tau * h / WorldWidth;
            // Lower harmonics carry more amplitude (1/h falloff) so the big rolling
            // shapes dominate and the high harmonics only add fine texture.
            float weight = 1f / h;
            float amp = weight * (0.7f + (float)rng.NextDouble() * 0.6f);
            float phase = (float)(rng.NextDouble() * Math.PI * 2.0);
            _harmonics[i] = new Harmonic(k, amp, phase);
            ampSum += amp;
        }
        // Normalise so the worst-case sum of |amp| maps to exactly Amplitude.
        _ampScale = ampSum > 0f ? Amplitude / ampSum : 0f;
    }

    // The terrain Y (world units, larger == lower on screen) at any world X. The
    // input is wrapped into [0, WorldWidth) first, so callers may pass negative or
    // out-of-range X (e.g. a seam-replica screen walk) and still get the right,
    // continuous value. This is the workhorse the renderer and collision call.
    public float HeightAt(float worldX)
    {
        float x = Camera2D.Wrap(worldX, WorldWidth);
        float sum = 0f;
        for (int i = 0; i < _harmonics.Length; i++)
        {
            ref readonly Harmonic h = ref _harmonics[i];
            sum += h.Amp * MathF.Cos(h.K * x + h.Phase);
        }
        return BaselineY + sum * _ampScale;
    }

    // The terrain slope dY/dX at a world X — the analytic derivative of HeightAt,
    // so it is exact (no finite-difference noise) and equally seamless. Used for
    // surface-aligned effects and AI that wants to know how steep the ground is.
    public float SlopeAt(float worldX)
    {
        float x = Camera2D.Wrap(worldX, WorldWidth);
        float sum = 0f;
        for (int i = 0; i < _harmonics.Length; i++)
        {
            ref readonly Harmonic h = ref _harmonics[i];
            // d/dx [amp*cos(k*x + p)] = -amp*k*sin(k*x + p)
            sum += -h.Amp * h.K * MathF.Sin(h.K * x + h.Phase);
        }
        return sum * _ampScale;
    }

    // True if [worldX - halfSpan, worldX + halfSpan] is "flat enough" — the
    // terrain height varies by less than maxRise across the span. Used to pick
    // believable standing spots for humanoids (no one perches on a cliff). Cheap:
    // it samples the two ends plus the centre rather than integrating slope.
    public bool IsFlat(float worldX, float halfSpan, float maxRise)
    {
        float a = HeightAt(worldX - halfSpan);
        float b = HeightAt(worldX);
        float c = HeightAt(worldX + halfSpan);
        float lo = MathF.Min(a, MathF.Min(b, c));
        float hi = MathF.Max(a, MathF.Max(b, c));
        return hi - lo <= maxRise;
    }

    // Emit the visible terrain silhouette as a screen-space polyline path. Walks
    // *screen* X across the viewport in `stepPx` steps, converts each to world X
    // via the camera, samples HeightAt (wrapped + periodic, so the seam needs no
    // special handling), and converts the world height back to a screen Y. The
    // returned SKPath is an OPEN polyline along the surface; the caller can stroke
    // it directly or close it down to the canvas bottom to fill the ground.
    //
    // Because we iterate screen pixels and let HeightAt wrap the world X, the
    // silhouette is automatically continuous across the wrap seam — there is no
    // visible join even when the seam sits in the middle of the viewport.
    public SKPath BuildVisibleStrip(Camera2D cam, float viewW, float stepPx = 8f)
    {
        if (stepPx < 1f) stepPx = 1f;
        var b = new SKPathBuilder();
        bool first = true;
        for (float sx = 0f; sx <= viewW + stepPx; sx += stepPx)
        {
            float worldX = cam.ToWorldX(sx);
            float worldY = HeightAt(worldX);
            float sy = cam.ToScreenY(worldY);
            if (first) { b.MoveTo(sx, sy); first = false; }
            else        b.LineTo(sx, sy);
        }
        return b.Detach();
    }
}
