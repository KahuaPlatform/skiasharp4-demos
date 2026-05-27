using SkiaSharp;

namespace UnoGallery.Effects;

/// <summary>
/// Reflects an SKPicture of the tiles in a floor plane. Implemented as one
/// offscreen layer:
///   1. SaveLayer
///   2. Flip the world vertically about the floor line
///   3. Replay the tiles picture (which contains live tile content rendered
///      at the current frame — no static snapshot drift)
///   4. Gradient alpha mask via DstIn so the reflection fades toward the
///      bottom edge
///   5. Clear above the floor
///   6. Restore the layer (composites onto the main canvas)
///
/// Taking an <see cref="SKPicture"/> instead of iterating placements means
/// live tiles' Live.Draw is called exactly once per frame (when the picture
/// is recorded), and the picture is replayed twice (reflection + main).
/// </summary>
public sealed class ReflectionPass
{
    const float FloorYFrac = 0.80f;
    const float ReflectionAlpha = 0.45f;

    public void DrawFromPicture(SKCanvas canvas, SKSize size, SKPicture tilesPicture)
    {
        float floorY = size.Height * FloorYFrac;

        // Bound the offscreen layer to just the floor band (~20 % of viewport
        // height). Skia allocates a smaller backing surface accordingly, and
        // the picture replay outside this band is clip-rejected for free.
        var bandBounds = new SKRect(0, floorY, size.Width, size.Height);
        using var layerPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha((byte)(255 * ReflectionAlpha)),
        };
        canvas.SaveLayer(bandBounds, layerPaint);

        canvas.Save();
        canvas.Translate(0, 2f * floorY);
        canvas.Scale(1f, -1f);
        canvas.DrawPicture(tilesPicture);
        canvas.Restore();

        // Gradient alpha mask: opaque at the floor line, transparent at the bottom edge.
        using (var mask = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, floorY),
                new SKPoint(0, size.Height),
                new[] { new SKColor(255, 255, 255, 255), new SKColor(255, 255, 255, 0) },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp),
            BlendMode = SKBlendMode.DstIn,
        })
        {
            canvas.DrawRect(0, floorY, size.Width, size.Height - floorY, mask);
        }

        // No clear-above-floor needed — layer bounds already exclude that region.

        canvas.Restore(); // composite layer

        // Horizon line so the floor reads as a plane.
        using (var horizon = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, floorY - 4f),
                new SKPoint(0, floorY + 4f),
                new[] { new SKColor(255, 255, 255, 0), new SKColor(255, 255, 255, 35), new SKColor(255, 255, 255, 0) },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp),
            BlendMode = SKBlendMode.Plus,
        })
        {
            canvas.DrawRect(0, floorY - 4f, size.Width, 8f, horizon);
        }
    }
}
