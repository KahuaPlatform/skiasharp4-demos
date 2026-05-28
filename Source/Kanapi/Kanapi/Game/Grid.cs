using System;
using System.Collections.Generic;

namespace Kanapi.Game;

// Mushroom field on a fixed-size grid. Centipede navigates the grid; the player
// roams freely in the bottom rows. Mushrooms have 4 HP and shed visible petals
// per hit; on full destruction they vanish and award the player 1 point.
public sealed class MushroomGrid
{
    public const int  Cols     = 30;
    public const int  Rows     = 30;
    public const float CellSize = 24f;

    // PlayerZoneTopRow defines where the player's movable area starts. Rows
    // 0..PlayerZoneTopRow-1 are "centipede roaming" rows.
    public const int PlayerZoneTopRow = 22;

    readonly Mushroom?[,] _cells = new Mushroom?[Cols, Rows];

    // Total alive mushrooms — used by Flea spawn logic in future expansions.
    public int Count { get; private set; }

    public Mushroom? Get(int col, int row)
    {
        if (col < 0 || col >= Cols || row < 0 || row >= Rows) return null;
        return _cells[col, row];
    }

    public void Set(Mushroom m)
    {
        if (!InBounds(m.Col, m.Row)) return;
        if (_cells[m.Col, m.Row] == null) Count++;
        _cells[m.Col, m.Row] = m;
    }

    public bool Remove(int col, int row)
    {
        if (!InBounds(col, row)) return false;
        if (_cells[col, row] == null) return false;
        _cells[col, row] = null;
        Count--;
        return true;
    }

    public static bool InBounds(int col, int row) =>
        col >= 0 && col < Cols && row >= 0 && row < Rows;

    public static Vec2 CellCenter(int col, int row) =>
        new(col * CellSize + CellSize * 0.5f, row * CellSize + CellSize * 0.5f);

    // World point -> (col, row). Clamped to grid bounds.
    public static (int col, int row) WorldToCell(Vec2 p)
    {
        int c = Math.Clamp((int)MathF.Floor(p.X / CellSize), 0, Cols - 1);
        int r = Math.Clamp((int)MathF.Floor(p.Y / CellSize), 0, Rows - 1);
        return (c, r);
    }

    public IEnumerable<Mushroom> AllMushrooms()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (_cells[c, r] != null) yield return _cells[c, r]!;
    }

    // Generate a randomized starting field. Sparser at the bottom (so the player
    // has clear lines of sight).
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
