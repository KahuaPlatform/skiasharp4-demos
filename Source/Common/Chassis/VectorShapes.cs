using System;
using SkiaSharp;

namespace Arcade.Common.Chassis;

// Helpers for building and drawing the vector silhouettes the neon games are
// made of. Generalises the path idioms scattered through the renderers (e.g.
// Pohaku's BuildPath for the ship/life icons and its translate-rotate-stroke
// asteroid loop) into three reusable pieces:
//
//   Poly   — bake a point list into a cached SKPath (build once, draw many).
//   Blob   — a jittered polygon for organic shapes (asteroids, rocks, debris).
//   DrawAt — place a path in the world: Save/Translate/Rotate/Scale, neon-stroke,
//            Restore.
//
// All path construction uses SKPathBuilder (the SkiaSharp 4 idiom) rather than
// the deprecated SKPath.MoveTo/LineTo instance API, matching Pohaku's Renderer.
public static class VectorShapes
{
    // Bake a list of points into an SKPath. When close is true the path is a
    // closed polygon (last point joins back to the first); otherwise it is an
    // open polyline. Build these once at startup and reuse the returned path on
    // every frame — that's the whole point of caching the geometry.
    //
    // The returned SKPath is detached and owned by the caller (it is not pooled
    // or shared), so the caller may keep it for the lifetime of the game.
    public static SKPath Poly(ReadOnlySpan<SKPoint> points, bool close)
    {
        using var builder = new SKPathBuilder();
        builder.AddPoly(points, close);
        return builder.Detach();
    }

    // Build a closed, jittered polygon approximating a circle of the given
    // radius with verts vertices. Each vertex angle is evenly spaced, and its
    // radius is perturbed by up to +/- jitter (a fraction, e.g. 0.3 => +/-30%)
    // using the supplied Random, producing the lumpy "asteroid" silhouette.
    //
    // The supplied Random is the deterministic seed source — pass a per-entity
    // Random so a given rock always rebuilds to the same shape if needed. Jitter
    // is clamped to [0, 1) so the radius can never collapse to zero or invert.
    public static SKPath Blob(Random rng, float radius, int verts, float jitter)
    {
        if (verts < 3) verts = 3;                       // a polygon needs >= 3 sides
        jitter = Math.Clamp(jitter, 0f, 0.99f);

        Span<SKPoint> points = verts <= 64
            ? stackalloc SKPoint[verts]
            : new SKPoint[verts];

        float step = MathF.Tau / verts;
        for (int i = 0; i < verts; i++)
        {
            float angle = i * step;
            // Perturb radius within +/- jitter of the nominal radius.
            float r = radius * (1f + ((float)rng.NextDouble() * 2f - 1f) * jitter);
            points[i] = new SKPoint(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
        }

        return Poly(points, close: true);
    }

    // Draw a cached path at a world position with rotation (degrees) and uniform
    // scale, stroked with the shared neon halo+sharp passes. Mirrors the
    // Save/Translate/RotateDegrees/Scale + stroke + Restore idiom the renderers
    // hand-roll per entity — the canvas transform is fully restored on exit, so
    // the path's own coordinates stay local/origin-centred.
    //
    // rotation is in degrees (matching SKCanvas.RotateDegrees); pass 0 for an
    // upright shape. A scale of 1 draws the path at its authored size.
    public static void DrawAt(SKCanvas c, SKPath path, float x, float y,
                              float rotation, float scale, SKColor color)
    {
        c.Save();
        c.Translate(x, y);
        if (rotation != 0f) c.RotateDegrees(rotation);
        if (scale != 1f) c.Scale(scale);
        NeonDraw.Stroke(c, path, color);
        c.Restore();
    }
}
