using System;
using System.Collections.Generic;

namespace Arcade.Common.Chassis;

// Authored-level helper: validate a rectangular block of ASCII rows and call
// back once per glyph with its column, row, and character. Generalises the
// hand-rolled "string[] Layout + nested-loop switch" idiom in Hahai's `Arena`
// constructor so any authored-level game can share the parse + the up-front
// rectangularity check (a ragged map is a content bug, and is far cheaper to
// catch here than to chase as a mis-rendered tile later).
//
// AsciiMap deliberately knows nothing about tiles or entities: it just walks the
// grid and hands each glyph to the game, which decides whether '#' is a wall, a
// 'G' is a generator entity, and so on. That terrain-vs-feature split lives in
// the consumer (Koa.Level), not here.
public static class AsciiMap
{
    // Validate that every row in `rows` is the same width (and that there is at
    // least one non-empty row), then invoke `onCell(col, row, glyph)` for every
    // character, scanning left-to-right, top-to-bottom. Returns the grid
    // dimensions so the caller can size its TileGrid to match.
    //
    // Throws ArgumentException on an empty map or a ragged row — fail loud at
    // load time rather than silently mis-parsing.
    public static (int cols, int rows) Parse(IReadOnlyList<string> rows, Action<int, int, char> onCell)
    {
        if (rows is null) throw new ArgumentNullException(nameof(rows));
        if (rows.Count == 0) throw new ArgumentException("ASCII map has no rows", nameof(rows));

        int cols = rows[0].Length;
        if (cols == 0) throw new ArgumentException("ASCII map row 0 is empty", nameof(rows));

        // Rectangularity check first, so onCell never fires for a malformed map.
        for (int r = 0; r < rows.Count; r++)
        {
            if (rows[r].Length != cols)
                throw new ArgumentException(
                    $"ASCII map is ragged: row {r} has {rows[r].Length} columns, expected {cols}", nameof(rows));
        }

        for (int r = 0; r < rows.Count; r++)
        {
            string line = rows[r];
            for (int c = 0; c < cols; c++)
                onCell(c, r, line[c]);
        }

        return (cols, rows.Count);
    }

    // Convenience overload for the common `string[]` literal case.
    public static (int cols, int rows) Parse(string[] rows, Action<int, int, char> onCell) =>
        Parse((IReadOnlyList<string>)rows, onCell);
}
