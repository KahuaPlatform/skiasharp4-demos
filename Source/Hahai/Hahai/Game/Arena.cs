using System;
using System.Collections.Generic;

namespace Hahai.Game;

// Maze grid for Hahai. Cells are 24×24 px in world space; the layout is the
// classic 28×31 Pac-Man footprint hand-typed below as ASCII. Each glyph maps
// to a Tile enum value; pellets/power-pellets are also tracked in a parallel
// grid that GameWorld mutates as Pac eats them.
//
//   '#' wall          '.' pellet
//   ' ' open (no pel) 'o' power pellet
//   '-' ghost door    'H' ghost-house interior
//   'T' tunnel cell (Col 0 and Col Cols-1 of one row wrap to each other)
//
// The two tunnel rows on the sides of the middle row let Pac and ghosts wrap
// horizontally; everything else is bounded by walls.
public enum Tile : byte
{
    Open,
    Wall,
    GhostDoor,
    House,
    Tunnel,
}

public sealed class Arena
{
    public const int   Cols      = 28;
    public const int   Rows      = 31;
    public const float CellSize  = 24f;
    public const float WorldW    = Cols * CellSize;   // 672
    public const float WorldH    = Rows * CellSize;   // 744

    // Classic-ish Pac-Man maze. Rows are top-down. 28 chars wide each.
    static readonly string[] Layout =
    {
        "############################",
        "#............##............#",
        "#.####.#####.##.#####.####.#",
        "#o####.#####.##.#####.####o#",
        "#.####.#####.##.#####.####.#",
        "#..........................#",
        "#.####.##.########.##.####.#",
        "#.####.##.########.##.####.#",
        "#......##....##....##......#",
        "######.##### ## #####.######",
        "     #.##### ## #####.#     ",
        "     #.##          ##.#     ",
        "     #.## ###--### ##.#     ",
        "######.## #HHHHHH# ##.######",
        "T     .   #HHHHHH#   .     T",
        "######.## #HHHHHH# ##.######",
        "     #.## ######## ##.#     ",
        "     #.##          ##.#     ",
        "     #.## ######## ##.#     ",
        "######.## ######## ##.######",
        "#............##............#",
        "#.####.#####.##.#####.####.#",
        "#.####.#####.##.#####.####.#",
        "#o..##................##..o#",
        "###.##.##.########.##.##.###",
        "###.##.##.########.##.##.###",
        "#......##....##....##......#",
        "#.##########.##.##########.#",
        "#.##########.##.##########.#",
        "#..........................#",
        "############################",
    };

    public readonly Tile[,] Tiles    = new Tile[Cols, Rows];
    public readonly bool[,] Pellets  = new bool[Cols, Rows];
    public readonly bool[,] PowerDot = new bool[Cols, Rows];
    public int RemainingPellets;

    public Arena()
    {
        if (Layout.Length != Rows) throw new InvalidOperationException($"Layout has {Layout.Length} rows, expected {Rows}");
        for (int r = 0; r < Rows; r++)
        {
            var line = Layout[r];
            if (line.Length != Cols) throw new InvalidOperationException($"Row {r} has {line.Length} cols, expected {Cols}");
            for (int c = 0; c < Cols; c++)
            {
                switch (line[c])
                {
                    case '#': Tiles[c, r] = Tile.Wall;                                    break;
                    case '-': Tiles[c, r] = Tile.GhostDoor;                               break;
                    case 'H': Tiles[c, r] = Tile.House;                                   break;
                    case 'T': Tiles[c, r] = Tile.Tunnel;                                  break;
                    case '.': Tiles[c, r] = Tile.Open; Pellets[c, r]  = true; RemainingPellets++; break;
                    case 'o': Tiles[c, r] = Tile.Open; PowerDot[c, r] = true; RemainingPellets++; break;
                    default:  Tiles[c, r] = Tile.Open;                                    break;
                }
            }
        }
    }

    // Reset pellets/power-dots back to the spawn layout (used at level start).
    public void ResetPellets()
    {
        RemainingPellets = 0;
        for (int r = 0; r < Rows; r++)
        {
            var line = Layout[r];
            for (int c = 0; c < Cols; c++)
            {
                bool pel = line[c] == '.';
                bool pow = line[c] == 'o';
                Pellets[c, r]  = pel;
                PowerDot[c, r] = pow;
                if (pel || pow) RemainingPellets++;
            }
        }
    }

    public bool IsWalkable(int col, int row, bool allowDoor = false)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows) return false;
        var t = Tiles[col, row];
        if (t == Tile.Wall) return false;
        if (t == Tile.GhostDoor && !allowDoor) return false;
        return true;
    }

    public bool IsTunnel(int col, int row) =>
        row >= 0 && row < Rows && col >= 0 && col < Cols && Tiles[col, row] == Tile.Tunnel;

    public static Vec2 CellCenter(int col, int row) =>
        new(col * CellSize + CellSize * 0.5f, row * CellSize + CellSize * 0.5f);

    public static (int col, int row) WorldToCell(Vec2 p)
    {
        int c = (int)MathF.Floor(p.X / CellSize);
        int r = (int)MathF.Floor(p.Y / CellSize);
        return (c, r);
    }

    // Standard Pac-Man scatter corners (one per ghost kind). Used as the
    // chase-state "go home" target during Scatter phases.
    public static (int col, int row) ScatterCorner(GhostKind k) => k switch
    {
        GhostKind.Blinky => (Cols - 2, 1),
        GhostKind.Pinky  => (1,         1),
        GhostKind.Inky   => (Cols - 1, Rows - 1),
        GhostKind.Clyde  => (0,        Rows - 1),
        _                => (Cols / 2, Rows / 2),
    };
}
