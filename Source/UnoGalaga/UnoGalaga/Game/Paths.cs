namespace UnoGalaga.Game;

// Parametric path helpers for the wave choreography. Paths are cubic Bezier
// curves evaluated at t in [0, 1]. Each helper returns world-space position;
// the caller drives `t` over time to traverse the curve.
public static class Paths
{
    public static Vec2 Bezier3(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, float t)
    {
        float u  = 1f - t;
        float u2 = u * u;
        float t2 = t * t;
        return (u2 * u) * p0
             + (3f * u2 * t) * p1
             + (3f * u  * t2) * p2
             + (t2 * t) * p3;
    }

    // Entry path: sweep from off-screen edge, loop over the top, settle into the formation slot.
    // `fromLeft` selects which side the stream comes from — alternating sides at spawn time
    // produces Galaga's twin-stream entry rhythm.
    public static Vec2 EntryPath(Vec2 slot, bool fromLeft, float worldW, float worldH, float t)
    {
        Vec2 p0, p1, p2;
        if (fromLeft)
        {
            p0 = new Vec2(-60f,           worldH * 0.18f);
            p1 = new Vec2(worldW * 0.55f, -80f);
            p2 = new Vec2(worldW * 0.72f, slot.Y * 0.5f);
        }
        else
        {
            p0 = new Vec2(worldW + 60f,   worldH * 0.18f);
            p1 = new Vec2(worldW * 0.45f, -80f);
            p2 = new Vec2(worldW * 0.28f, slot.Y * 0.5f);
        }
        return Bezier3(p0, p1, p2, slot, t);
    }

    // Dive path: loop up-and-out from the slot (the "Immelmann" hook that makes Galaga's
    // dives feel like fighter planes), arc down past where the player was, exit off-bottom.
    // `curlSign` of +1 curls right; -1 curls left — typically chosen by slot column so the
    // pair-dive comes in from both sides of the screen.
    public static Vec2 DivePath(Vec2 origin, Vec2 playerPos, float worldH, float curlSign, float t)
    {
        var p1 = new Vec2(origin.X + curlSign *  90f, origin.Y - 60f);
        var p2 = new Vec2(playerPos.X + curlSign * 70f, playerPos.Y - 80f);
        var p3 = new Vec2(playerPos.X, worldH + 80f);
        return Bezier3(origin, p1, p2, p3, t);
    }

    // Rejoin path: post-dive, the enemy reappears above the formation and swoops back to
    // its slot with a soft S-curve. The off-screen start at y < 0 hides the "teleport"
    // from the bottom-of-screen dive exit. `curlSign` should match the dive's curl so the
    // visual flow reads continuously.
    public static Vec2 RejoinPath(Vec2 slot, float curlSign, float t)
    {
        var p0 = new Vec2(slot.X + curlSign *  90f, -90f);
        var p1 = new Vec2(slot.X - curlSign *  40f, slot.Y * 0.3f);
        var p2 = new Vec2(slot.X + curlSign *  25f, slot.Y * 0.75f);
        return Bezier3(p0, p1, p2, slot, t);
    }
}
