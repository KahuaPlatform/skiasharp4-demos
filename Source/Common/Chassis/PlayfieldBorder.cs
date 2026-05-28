using SkiaSharp;

namespace Arcade.Common.Chassis;

public static class PlayfieldBorder
{
    // Thin neon rectangle around the world's logical playfield. Defines where
    // gameplay begins so the BackgroundSurface stars in the side bars don't
    // visually merge with the playfield.
    public static void Draw(SKCanvas c, float worldW, float worldH, SKColor color)
    {
        var rect = new SKRect(0, 0, worldW, worldH);
        NeonPaints.StrokeHalo.StrokeWidth = 6f;
        NeonPaints.StrokeHalo.Color = color.WithAlpha(0x80);
        c.DrawRect(rect, NeonPaints.StrokeHalo);
        NeonPaints.StrokeSharp.StrokeWidth = 1.4f;
        NeonPaints.StrokeSharp.Color = color.WithAlpha(0xC0);
        c.DrawRect(rect, NeonPaints.StrokeSharp);
        NeonPaints.StrokeHalo.StrokeWidth  = NeonPaints.DefaultStrokeHaloWidth;
        NeonPaints.StrokeSharp.StrokeWidth = NeonPaints.DefaultStrokeSharpWidth;
    }
}
