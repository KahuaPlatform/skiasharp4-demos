using UnoGallery.Models;

namespace UnoGallery.Data;

/// <summary>
/// Append-only store of <see cref="GalleryItem"/>. Raises <see cref="ItemAdded"/>
/// as items stream in from a source, so the scene can rebuild placements
/// incrementally without waiting for the full set.
/// </summary>
public sealed class ImageStore
{
    readonly List<GalleryItem> _items = new();
    readonly Lock _lock = new();

    public event Action<GalleryItem>? ItemAdded;
    public event Action? Cleared;

    public ImmutableArray<GalleryItem> Snapshot()
    {
        lock (_lock)
        {
            return _items.ToImmutableArray();
        }
    }

    public void Add(GalleryItem item)
    {
        lock (_lock)
        {
            _items.Add(item);
        }
        ItemAdded?.Invoke(item);
    }

    /// <summary>
    /// Removes all items and disposes their <see cref="SkiaSharp.SKImage"/>
    /// references. Callers must ensure no in-flight render is reading the
    /// store — for the gallery this is fine because reads happen on the UI
    /// thread and Clear is invoked from there.
    /// </summary>
    public void Clear()
    {
        GalleryItem[] toDispose;
        lock (_lock)
        {
            toDispose = _items.ToArray();
            _items.Clear();
        }
        foreach (var it in toDispose) it.Image.Dispose();
        Cleared?.Invoke();
    }

    public async Task PopulateAsync(IImageSource source, CancellationToken ct = default)
    {
        await foreach (var item in source.LoadAsync(ct).ConfigureAwait(false))
        {
            Add(item);
        }
    }
}
