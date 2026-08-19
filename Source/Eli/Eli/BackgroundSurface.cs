namespace Eli;

// Per-demo thin wrapper around the shared `Arcade.Common.AmbientStarBackdrop`.
// Lives in the demo's local namespace so MainPage.xaml can reference it as
// `<local:BackgroundSurface>`. Eli retints the backdrop to underground browns so
// the drifting stars read as dust motes hanging in the cavern air rather than as
// deep space. All actual rendering lives in the base class.
public sealed class BackgroundSurface : Arcade.Common.AmbientStarBackdrop
{
    // Underground vertical gradient: near-black soil at the top warming to a
    // lamp-lit earth tone below. Matches the in-game NeonBackground override in
    // Renderer so the window side-bars and the field share a palette.
    protected override SkiaSharp.SKColor BgTop    => new(0x0B, 0x06, 0x03);
    protected override SkiaSharp.SKColor BgBottom => new(0x24, 0x14, 0x08);
}
