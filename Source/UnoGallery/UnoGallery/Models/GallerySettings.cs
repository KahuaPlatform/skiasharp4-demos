namespace UnoGallery.Models;

public enum QualityTier { Low, Medium, High }

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
