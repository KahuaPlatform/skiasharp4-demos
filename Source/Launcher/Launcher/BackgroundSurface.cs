namespace Launcher;

// Per-demo thin wrapper around the shared `Arcade.Common.AmbientStarBackdrop`.
// Lives in the demo's local namespace so MainPage.xaml can reference it as
// `<local:BackgroundSurface>`. All actual rendering lives in the base class.
public sealed class BackgroundSurface : Arcade.Common.AmbientStarBackdrop { }
