namespace Paku.Game;

// Paku's model types (agar.io-style cell-eat-cell arena). All cells live in
// world-space coordinates (0..WorldWidth, 0..WorldHeight); the renderer applies
// a camera transform to map that large world onto the screen. A cell's radius is
// derived from its mass: r = sqrt(mass) * RadiusScale, so area scales linearly
// with mass and absorbing prey grows the blob in a believable way.

/// <summary>AI personality of a non-player cell.</summary>
public enum CellBehavior
{
    /// <summary>Wanders randomly; ignores the player and other cells.</summary>
    Passive,
    /// <summary>Actively chases smaller prey (incl. the player) and flees bigger threats.</summary>
    Hunter,
}

/// <summary>
/// A single living blob — the player or an enemy. Carries physics state, mass,
/// a neon hue, an AI behavior, and the harmonic parameters that give it a unique
/// wobbling amoeba silhouette.
/// </summary>
public class Cell
{
    /// <summary>World-space center position.</summary>
    public Vec2 Pos;
    /// <summary>Velocity in world units/second.</summary>
    public Vec2 Vel;
    /// <summary>Mass — drives radius, absorption rules, and score value.</summary>
    public float Mass;
    /// <summary>Cleared to false when absorbed; dead cells are reaped each frame.</summary>
    public bool Alive = true;
    /// <summary>Accent hue in degrees [0,360) for the neon body color.</summary>
    public float Hue;
    /// <summary>AI personality (ignored for the player cell).</summary>
    public CellBehavior Behavior;
    /// <summary>How far, in world units, a Hunter can sense prey/threats.</summary>
    public float HuntRange;

    // Per-cell shape identity: LobeCount sine harmonics with random amplitude and
    // phase give each cell a distinct amoeba-like silhouette that wobbles over time.
    /// <summary>Random seed used when generating this cell's lobe parameters.</summary>
    public int Seed;
    /// <summary>Per-harmonic distortion amplitude (fraction of radius).</summary>
    public float[] Lobes = Array.Empty<float>();
    /// <summary>Per-harmonic phase offset, in radians.</summary>
    public float[] Phases = Array.Empty<float>();
    /// <summary>Number of sine harmonics summed to wobble the membrane.</summary>
    public const int LobeCount = 7;

    /// <summary>Base (un-wobbled) radius derived from <see cref="Mass"/>.</summary>
    public float Radius => MathF.Sqrt(Mass) * RadiusScale;
    /// <summary>Multiplier converting sqrt(mass) into pixels.</summary>
    public const float RadiusScale = 2.5f;

    /// <summary>
    /// Returns the wobbled radius at a given perimeter <paramref name="angle"/>
    /// and animation <paramref name="time"/>, summing the cell's lobe harmonics.
    /// The renderer samples this around the circle to build the blob outline.
    /// </summary>
    public float RadiusAt(float angle, float time)
    {
        float r = Radius;
        float wobble = 0f;
        for (int i = 0; i < Lobes.Length; i++)
        {
            // Harmonics 2,3,4,... around the perimeter; each animates at its own
            // rate (0.8 + i*0.3) so the membrane ripples rather than pulsing.
            float freq = i + 2;
            wobble += Lobes[i] * MathF.Sin(freq * angle + Phases[i] + time * (0.8f + i * 0.3f));
        }
        return r * (1f + wobble);
    }

    /// <summary>
    /// Seeds this cell's lobe amplitudes and phases from <paramref name="rng"/>,
    /// giving it a one-time random silhouette. Call once at spawn.
    /// </summary>
    public void InitShape(Random rng)
    {
        Seed = rng.Next();
        Lobes = new float[LobeCount];
        Phases = new float[LobeCount];
        for (int i = 0; i < LobeCount; i++)
        {
            Lobes[i] = 0.04f + rng.NextSingle() * 0.10f;  // 4-14% distortion per harmonic
            Phases[i] = rng.NextSingle() * MathF.Tau;
        }
    }

    /// <summary>
    /// Integrates position from velocity, applies a soft bounce off the world
    /// bounds, and a light drag so cells coast. Called every frame for every
    /// live cell.
    /// </summary>
    public void Update(float dt, float worldW, float worldH)
    {
        Pos.X += Vel.X * dt;
        Pos.Y += Vel.Y * dt;

        // Soft bounce off world edges: clamp inside and reflect velocity at half
        // speed so cells don't ping-pong forever along the wall.
        float r = Radius;
        if (Pos.X - r < 0)      { Pos.X = r;          Vel.X = MathF.Abs(Vel.X) * 0.5f; }
        if (Pos.X + r > worldW) { Pos.X = worldW - r;  Vel.X = -MathF.Abs(Vel.X) * 0.5f; }
        if (Pos.Y - r < 0)      { Pos.Y = r;          Vel.Y = MathF.Abs(Vel.Y) * 0.5f; }
        if (Pos.Y + r > worldH) { Pos.Y = worldH - r;  Vel.Y = -MathF.Abs(Vel.Y) * 0.5f; }

        // Very light drag — cells coast for a long time once moving
        Vel.X *= 0.9995f;
        Vel.Y *= 0.9995f;
    }
}

/// <summary>
/// A tiny static food dot scattered around the world. No physics — just a
/// position and hue; any cell that touches it gains <see cref="Mass"/>.
/// </summary>
public class Spore
{
    /// <summary>World-space position.</summary>
    public Vec2 Pos;
    /// <summary>Accent hue in degrees [0,360).</summary>
    public float Hue;
    /// <summary>Cleared to false when eaten; reaped each frame.</summary>
    public bool Alive = true;
    /// <summary>Mass granted to whoever absorbs the spore.</summary>
    public const float Mass = 2f;
    /// <summary>Visual/collision radius.</summary>
    public const float Radius = 4f;
}

/// <summary>
/// A short-lived visual particle (absorb burst, thrust exhaust). Fades and
/// shrinks over its lifetime; no gameplay effect.
/// </summary>
public class Particle
{
    /// <summary>World-space position.</summary>
    public Vec2 Pos;
    /// <summary>Velocity in world units/second.</summary>
    public Vec2 Vel;
    /// <summary>Seconds of life remaining.</summary>
    public float Life;
    /// <summary>Initial lifetime, used to compute <see cref="Alpha"/>.</summary>
    public float MaxLife;
    /// <summary>Accent hue in degrees [0,360).</summary>
    public float Hue;
    /// <summary>Current radius (shrinks each frame).</summary>
    public float Size;

    /// <summary>True while the particle still has life left.</summary>
    public bool Alive => Life > 0;
    /// <summary>Normalized remaining-life [0,1], used as draw opacity.</summary>
    public float Alpha => Math.Clamp(Life / MaxLife, 0f, 1f);

    /// <summary>Advances position, applies drag, ages the particle, and shrinks it.</summary>
    public void Update(float dt)
    {
        Pos.X += Vel.X * dt;
        Pos.Y += Vel.Y * dt;
        Vel.X *= 0.97f;
        Vel.Y *= 0.97f;
        Life -= dt;
        Size *= 0.995f;
    }
}
