using System;
using System.Collections.Generic;

namespace Lua.Game;

// A "well" is the iconic Tempest playfield: an open or closed polyline of N segments
// projected from the rim (camera plane) toward a vanishing point in the distance.
// Each level uses a different well shape.
//
// Coordinates:
//   World space is GameWorld.Width × GameWorld.Height. The well is centred at
//   (Center.X, Center.Y). RimPoints are world-space vertex positions at the rim
//   (z = 0). The vanishing point sits at Center, scaled inward by Project().
//   Depth runs 0..1 where 0 = rim (closest, drawn largest) and 1 = far end
//   (vanishing point).
//
// Segments:
//   N rim vertices form N-1 (open) or N (closed) segments. Segment i is the polyline
//   edge between RimPoints[i] and RimPoints[(i+1) % N].
/// <summary>
/// The Tempest playfield "well": a rim polyline projected toward a central
/// vanishing point. Provides the perspective projection plus the per-segment
/// geometry queries (midpoint, direction, normal, length, neighbour stepping)
/// the player and enemies use to walk and climb the tube.
/// </summary>
public sealed class Well
{
    /// <summary>World-space center; also the vanishing point.</summary>
    public Vec2 Center;
    /// <summary>Rim (z=0) vertex positions in world space.</summary>
    public Vec2[] RimPoints = Array.Empty<Vec2>();
    /// <summary>True if the rim wraps (player can circle); false for open shapes.</summary>
    public bool Closed;
    /// <summary>Which shape this well was built as.</summary>
    public WellShape Shape;
    /// <summary>Number of segments (N for closed, N-1 for open).</summary>
    public int SegmentCount => Closed ? RimPoints.Length : RimPoints.Length - 1;

    /// <summary>
    /// Projects a rim point inward by depth <paramref name="z"/> (0 = rim, 1 =
    /// vanishing point). The perspective curve <c>1/(1+k·z)</c> gives the strong
    /// tunnel-in effect.
    /// </summary>
    public Vec2 Project(Vec2 rim, float z)
    {
        float zClamped = Math.Clamp(z, 0f, 1f);
        float s = 1f / (1f + zClamped * PerspectiveK);
        return new Vec2(Center.X + (rim.X - Center.X) * s,
                        Center.Y + (rim.Y - Center.Y) * s);
    }

    /// <summary>Projects rim vertex <paramref name="i"/> at depth <paramref name="z"/>.</summary>
    public Vec2 ProjectVertex(int i, float z) => Project(RimPoints[i], z);

