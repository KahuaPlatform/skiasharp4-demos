using System;

namespace Heiau.Game;

/// <summary>
/// Builders + collision helpers for the three concentric, counter-rotating
/// energy rings around the turret. Each ring is N independently-destructible
/// angular segments; bullets destroy a single segment and pass through any gap.
/// </summary>
public static class RingGeometry
{
    /// <summary>Angular segments in every ring.</summary>
    public const int SegmentsPerRing = 12;
    /// <summary>Half-arc of one segment, in radians (15°).</summary>
    public const float SegmentHalfArc = MathF.PI / SegmentsPerRing;

    /// <summary>
    /// Builds the three-ring layout for <paramref name="level"/> (higher = faster
    /// spin + tougher segments). Returns outer→inner.
    /// </summary>
    public static Ring[] BuildRings(int level)
    {
        // Outer / middle / inner radii (world units around a 900-wide square).
        var radii    = new[] { 220f, 160f, 100f };
        // Alternating direction; magnitude scales with level. The angular speeds
        // are intentionally inverse-with-radius — at equal angular velocity the
        // outer ring covers way more linear pixels per second and reads as
        // faster. These ratios make the inner ring visibly the quickest, which
        // matches the original arcade's feel.
        float baseSpeed = 0.35f + level * 0.05f;
        var speeds = new[]
        {
            +baseSpeed * 0.55f,   // outer — slowest
            +baseSpeed * 0.95f,   // middle — same direction as outer
            -baseSpeed * 1.70f,   // inner — fastest, counter-rotates the other two
        };
        // Distinct hue per ring so the rings read as three distinct things.
        var hues = new[] { 200f, 280f, 340f };

        // Per-segment HP scales gently with level — keeps early-game pacing brisk
        // while making higher levels demand more bullets-on-target.
        int hpPerSegment = 3 + Math.Min(2, level / 3);

        var rings = new Ring[3];
        for (int i = 0; i < 3; i++)
        {
            var hp = new int[SegmentsPerRing];
            for (int s = 0; s < SegmentsPerRing; s++) hp[s] = hpPerSegment;
            rings[i] = new Ring
            {
                Radius          = radii[i],
                Rotation        = 0f,
                AngularSpeed    = speeds[i],
                Health          = hp,
                MaxHealth       = hpPerSegment,
                HitFlash        = new float[SegmentsPerRing],
                AliveCount      = SegmentsPerRing,
                SegmentColorHue = hues[i],
            };
        }
        return rings;
    }

    /// <summary>
    /// Returns the index of the live segment that <paramref name="point"/> hits
    /// (must be near the ring radius AND inside a non-destroyed wedge), or -1.
    /// </summary>
    public static int HitSegment(Ring ring, Vec2 worldCenter, Vec2 point, float tolerance = 4f)
    {
        float dx = point.X - worldCenter.X;
        float dy = point.Y - worldCenter.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        // Hit only if we're roughly on the ring's radius (tolerance for visible width).
        if (MathF.Abs(dist - ring.Radius) > tolerance + 3f) return -1;

        float angle = MathF.Atan2(dy, dx) - ring.Rotation;
        // Snap to nearest segment index. Segment k occupies the wedge centred at
        // 2π·k/N (in ring-local angle).
        float segWidth = MathF.Tau / ring.Segments;
        float normAngle = (angle % MathF.Tau + MathF.Tau) % MathF.Tau;
        int k = (int)MathF.Round(normAngle / segWidth) % ring.Segments;

        // Confirm we're inside that segment's actual arc (not in a destroyed gap).
        float segCenter = k * segWidth;
        float delta = WrapAngle(normAngle - segCenter);
        if (MathF.Abs(delta) > SegmentHalfArc * 0.92f) return -1;
        return ring.IsAlive(k) ? k : -1;
    }

    /// <summary>Wraps an angle into [-π, π].</summary>
    public static float WrapAngle(float a)
    {
        a = (a + MathF.PI) % MathF.Tau;
        if (a < 0) a += MathF.Tau;
        return a - MathF.PI;
    }
}
