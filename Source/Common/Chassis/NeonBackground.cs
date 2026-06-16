using SkiaSharp;

namespace Arcade.Common.Chassis;

/// <summary>
/// Paints the standard deep-space vertical gradient that fills the playfield
/// canvas at the start of every game's render pass. The colors match
/// <see cref="AmbientStarBackdrop"/> so the playfield and the window side bars
/// share one base palette.
/// </summary>
public static class NeonBackground
{
    static readonly SKColor BgTop    = new(0x05, 0x00, 0x14); // near-black indigo
    static readonly SKColor BgBottom = new(0x18, 0x02, 0x36); // deep purple

    /// <summary>Fills <c>cw × ch</c> with the default deep-space gradient.</summary>
    public static void Draw(SKCanvas c, float cw, float ch) =>
        Draw(c, cw, ch, BgTop, BgBottom);

    /// <summary>Fills <c>cw × ch</c> with a custom top→bottom vertical gradient.</summary>
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
