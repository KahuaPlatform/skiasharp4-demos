namespace UnoGallery.Models;

/// <summary>Overall render-quality preset (scales effect cost vs fidelity).</summary>
public enum QualityTier { Low, Medium, High }

/// <summary>
/// Live toggles for the post-processing effects, transitions, and profiler, plus
/// the quality tier. Carried inside <see cref="GallerySceneState"/> so the
/// pipeline reads the current settings each frame.
/// </summary>
public sealed record GallerySettings(
    bool EnableAmbientBackground,
    bool EnableVignette,
    bool EnableGrain,
    bool EnableBloom,
    bool EnableToneGrade,
    bool EnableChromaShift,
    bool EnableHoverGlow,
    bool EnableDissolveTransition,
    bool EnableIrisTransition,
    bool ShowProfiler,
    QualityTier Quality)
{
    /// <summary>The default preset: every effect on, profiler off, High quality.</summary>
    public static GallerySettings Default { get; } = new(
        EnableAmbientBackground: true,
        EnableVignette: true,
        EnableGrain: true,
        EnableBloom: true,
        EnableToneGrade: true,
        EnableChromaShift: true,
        EnableHoverGlow: true,
        EnableDissolveTransition: true,
        EnableIrisTransition: true,
        ShowProfiler: false,
        Quality: QualityTier.High);
}
