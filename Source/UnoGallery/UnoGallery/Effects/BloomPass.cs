using SkiaSharp;

namespace UnoGallery.Effects;

/// <summary>
/// Cheap bloom that replays the bg+reflection+tiles content through a
/// blur+threshold paint, Plus-blended over the live canvas. Source comes from
/// an <see cref="SKPicture"/> captured during the main pipeline pass — we
/// don't need access to the underlying <see cref="SKSurface"/>, so this works
/// inside <c>SKCanvasElement.RenderOverride</c>.
/// </summary>
public sealed class BloomPass
{
    const float BlurRadius = 10f;
    const byte  BloomAlpha = 60;

    // Highlight pass: pixels below ~0.55 contribute nothing, above ramp fast.
    static readonly SKColorFilter Threshold = SKColorFilter.CreateColorMatrix(new float[]
    {
        2.5f, 0,    0,    0, -140f,
        0,    2.5f, 0,    0, -140f,
        0,    0,    2.5f, 0, -140f,
        0,    0,    0,    1, 0,
    });

    public void Draw(SKCanvas canvas, SKPicture content)
    {
        using var paint = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateBlur(BlurRadius, BlurRadius),
            ColorFilter = Threshold,
            BlendMode = SKBlendMode.Plus,
            Color = SKColors.White.WithAlpha(BloomAlpha),
            IsAntialias = true,
        };
        var identity = SKMatrix.CreateIdentity();
        canvas.DrawPicture(content, in identity, paint);
    }
}
