using SkiaSharp;
using UnoGallery.Audio;
using UnoGallery.Models;
using UnoGallery.Shaders;

namespace UnoGallery.Effects;

/// <summary>
/// Animated ambient background. Two implementations:
///
///  - <b>SKSL plasma</b> — animated curl-noise tinted by the focused tile's
///    accent palette. Used whenever <see cref="ShaderLibrary.AmbientPlasma"/>
///    compiled, which is the normal case on both SkiaSharp 3.119.4 and 4.151.0.
///    This is the intended look.
///  - <b>Dual radial gradient</b> — fallback for when the SKSL runtime-effect
///    path is unavailable (it was on SkiaSharp 4.147-preview, which AV'd in
///    <c>SKRuntimeEffectUniforms..ctor</c>). Two counter-orbiting tinted
///    glows over a near-black base. Reads as plasma-adjacent at a glance.
/// </summary>
public sealed class BackgroundPass
{
    public void Draw(SKCanvas canvas, SKSize size, GallerySceneState state)
    {
        if (!state.Settings.EnableAmbientBackground)
        {
            canvas.Clear(new SKColor(10, 12, 18));
            return;
        }

        var accent = PickAccent(state);
        var plasma = ShaderLibrary.Instance.AmbientPlasma;
        float pulse = AudioSourceManager.Instance.Analyzer.Pulse;

        if (plasma is not null)
            DrawPlasma(canvas, size, state.WallClockSeconds, accent, plasma, pulse);
        else
            DrawGradientFallback(canvas, size, state.WallClockSeconds, accent, pulse);
    }

    static void DrawPlasma(SKCanvas canvas, SKSize size, float t, SKColor accent, SKRuntimeEffect effect, float pulse)
    {
        // iIntensity is bumped by the audio beat pulse — peaks at 1.7x on a
        // kick, decays smoothly back to 1.0.
        float intensity = 1.0f + pulse * 0.7f;
        using var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["iTime"] = t,
            ["iResolution"] = new[] { size.Width, size.Height },
            ["iAccent"] = new[]
            {
                accent.Red   / 255f,
                accent.Green / 255f,
                accent.Blue  / 255f,
                1f,
            },
            ["iIntensity"] = intensity,
        };
        using var shader = effect.ToShader(uniforms);
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, 0, size.Width, size.Height, paint);
    }

    static void DrawGradientFallback(SKCanvas canvas, SKSize size, float t, SKColor accent, float pulse)
    {
        canvas.Clear(new SKColor(6, 8, 14));

        // Low-frequency turbulence colourised through the accent.
        using (var noisePaint = new SKPaint
        {
            Shader = SKShader.CreatePerlinNoiseTurbulence(0.0035f, 0.0035f, 2, t * 0.6f),
            ColorFilter = SKColorFilter.CreateBlendMode(
                accent.WithAlpha(110),
                SKBlendMode.Modulate),
            BlendMode = SKBlendMode.Screen,
        })
        {
            canvas.DrawRect(0, 0, size.Width, size.Height, noisePaint);
        }

        var c1 = new SKPoint(
            size.Width  * (0.5f + 0.20f * MathF.Sin(t * 0.13f)),
            size.Height * (0.5f + 0.16f * MathF.Cos(t * 0.11f)));
        byte glowAlpha = (byte)Math.Clamp(95f + pulse * 130f, 0f, 255f);
        float glowRadius = MathF.Max(size.Width, size.Height) * (0.7f + pulse * 0.20f);
        using (var glow = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                c1,
                glowRadius,
                new[] { accent.WithAlpha(glowAlpha), new SKColor(6, 8, 14, 0) },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp),
            BlendMode = SKBlendMode.Plus,
        })
        {
            canvas.DrawRect(0, 0, size.Width, size.Height, glow);
        }

        var c2 = new SKPoint(
            size.Width  * (0.5f - 0.25f * MathF.Sin(t * 0.09f + 1.7f)),
            size.Height * (0.5f - 0.18f * MathF.Cos(t * 0.07f + 0.3f)));
        using (var cool = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                c2,
                MathF.Max(size.Width, size.Height) * 0.6f,
                new[] { new SKColor(30, 70, 180, 65), new SKColor(6, 8, 14, 0) },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp),
            BlendMode = SKBlendMode.Plus,
        })
        {
            canvas.DrawRect(0, 0, size.Width, size.Height, cool);
        }
    }

    static SKColor PickAccent(GallerySceneState state)
    {
        if (state.Items.IsDefaultOrEmpty) return new SKColor(80, 100, 200);
        var key = state.HoveredItemId ?? state.FocusedItemId ?? state.Items[0].Id;
        for (int i = 0; i < state.Items.Length; i++)
        {
            if (state.Items[i].Id == key)
                return state.Items[i].Palette[Math.Min(2, state.Items[i].Palette.Length - 1)];
        }
        return state.Items[0].Palette[Math.Min(2, state.Items[0].Palette.Length - 1)];
    }
}