    /// <summary>World-space midpoint of segment <paramref name="s"/> at depth <paramref name="z"/>.</summary>
    public Vec2 SegmentMid(int s, float z)
    {
        var a = RimPoints[s];
        var b = RimPoints[(s + 1) % RimPoints.Length];
        var mid = new Vec2((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);
        return Project(mid, z);
    }

    /// <summary>Unit direction along segment <paramref name="s"/> at the rim (vertex s → s+1).</summary>
    public Vec2 SegmentDir(int s)
    {
        var a = RimPoints[s];
        var b = RimPoints[(s + 1) % RimPoints.Length];
        return new Vec2(b.X - a.X, b.Y - a.Y).Normalized();
    }

    /// <summary>
    /// Outward-facing unit normal at segment <paramref name="s"/> (points away
    /// from <see cref="Center"/>). Used to anchor the player claw + enemies on the rim.
    /// </summary>
    public Vec2 SegmentNormal(int s)
    {
        var mid = (RimPoints[s] + RimPoints[(s + 1) % RimPoints.Length]) * 0.5f;
        return (mid - Center).Normalized();
    }

    /// <summary>Rim length of segment <paramref name="s"/>.</summary>
    public float SegmentLength(int s)
    {
        var a = RimPoints[s];
        var b = RimPoints[(s + 1) % RimPoints.Length];
        return (b - a).Length;
    }

    /// <summary>
    /// Steps a segment index by <paramref name="delta"/> applying the well's
    /// open/closed wrap rules. Returns the new index, or -1 if the move would step
    /// off an open end.
    /// </summary>
    public int Step(int seg, int delta)
    {
        int n = SegmentCount;
        int next = seg + delta;
        if (Closed) return ((next % n) + n) % n;
        if (next < 0 || next >= n) return -1;
        return next;
    }

    public const float PerspectiveK = 4.0f; // strong tunnel-in effect

    /// <summary>
    /// True for odd segments; the renderer paints alternate segments a different
    /// cool color for retro contrast.
    /// </summary>
    public bool IsAlternateSlot(int s) => (s & 1) == 1;
}

/// <summary>The ten Tempest well silhouettes cycled per level.</summary>
public enum WellShape
{
    Circle,        // closed 16-gon
    Square,        // closed square with 4 vertices per side
    Plus,          // open cross / plus
    V,             // open V (deep valley)
    Bowtie,        // open figure-eight pinch
    Step,          // open staircase
    Triangle,      // closed triangle
    Heart,         // closed heart-ish
    Trapezoid,     // closed trapezoid
    InfinityLoop,  // open infinity sign
}

/// <summary>Factory for the ten well shapes plus the per-level shape schedule.</summary>
public static class Wells
{
    /// <summary>The order shapes cycle through as levels advance.</summary>
    public static readonly WellShape[] LevelOrder =
    {
        WellShape.Circle,
        WellShape.Square,
        WellShape.Plus,
        WellShape.Bowtie,
        WellShape.Triangle,
        WellShape.V,
        WellShape.Trapezoid,
        WellShape.Step,
        WellShape.Heart,
        WellShape.InfinityLoop,
    };

    /// <summary>Returns the well shape for a 1-based <paramref name="level"/> (wraps).</summary>
    public static WellShape ForLevel(int level) =>
        LevelOrder[Math.Max(0, level - 1) % LevelOrder.Length];

    /// <summary>Builds a well of the given <paramref name="shape"/> centered at (cx, cy) with base radius r.</summary>
    public static Well Build(WellShape shape, float cx, float cy, float r)
    {
        var w = new Well { Center = new Vec2(cx, cy), Shape = shape };
        switch (shape)
        {
            case WellShape.Circle:        BuildCircle(w, r);        break;
            case WellShape.Square:        BuildSquare(w, r);        break;
            case WellShape.Plus:          BuildPlus(w, r);          break;
            case WellShape.V:             BuildV(w, r);             break;
            case WellShape.Bowtie:        BuildBowtie(w, r);        break;
            case WellShape.Step:          BuildStep(w, r);          break;
            case WellShape.Triangle:      BuildTriangle(w, r);      break;
            case WellShape.Heart:         BuildHeart(w, r);         break;
            case WellShape.Trapezoid:     BuildTrapezoid(w, r);     break;
            case WellShape.InfinityLoop:  BuildInfinity(w, r);      break;
        }
        return w;
    }

    static void BuildCircle(Well w, float r)
    {
        const int N = 16;
        var pts = new Vec2[N];
        // Start the seam at the top so two segments straddle the vertical axis —
        // matches Tempest's classic look where the player can sit at "12 o'clock".
        for (int i = 0; i < N; i++)
        {
            float ang = (float)(-Math.PI / 2.0 + 2.0 * Math.PI * i / N);
            pts[i] = new Vec2(w.Center.X + r * (float)Math.Cos(ang),
                              w.Center.Y + r * (float)Math.Sin(ang));
        }
        w.RimPoints = pts;
        w.Closed = true;
    }

