using System.Reflection;
using SkiaSharp;

namespace UnoGallery.Shaders;

/// <summary>
/// Loads SKSL sources from embedded resources and compiles them into runtime
/// effects. Effects with uniforms are loaded only on SkiaSharp 3 — the v4.147
/// preview AVs inside <c>SKRuntimeEffectUniforms..ctor</c> on first use, so on
/// v4 the relevant getters return null and callers fall back to non-SKSL
/// Skia primitives.
///
/// <see cref="ToneGrade"/> uses the zero-uniforms / parameterless
/// <c>ToColorFilter()</c> path and works on both versions.
/// </summary>
public sealed class ShaderLibrary
{
    static readonly Lazy<ShaderLibrary> _instance = new(() => new ShaderLibrary());
    public static ShaderLibrary Instance => _instance.Value;

    /// <summary>
    /// Cinematic tone-grade color filter (split-tone + contrast). Works on
    /// both SkiaSharp 3 and 4.147-preview because it has no uniforms and
    /// goes through the parameterless <see cref="SKRuntimeEffect.ToColorFilter()"/>.
    /// </summary>
    public SKColorFilter? ToneGrade { get; }

    /// <summary>
    /// Animated plasma background shader (curl-noise + radial fall-off, tinted
    /// by an accent uniform). Null on SkiaSharp 4.147-preview because the
    /// runtime-effect uniforms path AVs in native; callers should use the
    /// non-SKSL gradient fallback in that case.
    /// </summary>
    public SKRuntimeEffect? AmbientPlasma { get; }

    /// <summary>
    /// Radial chromatic aberration post-pass shader. Takes a child shader
    /// (the scene to filter) and an amount uniform. Null on v4.147-preview.
    /// </summary>
    public SKRuntimeEffect? ChromaShift { get; }

    /// <summary>Pulsing radial halo to lift the hovered tile. Null on v4.147-preview.</summary>
    public SKRuntimeEffect? HoverGlow { get; }

    /// <summary>Noise-threshold dissolve between two scenes. Null on v4.147-preview.</summary>
    public SKRuntimeEffect? Dissolve { get; }

    /// <summary>Circular iris reveal between two scenes. Null on v4.147-preview.</summary>
    public SKRuntimeEffect? Iris { get; }

    /// <summary>Per-tile plasma shader (sine field + polar spiral). Null on v4.147-preview.</summary>
    public SKRuntimeEffect? PlasmaTile { get; }

    ShaderLibrary()
    {
        ToneGrade = TryCompileColorFilter("ToneGrade.sksl");

#if SKIA_V4
        AmbientPlasma = null; // v4.147-preview: uniforms path crashes
        ChromaShift = null;
        HoverGlow = null;
        Dissolve = null;
        Iris = null;
        PlasmaTile = null;
#else
        AmbientPlasma = TryCompileShader("Ambient.Plasma.sksl");
        ChromaShift = TryCompileShader("ChromaShift.sksl");
        HoverGlow = TryCompileShader("HoverGlow.sksl");
        Dissolve = TryCompileShader("Dissolve.sksl");
        Iris = TryCompileShader("Iris.sksl");
        PlasmaTile = TryCompileShader("PlasmaTile.sksl");
#endif
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
