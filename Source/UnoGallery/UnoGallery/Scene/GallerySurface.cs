using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using UnoGallery.Data;
using UnoGallery.Models;
using Windows.Storage;
using WinSize = Windows.Foundation.Size;

namespace UnoGallery.Scene;

/// <summary>
/// Direct-GPU Skia surface for the gallery. Subclasses Uno's
/// <see cref="SKCanvasElement"/> so paint goes straight into the same GPU
/// compositor surface Uno uses for the rest of the XAML tree — no bitmap
/// upload, no readback. <see cref="RenderOverride"/> is invoked by Uno
/// whenever we call <see cref="SKCanvasElement.Invalidate"/>; a continuous
/// 60 fps tick comes from <see cref="CompositionTarget.Rendering"/>.
/// </summary>
public sealed class GallerySurface : SKCanvasElement
{
    readonly Stopwatch _clock = Stopwatch.StartNew();
    ImageStore? _store;
    SceneController? _controller;
    bool _renderingHooked;

    public GallerySurface()
    {
        // The base class is a FrameworkElement, so XAML can instantiate us
        // directly with `<scene:GallerySurface />`.
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;

        PointerMoved   += OnPointerMoved;
        PointerExited  += OnPointerExited;
        PointerPressed += OnPointerPressed;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        _store = new ImageStore();
        _controller = new SceneController(_store);
        _store.ItemAdded += _ => Invalidate();

        if (!_renderingHooked)
        {
            CompositionTarget.Rendering += OnRendering;
            _renderingHooked = true;
        }
        _ = LoadAsync();
    }

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_renderingHooked)
        {
            CompositionTarget.Rendering -= OnRendering;
            _renderingHooked = false;
        }
    }

    async Task LoadAsync()
    {
        try
        {
            if (_store is null) return;
            await _store.PopulateAsync(new ProceduralSampleSource()).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnoGallery] sample load failed: {ex}");
        }
    }

    void OnRendering(object? sender, object args)
    {
        _controller?.Tick((float)_clock.Elapsed.TotalSeconds);
        Invalidate();
    }

    protected override void RenderOverride(SKCanvas canvas, WinSize area)
    {
        _controller?.Render(canvas, new SKSize((float)area.Width, (float)area.Height));
    }

    (float vx, float vy, SKSize size) ViewportCoords(PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        // SKCanvasElement composes into the GPU surface using the element's
        // actual size, so device pixels == DIPs here for hit-testing purposes.
        var size = new SKSize((float)ActualWidth, (float)ActualHeight);
        return ((float)pt.Position.X, (float)pt.Position.Y, size);
    }

    void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_controller is null) return;
        var (vx, vy, size) = ViewportCoords(e);
        _controller.SetHovered(_controller.HitTest(vx, vy, size));

        float nx = size.Width  > 0 ? (vx / size.Width)  * 2f - 1f : 0f;
        float ny = size.Height > 0 ? (vy / size.Height) * 2f - 1f : 0f;
        _controller.SetViewerPosition(new Vector2(nx, ny));
    }

    void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _controller?.SetHovered(null);
        _controller?.SetViewerPosition(Vector2.Zero);
    }

    void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_controller is null) return;

        if (_controller.State.CurrentLayout == LayoutMode.Detail
            || _controller.State.TargetLayout == LayoutMode.Detail)
        {
            _controller.Unfocus();
            return;
        }

        var (vx, vy, size) = ViewportCoords(e);
        if (_controller.HitTest(vx, vy, size) is int id)
            _controller.Focus(id);
    }

    public void SetLayout(LayoutMode mode) => _controller?.RequestLayout(mode);
    public void Dismiss() => _controller?.Unfocus();

    public void SetDemoMode(bool enabled)
    {
        if (_controller is null) return;
        _controller.DemoMode = enabled;
    }

    public bool IsDemoMode => _controller?.DemoMode ?? false;

    public void UpdateSettings(Func<GallerySettings, GallerySettings> mutate) =>
        _controller?.UpdateSettings(mutate);

    public GallerySettings? CurrentSettings => _controller?.State.Settings;

    public LayoutMode CurrentLayout => _controller?.State.CurrentLayout ?? LayoutMode.Grid;

    /// <summary>
    /// Replace the gallery contents with images decoded from a folder.
    /// Existing items (procedural samples or a previous folder) are cleared
    /// and disposed before the new source starts streaming items in.
    /// </summary>
    public async Task LoadFromFolderAsync(StorageFolder folder, CancellationToken ct = default)
    {
        if (_store is null) return;
        _store.Clear();
        Invalidate();
        await _store.PopulateAsync(new FolderSource(folder), ct).ConfigureAwait(true);
    }
}
