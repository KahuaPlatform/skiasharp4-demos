using System;
using SkiaSharp;

namespace Arcade.Common.Chassis;

// A generic, fixed-size 2-D grid of value-type cells plus the cell math and the
// continuous "circle vs. solid tiles" motion resolver every top-down tile game
// needs. Generalises the bespoke `Arena`/`Grid` grids in Hahai/Kanapi.
//
// `T` is a value type (an enum like Koa's `Tile`, or a struct) so the backing
// store is a flat blittable array with no per-cell allocation. The grid itself
// has no notion of "what is solid" — the caller supplies an `isSolid` predicate
// to MoveCircle, because solidity is game-specific (a door is solid until a key
// opens it, etc.).
//
// Coordinate conventions, shared by every consumer:
//   * world space is pixels; (0,0) is the grid's top-left corner.
//   * cell (col,row) spans world x in [col*CellSize,(col+1)*CellSize) and
//     likewise for y; CellCenter returns the middle of that span.
//   * WorldToCell floors, so negative world coords map to negative cells (which
//     InBounds then rejects) — callers never get a false in-bounds for off-grid
//     points.
public sealed class TileGrid<T> where T : struct
{
    // Grid dimensions in cells and the world-space size of one (square) cell.
    public int Cols { get; }
    public int Rows { get; }
    public float CellSize { get; }

    // Total world extent in pixels — the bounds a clamped Camera2D frames to.
    public float WorldWidth  => Cols * CellSize;
    public float WorldHeight => Rows * CellSize;

    // Flat [col + row*Cols] backing store. Exposed via the indexer; the raw array
    // is intentionally not public so callers go through the bounds-aware accessor.
    readonly T[] _cells;

    // Sub-pixel slack used by MoveCircle when sampling the body's perpendicular
    // extent: a circle that only grazes a neighbouring wall row/column by less
    // than this is treated as clear, so an entity travelling a 1-tile corridor
    // that is a hair off-centre doesn't snag on the seam.
    const float Epsilon = 0.5f;

    public TileGrid(int cols, int rows, float cellSize)
    {
        if (cols <= 0 || rows <= 0) throw new ArgumentOutOfRangeException(nameof(cols), "grid must be at least 1x1");
        if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
        Cols = cols;
        Rows = rows;
        CellSize = cellSize;
        _cells = new T[cols * rows];
    }

    // True when (col,row) is a valid cell index.
    public bool InBounds(int col, int row) => col >= 0 && col < Cols && row >= 0 && row < Rows;

    // Indexed cell access. Reads of out-of-bounds cells return default(T) so the
    // caller can treat "off the edge" uniformly (Koa maps default == Tile.Floor,
    // and walls its perimeter, so the open default is never actually reachable);
    // writes to out-of-bounds cells are ignored.
    public T this[int col, int row]
    {
        get => InBounds(col, row) ? _cells[col + row * Cols] : default;
        set { if (InBounds(col, row)) _cells[col + row * Cols] = value; }
    }

    // --- Cell <-> world math -------------------------------------------------

    // World-space centre of a cell. Static-style helper but instance-bound so it
    // picks up this grid's CellSize.
    public Vec2 CellCenter(int col, int row) =>
        new(col * CellSize + CellSize * 0.5f, row * CellSize + CellSize * 0.5f);

    // Which cell a world point falls in (floored — see the type remarks). May be
    // out of bounds; pair with InBounds when that matters.
    public (int col, int row) WorldToCell(float x, float y) =>
        ((int)MathF.Floor(x / CellSize), (int)MathF.Floor(y / CellSize));

    public (int col, int row) WorldToCell(Vec2 p) => WorldToCell(p.X, p.Y);

    // --- Continuous circle-vs-tile motion (the wall slide) -------------------

