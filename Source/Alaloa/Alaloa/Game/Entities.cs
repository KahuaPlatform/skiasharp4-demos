using System.Collections.Generic;

namespace Alaloa.Game;

public enum GameMode { Title, Playing, RoundOver, GameOver, Attract }

public enum Direction { Up, Right, Down, Left }

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

// One light cycle. Position is continuous (smooth motion); HeadCol/HeadRow
// track which cell the head is currently inside. Trail is a polyline of
// corner positions appended on every direction change — used for rendering.
// Per-cell ownership is tracked by the shared Arena.
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

public sealed class Particle
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Life;
    public float MaxLife;
    public uint  Color;
    public float Size = 2.0f;
}
