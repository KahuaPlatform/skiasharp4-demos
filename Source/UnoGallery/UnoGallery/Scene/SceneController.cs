using System.Numerics;
using SkiaSharp;
using UnoGallery.Audio;
using UnoGallery.Data;
using UnoGallery.Diagnostics;
using UnoGallery.Effects;
using UnoGallery.Layouts;
using UnoGallery.Models;

namespace UnoGallery.Scene;

/// <summary>
/// Owns the per-frame mutable state for the gallery: time, items, layout
/// transitions, hover/focus, settings. Two transition channels run together:
/// layout (Grid/Helix/Carousel/Detail) and effects toggles (rendered next frame).
/// The renderer reads a snapshot via <see cref="Render"/> and never sees the
/// in-flight interpolation directly.
/// </summary>
public sealed class SceneController
{
    const float TransitionDuration = 0.9f;
    const float DemoLayoutInterval = 5.0f;
    const float DemoIdleResume = 3.0f;
    const float ParallaxStrength = 14f;   // px max shift when pointer at viewport corner

    readonly ImageStore _store;
    readonly EffectsPipeline _effects = new();

    readonly Dictionary<LayoutMode, ILayout> _layouts = new()
    {
        [LayoutMode.Grid]     = new GridLayout(),
        [LayoutMode.Helix]    = new HelixLayout(),
        [LayoutMode.Carousel] = new CarouselLayout(),
        [LayoutMode.Detail]   = new DetailLayout(),
    };

    GallerySceneState _state = GallerySceneState.Empty;
    float _transitionStartTime;
    float _lastDemoLayoutTime;
    float _lastInteractionTime = -1000f;
    LayoutMode _layoutBeforeFocus = LayoutMode.Grid;

    public bool DemoMode { get; set; } = true;

    public SceneController(ImageStore store)
    {
        _store = store;
        _store.ItemAdded += OnItemAdded;
        _state = _state with { Items = _store.Snapshot() };
    }

    public GallerySceneState State => _state;

    void OnItemAdded(GalleryItem _) => _state = _state with { Items = _store.Snapshot() };

    public void Tick(float wallClockSeconds)
    {
        _state = _state with { WallClockSeconds = wallClockSeconds };

        // One FFT + beat-detection tick per frame, against whichever audio
        // source the user has selected. WaveformTile reads the spectrum;
        // BackgroundPass reads the beat pulse.
        using (FrameProfiler.Measure("audio.update"))
            AudioSourceManager.Instance.Update(wallClockSeconds);

        if (_state.TargetLayout is LayoutMode target)
        {
            float t01 = Math.Clamp((wallClockSeconds - _transitionStartTime) / TransitionDuration, 0f, 1f);
            if (t01 >= 1f)
            {
                // Clearing FocusedItemId is deferred to transition completion so
                // that DetailLayout keeps the correct hero tile during a dismiss
                // animation. If we cleared it inside Unfocus(), DetailLayout's
                // "outgoing" frames would fall back to items[0] and we'd see a
                // different image fly back to the layout — the bug we hit before.
                bool leftDetail = _state.CurrentLayout == LayoutMode.Detail && target != LayoutMode.Detail;
                _state = _state with
                {
                    CurrentLayout = target,
                    TargetLayout = null,
                    TransitionProgress = 0f,
                    FocusedItemId = leftDetail ? null : _state.FocusedItemId,
                };
            }
            else
            {
                _state = _state with { TransitionProgress = Easing.InOutQuart(t01) };
            }
        }

        // Auto-cycle layouts when idle. Skip Detail — it's modal, not part of the rotation.
        if (DemoMode
            && _state.TargetLayout is null
            && _state.CurrentLayout != LayoutMode.Detail
            && !_state.Items.IsDefaultOrEmpty
            && wallClockSeconds - _lastInteractionTime > DemoIdleResume
            && wallClockSeconds - _lastDemoLayoutTime > DemoLayoutInterval)
        {
            RequestLayoutInternal(NextDemoLayout(_state.CurrentLayout));
            _lastDemoLayoutTime = wallClockSeconds;
        }
    }

    public void RequestLayout(LayoutMode target)
    {
        _lastInteractionTime = _state.WallClockSeconds;
        // Manual layout pick implies leaving Detail mode.
        if (_state.CurrentLayout == LayoutMode.Detail || _state.TargetLayout == LayoutMode.Detail)
            _state = _state with { FocusedItemId = null };
        RequestLayoutInternal(target);
    }

    void RequestLayoutInternal(LayoutMode target)
    {
        if (_state.CurrentLayout == target && _state.TargetLayout is null) return;
        _state = _state with
        {
            TargetLayout = target,
            TransitionProgress = 0f,
        };
        _transitionStartTime = _state.WallClockSeconds;
    }

    static LayoutMode NextDemoLayout(LayoutMode current) => current switch
    {
        LayoutMode.Grid     => LayoutMode.Helix,
        LayoutMode.Helix    => LayoutMode.Carousel,
        LayoutMode.Carousel => LayoutMode.Grid,
        _                   => LayoutMode.Grid,
    };

    public void Focus(int itemId)
    {
        _lastInteractionTime = _state.WallClockSeconds;
        if (_state.CurrentLayout != LayoutMode.Detail && _state.TargetLayout != LayoutMode.Detail)
            _layoutBeforeFocus = _state.CurrentLayout;
        _state = _state with { FocusedItemId = itemId };
        RequestLayoutInternal(LayoutMode.Detail);
    }

    public void Unfocus()
    {
        _lastInteractionTime = _state.WallClockSeconds;
        if (_state.CurrentLayout != LayoutMode.Detail && _state.TargetLayout != LayoutMode.Detail) return;
        // FocusedItemId stays set — Tick clears it when the dismiss transition
        // completes, so DetailLayout shows the right hero throughout the swap.
        RequestLayoutInternal(_layoutBeforeFocus);
    }

