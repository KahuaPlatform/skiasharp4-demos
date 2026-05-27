using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// A "live" gallery tile that renders fresh content each frame instead of
/// presenting a static <see cref="SKImage"/>. The pipeline calls
/// <see cref="Draw"/> from the same code path it uses for static tiles, so
/// live tiles get the same drop shadow, opacity, bokeh blur, and rotation
/// treatment, and participate in layout transitions normally.
///
/// Reflections still sample the <see cref="GalleryItem.Image"/> (a one-shot
/// snapshot taken at construction) — keeps the per-frame render cost
/// bounded and is a fine visual approximation.
/// </summary>
public interface ILiveTile
{
    string Caption { get; }
    ImmutableArray<SKColor> Palette { get; }

    /// <summary>
    /// Paint the live content into <paramref name="dest"/> on the supplied
    /// canvas. Implementations are responsible for tracking their own state
    /// across frames (Conway grid, boid positions, mandelbrot cache, etc).
    /// </summary>
    /// <param name="canvas">Target canvas; already transformed for any tile rotation.</param>
    /// <param name="dest">Tile rectangle in canvas coordinates.</param>
    /// <param name="wallClockSeconds">Seconds since app start.</param>
    void Draw(SKCanvas canvas, SKRect dest, float wallClockSeconds);
}
