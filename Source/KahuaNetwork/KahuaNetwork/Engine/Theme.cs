using SkiaSharp;

namespace KahuaNetwork.Engine;

/// <summary>The shared neon palette (backgrounds, grid, role accents) and risk gradient for the demo.</summary>
internal static class Theme
{
    public static readonly SKColor Background = new(0x05, 0x07, 0x12);
    public static readonly SKColor BackgroundDeep = new(0x02, 0x03, 0x08);
    public static readonly SKColor GridFar = new(0x10, 0x1C, 0x36);
    public static readonly SKColor GridNear = new(0x1B, 0x3E, 0x7A);

    public static readonly SKColor Cyan = new(0x4D, 0xE1, 0xFF);
    public static readonly SKColor Magenta = new(0xFF, 0x4D, 0xC2);
    public static readonly SKColor Lime = new(0x9B, 0xFF, 0x6B);
    public static readonly SKColor Amber = new(0xFF, 0xC2, 0x4D);
    public static readonly SKColor Red = new(0xFF, 0x4D, 0x5E);
    public static readonly SKColor Violet = new(0xB57BFF);

    public static readonly SKColor TextPrimary = new(0xEA, 0xF7, 0xFF);
    public static readonly SKColor TextSecondary = new(0x96, 0xB6, 0xD4);
    public static readonly SKColor TextDim = new(0x5C, 0x7A, 0x99);

    public static readonly SKColor PanelFill = new SKColor(0x14, 0x1F, 0x33).WithAlpha(190);
    public static readonly SKColor PanelStroke = new SKColor(0x4D, 0xE1, 0xFF).WithAlpha(80);

    public static SKColor RiskColor(double risk)
    {
        // 0 = lime, 0.5 = amber, 1 = red
        if (risk < 0.5)
        {
            float t = (float)(risk / 0.5);
            return Lerp(Lime, Amber, t);
        }
        else
        {
            float t = (float)((risk - 0.5) / 0.5);
            return Lerp(Amber, Red, t);
        }
    }

    public static SKColor Lerp(SKColor a, SKColor b, float t)
    {
        t = System.Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }
}
