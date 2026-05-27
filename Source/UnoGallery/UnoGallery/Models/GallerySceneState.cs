using System.Numerics;

namespace UnoGallery.Models;

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
