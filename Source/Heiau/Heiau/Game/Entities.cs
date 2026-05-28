using System;

namespace Heiau.Game;

public enum GameMode { Title, Playing, GameOver, Attract }

// Player ship — Asteroids-style: rotate, thrust with inertia, fire bullets,
// screen wraps at the world edges.
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

// Central turret — the "sacred stone" at the heart of the heiau. Has a rotating
// barrel that tracks the player. Periodically fires aimed shots.
public sealed class Turret
{
    public Vec2  Position;          // world center
    public float BarrelAngle;       // rad, where the barrel points
    public float FireCooldown;
    public bool  Alive = true;
    public float Spin;              // small idle wobble for visual life
}

// A ring is a circle of N segments at a fixed radius. Each segment can be alive
// or destroyed. The whole ring rotates around the world center at its own rate
// and direction; once all segments are dead the ring is gone.
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

// Sparx — homing energy mines that detach from a ring segment when it's
// destroyed. They drift toward the player at a moderate clip; shooting one is
// the only safe response. Bonus points for the kill, but they're a constant
// pressure that punishes slow play.
public sealed class Spark
{
    public Vec2  Position;
    public Vec2  Velocity;
    public float Life;       // seconds remaining before self-dissipate
    public float Hue;        // animated for the pulsing visual
}

// Player + enemy bullets share this struct. FromPlayer flips behaviour.
public sealed class Bullet
{
    public Vec2  Position;
    public Vec2  Velocity;
    public float Life;
    public bool  FromPlayer;
}

public sealed class Particle
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Life;
    public float MaxLife;
    public uint  Color;
    public float Size = 2.0f;
}

public sealed class ScorePopup
{
    public Vec2  Pos;
    public int   Value;
    public float Life;
    public float MaxLife;
    public uint  Color;
}
