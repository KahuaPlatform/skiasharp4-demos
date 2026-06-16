namespace HokuLele;

/// <summary>
/// Per-demo thin wrapper around the shared <see cref="Arcade.Common.AmbientStarBackdrop"/>
/// so the page can reference <c>&lt;local:BackgroundSurface&gt;</c> in XAML. All
/// rendering lives in the base class.
/// </summary>
public sealed class BackgroundSurface : Arcade.Common.AmbientStarBackdrop { }
