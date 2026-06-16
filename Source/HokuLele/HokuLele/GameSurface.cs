using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using HokuLele.Game;
using Windows.Foundation;

namespace HokuLele;

/// <summary>
/// The Skia playfield element. Subclasses <see cref="SKCanvasElement"/> so draws
/// go straight into Uno's Skia composition tree; forwards each paint to the
/// static <see cref="Renderer"/> after telling the world its current size.
/// </summary>
public sealed class GameSurface : SKCanvasElement
{
    /// <summary>The world to render; set by <c>MainPage</c> on load.</summary>
    public GameWorld? World { get; set; }

    /// <summary>Per-frame paint entry point invoked by the Skia compositor.</summary>
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (World is null) return;
        float w = (float)area.Width;
        float h = (float)area.Height;
        World.Resize(w, h);
        Renderer.Render(canvas, World, w, h);
    }
}
