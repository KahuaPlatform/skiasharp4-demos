namespace UnoGallery.Models;

/// <summary>The four gallery arrangements the scene can morph between.</summary>
public enum LayoutMode
{
    /// <summary>Flat paged grid of tiles.</summary>
    Grid,
    /// <summary>Tiles wound around a vertical helix.</summary>
    Helix,
    /// <summary>Horizontal coverflow-style carousel.</summary>
    Carousel,
    /// <summary>One focused tile large, the rest demoted.</summary>
    Detail,
}
