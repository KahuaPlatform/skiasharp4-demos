using System;
using System.Collections.Generic;

namespace Kanapi.Game;

/// <summary>Top-level game state for Kanapi's Centipede gameplay.</summary>
public enum GameMode { Title, Playing, GameOver, Attract }

/// <summary>The player blaster — confined to the bottom shooter zone, 4-way move, auto-fires up.</summary>
public sealed class Player
{
    public Vec2  Position;
    public bool  Alive = true;
    public float Invuln;
    public float ShootCooldown;
}

/// <summary>
/// A grid mushroom with 4 HP (sheds a petal per hit). Poisoned mushrooms flip
/// color and make a touching centipede dive straight down.
/// </summary>
public sealed class Mushroom
{
    public int Col, Row;
    public int Health = 4;           // 4 segments knocked off, 1 hit each
    public bool Poisoned;
}

/// <summary>
/// One centipede link. Moves continuously toward a target cell; the head picks
/// the next cell from grid logic, body links follow the leader's prior position.
/// </summary>
public sealed class CentipedeSegment
{
    public Vec2 Position;            // continuous world coords
    public Vec2 Target;               // cell-centre we're moving toward
    public bool IsHead;
    public bool Poisoned;
    public int  HorizDir = +1;        // +1 right, -1 left (head only)
    public int  VertDir  = +1;        // +1 down, -1 up (head only; up when poisoned)
}

/// <summary>An ordered chain of centipede segments (head first). Splits into two chains when shot.</summary>
public sealed class CentipedeChain
{
    public List<CentipedeSegment> Segments = new();
    public float SpeedFactor = 1f;    // scaled by level
}

/// <summary>A spider that zigzags through the player zone eating mushrooms; closer kills score more.</summary>
public sealed class Spider
{
    public Vec2  Position;
    public Vec2  Velocity;
    public float DirTimer;            // seconds until next vector change
    public bool  Alive = true;
}

/// <summary>A player shot travelling straight up.</summary>
public sealed class Bullet
{
    public Vec2  Position;
    public float Life;
    public bool  Alive = true;
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
