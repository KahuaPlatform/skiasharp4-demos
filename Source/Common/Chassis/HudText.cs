using SkiaSharp;

namespace Arcade.Common.Chassis;

public static class HudText
{
    // Standard halo-plus-sharp text rendering used for scoring, placards,
    // game-over panels, etc. Mutates NeonPaints.FillHalo/Sharp Color per call.
    public static void Draw(SKCanvas c, string text, float x, float y, SKTextAlign align, SKFont font, SKColor color)
    {
        NeonPaints.FillHalo.Color = color.WithAlpha(0xC0);
        c.DrawText(text, x, y, align, font, NeonPaints.FillHalo);
        NeonPaints.FillSharp.Color = color;
        c.DrawText(text, x, y, align, font, NeonPaints.FillSharp);
    }
}
