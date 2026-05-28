using System;

namespace Hahai.Game;

public enum GameMode { Title, Playing, GameOver, Attract }

public enum Direction { None, Up, Right, Down, Left }

public static class Directions
{
    public static (int dx, int dy) Delta(Direction d) => d switch
    {
        Direction.Up    => ( 0, -1),
        Direction.Down  => ( 0, +1),
        Direction.Left  => (-1,  0),
        Direction.Right => (+1,  0),
        _               => ( 0,  0),
    };

    public static bool IsOpposite(Direction a, Direction b) =>
        (a == Direction.Up    && b == Direction.Down)  ||
        (a == Direction.Down  && b == Direction.Up)    ||
        (a == Direction.Left  && b == Direction.Right) ||
        (a == Direction.Right && b == Direction.Left);
}

// Pac — the player. Smooth motion between cell centres along corridors;
// turns only happen at intersections where the queued direction is valid.
public sealed class Pac
{
    public Vec2      Position;
    public int       Col, Row;
    public Direction Dir = Direction.Left;
    public Direction Pending = Direction.Left;
    public float     MouthPhase;     // animated open/close cycle, 0..1
    public bool      Alive = true;
}

public enum GhostKind { Blinky, Pinky, Inky, Clyde }

// Ghost state machine:
//   Chase       — pursuing the player with kind-specific targeting
//   Scatter     — heading for the kind's home corner; alternates with Chase
//   Frightened  — power pellet active; wandering randomly, edible by player
//   Eaten       — eyes-only state; returns to ghost house to respawn
public enum GhostState { Chase, Scatter, Frightened, Eaten }

public sealed class Ghost
{
    public GhostKind  Kind;
    public Vec2       Position;
    public int        Col, Row;
    public Direction  Dir = Direction.Left;
    public GhostState State = GhostState.Chase;
    public float      StateTimer;     // for chase/scatter alternation
    public float      ReleaseDelay;   // seconds before leaving the ghost house at level start
    public bool       InHouse = true; // sitting inside the ghost-house at spawn
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
