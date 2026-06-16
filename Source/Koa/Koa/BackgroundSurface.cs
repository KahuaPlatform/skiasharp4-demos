namespace Koa;

// Per-demo thin wrapper around the shared `Arcade.Common.AmbientStarBackdrop`.
// Lives in the demo's local namespace so MainPage.xaml can reference it as
// `<local:BackgroundSurface>`. Koa retints the backdrop to a crypt-dark purple
// (the dungeon torch-lit gloom) so the drifting stars read as torch-dust motes
// rather than deep space. All actual rendering lives in the base class.
public sealed class BackgroundSurface : Arcade.Common.AmbientStarBackdrop
{
    // Crypt-dark vertical gradient: near-black violet at the top fading to a
    // slightly warmer deep purple at the floor. Matches the in-game NeonBackground
    // override in Renderer so the window side-bars and playfield share a palette.
    protected override SkiaSharp.SKColor BgTop    => new(0x0A, 0x04, 0x10);
    protected override SkiaSharp.SKColor BgBottom => new(0x1A, 0x0A, 0x24);
}
