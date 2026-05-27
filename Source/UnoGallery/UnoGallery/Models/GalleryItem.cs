using SkiaSharp;
using UnoGallery.LiveTiles;

namespace UnoGallery.Models;

/// <summary>
/// A tile in the gallery. <see cref="Image"/> is always populated — for live
/// tiles it's a one-shot snapshot taken at construction time and used by
/// the reflection floor (which doesn't re-render live content). If <see
/// cref="Live"/> is non-null, the main tile draw delegates to it for each
/// frame instead of blitting the static image.
/// </summary>
public sealed partial record GalleryItem(
    int Id,
    string Caption,
    SKImage Image,
    ImmutableArray<SKColor> Palette,
    ILiveTile? Live = null);
