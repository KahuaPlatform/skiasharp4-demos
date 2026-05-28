using System;
using System.Collections.Generic;

namespace Mahina.Game;

// Builds randomized lunar surface terrain — a midpoint-displaced polyline along
// the bottom of the world, with N flat landing pads of varying widths inserted
// in place of the natural slope at chosen X positions.
//
// Returned terrain is in world coordinates (smaller Y = higher in the air,
// larger Y = ground level).
public static class TerrainBuilder
{
    // Multiplier choices and their pad widths (in world pixels). Narrower pads
    // are worth more points — matches the original arcade's risk/reward.
    static readonly (int multiplier, float widthPx)[] PadMenu =
    {
        (5, 38f),   // narrow / expert
        (3, 60f),   // medium
        (2, 100f),  // wide / safe
    };

    public static Terrain Build(int level, float worldW, float worldH, Random rng)
    {
        const int Resolution = 64;
        var pts = new Vec2[Resolution + 1];

        // Base elevation curve via midpoint displacement gives a "lunar" jagged feel.
        float baseY = worldH * 0.78f;
        float amp   = worldH * 0.18f * MathF.Min(1.4f, 0.7f + level * 0.08f);
        var heights = new float[Resolution + 1];
        heights[0]            = baseY + (float)(rng.NextDouble() - 0.5) * amp * 0.3f;
        heights[Resolution]   = baseY + (float)(rng.NextDouble() - 0.5) * amp * 0.3f;
        Displace(heights, 0, Resolution, amp, rng);

        for (int i = 0; i <= Resolution; i++)
        {
            float x = (float)i / Resolution * worldW;
            pts[i] = new Vec2(x, heights[i]);
        }

        // Pick pads. Difficulty curve:
        //   levels 1-2:  2 pads (2x, 3x)
        //   levels 3-4:  3 pads (2x, 3x, 5x)
        //   levels 5+ :  3 pads (3x, 3x, 5x)  — no easy wide pad
        var padMix = level switch
        {
            <= 2 => new[] { PadMenu[2], PadMenu[1] },           // wide + medium
            <= 4 => new[] { PadMenu[2], PadMenu[1], PadMenu[0] }, // wide + medium + narrow
            _    => new[] { PadMenu[1], PadMenu[1], PadMenu[0] }, // medium + medium + narrow
        };

        // Shuffle so the pad order is randomised across levels.
        for (int i = padMix.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (padMix[i], padMix[j]) = (padMix[j], padMix[i]);
        }

        var pads = new List<LandingPad>();
        float slotWidth = worldW / padMix.Length;
        for (int k = 0; k < padMix.Length; k++)
        {
            float slotStart = slotWidth * k + 40f;
            float slotEnd   = slotWidth * (k + 1) - 40f;
            float center = slotStart + (float)rng.NextDouble() * (slotEnd - slotStart);
            float halfW  = padMix[k].widthPx * 0.5f;

            // Clamp pad bounds so it doesn't cross slot boundaries (so adjacent
            // pads can't merge into a single super-pad).
            float padL = MathF.Max(slotStart, center - halfW);
            float padR = MathF.Min(slotEnd,   center + halfW);

            // Sample the terrain elevation at the pad center and flatten the local
            // segment around it. Find indices that fall inside [padL, padR] and pin
            // them to a flat Y; also splice in exact-edge vertices.
            float padY = SampleHeightAt(pts, (padL + padR) * 0.5f);
            pads.Add(new LandingPad { X0 = padL, X1 = padR, Y = padY, Multiplier = padMix[k].multiplier });
        }

        // Apply the pads to the polyline: replace any vertices inside a pad's X range
        // with the pad surface; insert exact pad-edge points so the line stays clean.
        var flattened = new List<Vec2>(pts.Length + pads.Count * 2);
        for (int i = 0; i <= Resolution; i++)
            flattened.Add(pts[i]);

        foreach (var pad in pads)
        {
            // Remove any vertices strictly inside [pad.X0, pad.X1].
            for (int i = flattened.Count - 1; i >= 0; i--)
            {
                if (flattened[i].X > pad.X0 && flattened[i].X < pad.X1)
                    flattened.RemoveAt(i);
            }
            // Insert the pad's two end-points at the pad's Y.
            InsertSorted(flattened, new Vec2(pad.X0, pad.Y));
            InsertSorted(flattened, new Vec2(pad.X1, pad.Y));
        }

        return new Terrain { Points = flattened.ToArray(), Pads = pads.ToArray() };
    }

    // Midpoint displacement — recursively halves the segment, perturbs the midpoint
    // by a shrinking amplitude.
    static void Displace(float[] heights, int i0, int i1, float amp, Random rng)
    {
        if (i1 - i0 < 2) return;
        int mid = (i0 + i1) / 2;
        float baseline = (heights[i0] + heights[i1]) * 0.5f;
        heights[mid] = baseline + (float)(rng.NextDouble() - 0.5) * amp;
        Displace(heights, i0, mid, amp * 0.55f, rng);
        Displace(heights, mid, i1, amp * 0.55f, rng);
    }

    static float SampleHeightAt(Vec2[] pts, float x)
    {
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var a = pts[i];
            var b = pts[i + 1];
            if (x >= a.X && x <= b.X)
            {
                float t = (x - a.X) / MathF.Max(0.0001f, b.X - a.X);
                return a.Y + (b.Y - a.Y) * t;
            }
        }
        return pts[^1].Y;
    }

    static void InsertSorted(List<Vec2> list, Vec2 p)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].X > p.X) { list.Insert(i, p); return; }
        }
        list.Add(p);
    }

    // Walk terrain segments and find the Y for a given X. Used for collision tests.
    public static float HeightAt(Terrain t, float x)
    {
        if (t.Points.Length == 0) return 0;
        if (x <= t.Points[0].X) return t.Points[0].Y;
        if (x >= t.Points[^1].X) return t.Points[^1].Y;
        for (int i = 0; i < t.Points.Length - 1; i++)
        {
            var a = t.Points[i];
            var b = t.Points[i + 1];
            if (x >= a.X && x <= b.X)
            {
                float u = (x - a.X) / MathF.Max(0.0001f, b.X - a.X);
                return a.Y + (b.Y - a.Y) * u;
            }
        }
        return t.Points[^1].Y;
    }

    // Find the pad (if any) whose X-range contains the given X.
    public static LandingPad? PadAt(Terrain t, float x)
    {
        foreach (var p in t.Pads)
            if (x >= p.X0 && x <= p.X1) return p;
        return null;
    }
}
