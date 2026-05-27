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

    // Entry path: enemy sweeps in from off-screen and settles into its formation slot.
    // `flightIdx` selects one of four distinct choreographies — Galaga uses 4 different
    // entry flights per stage. Each flight is mirrored on the fromLeft/fromRight halves
    // of the two-stream entry rhythm.
    public static Vec2 EntryPath(int flightIdx, Vec2 slot, bool fromLeft, float worldW, float worldH, float t)
    {
        int f = ((flightIdx % 4) + 4) % 4;
        float side = fromLeft ? 1f : -1f;
        Vec2 p0, p1, p2;

        switch (f)
        {
            case 0:  // wide arc from top corners, looping over the top of the formation
                p0 = new Vec2(fromLeft ? -60f : worldW + 60f, worldH * 0.18f);
                p1 = new Vec2(worldW * (fromLeft ? 0.55f : 0.45f), -80f);
                p2 = new Vec2(worldW * (fromLeft ? 0.72f : 0.28f), slot.Y * 0.5f);
                break;

            case 1:  // diving in from above the slot with a counter-curve hook
                p0 = new Vec2(slot.X - side * 130f, -90f);
                p1 = new Vec2(slot.X + side * 100f, slot.Y * 0.25f);
                p2 = new Vec2(slot.X - side * 55f,  slot.Y * 0.72f);
                break;

            case 2:  // barrel-roll from the opposite side, crossing over the top
                p0 = new Vec2(fromLeft ? -60f : worldW + 60f, worldH * 0.40f);
                p1 = new Vec2(worldW * (fromLeft ? 0.85f : 0.15f), -50f);
                p2 = new Vec2(worldW * (fromLeft ? 0.20f : 0.80f), slot.Y * 0.40f);
                break;

            default: // long, dramatic looping sweep — used for the final flight
                p0 = new Vec2(fromLeft ? -80f : worldW + 80f, worldH * 0.55f);
                p1 = new Vec2(worldW * (fromLeft ? 0.6f : 0.4f), -100f);
                p2 = new Vec2(worldW * 0.5f,                    slot.Y * 0.3f);
                break;
        }
        return Bezier3(p0, p1, p2, slot, t);
    }

    // Dive path: loop up-and-out from the slot (the "Immelmann" hook), arc down past
    // where the player was, exit off-bottom. `curlSign` of +1 curls right; -1 left.
    public static Vec2 DivePath(Vec2 origin, Vec2 playerPos, float worldH, float curlSign, float t)
    {
        var p1 = new Vec2(origin.X + curlSign *  90f, origin.Y - 60f);
        var p2 = new Vec2(playerPos.X + curlSign * 70f, playerPos.Y - 80f);
        var p3 = new Vec2(playerPos.X, worldH + 80f);
        return Bezier3(origin, p1, p2, p3, t);
    }

    // Rejoin path: post-dive, the enemy reappears above the formation and swoops back
    // to its slot with a soft S-curve.
    public static Vec2 RejoinPath(Vec2 slot, float curlSign, float t)
    {
        var p0 = new Vec2(slot.X + curlSign *  90f, -90f);
        var p1 = new Vec2(slot.X - curlSign *  40f, slot.Y * 0.3f);
        var p2 = new Vec2(slot.X + curlSign *  25f, slot.Y * 0.75f);
        return Bezier3(p0, p1, p2, slot, t);
    }

    // Challenge-stage flythrough path. Galaga has four distinct challenge-stage
    // choreographies (stages 3, 7, 11, 15 then repeat at 19, 23, 27, 31, ...).
    // `stagePattern` picks one of the four; `subIdx` (0..7) selects a sub-variant
    // within that pattern so the 40 enemies don't all retrace the same arc.
    public static Vec2 ChallengePath(int stagePattern, int subIdx, float worldW, float worldH, float t)
    {
        int p = ((stagePattern % 4) + 4) % 4;
        int s = ((subIdx % 8) + 8) % 8;
        bool fromLeft = s < 4;
        int sub = s % 4;
        float sideSign = fromLeft ? 1f : -1f;

        Vec2 p0 = default, p1 = default, p2 = default, p3 = default;
        float[] entryY = { 0.12f, 0.22f, 0.32f, 0.42f };

        switch (p)
        {
            case 0:  // S-CURVES — enter from sides, weave through, exit bottom-center
            {
                float startY = worldH * entryY[sub];
                if (fromLeft)
                {
                    p0 = new Vec2(-60f, startY);
                    p1 = new Vec2(worldW * 0.65f, worldH * 0.25f);
                    p2 = new Vec2(worldW * 0.25f, worldH * 0.65f);
                    p3 = new Vec2(worldW * 0.50f + sub * 40f, worldH + 80f);
                }
                else
                {
                    p0 = new Vec2(worldW + 60f, startY);
                    p1 = new Vec2(worldW * 0.35f, worldH * 0.25f);
                    p2 = new Vec2(worldW * 0.75f, worldH * 0.65f);
                    p3 = new Vec2(worldW * 0.50f - sub * 40f, worldH + 80f);
                }
                break;
            }

            case 1:  // LOOPS — drop from top, loop in one direction, exit at varied spots
            {
                float spread = worldW * (0.22f + sub * 0.12f);
                p0 = new Vec2(worldW * 0.5f - sideSign * 60f, -50f);
                p1 = new Vec2(worldW * 0.5f + sideSign * spread, worldH * 0.20f);
                p2 = new Vec2(worldW * 0.5f - sideSign * spread * 0.7f, worldH * 0.55f);
                p3 = new Vec2(worldW * 0.5f + sideSign * spread * 1.1f, worldH + 80f);
                break;
            }

            case 2:  // SPIRALS — wide centre-line arc, alternating sides
            {
                float arc = worldW * (0.30f + sub * 0.10f);
                p0 = new Vec2(worldW * 0.5f + sideSign * 40f, -50f);
                p1 = new Vec2(worldW * 0.5f - sideSign * arc, worldH * 0.30f);
                p2 = new Vec2(worldW * 0.5f + sideSign * arc, worldH * 0.70f);
                p3 = new Vec2(worldW * 0.5f - sideSign * arc * 0.4f, worldH + 80f);
                break;
            }

            default: // DIAGONAL SWEEPS WITH VERTICAL DROPS
            {
                if (fromLeft)
                {
                    p0 = new Vec2(-60f, worldH * 0.10f);
                    p1 = new Vec2(worldW * 0.90f, worldH * 0.32f);
                    p2 = new Vec2(worldW * 0.85f - sub * 50f, worldH * 0.55f);
                    p3 = new Vec2(worldW * 0.85f - sub * 50f, worldH + 80f);
                }
                else
                {
                    p0 = new Vec2(worldW + 60f, worldH * 0.10f);
                    p1 = new Vec2(worldW * 0.10f, worldH * 0.32f);
                    p2 = new Vec2(worldW * 0.15f + sub * 50f, worldH * 0.55f);
                    p3 = new Vec2(worldW * 0.15f + sub * 50f, worldH + 80f);
                }
                break;
            }
        }
        return Bezier3(p0, p1, p2, p3, t);
    }

    // Mystery flyby: a special enemy traverses the top of the screen in a straight line
    // from one side to the other. Galaga doesn't actually do this — it's borrowed from
    // Space Invaders' UFO — but it adds an arcade-flavour bonus target.
    public static Vec2 FlybyPath(bool fromLeft, float worldW, float t)
    {
        const float Y = 90f;
        float startX = fromLeft ? -50f : worldW + 50f;
        float endX   = fromLeft ?  worldW + 50f : -50f;
        return new Vec2(startX + (endX - startX) * t, Y);
    }
}
