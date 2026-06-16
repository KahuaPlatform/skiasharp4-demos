namespace Kiai;

// Per-demo thin wrapper around the shared `Arcade.Common.AmbientStarBackdrop`.
// Sits behind the stretched GameSurface and fills the whole window with the
// deep-space gradient + drifting starfield so the night-flight mood reads even
// in the radar strip's gaps. The slightly deeper blue override gives Kia'i its
// own "patrol after dusk" tint distinct from Pohaku's purple.
public sealed class BackgroundSurface : Arcade.Common.AmbientStarBackdrop
{
    protected override SkiaSharp.SKColor BgTop    => new(0x03, 0x05, 0x18);
    protected override SkiaSharp.SKColor BgBottom => new(0x0A, 0x02, 0x2A);
}