    static void BuildSquare(Well w, float r)
    {
        // 4 vertices per side, 16 total — gives 16 segments like Circle.
        const int perSide = 4;
        var pts = new Vec2[perSide * 4];
        float s = r * 0.95f; // square inscribes outside the unit circle
        var corners = new[]
        {
            new Vec2(-s, -s), new Vec2( s, -s),
            new Vec2( s,  s), new Vec2(-s,  s),
        };
        int k = 0;
        for (int c = 0; c < 4; c++)
        {
            var a = corners[c];
            var b = corners[(c + 1) % 4];
            for (int i = 0; i < perSide; i++)
            {
                float t = (float)i / perSide;
                pts[k++] = new Vec2(w.Center.X + a.X + (b.X - a.X) * t,
                                    w.Center.Y + a.Y + (b.Y - a.Y) * t);
            }
        }
        w.RimPoints = pts;
        w.Closed = true;
    }

    static void BuildPlus(Well w, float r)
    {
        // Cross / plus: open polyline, traced as 13 vertices forming the outline of
        // a single arm doubled — keeps it open so the player can't wrap.
        // Use a stylised "U" that looks like a plus when rendered with depth.
        float a = r * 0.40f;
        float b = r * 0.95f;
        // Trace top arm down to bottom, mirroring through 13 points.
        var pts = new[]
        {
            new Vec2(-a, -b), new Vec2(-a, -a), new Vec2(-b, -a),
            new Vec2(-b,  a), new Vec2(-a,  a), new Vec2(-a,  b),
            new Vec2( a,  b),
            new Vec2( a,  a), new Vec2( b,  a),
            new Vec2( b, -a), new Vec2( a, -a), new Vec2( a, -b),
            new Vec2(-a, -b),
        };
        for (int i = 0; i < pts.Length; i++)
        {
            pts[i] = new Vec2(w.Center.X + pts[i].X, w.Center.Y + pts[i].Y);
        }
        w.RimPoints = pts;
        w.Closed = true;
    }

    static void BuildV(Well w, float r)
    {
        // Deep V open at the top — 13 vertices in a sawtooth descent.
        const int N = 13;
        var pts = new Vec2[N];
        float wHalf = r * 1.05f;
        for (int i = 0; i < N; i++)
        {
            float t = (float)i / (N - 1);              // 0..1 across mouth
            float x = -wHalf + 2 * wHalf * t;
            float y = -r * 0.45f + (1f - Math.Abs(0.5f - t) * 2f) * r * 0.95f;
            pts[i] = new Vec2(w.Center.X + x, w.Center.Y + y);
        }
        w.RimPoints = pts;
        w.Closed = false;
    }

    static void BuildBowtie(Well w, float r)
    {
        // Pinched bowtie / hourglass — crosses through the centre.
        var pts = new[]
        {
            new Vec2(-r,    -r * 0.8f), new Vec2(-r * 0.7f, -r * 0.5f),
            new Vec2(-r * 0.35f, -r * 0.15f), new Vec2(0f, 0f),
            new Vec2( r * 0.35f, -r * 0.15f), new Vec2( r * 0.7f, -r * 0.5f),
            new Vec2( r,    -r * 0.8f),
            new Vec2( r,     r * 0.8f), new Vec2( r * 0.7f,  r * 0.5f),
            new Vec2( r * 0.35f,  r * 0.15f), new Vec2(0f, 0f),
            new Vec2(-r * 0.35f,  r * 0.15f), new Vec2(-r * 0.7f,  r * 0.5f),
            new Vec2(-r,     r * 0.8f),
        };
        for (int i = 0; i < pts.Length; i++)
            pts[i] = new Vec2(w.Center.X + pts[i].X, w.Center.Y + pts[i].Y);
        w.RimPoints = pts;
        w.Closed = false;
    }

    static void BuildStep(Well w, float r)
    {
        // Staircase: zig-zags across the screen, open ends.
        const int steps = 7;
        var pts = new List<Vec2>();
        float xStart = -r * 0.95f;
        float xStep = (r * 1.9f) / steps;
        float yLow  = -r * 0.55f;
        float yHigh =  r * 0.55f;
        for (int i = 0; i <= steps; i++)
        {
            float x = xStart + xStep * i;
            float y = (i % 2 == 0) ? yLow : yHigh;
            pts.Add(new Vec2(x, y));
            if (i < steps)
            {
                pts.Add(new Vec2(x + xStep, y));
            }
        }
        for (int i = 0; i < pts.Count; i++)
            pts[i] = new Vec2(w.Center.X + pts[i].X, w.Center.Y + pts[i].Y);
        w.RimPoints = pts.ToArray();
        w.Closed = false;
    }

