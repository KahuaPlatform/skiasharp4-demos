using System;
using SkiaSharp;

namespace Launcher.Game;

// Launcher state machine. There's no actual gameplay here — just a render loop
// to drive the marquee + hue-cycling title + ambient backdrop. Pointer position
// is tracked so the renderer can highlight the card under the cursor.
public enum LauncherTheme
{
    Neon,          // Default arcade catalog look — deep-space gradient + glowing card frames.
    BobRoss,       // Painted Hawaiian sunset — pastel sky, ocean waves, palm silhouettes,
                   // distant mountains, and cream-parchment cards. Toggled via the T key.
}

public sealed class LauncherWorld
{
    public const float WorldW = 1280f;
    public const float WorldH = 720f;
    public float Width  => WorldW;
    public float Height => WorldH;

    public float PointerX;
    public float PointerY;
    public int   HoverIndex = -1;
    public int   PressedIndex = -1;
    public LauncherTheme Theme = LauncherTheme.Neon;

    // Set by the renderer each frame so the surface knows where the card
    // hit-rects ended up after layout. Surface uses these to update HoverIndex.
    public SKRect[] CardRects = Array.Empty<SKRect>();

    public void Update(float dt)
    {
        // Pure visual update — nothing to integrate. Hover/press state is
        // driven externally by pointer events.
    }
}
