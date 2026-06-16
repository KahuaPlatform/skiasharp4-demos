using System;

namespace Hahai.Game;

/// <summary>Top-level game state for Hahai's Pac-Man-style chase.</summary>
public enum GameMode { Title, Playing, GameOver, Attract }

/// <summary>A grid travel direction (<c>None</c> = stationary).</summary>
public enum Direction { None, Up, Right, Down, Left }

/// <summary>Helpers for <see cref="Direction"/> (cell delta + opposite test).</summary>
public static class Directions
{
    /// <summary>Returns the (dx,dy) cell step for a direction.</summary>
    public static (int dx, int dy) Delta(Direction d) => d switch
    {
        Direction.Up    => ( 0, -1),
        Direction.Down  => ( 0, +1),
        Direction.Left  => (-1,  0),
        Direction.Right => (+1,  0),
        _               => ( 0,  0),
    };

    /// <summary>True if <paramref name="a"/> and <paramref name="b"/> are 180° apart.</summary>
    public static bool IsOpposite(Direction a, Direction b) =>
        (a == Direction.Up    && b == Direction.Down)  ||
        (a == Direction.Down  && b == Direction.Up)    ||
        (a == Direction.Left  && b == Direction.Right) ||
        (a == Direction.Right && b == Direction.Left);
}

/// <summary>
/// The player (the Honu/sea-turtle). Moves smoothly between cell centers along
/// corridors; the queued <see cref="Pending"/> turn is honored only at an
/// intersection where it's a legal move.
/// </summary>
public sealed class Pac
{
    public Vec2      Position;
    public int       Col, Row;
    public Direction Dir = Direction.Left;
    public Direction Pending = Direction.Left;
    public float     MouthPhase;     // animated open/close cycle, 0..1
    public bool      Alive = true;
}

/// <summary>The four ghost (Mo'o) personalities, each with a distinct chase target.</summary>
public enum GhostKind { Blinky, Pinky, Inky, Clyde }

// Ghost state machine:
//   Chase       — pursuing the player with kind-specific targeting
//   Scatter     — heading for the kind's home corner; alternates with Chase
//   Frightened  — power pellet active; wandering randomly, edible by player
//   Eaten       — eyes-only state; returns to ghost house to respawn
/// <summary>A ghost's current behavior phase (see the comment above for each).</summary>
public enum GhostState { Chase, Scatter, Frightened, Eaten }

/// <summary>One ghost (Mo'o). <see cref="Kind"/> + <see cref="State"/> select its target/behavior.</summary>
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

/// <summary>A short-lived eat/death particle (purely visual).</summary>
public sealed class Particle
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Life;
    public float MaxLife;
    public uint  Color;
    public float Size = 2.0f;
}

/// <summary>A floating "+score" number shown briefly after eating a ghost.</summary>
public sealed class ScorePopup
{
    public Vec2  Pos;
    public int   Value;
    public float Life;
    public float MaxLife;
    public uint  Color;
}
