using System;

namespace Koa.Game;

// Thin domain wrapper over the shared FlowField. Holds one field sized to the
// current map and exposes the two operations the sim needs: rebuild the flood
// from the hero's cell (cheap, done every few frames), and read each enemy's
// step direction. One field serves the entire swarm — that's the whole point of
// the flow-field approach over per-enemy pathfinding.
public sealed class Pathing
{
    readonly TileMap _map;
    readonly FlowField _field;

    public Pathing(TileMap map)
    {
        _map = map;
        _field = new FlowField(map.Cols, map.Rows);
    }

    // Re-flood the distance field outward from the hero's current cell across all
    // walkable tiles. Called on a cadence (every N frames) from GameWorld, not
    // every frame — the field stays "good enough" between rebuilds because the
    // hero only moves a fraction of a cell per frame.
    public void Rebuild(Vec2 heroPos)
    {
        var (hc, hr) = _map.WorldToCell(heroPos);
        _field.Rebuild(hc, hr, _map.IsWalkable);
    }

    // Multi-source flood from several hero positions (co-op): each enemy then
    // chases whichever hero is nearest, for free.
    public void Rebuild(ReadOnlySpan<Vec2> heroPositions)
    {
        Span<(int, int)> cells = heroPositions.Length <= 8
            ? stackalloc (int, int)[heroPositions.Length]
            : new (int, int)[heroPositions.Length];
        for (int i = 0; i < heroPositions.Length; i++)
            cells[i] = _map.WorldToCell(heroPositions[i]);
        _field.Rebuild(cells, _map.IsWalkable);
    }

    // The step toward the hero from a world position, as a (possibly zero)
    // direction vector. The enemy multiplies this by its speed and feeds it to the
    // wall-slide mover. Unlike the field's raw 4-neighbour step, this blends the
    // descending horizontal and vertical neighbours into a DIAGONAL when both
    // descend, so enemies cut corners toward the hero in the open instead of
    // moving only along the axes. A diagonal is suppressed when the corner cell is
    // not walkable, so they never clip through wall corners (and naturally fall
    // back to single-axis movement inside 1-tile corridors).
    public Vec2 FlowDir(Vec2 from)
    {
        var (c, r) = _map.WorldToCell(from);
        int here = _field.Dist(c, r);
        if (here == FlowField.Unreachable) return Vec2.Zero;

        int dl = _field.Dist(c - 1, r), dR = _field.Dist(c + 1, r);
        int du = _field.Dist(c, r - 1), dd = _field.Dist(c, r + 1);

        // Downhill on each axis, toward the lower-distance neighbour, only if it
        // actually descends below the current cell. (Walls read as Unreachable =
        // int.MaxValue, so they're never chosen.)
        float fx = 0f, fy = 0f;
        if      (dl < dR && dl < here) fx = -1f;
        else if (dR < dl && dR < here) fx =  1f;
        if      (du < dd && du < here) fy = -1f;
        else if (dd < du && dd < here) fy =  1f;

        // Allow the diagonal only when the corner cell is open; otherwise keep the
        // steeper axis so we don't cut through a wall corner.
        if (fx != 0f && fy != 0f && !_map.IsWalkable(c + (int)fx, r + (int)fy))
        {
            int hx = fx < 0f ? dl : dR;
            int hy = fy < 0f ? du : dd;
            if (hx <= hy) fy = 0f; else fx = 0f;
        }

        return new Vec2(fx, fy);
    }

    // Whether a cell was reached by the last flood (used to tell whether an
    // enemy actually has a path to the hero).
    public bool Reachable(Vec2 from)
    {
        var (c, r) = _map.WorldToCell(from);
        return _field.Dist(c, r) != FlowField.Unreachable;
    }
}