    // Move a circle of `radius` centred at `pos` by (`dx`,`dy`) world units,
    // resolving collisions against solid tiles, and write the resolved position
    // back through `pos`. Returns true if the move was blocked on either axis
    // (Koa uses that to expire projectiles that hit a wall).
    //
    // The key technique — and the reason this lives in the chassis — is that the
    // two axes are resolved INDEPENDENTLY: we apply the full X displacement and
    // clamp it against any solid cell the circle's vertical extent overlaps, then
    // (from the already-updated X) apply Y and clamp it likewise. Resolving axes
    // separately is what produces Gauntlet-style wall sliding: pushing diagonally
    // into a wall zeroes only the blocked axis, so the entity keeps gliding along
    // the free one instead of stopping dead. Doing both axes together (a single
    // swept test) would instead snag on the wall and kill all motion.
    //
    // `isSolid(col,row)` is the caller's solidity predicate; it is also treated
    // as solid for out-of-bounds cells by the caller's own convention (Koa walls
    // its perimeter, so the world edge is solid regardless).
    //
    // Implementation is an axis-separated SWEPT resolver. Each axis is resolved
    // against the *leading-edge* cell line every step (no "only when newly
    // entering" gate — that gate let an entity already flush against or sitting
    // inside a wall column drift straight through). Because a single frame's
    // displacement can exceed a cell — which would tunnel through a 1-tile-thick
    // wall — the whole move is sub-stepped so no sub-step advances more than
    // (roughly) half a cell on either axis before the wall test runs again.
    public bool MoveCircle(ref Vec2 pos, float radius, float dx, float dy, Func<int, int, bool> isSolid)
    {
        // Sub-step so a single big delta can't tunnel a thin wall: cap the
        // per-step travel on the dominant axis to half a cell.
        float maxStep = CellSize * 0.5f;
        float dist = MathF.Max(MathF.Abs(dx), MathF.Abs(dy));
        int steps = dist <= maxStep ? 1 : (int)MathF.Ceiling(dist / maxStep);

        float sx = dx / steps;
        float sy = dy / steps;

        bool blocked = false;
        for (int i = 0; i < steps; i++)
        {
            if (sx != 0f && MoveAxisX(ref pos, radius, sx, isSolid)) blocked = true;
            if (sy != 0f && MoveAxisY(ref pos, radius, sy, isSolid)) blocked = true;
        }
        return blocked;
    }

    // Resolve a single X sub-step. Tests the column the circle's leading X edge
    // lands in (at the candidate position) across the body's vertical span, and
    // on a hit clamps the leading face flush against that wall column. Returns
    // true if blocked.
    bool MoveAxisX(ref Vec2 pos, float radius, float dx, Func<int, int, bool> isSolid)
    {
        float newX = pos.X + dx;
        // Leading edge of the circle at the candidate position.
        float edge = dx > 0f ? newX + radius : newX - radius;
        int edgeCol = (int)MathF.Floor(edge / CellSize);

        // Rows the circle's vertical extent overlaps (use current Y; Y resolves in
        // its own pass). A tiny epsilon shrink stops a centred circle in a 1-wide
        // corridor from snagging on the seam where two wall rows meet its edge.
        float shrink = MathF.Max(0f, radius - Epsilon);
        int rowMin = (int)MathF.Floor((pos.Y - shrink) / CellSize);
        int rowMax = (int)MathF.Floor((pos.Y + shrink) / CellSize);

        bool hit = false;
        for (int r = rowMin; r <= rowMax && !hit; r++)
            if (isSolid(edgeCol, r)) hit = true;

        if (hit)
        {
            // Clamp the leading face exactly onto the wall column's near face.
            // Walls are solid: this both stops a head-on move at the face and
            // holds an entity already flush against the wall from drifting in.
            pos.X = dx > 0f ? edgeCol * CellSize - radius
                            : (edgeCol + 1) * CellSize + radius;
            return true;
        }

        pos.X = newX;
        return false;
    }

    // Resolve a single Y sub-step (mirror of MoveAxisX, using the already-resolved
    // X for the horizontal body span).
    bool MoveAxisY(ref Vec2 pos, float radius, float dy, Func<int, int, bool> isSolid)
    {
        float newY = pos.Y + dy;
        float edge = dy > 0f ? newY + radius : newY - radius;
        int edgeRow = (int)MathF.Floor(edge / CellSize);

        float shrink = MathF.Max(0f, radius - Epsilon);
        int colMin = (int)MathF.Floor((pos.X - shrink) / CellSize);
        int colMax = (int)MathF.Floor((pos.X + shrink) / CellSize);

        bool hit = false;
        for (int c = colMin; c <= colMax && !hit; c++)
            if (isSolid(c, edgeRow)) hit = true;

        if (hit)
        {
            pos.Y = dy > 0f ? edgeRow * CellSize - radius
                            : (edgeRow + 1) * CellSize + radius;
            return true;
        }

        pos.Y = newY;
        return false;
    }

    // Convenience: the world-space rect of a cell (handy for tile rendering /
    // debug overlays).
    public SKRect CellRect(int col, int row) =>
        new(col * CellSize, row * CellSize, (col + 1) * CellSize, (row + 1) * CellSize);
}
