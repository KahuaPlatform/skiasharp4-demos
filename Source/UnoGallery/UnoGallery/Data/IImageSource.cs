using UnoGallery.Models;

namespace UnoGallery.Data;

/// <summary>
/// A source of gallery items, streamed asynchronously so the UI can show tiles as
/// they arrive. Implementations include the procedural sample set and the
/// EXIF-aware folder loader.
/// </summary>
public interface IImageSource
{
    /// <summary>Streams gallery items as they're produced/decoded; honors <paramref name="ct"/>.</summary>
    IAsyncEnumerable<GalleryItem> LoadAsync(CancellationToken ct = default);
}
