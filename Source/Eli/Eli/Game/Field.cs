using System;

namespace Eli.Game;

// Eli's terrain glyphs. Unlike Koa's dungeon — which is authored once and only
// ever flips Door -> Floor — this grid is REWRITTEN CONTINUOUSLY: the digger
// carves Dirt -> Tunnel wherever it walks, and falling boulders carve as they
// drop. Every consumer of grid state (flow field, boulder support, culling)
// therefore has to tolerate per-frame terrain edits; see TerrainDirty below.
public enum Tile : byte
{
    Sky,     // rows 0..SkyRows-1: the horizon band ABOVE the ground. Solid to
             // everything — it is scenery, not playable space. The top dirt row
             // is the surface, and the digger is pinned to it.
    Dirt,    // packed earth: passable BY THE DIGGER ONLY (at dig speed), carved on contact.
    Tunnel,  // carved out: freely walkable by everything.
    Rock,    // bedrock frame + floor: indestructible, stops everything including boulders.
}

// The mutable dirt field. Thin domain wrapper over the shared TileGrid<Tile>,
// exactly as Koa's TileMap wraps it — all cell math and the axis-separated
// wall-slide resolver come from TileGrid<T>. What Eli adds on top is the three
// solidity predicates (see below), the carve operation, the stratum lookup, and
// the dirty flag that forces a flow-field re-flood after a terrain edit.
public sealed class Field
{
    public const float CellSize = 32f;

    // Rows 0..SkyRows-1 are open sky; the dirt starts below them.
    public const int SkyRows = 2;

    // Rows per stratum. Four strata of StrataRows each fill the rest of the grid.
    public const int StrataRows = 7;
    public const int StrataCount = 4;

    public readonly TileGrid<Tile> Grid;

    // Cells occupied by a boulder that is currently sitting still.
    //
    // Boulders stay ENTITIES (a tile cannot hold a sub-cell Y while one falls),
    // but a settled boulder fills exactly one cell and has to be as solid as
    // bedrock. This overlay is how the entity makes itself felt by the tile-based
    // predicates: MoveCircle then stops bodies flush against its face, and — because
    // IsWalkable is the inverse of IsBlockedForEnemy — the flow field routes the
    // swarm around it instead of straight through it. A falling boulder clears its
    // cell, because from then on it crushes rather than blocks.
    readonly bool[] _boulderCells;

    // Set by any terrain edit (Carve). GameWorld consumes it to force a
    // flow-field rebuild in the same frame, because a stale field over freshly
    // dug terrain reads immediately as enemies walking into dirt (or refusing to
    // enter a tunnel that now exists). Koa can get away with a pure frame
    // cadence because its terrain almost never changes; Eli's changes most
    // frames the player is moving.
    bool _terrainDirty;

    public int   Cols        => Grid.Cols;
    public int   Rows        => Grid.Rows;
    public float WorldWidth  => Grid.WorldWidth;
    public float WorldHeight => Grid.WorldHeight;

    public Field(int cols, int rows)
    {
        Grid = new TileGrid<Tile>(cols, rows, CellSize);
        _boulderCells = new bool[cols * rows];
    }

    public Tile this[int col, int row]
    {
        get => Grid[col, row];
        set => Grid[col, row] = value;
    }

    public Vec2 CellCenter(int col, int row) => Grid.CellCenter(col, row);
    public (int col, int row) WorldToCell(Vec2 p) => Grid.WorldToCell(p);
    public (int col, int row) WorldToCell(float x, float y) => Grid.WorldToCell(x, y);
    public bool InBounds(int col, int row) => Grid.InBounds(col, row);

    public bool HasBoulder(int col, int row) =>
        Grid.InBounds(col, row) && _boulderCells[col + row * Cols];

    // Mark/clear a boulder's cell. Counts as a terrain edit, so the flow field
    // re-floods in the same frame — otherwise the swarm keeps walking a route that
    // a boulder has just blocked (or ignores one it has just vacated).
    public void SetBoulderCell(int col, int row, bool occupied)
    {
        if (!Grid.InBounds(col, row)) return;
        int i = col + row * Cols;
        if (_boulderCells[i] == occupied) return;
        _boulderCells[i] = occupied;
        _terrainDirty = true;
    }

    // Read-and-clear: returns true (once) if terrain changed since the last call.
    public bool ConsumeTerrainDirty()
    {
        if (!_terrainDirty) return false;
        _terrainDirty = false;
        return true;
    }

    // --- The three solidity predicates --------------------------------------
    //
    // Koa needed two (bodies vs. projectiles). Eli needs three, because "what is
    // solid" genuinely differs three ways — and keeping them separate is what
    // lets the digger tunnel through the same cells that stop everything else.

