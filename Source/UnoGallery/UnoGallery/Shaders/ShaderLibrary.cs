using System.Reflection;
using SkiaSharp;

namespace UnoGallery.Shaders;

/// <summary>
/// Loads SKSL sources from embedded resources and compiles them into runtime
/// effects. Every effect loads on both SkiaSharp 3.119.4 and SkiaSharp 4.151.0.
///
/// History: the uniforms-bearing effects used to be gated behind
/// <c>!SKIA_V4</c> because SkiaSharp <b>4.147.0-preview.3.1</b> AV'd inside
/// native <c>sk_runtimeeffect_get_uniform_byte_size</c> on the first
/// <c>SKRuntimeEffectUniforms</c> construction. That gate is gone: verified on
/// 4.151.0 (SkiaSharp 4 stable) that all six compile, bind uniforms and child
/// shaders, and render in the Uno host. Callers still null-check, so a future
/// regression degrades to the non-SKSL fallbacks rather than crashing.
///
/// If you ever pin an older 4.14x preview again, expect the AV back.
/// </summary>
public sealed class ShaderLibrary
{
    static readonly Lazy<ShaderLibrary> _instance = new(() => new ShaderLibrary());
    public static ShaderLibrary Instance => _instance.Value;

    /// <summary>
    /// Cinematic tone-grade color filter (split-tone + contrast). Has no
    /// uniforms and goes through the parameterless
    /// <see cref="SKRuntimeEffect.ToColorFilter()"/>, which is why this one
    /// survived even the 4.147-preview uniform bug.
    /// </summary>
    public SKColorFilter? ToneGrade { get; }

    /// <summary>
    /// Animated plasma background shader (curl-noise + radial fall-off, tinted
    /// by an accent uniform). Null only if compilation fails, in which case
    /// callers use the non-SKSL gradient fallback.
    /// </summary>
    public SKRuntimeEffect? AmbientPlasma { get; }

    /// <summary>
    /// Radial chromatic aberration post-pass shader. Takes a child shader
    /// (the scene to filter) and an amount uniform.
    /// </summary>
    public SKRuntimeEffect? ChromaShift { get; }

    /// <summary>Pulsing radial halo to lift the hovered tile.</summary>
    public SKRuntimeEffect? HoverGlow { get; }

    /// <summary>Noise-threshold dissolve between two scenes.</summary>
    public SKRuntimeEffect? Dissolve { get; }

    /// <summary>Circular iris reveal between two scenes.</summary>
    public SKRuntimeEffect? Iris { get; }

    /// <summary>Per-tile plasma shader (sine field + polar spiral).</summary>
    public SKRuntimeEffect? PlasmaTile { get; }

    ShaderLibrary()
    {
        ToneGrade = TryCompileColorFilter("ToneGrade.sksl");

        AmbientPlasma = TryCompileShader("Ambient.Plasma.sksl");
        ChromaShift = TryCompileShader("ChromaShift.sksl");
        HoverGlow = TryCompileShader("HoverGlow.sksl");
        Dissolve = TryCompileShader("Dissolve.sksl");
        Iris = TryCompileShader("Iris.sksl");
        PlasmaTile = TryCompileShader("PlasmaTile.sksl");
    }

    static SKColorFilter? TryCompileColorFilter(string resourceName)
    {
        try
        {
            var src = ReadEmbedded(resourceName);
            var effect = SKRuntimeEffect.CreateColorFilter(src, out string errors);
            if (effect is null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ShaderLibrary] color-filter compile failed for '{resourceName}': {errors}");
                return null;
            }
            return effect.ToColorFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ShaderLibrary] color-filter binding failed for '{resourceName}': {ex.Message}");
            return null;
        }
    }

    static SKRuntimeEffect? TryCompileShader(string resourceName)
    {
        try
        {
            var src = ReadEmbedded(resourceName);
            var effect = SKRuntimeEffect.CreateShader(src, out string errors);
            if (effect is null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ShaderLibrary] shader compile failed for '{resourceName}': {errors}");
                return null;
            }
            return effect;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ShaderLibrary] shader compile threw for '{resourceName}': {ex.Message}");
            return null;
        }
    }

    static string ReadEmbedded(string fileName)
    {
        var asm = typeof(ShaderLibrary).Assembly;
        string suffix = "Shaders." + fileName;
        var match = asm.GetManifestResourceNames()
                       .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Embedded shader '{fileName}' not found. Available: " +
                string.Join(", ", asm.GetManifestResourceNames()));
        using var stream = asm.GetManifestResourceStream(match)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
