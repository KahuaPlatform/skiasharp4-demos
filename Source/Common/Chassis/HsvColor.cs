using System;
using SkiaSharp;

namespace Arcade.Common.Chassis;

/// <summary>
/// HSV→RGB color conversion used everywhere the neon chassis cycles hue over
/// time (rainbow titles, marquee letters, energy rings, particle bursts, …).
/// </summary>
public static class HsvColor
{
    /// <summary>
    /// Converts an HSV triple to an opaque <see cref="SKColor"/>.
    /// </summary>
    /// <param name="hue">Hue in degrees; any value is wrapped into [0, 360).</param>
    /// <param name="sat">Saturation in [0, 1].</param>
    /// <param name="val">Value/brightness in [0, 1].</param>
    public static SKColor HsvToRgb(float hue, float sat, float val)
    {
        // Wrap hue into [0,360) so callers can pass an ever-increasing time*speed.
        hue = ((hue % 360f) + 360f) % 360f;
        // Standard sextant decomposition: c is chroma, x the second-largest
        // component, m the achromatic offset added back to every channel.
        float c = val * sat;
        float x = c * (1f - MathF.Abs((hue / 60f) % 2f - 1f));
        float m = val - c;
        float r, g, b;
        switch ((int)(hue / 60f) % 6)
        {
            case 0: r = c; g = x; b = 0; break;
            case 1: r = x; g = c; b = 0; break;
            case 2: r = 0; g = c; b = x; break;
            case 3: r = 0; g = x; b = c; break;
            case 4: r = x; g = 0; b = c; break;
            default: r = c; g = 0; b = x; break;
        }
        return new SKColor(
            (byte)MathF.Round((r + m) * 255f),
            (byte)MathF.Round((g + m) * 255f),
            (byte)MathF.Round((b + m) * 255f));
    }
}
