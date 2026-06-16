using System;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace KahuaNetwork;

/// <summary>
/// The single Skia surface the entire demo draws into. Subclasses
/// <see cref="SKCanvasElement"/> so its draws hook straight into Uno's Skia render
/// tree (no intermediate blit), and delegates each paint to <see cref="Painter"/>,
/// which <c>MainPage</c> wires to the scene+HUD render.
/// </summary>
public sealed class SceneCanvas : SKCanvasElement
{
    /// <summary>The per-frame paint callback, set by the host page.</summary>
    public Action<SKCanvas, Size>? Painter { get; set; }

    /// <summary>Invoked by the compositor each frame; forwards to <see cref="Painter"/>.</summary>
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        Painter?.Invoke(canvas, area);
    }
}
