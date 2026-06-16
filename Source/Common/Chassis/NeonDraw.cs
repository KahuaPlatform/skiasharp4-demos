using SkiaSharp;

namespace Arcade.Common.Chassis;

/// <summary>
/// Convenience helpers for the chassis's signature neon-glow primitives. Each
/// paints a blurred wide "halo" pass followed by a crisp narrow "sharp" pass so
/// the result reads as a glowing line/shape over the dark playfield. All draws
/// reuse the shared <see cref="NeonPaints"/> pool (no per-call allocation).
/// </summary>
public static class NeonDraw
{
    /// <summary>Strokes an arbitrary <see cref="SKPath"/> in glowing neon <paramref name="color"/>.</summary>
    public static void Stroke(SKCanvas c, SKPath path, SKColor color)
    {
        // Halo first (semi-transparent so overlaps bloom), then the sharp core.
        NeonPaints.StrokeHalo.Color = color.WithAlpha(0xC0);
        c.DrawPath(path, NeonPaints.StrokeHalo);
        NeonPaints.StrokeSharp.Color = color;
        c.DrawPath(path, NeonPaints.StrokeSharp);
    }

    /// <summary>
    /// Draws a glowing neon line segment. <paramref name="halo"/>/<paramref name="sharp"/>
    /// override the default stroke widths for this one call; both are restored to
    /// the <see cref="NeonPaints"/> defaults before returning.
    /// </summary>
    /// <param name="halo">Width of the blurred glow pass.</param>
    /// <param name="sharp">Width of the crisp core pass.</param>
    public static void Line(SKCanvas c, float x1, float y1, float x2, float y2, SKColor color,
                            float halo = NeonPaints.DefaultStrokeHaloWidth,
                            float sharp = NeonPaints.DefaultStrokeSharpWidth)
    {
        NeonPaints.StrokeHalo.StrokeWidth = halo;
        NeonPaints.StrokeHalo.Color = color.WithAlpha(0xC0);
        c.DrawLine(x1, y1, x2, y2, NeonPaints.StrokeHalo);
        NeonPaints.StrokeSharp.StrokeWidth = sharp;
        NeonPaints.StrokeSharp.Color = color;
        c.DrawLine(x1, y1, x2, y2, NeonPaints.StrokeSharp);
        // Restore the shared widths so the next caller isn't poisoned.
        NeonPaints.StrokeHalo.StrokeWidth  = NeonPaints.DefaultStrokeHaloWidth;
        NeonPaints.StrokeSharp.StrokeWidth = NeonPaints.DefaultStrokeSharpWidth;
    }

    /// <summary>
    /// Fills a glowing neon disc of radius <paramref name="r"/>. The halo pass is
    /// drawn at 1.8× radius so the glow extends beyond the solid core.
    /// </summary>
    public static void CircleFill(SKCanvas c, float cx, float cy, float r, SKColor color)
    {
        NeonPaints.FillHalo.Color = color.WithAlpha(0xB0);
        c.DrawCircle(cx, cy, r * 1.8f, NeonPaints.FillHalo);
        NeonPaints.FillSharp.Color = color;
        c.DrawCircle(cx, cy, r, NeonPaints.FillSharp);
    }
}