    static void BuildTriangle(Well w, float r)
    {
        // Closed triangle, 4 verts per side -> 12 segments. Apex points up.
        const int perSide = 4;
        var pts = new Vec2[perSide * 3];
        var corners = new[]
        {
            new Vec2(0f, -r),
            new Vec2( r * 0.95f,  r * 0.6f),
            new Vec2(-r * 0.95f,  r * 0.6f),
        };
        int k = 0;
        for (int c = 0; c < 3; c++)
        {
            var a = corners[c];
            var b = corners[(c + 1) % 3];
            for (int i = 0; i < perSide; i++)
            {
                float t = (float)i / perSide;
                pts[k++] = new Vec2(w.Center.X + a.X + (b.X - a.X) * t,
                                    w.Center.Y + a.Y + (b.Y - a.Y) * t);
            }
        }
        w.RimPoints = pts;
        w.Closed = true;
    }

    static void BuildHeart(Well w, float r)
    {
        const int N = 14;
        var pts = new Vec2[N];
        // Parametric heart: (16 sin^3 t, 13 cos t - 5 cos 2t - 2 cos 3t - cos 4t) / 16
        for (int i = 0; i < N; i++)
        {
            float t = (float)(2.0 * Math.PI * i / N) - (float)Math.PI / 2f;
            float x = 16f * (float)Math.Pow(Math.Sin(t), 3);
            float y = 13f * (float)Math.Cos(t)
                    - 5f  * (float)Math.Cos(2 * t)
                    - 2f  * (float)Math.Cos(3 * t)
                    -        (float)Math.Cos(4 * t);
            // Heart formula puts +y "up"; we want screen-space (+y down), so invert.
            pts[i] = new Vec2(w.Center.X + x * (r / 16f), w.Center.Y - y * (r / 16f));
        }
        w.RimPoints = pts;
        w.Closed = true;
    }

    static void BuildTrapezoid(Well w, float r)
    {
        const int perSide = 4;
        var pts = new Vec2[perSide * 4];
        var corners = new[]
        {
            new Vec2(-r * 0.55f, -r * 0.9f),
            new Vec2( r * 0.55f, -r * 0.9f),
            new Vec2( r * 0.95f,  r * 0.9f),
            new Vec2(-r * 0.95f,  r * 0.9f),
        };
        int k = 0;
        for (int c = 0; c < 4; c++)
        {
            var a = corners[c];
            var b = corners[(c + 1) % 4];
            for (int i = 0; i < perSide; i++)
            {
                float t = (float)i / perSide;
                pts[k++] = new Vec2(w.Center.X + a.X + (b.X - a.X) * t,
                                    w.Center.Y + a.Y + (b.Y - a.Y) * t);
            }
        }
        w.RimPoints = pts;
        w.Closed = true;
    }

    static void BuildInfinity(Well w, float r)
    {
        // Lemniscate-ish infinity sign open at both ends.
        const int N = 17;
        var pts = new Vec2[N];
        for (int i = 0; i < N; i++)
        {
            float t = (float)i / (N - 1);             // 0..1 across the curve
            float ang = -(float)Math.PI + 2f * (float)Math.PI * t;
            float denom = 1f + (float)(Math.Sin(ang) * Math.Sin(ang));
            float x =  r * (float)Math.Cos(ang) / denom;
            float y =  r * (float)(Math.Sin(ang) * Math.Cos(ang)) / denom * 0.9f;
            pts[i] = new Vec2(w.Center.X + x, w.Center.Y + y);
        }
        w.RimPoints = pts;
        w.Closed = false;
    }
}
