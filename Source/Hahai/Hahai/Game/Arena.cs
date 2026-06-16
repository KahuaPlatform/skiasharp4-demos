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

/// <summary>The kind of a single maze cell.</summary>
public enum Tile : byte
{
    Open,
    Wall,
    GhostDoor,
    House,
    Tunnel,
}

/// <summary>
/// The 28×31 maze: a fixed ASCII layout parsed into a <see cref="Tile"/> grid plus
/// parallel pellet / power-dot grids the <c>GameWorld</c> mutates as Pac eats.
/// Also exposes walkability, tunnel-wrap, cell↔world, and per-ghost scatter-corner
/// helpers.
/// </summary>
public sealed class Arena
{
    /// <summary>Maze columns.</summary>
    public const int   Cols      = 28;
    /// <summary>Maze rows.</summary>
    public const int   Rows      = 31;
    /// <summary>Cell edge length in world units.</summary>
    public const float CellSize  = 24f;
    /// <summary>World width (672).</summary>
    public const float WorldW    = Cols * CellSize;   // 672
    /// <summary>World height (744).</summary>
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

    /// <summary>Restores pellets and power-dots to the spawn layout (called at level start).</summary>
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

    /// <summary>
    /// True if (col,row) can be entered: in-bounds and not a wall (and not the ghost
    /// door unless <paramref name="allowDoor"/>, which only eaten ghosts get).
    /// </summary>
    public bool IsWalkable(int col, int row, bool allowDoor = false)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows) return false;
        var t = Tiles[col, row];
        if (t == Tile.Wall) return false;
        if (t == Tile.GhostDoor && !allowDoor) return false;
        return true;
    }

    /// <summary>True if (col,row) is a tunnel cell (the side-wrap corridors).</summary>
    public bool IsTunnel(int col, int row) =>
        row >= 0 && row < Rows && col >= 0 && col < Cols && Tiles[col, row] == Tile.Tunnel;

    /// <summary>World-space center of cell (col,row).</summary>
    public static Vec2 CellCenter(int col, int row) =>
        new(col * CellSize + CellSize * 0.5f, row * CellSize + CellSize * 0.5f);

    /// <summary>Maps a world point to its (col,row) cell.</summary>
    public static (int col, int row) WorldToCell(Vec2 p)
    {
        int c = (int)MathF.Floor(p.X / CellSize);
        int r = (int)MathF.Floor(p.Y / CellSize);
        return (c, r);
    }

    /// <summary>
    /// The classic per-ghost scatter corner — the "go home" target each ghost
    /// heads to during Scatter phases.
    /// </summary>
    public static (int col, int row) ScatterCorner(GhostKind k) => k switch
    {
        GhostKind.Blinky => (Cols - 2, 1),
        GhostKind.Pinky  => (1,         1),
        GhostKind.Inky   => (Cols - 1, Rows - 1),
        GhostKind.Clyde  => (0,        Rows - 1),
        _                => (Cols / 2, Rows / 2),
    };
}
