using System;
using System.Collections.Generic;

namespace Arcade.Common.Chassis;

// A multi-source breadth-first distance field over a walkable grid, plus the
// per-cell "step toward the nearest source" direction it implies. This is the
// swarm-AI workhorse for any "many enemies chase the player(s)" game (Koa's
// generator hordes): instead of every enemy running its own pathfind every
// frame (which both costs O(enemies * search) and corner-clips around concave
// walls), we flood ONE field outward from the hero cell(s) once every few
// frames, and each enemy just reads the precomputed best neighbour. Cost is
// O(cells) per rebuild, shared by all enemies, and the field naturally routes
// around concave geometry because BFS distance is true shortest-path-on-the-grid.
//
// Multi-source is free: seed the queue with every hero cell at distance 0 and
// the flood gives "distance to the NEAREST hero" everywhere — i.e. co-op "chase
// whoever is closest" with no extra code.
//
// 4-connected (orthogonal) flood. Diagonal movement is left to the consumer's
// continuous mover (Koa steps via TileGrid.MoveCircle), so the field only needs
// to express cardinal gradient; enemies that want to cut a diagonal can blend
// two adjacent cardinal flows. Unreachable / solid cells carry distance
// Unreachable and yield a zero FlowDir.
public sealed class FlowField
{
    // Sentinel distance for "wall or not yet reached". Picked large enough that
    // it never collides with a real distance on any sane grid.
    public const int Unreachable = int.MaxValue;

    public int Cols { get; }
    public int Rows { get; }

    // BFS distance (in cells) from the nearest source to each cell, or
    // Unreachable. Flat [col + row*Cols], exposed read-only via Dist(col,row).
    readonly int[] _dist;

    // Scratch queue reused across rebuilds so a steady-state rebuild allocates
    // nothing. Capacity grows to the cell count on first use.
    readonly Queue<int> _queue;

    public FlowField(int cols, int rows)
    {
        if (cols <= 0 || rows <= 0) throw new ArgumentOutOfRangeException(nameof(cols));
        Cols = cols;
        Rows = rows;
        _dist = new int[cols * rows];
        _queue = new Queue<int>(cols * rows);
    }

    bool InBounds(int col, int row) => col >= 0 && col < Cols && row >= 0 && row < Rows;

    // Distance from the nearest source to (col,row); Unreachable off-grid or for
    // walls/unreached cells.
    public int Dist(int col, int row) => InBounds(col, row) ? _dist[col + row * Cols] : Unreachable;

    // Flood the field from one source cell. Equivalent to Rebuild with a single
    // source; provided for the common single-hero case.
    public void Rebuild(int sourceCol, int sourceRow, Func<int, int, bool> isWalkable) =>
        Rebuild(stackalloc (int, int)[1] { (sourceCol, sourceRow) }, isWalkable);

    // Flood the field outward from every source cell simultaneously. After this
    // call Dist/FlowDir reflect distance-to-nearest-source across all walkable
    // cells. `isWalkable(col,row)` is the consumer's predicate (false for walls,
    // closed doors, out-of-bounds, etc.). Sources that are themselves not
    // walkable are skipped.
    public void Rebuild(ReadOnlySpan<(int col, int row)> sources, Func<int, int, bool> isWalkable)
    {
        // Reset every cell to Unreachable. (A full clear each rebuild is simplest
        // and the field is small; the per-rebuild cost is dominated by the flood.)
        Array.Fill(_dist, Unreachable);
        _queue.Clear();

        foreach (var (sc, sr) in sources)
        {
            if (!InBounds(sc, sr) || !isWalkable(sc, sr)) continue;
            int idx = sc + sr * Cols;
            if (_dist[idx] == 0) continue; // de-dupe coincident sources
            _dist[idx] = 0;
            _queue.Enqueue(idx);
        }

        // Standard 4-neighbour BFS. Because every edge has unit cost, the first
        // time we reach a cell is its shortest distance, so no relaxation pass is
        // needed.
        while (_queue.Count > 0)
        {
            int idx = _queue.Dequeue();
            int col = idx % Cols;
            int row = idx / Cols;
            int next = _dist[idx] + 1;

            // Up, down, left, right.
            TryVisit(col,     row - 1, next, isWalkable);
            TryVisit(col,     row + 1, next, isWalkable);
            TryVisit(col - 1, row,     next, isWalkable);
            TryVisit(col + 1, row,     next, isWalkable);
        }
    }

    void TryVisit(int col, int row, int dist, Func<int, int, bool> isWalkable)
    {
        if (!InBounds(col, row)) return;
        int idx = col + row * Cols;
        if (_dist[idx] != Unreachable) return;   // already reached (shorter or equal)
        if (!isWalkable(col, row)) return;        // wall / closed door
        _dist[idx] = dist;
        _queue.Enqueue(idx);
    }

    // The unit step (dc,dr) from (col,row) toward the source: the orthogonal
    // neighbour with the lowest distance. Returns (0,0) when the cell is itself
    // a source, unreachable, or has no lower neighbour (e.g. fully walled in) —
    // the caller treats that as "hold position". This is the per-enemy read that
    // makes the whole swarm follow the hero for the cost of four array lookups.
    public (int dc, int dr) FlowDir(int col, int row)
    {
        int here = Dist(col, row);
        if (here == Unreachable) return (0, 0);

        int best = here;
        int bdc = 0, bdr = 0;

        // Pick the neighbour that most reduces distance. Ties resolve to the
        // first checked (up), which is fine — any descending neighbour is a valid
        // shortest step.
        Consider(col,     row - 1, ref best, ref bdc, ref bdr,  0, -1);
        Consider(col,     row + 1, ref best, ref bdc, ref bdr,  0,  1);
        Consider(col - 1, row,     ref best, ref bdc, ref bdr, -1,  0);
        Consider(col + 1, row,     ref best, ref bdc, ref bdr,  1,  0);

        return (bdc, bdr);
    }

    void Consider(int col, int row, ref int best, ref int bdc, ref int bdr, int dc, int dr)
    {
        int d = Dist(col, row);
        if (d < best)
        {
            best = d;
            bdc = dc;
            bdr = dr;
        }
    }
}
