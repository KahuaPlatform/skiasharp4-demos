using System.Numerics;

namespace UnoGallery.Models;

/// <summary>
/// The immutable snapshot of everything the scene needs to render a frame: the
/// items, the current/target layout and the morph progress between them, focus +
/// hover, the viewer position, wall-clock time, and the live settings. New states
/// are produced each tick rather than mutating in place.
/// </summary>
public sealed record GallerySceneState(
    ImmutableArray<GalleryItem> Items,
    LayoutMode CurrentLayout,
    LayoutMode? TargetLayout,
    float TransitionProgress,
    int? FocusedItemId,
    int? HoveredItemId,
    Vector2 ViewerWorldPosition,
    float WallClockSeconds,
    GallerySettings Settings)
{
    /// <summary>The initial empty scene: no items, Grid layout, at the origin.</summary>
    public static GallerySceneState Empty { get; } = new(
        Items: ImmutableArray<GalleryItem>.Empty,
        CurrentLayout: LayoutMode.Grid,
        TargetLayout: null,
        TransitionProgress: 0f,
        FocusedItemId: null,
        HoveredItemId: null,
        ViewerWorldPosition: Vector2.Zero,
        WallClockSeconds: 0f,
        Settings: GallerySettings.Default);
}
