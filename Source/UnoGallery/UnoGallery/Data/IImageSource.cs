using UnoGallery.Models;

namespace UnoGallery.Data;

public interface IImageSource
{
    IAsyncEnumerable<GalleryItem> LoadAsync(CancellationToken ct = default);
}
