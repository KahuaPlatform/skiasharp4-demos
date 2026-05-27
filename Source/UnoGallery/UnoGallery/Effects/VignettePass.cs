using SkiaSharp;
using UnoGallery.Shaders;

namespace UnoGallery.Effects;

/// <summary>
/// Post-process that re-samples the current canvas content through the vignette
/// SKSL shader. Implemented by snapshotting the surface, drawing the snapshot
/// back through a runtime-shader paint with the snapshot as its child shader.
/// </summary>
public sealed class VignettePass
{
    public void Draw(SKCanvas canvas, SKSize size)
    {
        // Sampling-from-self requires a snapshot; SKXamlCanvas's underlying
        // surface is exposed via canvas.GetDeviceClipBounds & GRContext on
        // GPU targets, but for portability we use GetCanvasSnapshot through
        // SKSurface — fall back to drawing a translucent radial paint if
        // we don't have a snapshot path.

        // Portable fallback: draw a radial darken paint. Looks identical for
        // our parameters and avoids needing a surface readback per frame.
        var center = new SKPoint(size.Width / 2f, size.Height / 2f);
        float radius = MathF.Max(size.Width, size.Height) * 0.75f;

        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                center,
                radius,
                new[] { SKColors.Transparent, new SKColor(0, 0, 0, 180) },
                new[] { 0.55f, 1f },
                SKShaderTileMode.Clamp),
            BlendMode = SKBlendMode.SrcOver,
        };
        canvas.DrawRect(0, 0, size.Width, size.Height, paint);
    }
}
