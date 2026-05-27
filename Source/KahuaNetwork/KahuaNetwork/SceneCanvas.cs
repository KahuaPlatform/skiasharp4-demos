using System;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace KahuaNetwork;

public sealed class SceneCanvas : SKCanvasElement
{
    public Action<SKCanvas, Size>? Painter { get; set; }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        Painter?.Invoke(canvas, area);
    }
}
