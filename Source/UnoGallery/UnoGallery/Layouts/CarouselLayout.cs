using System.Numerics;
using SkiaSharp;
using UnoGallery.Models;

namespace UnoGallery.Layouts;

/// <summary>
/// All items on a single horizontal ring around the viewer. Auto-rotates so
/// each tile takes a turn at the front. Items near the front are large and
/// sharp; items behind shrink, dim and blur until they're swept around to
/// the front again.
/// </summary>
public sealed class CarouselLayout : ILayout
{
    const float AngularSpeed = 0.22f;
    const float MinScale = 0.30f;
    const float MaxScale = 1.45f;

    public void Compute(
        ReadOnlySpan<GalleryItem> items,
        SKSize viewport,
        float t,
        int? hoveredItemId,
        Span<ItemPlacement> output)
    {
        if (items.Length == 0) return;

        float cx = viewport.Width * 0.5f;
        float cy = viewport.Height * 0.55f; // sit slightly below centre so app bar doesn't crowd
        float radius = MathF.Min(viewport.Width, viewport.Height) * 0.45f;
        float tileBase = MathF.Min(viewport.Width, viewport.Height) * 0.22f;

        for (int i = 0; i < items.Length; i++)
        {
            // Spread items evenly around the ring. Negative speed so leftmost item moves toward camera.
            float angle = i * (MathF.PI * 2f / items.Length) - t * AngularSpeed;
            float wx = MathF.Sin(angle) * radius;
            float wz = MathF.Cos(angle) * radius;   // +radius = directly in front of camera

            float depth01 = (wz + radius) / (2f * radius);    // 0 = back, 1 = front

            float scale = MinScale + (MaxScale - MinScale) * depth01;
            float screenX = cx + wx;
            // Tiny vertical arc so the ring reads as 3D rather than a flat sine wave.
            float screenY = cy + (1f - depth01) * tileBase * 0.4f;

            bool hovered = hoveredItemId == items[i].Id;
            float hoverBoost = hovered ? 1.10f : 1f;
            float opacity = 0.25f + depth01 * 0.75f;
            if (hovered) opacity = 1f;

            output[i] = new ItemPlacement(
                ItemId: items[i].Id,
                Center: new Vector2(screenX, screenY),
                Size: new Vector2(tileBase * scale * hoverBoost, tileBase * scale * hoverBoost),
                Rotation: 0f,
                Z: hovered ? radius * 2f : wz,
                Opacity: opacity,
                Sharpness: hovered ? 1f : MathF.Max(0.25f, depth01));
        }
    }
}
