using System.Numerics;
using SkiaSharp;
using UnoGallery.Models;

namespace UnoGallery.Layouts;

/// <summary>
/// Vertical helix. Items wind around a centred Y axis with a constant pitch,
/// auto-rotating slowly. Items in front of the axis (positive Z) are larger,
/// brighter, and sharper than those at the back. The full set occupies ~55 %
/// of the viewport height so the spiral reads as a single object rather than
/// a column of cards.
/// </summary>
public sealed class HelixLayout : ILayout
{
    const float ItemsPerTurn = 9f;        // ~3 turns for 30 items — readable spiral
    const float AngularSpeed = 0.22f;     // radians / sec, gentle drift
    const float HelixHeightFrac = 0.92f;  // claim almost the full canvas vertically
    const float MinScale = 0.42f;         // depth-driven scale at the back of the helix
    const float MaxScale = 1.15f;         // ... and at the front
    const float TileSizeFrac = 0.13f;     // tile size relative to viewport short edge

    public void Compute(
        ReadOnlySpan<GalleryItem> items,
        SKSize viewport,
        float t,
        int? hoveredItemId,
        Span<ItemPlacement> output)
    {
        if (items.Length == 0) return;

        float cx = viewport.Width * 0.5f;
        float cy = viewport.Height * 0.5f;
        float shortEdge = MathF.Min(viewport.Width, viewport.Height);
        float radius = shortEdge * 0.30f;
        float tileBase = shortEdge * TileSizeFrac;

        float totalHeight = viewport.Height * HelixHeightFrac;
        float pitch = items.Length > 1 ? totalHeight / (items.Length - 1) : 0f;
        float startY = cy + totalHeight * 0.5f; // bottom of helix

        for (int i = 0; i < items.Length; i++)
        {
            float angle = i * (MathF.PI * 2f / ItemsPerTurn) + t * AngularSpeed;
            float wx = MathF.Cos(angle) * radius;
            float wz = MathF.Sin(angle) * radius; // +Z = toward viewer
            float wy = startY - i * pitch;

            // Normalised depth: 0 at back of helix, 1 at front.
            float depth01 = 0.5f + wz / (radius * 2f);

            float scale = MinScale + (MaxScale - MinScale) * depth01;
            // Slight horizontal foreshortening — items at the back drift toward the centre line.
            float screenX = cx + wx * (0.55f + depth01 * 0.45f);
            float screenY = wy;

            bool hovered = hoveredItemId == items[i].Id;
            float hoverBoost = hovered ? 1.18f : 1f;
            float opacity = MathF.Pow(depth01, 0.7f) * 0.5f + 0.5f;
            if (hovered) opacity = 1f;

            output[i] = new ItemPlacement(
                ItemId: items[i].Id,
                Center: new Vector2(screenX, screenY),
                Size: new Vector2(tileBase * scale * hoverBoost, tileBase * scale * hoverBoost),
                Rotation: 0f,
                // Use world Z for painter-sort; hover lifts to the front.
                Z: hovered ? radius * 2f : wz,
                Opacity: opacity,
                Sharpness: hovered ? 1f : MathF.Max(0.35f, depth01));
        }
    }
}
