using SkiaSharp;
using UnoGallery.Models;

namespace UnoGallery.Layouts;

public interface ILayout
{
    /// <summary>
    /// Compute one placement per item into <paramref name="output"/>.
    /// Pure function — must not retain references. Caller guarantees
    /// <c>output.Length == items.Length</c>.
    /// </summary>
    void Compute(
        ReadOnlySpan<GalleryItem> items,
        SKSize viewport,
        float wallClockSeconds,
        int? hoveredItemId,
        Span<ItemPlacement> output);
}
