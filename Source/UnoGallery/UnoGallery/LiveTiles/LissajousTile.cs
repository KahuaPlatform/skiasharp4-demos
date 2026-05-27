#pragma warning disable CS0618 // SKPath obsolete in v4
using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Lissajous curve — the parametric figure (sin(a·u), sin(b·u + φ)) for
/// u ∈ [0, 2π]. With integer (a, b) the curve is closed; with slowly
/// drifting non-integer values it morphs continuously between shapes.
/// Frequencies and phase shift evolve on slow sin/cos so the figure
/// never quite repeats.
/// </summary>
public sealed class LissajousTile : ILiveTile
{
    const int Samples = 600;

    public string Caption => "Lissajous";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(6, 10, 18),
        new SKColor(120, 180, 255),
        new SKColor(255, 100, 200),
        new SKColor(255, 220, 130));

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        using var bg = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bg);

        // Slowly drifting parameters — integer-adjacent values give nice
        // near-closed shapes that morph through ratios.
        float a = 3f + 1.6f * MathF.Sin(t * 0.07f);
        float b = 4f + 1.6f * MathF.Cos(t * 0.053f + 0.7f);
        float phase = t * 0.4f;

        // Margin so the curve doesn't clip the rounded ends.
        float pad = MathF.Min(dest.Width, dest.Height) * 0.10f;
        float cx = dest.MidX;
        float cy = dest.MidY;
        float rx = dest.Width * 0.5f - pad;
        float ry = dest.Height * 0.5f - pad;

        using var path = new SKPath();
        for (int i = 0; i <= Samples; i++)
        {
            float u = i / (float)Samples * MathF.Tau;
            float x = cx + MathF.Sin(a * u) * rx;
            float y = cy + MathF.Sin(b * u + phase) * ry;
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }

        // Wide soft glow underneath.
        using (var glow = new SKPaint
        {
            Color = Palette[2].WithAlpha(150),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(4f, dest.Width / 70f),
            IsAntialias = true,
            ImageFilter = SKImageFilter.CreateBlur(6f, 6f),
        })
        {
            canvas.DrawPath(path, glow);
        }

        // Crisp gradient stroke on top.
        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(1.4f, dest.Width / 220f),
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(dest.Left, dest.Top),
                new SKPoint(dest.Right, dest.Bottom),
                new[] { Palette[1], Palette[2], Palette[3] },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp),
        };
        canvas.DrawPath(path, stroke);
    }
}
