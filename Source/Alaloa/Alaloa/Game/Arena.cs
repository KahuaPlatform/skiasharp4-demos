using System;

namespace Alaloa.Game;

// Cell-based arena. Each cell records which cycle owns its trail (-1 = empty).
// Continuous cycle motion + per-cell marking gives pixel-clean turns and
// pixel-clean collision (no line-segment intersection math).
public sealed class Arena
{
    public const int  Cols     = 90;
    public const int  Rows     = 90;
    public const float CellSize = 8f;
    public const float WorldW   = Cols * CellSize;   // 720
    public const float WorldH   = Rows * CellSize;   // 720

    // -1 = empty cell; otherwise the cycle index that laid the trail here.
    readonly int[,] _grid = new int[Cols, Rows];

    public Arena() { Clear(); }

    public void Clear()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                _grid[c, r] = -1;
    }

    public int Get(int col, int row)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows) return -2; // wall sentinel
        return _grid[col, row];
    }

    public void Mark(int col, int row, int owner)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows) return;
        _grid[col, row] = owner;
    }

    public static Vec2 CellCenter(int col, int row) =>
        new(col * CellSize + CellSize * 0.5f, row * CellSize + CellSize * 0.5f);

    public static (int col, int row) WorldToCell(Vec2 p)
    {
        int c = (int)MathF.Floor(p.X / CellSize);
        int r = (int)MathF.Floor(p.Y / CellSize);
        return (c, r);
    }
}
