using SkiaSharp;

namespace Arcade.Common.Chassis;

// Common neon-glow primitive helpers. Each one paints a "halo" pass (blurred,
// large stroke) followed by a "sharp" pass (crisp, narrow stroke) so the result
// reads as a glowing line/shape over the dark playfield.
public static class NeonDraw
{
    public static void Stroke(SKCanvas c, SKPath path, SKColor color)
    {
        NeonPaints.StrokeHalo.Color = color.WithAlpha(0xC0);
        c.DrawPath(path, NeonPaints.StrokeHalo);
        NeonPaints.StrokeSharp.Color = color;
        c.DrawPath(path, NeonPaints.StrokeSharp);
    }

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
        NeonPaints.StrokeHalo.StrokeWidth  = NeonPaints.DefaultStrokeHaloWidth;
        NeonPaints.StrokeSharp.StrokeWidth = NeonPaints.DefaultStrokeSharpWidth;
    }

    public static void CircleFill(SKCanvas c, float cx, float cy, float r, SKColor color)
    {
        NeonPaints.FillHalo.Color = color.WithAlpha(0xB0);
        c.DrawCircle(cx, cy, r * 1.8f, NeonPaints.FillHalo);
        NeonPaints.FillSharp.Color = color;
        c.DrawCircle(cx, cy, r, NeonPaints.FillSharp);
    }
}
