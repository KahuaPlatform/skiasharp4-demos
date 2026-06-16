using System;

namespace Alaloa.Game;

/// <summary>
/// The 90×90 cell grid that backs trail collision. Each cell stores the index of
/// the cycle that laid a trail there (-1 = empty). Continuous cycle motion plus
/// per-cell marking gives pixel-clean turns and collision with no segment math.
/// </summary>
public sealed class Arena
{
    /// <summary>Grid columns.</summary>
    public const int  Cols     = 90;
    /// <summary>Grid rows.</summary>
    public const int  Rows     = 90;
    /// <summary>Cell edge length in world units.</summary>
    public const float CellSize = 8f;
    /// <summary>World width (Cols·CellSize = 720).</summary>
    public const float WorldW   = Cols * CellSize;
    /// <summary>World height (Rows·CellSize = 720).</summary>
    public const float WorldH   = Rows * CellSize;

    // -1 = empty cell; otherwise the cycle index that laid the trail here.
    readonly int[,] _grid = new int[Cols, Rows];

    public Arena() { Clear(); }

    /// <summary>Resets every cell to empty (new round).</summary>
    public void Clear()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                _grid[c, r] = -1;
    }

    /// <summary>
    /// Returns the owner index at (col,row): -1 if empty, a cycle index if a trail
    /// is there, or -2 (wall sentinel) if out of bounds — so the AI/collision can
    /// treat the edge like a deadly trail.
    /// </summary>
    public int Get(int col, int row)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows) return -2; // wall sentinel
        return _grid[col, row];
    }

    /// <summary>Marks cell (col,row) as owned by cycle <paramref name="owner"/> (ignores out-of-bounds).</summary>
    public void Mark(int col, int row, int owner)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows) return;
        _grid[col, row] = owner;
    }

    /// <summary>World-space center of cell (col,row).</summary>
    public static Vec2 CellCenter(int col, int row) =>
        new(col * CellSize + CellSize * 0.5f, row * CellSize + CellSize * 0.5f);

    /// <summary>Maps a world point to its (col,row) cell (may be out of bounds).</summary>
    public static (int col, int row) WorldToCell(Vec2 p)
    {
        int c = (int)MathF.Floor(p.X / CellSize);
        int r = (int)MathF.Floor(p.Y / CellSize);
        return (c, r);
    }
}