    public void SetHovered(int? itemId)
    {
        if (_state.HoveredItemId == itemId) return;
        _state = _state with { HoveredItemId = itemId };
        _lastInteractionTime = _state.WallClockSeconds;
    }

    public void SetViewerPosition(Vector2 normalisedFromCenter)
    {
        _state = _state with { ViewerWorldPosition = normalisedFromCenter };
    }

    public void UpdateSettings(Func<GallerySettings, GallerySettings> mutate)
    {
        _state = _state with { Settings = mutate(_state.Settings) };
        _lastInteractionTime = _state.WallClockSeconds;
    }

    public void Render(SKCanvas canvas, SKSize size)
    {
        if (_state.Items.IsDefaultOrEmpty)
        {
            canvas.Clear(new SKColor(10, 12, 18));
            DrawWarming(canvas, size);
            return;
        }

        var items = _state.Items.AsSpan();
        ItemPlacement[] current;
        ItemPlacement[]? target;
        using (FrameProfiler.Measure("placements"))
        {
            current = ComputeRawPlacements(_state.CurrentLayout, items, size);
            target = _state.TargetLayout is LayoutMode t
                ? ComputeRawPlacements(t, items, size)
                : null;
        }
        _effects.Render(canvas, size, _state, current, target);
    }

    /// <summary>
    /// Compute placements for a single layout (no lerp), with pointer parallax
    /// applied. The pipeline chooses how to combine current + target arrays —
    /// lerp, SKSL dissolve, or SKSL iris — based on what's enabled.
    /// </summary>
    ItemPlacement[] ComputeRawPlacements(LayoutMode mode, ReadOnlySpan<GalleryItem> items, SKSize size)
    {
        var arr = new ItemPlacement[items.Length];
        Compute(mode, items, size, arr);
        ApplyParallax(arr, size);
        return arr;
    }

    void ApplyParallax(Span<ItemPlacement> placements, SKSize size)
    {
        if (_state.ViewerWorldPosition == Vector2.Zero) return;
        var view = _state.ViewerWorldPosition;
        for (int i = 0; i < placements.Length; i++)
        {
            var pl = placements[i];
            // Bigger / closer tiles shift more — sells "looking around" the scene.
            float closeness = Math.Clamp(pl.Size.X / (size.Width * 0.3f), 0.1f, 1f);
            placements[i] = pl with { Center = pl.Center - view * ParallaxStrength * closeness };
        }
    }

    /// <summary>
    /// Lerped placements — used by <see cref="HitTest"/> so clicking mid-transition
    /// hits whatever the user actually sees on screen even when the renderer is
    /// using a non-lerping transition (dissolve / iris).
    /// </summary>
    ItemPlacement[] ComputeLerpedPlacements(ReadOnlySpan<GalleryItem> items, SKSize size)
    {
        var current = ComputeRawPlacements(_state.CurrentLayout, items, size);
        if (_state.TargetLayout is LayoutMode target)
        {
            var to = ComputeRawPlacements(target, items, size);
            float p = _state.TransitionProgress;
            for (int i = 0; i < current.Length; i++)
                current[i] = Lerp(current[i], to[i], p);
        }
        return current;
    }

    /// <summary>
    /// DetailLayout needs <c>FocusedItemId</c>, which isn't part of ILayout's contract.
    /// We smuggle it via the <c>hoveredItemId</c> parameter when Detail is in play.
    /// </summary>
    void Compute(LayoutMode mode, ReadOnlySpan<GalleryItem> items, SKSize size, Span<ItemPlacement> output)
    {
        int? hint = mode == LayoutMode.Detail ? _state.FocusedItemId : _state.HoveredItemId;
        _layouts[mode].Compute(items, size, _state.WallClockSeconds, hint, output);
    }

    static ItemPlacement Lerp(ItemPlacement a, ItemPlacement b, float t) => new(
        ItemId:    a.ItemId,
        Center:    Vector2.Lerp(a.Center, b.Center, t),
        Size:      Vector2.Lerp(a.Size, b.Size, t),
        Rotation:  a.Rotation  + (b.Rotation  - a.Rotation)  * t,
        Z:         a.Z         + (b.Z         - a.Z)         * t,
        Opacity:   a.Opacity   + (b.Opacity   - a.Opacity)   * t,
        Sharpness: a.Sharpness + (b.Sharpness - a.Sharpness) * t);

    static void DrawWarming(SKCanvas canvas, SKSize size)
    {
        using var font = new SKFont { Size = 18 };
        using var paint = new SKPaint { Color = new SKColor(180, 180, 200), IsAntialias = true };
        const string msg = "Warming up the gallery...";
        var width = font.MeasureText(msg);
        canvas.DrawText(msg, (size.Width - width) * 0.5f, size.Height * 0.5f, SKTextAlign.Left, font, paint);
    }

    public int? HitTest(float vx, float vy, SKSize viewport)
    {
        if (_state.Items.IsDefaultOrEmpty) return null;
        var items = _state.Items.AsSpan();
        var placements = ComputeLerpedPlacements(items, viewport);

        int? best = null;
        float bestZ = float.NegativeInfinity;
        foreach (var p in placements)
        {
            var hx = vx - p.Center.X;
            var hy = vy - p.Center.Y;
            if (MathF.Abs(hx) <= p.Size.X * 0.5f && MathF.Abs(hy) <= p.Size.Y * 0.5f && p.Z >= bestZ)
            {
                best = p.ItemId;
                bestZ = p.Z;
            }
        }
        return best;
    }
}
