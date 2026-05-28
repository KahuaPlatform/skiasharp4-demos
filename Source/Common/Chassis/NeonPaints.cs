using SkiaSharp;

namespace Arcade.Common.Chassis;

// Static SKPaint instances shared by all neon-chassis rendering. The Color is
// mutated per-draw; everything else (stroke width, antialias, blur, cap/join)
// is configured once at startup.
//
// IMPORTANT: NeonStrokeHalo and NeonStrokeSharp have a default StrokeWidth.
// Helpers that temporarily change the width (NeonDraw.Line, PlayfieldBorder,
// terrain stroking, etc.) MUST restore it back to the default before exiting.
public static class NeonPaints
{
    public const float DefaultStrokeHaloWidth  = 5.5f;
    public const float DefaultStrokeSharpWidth = 2.0f;

    public static readonly SKPaint MarqueeHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 11f, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7f),
    };
    public static readonly SKPaint MarqueeSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
    };
    public static readonly SKPaint StrokeHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = DefaultStrokeHaloWidth, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f),
    };
    public static readonly SKPaint StrokeSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = DefaultStrokeSharpWidth, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
    };
    public static readonly SKPaint FillHalo = new()
    {
        Style = SKPaintStyle.Fill, IsAntialias = true,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f),
    };
    public static readonly SKPaint FillSharp = new()
    {
        Style = SKPaintStyle.Fill, IsAntialias = true,
    };
}