    // The digger: bedrock, the world edge and the SKY stop it. Dirt is PASSABLE
    // (at reduced speed, applied by GameWorld) — that is the whole game.
    //
    // Sky is solid deliberately. With it open the digger walked up into row 0 and
    // then ran the full width of the field at walk speed without digging at all —
    // a free highway over every level. Keeping it solid pins the digger to the
    // topmost dirt row, which is the surface.
    public bool IsBlockedForDigger(int col, int row)
    {
        if (!Grid.InBounds(col, row)) return true;
        var t = Grid[col, row];
        return t == Tile.Rock || t == Tile.Sky || HasBoulder(col, row);
    }

    // Enemies in tunnel mode: dirt is solid, so they are confined to the network
    // the player has dug. (A phasing enemy bypasses this predicate entirely by
    // not using MoveCircle at all.)
    //
    // Sky is solid here too, and that matters for more than movement: this
    // predicate backs IsWalkable, so an open sky band would let the flow field
    // flood across the top of the map and join every tunnel to every other one.
    // That left enemies almost never stranded (gutting the phasing trigger) and
    // defeated Level 4, whose quadrants are supposed to be joined only by digging
    // across the surface.
    public bool IsBlockedForEnemy(int col, int row)
    {
        if (!Grid.InBounds(col, row)) return true;
        var t = Grid[col, row];
        return t == Tile.Dirt || t == Tile.Rock || t == Tile.Sky || HasBoulder(col, row);
    }

    // The harpoon tip: stops on dirt and bedrock. Deliberately a separate
    // predicate from IsBlockedForEnemy even though the two agree today, so a
    // later "the harpoon bores a little way into dirt" tweak is a one-predicate
    // change rather than an edit shared with enemy movement.
    public bool IsBlockedForHarpoon(int col, int row)
    {
        if (!Grid.InBounds(col, row)) return true;
        var t = Grid[col, row];
        return t == Tile.Dirt || t == Tile.Rock || t == Tile.Sky || HasBoulder(col, row);
    }

    // Walkable for the flow field: the inverse of enemy solidity.
    public bool IsWalkable(int col, int row) => !IsBlockedForEnemy(col, row);

    public bool IsBlockedForDiggerAt(float x, float y)
    {
        var (c, r) = Grid.WorldToCell(x, y);
        return IsBlockedForDigger(c, r);
    }

    // True if the world point sits in undug earth (drives the digger's speed).
    public bool IsDirtAt(float x, float y)
    {
        var (c, r) = Grid.WorldToCell(x, y);
        return Grid.InBounds(c, r) && Grid[c, r] == Tile.Dirt;
    }

    // --- Digging ------------------------------------------------------------

    // Flip every Dirt cell overlapped by a circle of `radius` at `pos` to Tunnel.
    // Returns true if anything actually changed (and marks the terrain dirty so
    // the flow field re-floods this frame).
    //
    // Callers pass a SHRUNK radius (GameWorld.CarveFraction): because movement is
    // 4-directional and runs through the corridor-centering assist, the body is
    // always eased onto the cell centre line, so a shrunk carve radius yields
    // clean one-cell-wide corridors instead of a two-cell smear whenever the
    // body straddles a cell boundary.
    public bool Carve(Vec2 pos, float radius)
    {
        int c0 = (int)MathF.Floor((pos.X - radius) / CellSize);
        int c1 = (int)MathF.Floor((pos.X + radius) / CellSize);
        int r0 = (int)MathF.Floor((pos.Y - radius) / CellSize);
        int r1 = (int)MathF.Floor((pos.Y + radius) / CellSize);

        bool changed = false;
        for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
            {
                if (!Grid.InBounds(c, r) || Grid[c, r] != Tile.Dirt) continue;
                Grid[c, r] = Tile.Tunnel;
                changed = true;
            }

        if (changed) _terrainDirty = true;
        return changed;
    }

    // --- Strata -------------------------------------------------------------

    // Which of the four strata a row belongs to (0 = shallowest). Sky rows and
    // anything above the first stratum clamp to 0; anything below the last
    // clamps to StrataCount-1. Drives both the render hue and the depth score
    // multiplier — the strata are a scoring mechanic, not decoration.
    public static int StratumAt(int row)
    {
        int depth = row - SkyRows;
        if (depth < 0) return 0;
        return Math.Clamp(depth / StrataRows, 0, StrataCount - 1);
    }

    public int StratumAtWorld(float y)
    {
        var (_, r) = Grid.WorldToCell(0f, y);
        return StratumAt(r);
    }

    // --- Motion -------------------------------------------------------------

    // Forward the shared wall-slide resolver with each caller's own predicate.
    public bool MoveDigger(ref Vec2 pos, float radius, float dx, float dy) =>
        Grid.MoveCircle(ref pos, radius, dx, dy, IsBlockedForDigger);

    public bool MoveEnemy(ref Vec2 pos, float radius, float dx, float dy) =>
        Grid.MoveCircle(ref pos, radius, dx, dy, IsBlockedForEnemy);
}
