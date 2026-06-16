using System.Collections.Generic;

namespace Alaloa.Game;

/// <summary>Top-level game state for Alaloa's light-cycle duel.</summary>
public enum GameMode { Title, Playing, RoundOver, GameOver, Attract }

/// <summary>One of the four cardinal travel directions.</summary>
public enum Direction { Up, Right, Down, Left }

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

    /// <summary>True if <paramref name="a"/> and <paramref name="b"/> are 180° apart (an illegal U-turn).</summary>
    public static bool IsOpposite(Direction a, Direction b) =>
        (a == Direction.Up    && b == Direction.Down)  ||
        (a == Direction.Down  && b == Direction.Up)    ||
        (a == Direction.Left  && b == Direction.Right) ||
        (a == Direction.Right && b == Direction.Left);
}

/// <summary>
/// One light cycle. Moves continuously while <see cref="HeadCol"/>/<see cref="HeadRow"/>
/// track its current cell; <see cref="Trail"/> is the corner polyline (appended on
/// each turn) used for rendering. Per-cell trail ownership lives in the shared
/// <see cref="Arena"/>.
/// </summary>
public sealed class Cycle
{
    public int       OwnerIndex;       // 0..3
    public uint      Color;            // packed AARRGGBB for trail + head
    public Vec2      Position;
    public int       HeadCol;
    public int       HeadRow;
    public Direction Dir;
    public Direction PendingDir;       // queued turn from input; applied at next cell boundary
    public bool      Alive = true;
    public bool      IsPlayer;
    public List<Vec2> Trail = new();   // first entry = spawn point; new points appended on turn
    public float     AiTimer;          // throttles bot decision changes
}

/// <summary>A short-lived crash-burst particle (purely visual).</summary>
public sealed class Particle
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Life;
    public float MaxLife;
    public uint  Color;
    public float Size = 2.0f;
}
