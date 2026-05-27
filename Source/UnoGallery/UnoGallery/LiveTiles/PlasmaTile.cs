using SkiaSharp;
using UnoGallery.Shaders;

namespace UnoGallery.LiveTiles;

/// <summary>
/// SKSL plasma running per-tile (distinct from the canvas-wide ambient
/// plasma background — different shader, different uniforms, different
/// pace). When the shader fails to compile (SkiaSharp 4 preview crash on
/// uniforms), falls back to a moving radial gradient so the tile still
/// animates.
/// </summary>
public sealed class PlasmaTile : ILiveTile
{
    public string Caption => "Plasma";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(15, 5, 45),
        new SKColor(110, 30, 200),
        new SKColor(255, 90, 200),
        new SKColor(255, 220, 110));

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        var effect = ShaderLibrary.Instance.PlasmaTile;
        if (effect is null)
        {
            DrawFallback(canvas, dest, t);
            return;
        }

        var ca = Palette[1];
        var cb = Palette[2];

        using var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["iTime"]   = t,
            ["iSize"]   = new[] { dest.Width, dest.Height },
            ["iOffset"] = new[] { dest.Left, dest.Top },
            ["iColorA"] = new[] { ca.Red / 255f, ca.Green / 255f, ca.Blue / 255f, 1f },
            ["iColorB"] = new[] { cb.Red / 255f, cb.Green / 255f, cb.Blue / 255f, 1f },
        };
        using var shader = effect.ToShader(uniforms);
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(dest, paint);
    }

    void DrawFallback(SKCanvas canvas, SKRect dest, float t)
    {
        using var bg = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bg);

        float dx = dest.Width * 0.25f * MathF.Sin(t * 0.8f);
        float dy = dest.Height * 0.25f * MathF.Cos(t * 1.1f);
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(dest.MidX + dx, dest.MidY + dy),
                MathF.Max(dest.Width, dest.Height) * 0.75f,
                new[] { Palette[2], Palette[1], Palette[0] },
                new[] { 0f, 0.55f, 1f },
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(dest, paint);
    }
}
