using SkiaSharp;

namespace Arcade.Common.Chassis;

/// <summary>
/// The shared pool of <see cref="SKPaint"/> objects that drives every
/// neon-styled draw in the chassis. Each paint's <c>Color</c> is mutated
/// per-draw; everything else (stroke width, antialias, blur, cap/join) is
/// configured once here so there is zero per-frame paint allocation.
/// </summary>
/// <remarks>
/// The neon look is a two-pass technique: a blurred wide "halo" paint drawn
/// first, then a crisp narrow "sharp" paint on top. <b>Contract:</b>
/// <see cref="StrokeHalo"/>/<see cref="StrokeSharp"/> have a default
/// <c>StrokeWidth</c> (<see cref="DefaultStrokeHaloWidth"/>/
/// <see cref="DefaultStrokeSharpWidth"/>); any helper that temporarily changes
/// the width (e.g. <c>NeonDraw.Line</c>, <c>PlayfieldBorder</c>) MUST restore it
/// before returning, or unrelated downstream draws inherit the wrong width.
/// </remarks>
public static class NeonPaints
{
    /// <summary>Rest width of <see cref="StrokeHalo"/>; restore after any temporary change.</summary>
    public const float DefaultStrokeHaloWidth  = 5.5f;
    /// <summary>Rest width of <see cref="StrokeSharp"/>; restore after any temporary change.</summary>
    public const float DefaultStrokeSharpWidth = 2.0f;

    /// <summary>Wide blurred glow pass for marquee + rainbow-title glyphs.</summary>
    public static readonly SKPaint MarqueeHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 11f, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7f),
    };
    /// <summary>Crisp narrow pass drawn on top of <see cref="MarqueeHalo"/>.</summary>
    public static readonly SKPaint MarqueeSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
    };
    /// <summary>Glow pass for arbitrary stroked paths (<c>NeonDraw.Stroke</c>/<c>Line</c>).</summary>
    public static readonly SKPaint StrokeHalo = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = DefaultStrokeHaloWidth, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f),
    };
    /// <summary>Crisp pass drawn on top of <see cref="StrokeHalo"/>.</summary>
    public static readonly SKPaint StrokeSharp = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = DefaultStrokeSharpWidth, IsAntialias = true,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
    };
    /// <summary>Glow pass for filled shapes (<c>NeonDraw.CircleFill</c>, <c>HudText</c>).</summary>
    public static readonly SKPaint FillHalo = new()
    {
        Style = SKPaintStyle.Fill, IsAntialias = true,
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f),
    };
    /// <summary>Crisp pass drawn on top of <see cref="FillHalo"/>.</summary>
    public static readonly SKPaint FillSharp = new()
    {
        Style = SKPaintStyle.Fill, IsAntialias = true,
    };
}
