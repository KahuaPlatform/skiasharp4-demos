using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using UnoAsteroids.Game;
using Windows.Foundation;

namespace UnoAsteroids;

public sealed class GameSurface : SKCanvasElement
{
    public GameWorld? World { get; set; }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (World is null) return;
        float w = (float)area.Width;
        float h = (float)area.Height;
        World.Resize(w, h);
        Renderer.Render(canvas, World, w, h);
    }
}
