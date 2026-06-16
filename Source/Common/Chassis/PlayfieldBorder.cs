using SkiaSharp;

namespace Arcade.Common.Chassis;

/// <summary>
/// Draws a thin neon rectangle framing the world's logical playfield, so the
/// ambient starfield in the letterbox bars doesn't visually merge into the
/// gameplay area. Some demos (Hahai's maze, Alaloa's grid) skip it because their
/// playfield already frames itself.
/// </summary>
public static class PlayfieldBorder
{
    /// <summary>
    /// Outlines the <c>worldW × worldH</c> rectangle (in world coords) with the
    /// halo+sharp neon double-stroke in <paramref name="color"/>. Restores the
    /// shared stroke widths before returning.
    /// </summary>
    public static void Draw(SKCanvas c, float worldW, float worldH, SKColor color)
    {
        var rect = new SKRect(0, 0, worldW, worldH);
        NeonPaints.StrokeHalo.StrokeWidth = 6f;
        NeonPaints.StrokeHalo.Color = color.WithAlpha(0x80);
        c.DrawRect(rect, NeonPaints.StrokeHalo);
        NeonPaints.StrokeSharp.StrokeWidth = 1.4f;
        NeonPaints.StrokeSharp.Color = color.WithAlpha(0xC0);
        c.DrawRect(rect, NeonPaints.StrokeSharp);
        // Restore shared widths — the border uses non-default values above.
        NeonPaints.StrokeHalo.StrokeWidth  = NeonPaints.DefaultStrokeHaloWidth;
        NeonPaints.StrokeSharp.StrokeWidth = NeonPaints.DefaultStrokeSharpWidth;
    }
}
