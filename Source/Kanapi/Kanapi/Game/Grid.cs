using System;
using System.Collections.Generic;

namespace Kanapi.Game;

/// <summary>
/// The 30×30 mushroom field. The centipede navigates these cells and the player
/// roams the bottom rows; mushrooms have 4 HP, shed petals per hit, and award 1
/// point when destroyed. Also provides the cell↔world helpers used throughout.
/// </summary>
public sealed class MushroomGrid
{
    /// <summary>Grid columns.</summary>
    public const int  Cols     = 30;
    /// <summary>Grid rows.</summary>
    public const int  Rows     = 30;
    /// <summary>Cell edge length in world units.</summary>
    public const float CellSize = 24f;

    // PlayerZoneTopRow defines where the player's movable area starts. Rows
    // 0..PlayerZoneTopRow-1 are "centipede roaming" rows.
    public const int PlayerZoneTopRow = 22;

    readonly Mushroom?[,] _cells = new Mushroom?[Cols, Rows];

    // Total alive mushrooms — used by Flea spawn logic in future expansions.
    public int Count { get; private set; }

    /// <summary>Returns the mushroom at (col,row), or null if empty/out of bounds.</summary>
    public Mushroom? Get(int col, int row)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows) return null;
        return _cells[col, row];
    }

    /// <summary>Places a mushroom (updating the alive count); ignores out-of-bounds.</summary>
    public void Set(Mushroom m)
    {
        if (!InBounds(m.Col, m.Row)) return;
        if (_cells[m.Col, m.Row] == null) Count++;
        _cells[m.Col, m.Row] = m;
    }

    /// <summary>Removes the mushroom at (col,row); returns true if one was there.</summary>
    public bool Remove(int col, int row)
    {
        if (!InBounds(col, row)) return false;
        if (_cells[col, row] == null) return false;
        _cells[col, row] = null;
        Count--;
        return true;
    }

    /// <summary>True if (col,row) is inside the grid.</summary>
    public static bool InBounds(int col, int row) =>
        col >= 0 && col < Cols && row >= 0 && row < Rows;

    /// <summary>World-space center of cell (col,row).</summary>
    public static Vec2 CellCenter(int col, int row) =>
        new(col * CellSize + CellSize * 0.5f, row * CellSize + CellSize * 0.5f);

    /// <summary>Maps a world point to (col,row), clamped to grid bounds.</summary>
    public static (int col, int row) WorldToCell(Vec2 p)
    {
        int c = Math.Clamp((int)MathF.Floor(p.X / CellSize), 0, Cols - 1);
        int r = Math.Clamp((int)MathF.Floor(p.Y / CellSize), 0, Rows - 1);
        return (c, r);
    }

    /// <summary>Enumerates every live mushroom (row-major).</summary>
    public IEnumerable<Mushroom> AllMushrooms()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (_cells[c, r] != null) yield return _cells[c, r]!;
    }

    /// <summary>
    /// Clears and regenerates a randomized field for <paramref name="level"/>:
    /// denser near the top, sparser toward the player so lines of sight stay open,
    /// with a small per-level density bump.
    /// </summary>
    public void Reset(int level, Random rng)
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                _cells[c, r] = null;
        Count = 0;

        // Coverage decreases as you go down. Rows 0-2 are kept clear so the
        // centipede has room to enter without immediately bouncing.
        for (int r = 3; r < Rows; r++)
        {
            float densityTop  = 0.18f;
            float densityBot  = 0.05f;
            float t = (float)r / Rows;
            float density = densityTop + (densityBot - densityTop) * t;
            // Add a small per-level density bump so later levels feel busier.
            density += MathF.Min(0.08f, (level - 1) * 0.012f);

            for (int c = 0; c < Cols; c++)
            {
                if (rng.NextDouble() < density)
                {
                    Set(new Mushroom { Col = c, Row = r });
                }
            }
        }
    }
}
