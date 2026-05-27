using System.Numerics;
using SkiaSharp;
using UnoGallery.Models;

namespace UnoGallery.Layouts;

/// <summary>
/// Centered grid that auto-picks column count from the viewport's aspect ratio,
/// keeps tiles square, and adds a tiny per-item sinusoidal "breathe" so the
/// gallery never looks frozen. Hovered tile (if any) lifts, sharpens, and
/// scales up by ~8 % while neighbours dim and blur slightly.
/// </summary>
public sealed class GridLayout : ILayout
{
    const float TilePadding = 16f;
    const float OuterMargin = 48f;

    public void Compute(
        ReadOnlySpan<GalleryItem> items,
        SKSize viewport,
        float t,
        int? hoveredItemId,
        Span<ItemPlacement> output)
    {
        if (items.Length == 0) return;

        // Pick a column count that keeps tiles close to square within the viewport.
        float ar = viewport.Width / MathF.Max(1f, viewport.Height);
        int cols = Math.Max(1, (int)MathF.Round(MathF.Sqrt(items.Length * ar)));
        int rows = (items.Length + cols - 1) / cols;

        float availW = MathF.Max(1f, viewport.Width - OuterMargin * 2f);
        float availH = MathF.Max(1f, viewport.Height - OuterMargin * 2f);
        float tileSize = MathF.Min(
            (availW - TilePadding * (cols - 1)) / cols,
            (availH - TilePadding * (rows - 1)) / rows);

        float gridW = cols * tileSize + (cols - 1) * TilePadding;
        float gridH = rows * tileSize + (rows - 1) * TilePadding;
        float x0 = (viewport.Width - gridW) * 0.5f;
        float y0 = (viewport.Height - gridH) * 0.5f;

        for (int i = 0; i < items.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float baseX = x0 + col * (tileSize + TilePadding) + tileSize * 0.5f;
            float baseY = y0 + row * (tileSize + TilePadding) + tileSize * 0.5f;

            // Per-item phase so the breathing doesn't synchronise into a single pulse.
            float phase = items[i].Id * 0.73f;
            float bob = MathF.Sin(t * 0.9f + phase) * 3.0f;
            float drift = MathF.Cos(t * 0.6f + phase * 1.3f) * 2.0f;

            bool hovered = hoveredItemId == items[i].Id;
            bool somethingElseHovered = hoveredItemId.HasValue && !hovered;

            float scale = hovered ? 1.08f : (somethingElseHovered ? 0.96f : 1f);
            float sharpness = hovered ? 1f : (somethingElseHovered ? 0.55f : 1f);
            float opacity = somethingElseHovered ? 0.75f : 1f;
            float z = hovered ? 1f : 0f;

            output[i] = new ItemPlacement(
                ItemId: items[i].Id,
                Center: new Vector2(baseX + drift, baseY + bob),
                Size: new Vector2(tileSize * scale, tileSize * scale),
                Rotation: 0f,
                Z: z,
                Opacity: opacity,
                Sharpness: sharpness);
        }
    }
}
