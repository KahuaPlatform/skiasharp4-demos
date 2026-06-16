using System;

namespace Heiau.Game;

/// <summary>Top-level game state for Heiau's Star-Castle gameplay.</summary>
public enum GameMode { Title, Playing, GameOver, Attract }

/// <summary>The player ship — Asteroids-style: rotate, thrust with inertia, fire, screen-wrap.</summary>
public sealed class Ship
{
    public Vec2  Position;
    public Vec2  Velocity;
    public float AngleRadians;     // 0 = pointing right; rotates CCW with +
    public bool  Thrusting;
    public bool  Alive = true;
    public float Invuln;           // seconds of post-respawn invulnerability
    public float SpawnAnim;        // 0..1 fade-in after respawn
    public float ShootCooldown;
}

/// <summary>The central "sacred stone" turret: rotating barrel tracks the player and fires aimed shots.</summary>
public sealed class Turret
{
    public Vec2  Position;          // world center
    public float BarrelAngle;       // rad, where the barrel points
    public float FireCooldown;
    public bool  Alive = true;
    public float Spin;              // small idle wobble for visual life
}

/// <summary>
/// A rotating energy ring: N independently-destructible segments at a fixed
/// radius, spinning around the world center. The ring is gone once all segments die.
/// </summary>
public sealed class Ring
{
    public float Radius;
    public float Rotation;          // current angular offset (rad)
    public float AngularSpeed;      // rad/sec, signed (positive = CCW)
    public int[]   Health    = Array.Empty<int>();   // current HP per segment, 0 = destroyed
    public int     MaxHealth = 3;                    // starting HP for newly-built segments
    public float[] HitFlash  = Array.Empty<float>(); // 0..1, brief flash after a hit
    public int Segments => Health.Length;
    public int AliveCount;
    public float SegmentColorHue;   // shifts the per-ring base hue

    public bool IsAlive(int seg) => seg >= 0 && seg < Health.Length && Health[seg] > 0;
}

/// <summary>
/// A "Spark" — a homing energy mine that detaches from a destroyed ring segment
/// and drifts toward the player; shoot it for bonus points (the only safe answer).
/// </summary>
public sealed class Spark
{
    public Vec2  Position;
    public Vec2  Velocity;
    public float Life;       // seconds remaining before self-dissipate
    public float Hue;        // animated for the pulsing visual
}

/// <summary>A shot; <see cref="FromPlayer"/> distinguishes player vs turret bullets.</summary>
public sealed class Bullet
{
    public Vec2  Position;
    public Vec2  Velocity;
    public float Life;
    public bool  FromPlayer;
}

/// <summary>A short-lived explosion particle (purely visual).</summary>
public sealed class Particle
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Life;
    public float MaxLife;
    public uint  Color;
    public float Size = 2.0f;
}

/// <summary>A floating "+score" number shown briefly at a kill site.</summary>
public sealed class ScorePopup
{
    public Vec2  Pos;
    public int   Value;
    public float Life;
    public float MaxLife;
    public uint  Color;
}
