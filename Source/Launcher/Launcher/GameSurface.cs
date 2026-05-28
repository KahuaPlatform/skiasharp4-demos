using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Launcher.Game;
using Windows.Foundation;

namespace Launcher;

public sealed class GameSurface : SKCanvasElement
{
    public LauncherWorld? World { get; set; }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (World is null) return;
        Renderer.Render(canvas, World, (float)area.Width, (float)area.Height);
    }
}
