using System.Numerics;
using SkiaSharp;
using UnoGallery.Models;

namespace UnoGallery.Layouts;

/// <summary>
/// One tile takes the stage. The focused item (per <see cref="GallerySceneState.FocusedItemId"/>)
/// is centered and scaled to ~62 % of the viewport's short edge; everyone else is pushed to a
/// thin perimeter strip at low opacity. If no focus is set, the whole gallery dissolves to
/// nothing — the caller is expected to set <c>FocusedItemId</c> before requesting this layout.
/// </summary>
public sealed class DetailLayout : ILayout
{
    const float HeroSizeFrac = 0.62f;
    const float CrumbSizeFrac = 0.045f;
    const float CrumbOpacity = 0.18f;

    public void Compute(
        ReadOnlySpan<GalleryItem> items,
        SKSize viewport,
        float t,
        int? hoveredItemId,
        Span<ItemPlacement> output)
    {
        if (items.Length == 0) return;

        // The caller passes FocusedItemId via hoveredItemId here because ILayout doesn't
        // see the whole state. SceneController forwards FocusedItemId into both slots
        // when computing Detail placements so this layout can identify the hero tile.
        int heroId = hoveredItemId ?? items[0].Id;

        float cx = viewport.Width * 0.5f;
        float cy = viewport.Height * 0.52f;   // a touch below true centre so caption fits
        float shortEdge = MathF.Min(viewport.Width, viewport.Height);
        float heroSize = shortEdge * HeroSizeFrac;
        float crumbSize = shortEdge * CrumbSizeFrac;

        // Non-hero items orbit in a thin ellipse near the top edge — recognisable as
        // "context for what's selected" without competing with the hero.
        float ringRx = viewport.Width * 0.42f;
        float ringRy = viewport.Height * 0.05f;
        float ringCy = viewport.Height * 0.08f;
        float angularStep = items.Length > 1 ? MathF.Tau / items.Length : 0f;

        int crumbIdx = 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].Id == heroId)
            {
                output[i] = new ItemPlacement(
                    ItemId: items[i].Id,
                    Center: new Vector2(cx, cy),
                    Size: new Vector2(heroSize, heroSize),
                    Rotation: 0f,
                    Z: 1000f,
                    Opacity: 1f,
                    Sharpness: 1f);
            }
            else
            {
                float a = crumbIdx * angularStep + t * 0.05f;
                output[i] = new ItemPlacement(
                    ItemId: items[i].Id,
                    Center: new Vector2(cx + MathF.Cos(a) * ringRx, ringCy + MathF.Sin(a) * ringRy),
                    Size: new Vector2(crumbSize, crumbSize),
                    Rotation: 0f,
                    Z: -10f,
                    Opacity: CrumbOpacity,
                    Sharpness: 0.3f);
                crumbIdx++;
            }
        }
    }
}
