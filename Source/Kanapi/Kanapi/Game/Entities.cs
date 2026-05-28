using System;
using System.Collections.Generic;

namespace Kanapi.Game;

public enum GameMode { Title, Playing, GameOver, Attract }

// Player blaster — bottom-third "shooter zone", 4-directional movement,
// auto-fires while Space is held.
public sealed class Player
{
    public Vec2  Position;
    public bool  Alive = true;
    public float Invuln;
    public float ShootCooldown;
}

// Mushroom on the grid. Health = 4 -> 1; rendered with progressively fewer
// petals/dots as it takes damage. Poisoned mushrooms (set by scorpion contact)
// flip color and make any centipede that touches them dive straight down.
public sealed class Mushroom
{
    public int Col, Row;
    public int Health = 4;           // 4 segments knocked off, 1 hit each
    public bool Poisoned;
}

// One segment of a centipede chain. Position is continuous (smooth motion); the
// segment lerps toward a target cell, then picks the next target based on
// chain logic (head) or the leader's previous position (body).
public sealed class CentipedeSegment
{
    public Vec2 Position;            // continuous world coords
    public Vec2 Target;               // cell-centre we're moving toward
    public bool IsHead;
    public bool Poisoned;
    public int  HorizDir = +1;        // +1 right, -1 left (head only)
    public int  VertDir  = +1;        // +1 down, -1 up (head only; up when poisoned)
}

public sealed class CentipedeChain
{
    public List<CentipedeSegment> Segments = new();
    public float SpeedFactor = 1f;    // scaled by level
}

// Spider — zigzags through the player zone, eats mushrooms it crosses.
// Score depends on how close to the player it dies (closer = more).
public sealed class Spider
{
    public Vec2  Position;
    public Vec2  Velocity;
    public float DirTimer;            // seconds until next vector change
    public bool  Alive = true;
}

public sealed class Bullet
{
    public Vec2  Position;
    public float Life;
    public bool  Alive = true;
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
