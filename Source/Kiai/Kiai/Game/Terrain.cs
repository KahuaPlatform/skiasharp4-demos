using System;
using System.Collections.Generic;
using Arcade.Common.Chassis;

namespace Kiai.Game;

// Thin game-side wrapper over the shared SeamlessTerrain chassis piece. It owns
// the generated periodic height field for the planet surface and adds the one
// game-specific bit the chassis shouldn't know about: choosing believable
// standing spots for humanoids (flat-ground cells, spread out along the loop).
//
// Everything spatial delegates to SeamlessTerrain so the seam stays invisible:
// HeightAt wraps + is periodic, BuildVisibleStrip walks screen X and lets the
// field wrap. Terrain only exists so GameWorld has a single, named home for "the
// ground" plus spawn-point picking.
public sealed class Terrain
{
    public SeamlessTerrain Field { get; }
    public float WorldWidth => Field.WorldWidth;

    public Terrain(float worldWidth, float worldHeight, Random rng)
    {
        // Baseline sits in the lower third of the screen; amplitude is a sane
        // fraction of the height so the ridges never eat the whole playfield or
        // flatten out. Default harmonics {3,7,13,23} give rolling hills + texture.
        float baseline = worldHeight * 0.72f;
        float amplitude = worldHeight * 0.16f;
        Field = new SeamlessTerrain(worldWidth, baseline, amplitude, rng);
    }

    // The terrain Y (world units, larger == lower on screen) under any world X.
    public float HeightAt(float worldX) => Field.HeightAt(worldX);

    // Pick `count` world-X positions that sit on reasonably flat ground, spread
    // roughly evenly around the loop with a little jitter so they don't look
    // gridded. Each candidate is nudged until IsFlat passes (or we give up and
    // accept it after a few tries — the rescue loop tolerates a slightly sloped
    // colonist better than a missing one). Returns the chosen world-X values; the
    // caller reads HeightAt(x) to place each humanoid's feet.
    public List<float> PickHumanoidSpots(int count, Random rng)
    {
        var spots = new List<float>(count);
        if (count <= 0) return spots;

        float slice = WorldWidth / count;
        const float HalfSpan = 26f;   // how wide a "flat enough" footing must be
        const float MaxRise = 14f;    // max terrain rise across that span to qualify

        for (int i = 0; i < count; i++)
        {
            float baseX = i * slice + (float)rng.NextDouble() * slice;
            float x = baseX;
            // A few attempts to slide onto flatter ground near the slot.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (Field.IsFlat(x, HalfSpan, MaxRise)) break;
                x = baseX + ((float)rng.NextDouble() - 0.5f) * slice;
            }
            spots.Add(Camera2D.Wrap(x, WorldWidth));
        }
        return spots;
    }
}
