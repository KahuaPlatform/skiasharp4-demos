using SkiaSharp;

namespace Arcade.Common.Chassis;

public static class NeonBackground
{
    // Standard deep-space vertical gradient — drawn at the start of every
    // game's render pass to fill the playfield canvas. The BackgroundSurface
    // (AmbientStarBackdrop) renders a matching gradient on the full window so
    // the playfield and side bars share the same base palette.
    static readonly SKColor BgTop    = new(0x05, 0x00, 0x14);
    static readonly SKColor BgBottom = new(0x18, 0x02, 0x36);

    public static void Draw(SKCanvas c, float cw, float ch) =>
        Draw(c, cw, ch, BgTop, BgBottom);

    public static void Draw(SKCanvas c, float cw, float ch, SKColor top, SKColor bottom)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, ch),
                new[] { top, bottom }, SKShaderTileMode.Clamp),
        };
        c.DrawRect(0, 0, cw, ch, paint);
    }
}
