using System;

namespace Koa.Game;

// Koa's static terrain glyphs. Only terrain that participates in collision /
// culling lives here as a tile; collectables and generators are entities (see
// Level's terrain-vs-feature split). Door is a hybrid — it blocks like a wall
// until OpenDoor flips it to Floor when the hero spends a key.
public enum Tile : byte
{
    Floor,      // walkable (also the out-of-bounds default — perimeter is walled)
    Wall,       // solid
    Door,       // solid until opened with a key, then becomes Floor
    Exit,       // walkable; stepping on it clears the level
    Generator,  // a spawner sits here; solid until the generator is destroyed
    Void,       // solid, unlit "outside the dungeon" filler
}

// Thin domain wrapper over the shared TileGrid<Tile>. Owns the Koa-specific
// notions of "what blocks movement" and the key/door + generator-death tile
// mutations; all cell math and the wall-slide resolver come from TileGrid<T>.
public sealed class TileMap
{
    public const float CellSize = 32f;

    public readonly TileGrid<Tile> Grid;

    public int   Cols        => Grid.Cols;
    public int   Rows        => Grid.Rows;
    public float WorldWidth  => Grid.WorldWidth;
    public float WorldHeight => Grid.WorldHeight;

    public TileMap(int cols, int rows)
    {
        Grid = new TileGrid<Tile>(cols, rows, CellSize);
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

    // True for tiles that stop movement. Out-of-bounds counts as blocked so the
    // world edge is solid even though the grid's default cell reads as Floor.
    public bool IsBlocked(int col, int row)
    {
        if (!Grid.InBounds(col, row)) return true;
        var t = Grid[col, row];
        return t == Tile.Wall || t == Tile.Door || t == Tile.Generator || t == Tile.Void;
    }

    // Same test at a world point.
    public bool IsBlockedAt(float x, float y)
    {
        var (c, r) = Grid.WorldToCell(x, y);
        return IsBlocked(c, r);
    }

    // Walkable for AI flood/movement: anything not blocked (Floor / Exit). Doors
    // count as NOT walkable for the flow field so enemies don't path through
    // closed doors; the hero opens them by contact, not the swarm.
    public bool IsWalkable(int col, int row) => !IsBlocked(col, row);

    // Spend a key on a door cell, opening it to Floor. Returns true if a door was
    // actually there to open.
    public bool OpenDoor(int col, int row)
    {
        if (Grid.InBounds(col, row) && Grid[col, row] == Tile.Door)
        {
            Grid[col, row] = Tile.Floor;
            return true;
        }
        return false;
    }

    // When a generator entity dies, clear its tile so movement/AI can flow
    // through where it stood.
    public void ClearGenerator(int col, int row)
    {
        if (Grid.InBounds(col, row) && Grid[col, row] == Tile.Generator)
            Grid[col, row] = Tile.Floor;
    }

    // Forward the wall-slide resolver, supplying Koa's solidity predicate.
    public bool MoveCircle(ref Vec2 pos, float radius, float dx, float dy) =>
        Grid.MoveCircle(ref pos, radius, dx, dy, IsBlocked);

    // Projectile solidity: real walls/doors/edge stop a shot, but a generator
    // tile does NOT — generators are shootable targets, so a bullet must be able
    // to fly onto the generator's cell where the generator entity's hit-circle
    // (in GameWorld.HandleProjectileHits) can register the hit. (Generators stay
    // solid to MOVEMENT via IsBlocked, so heroes/enemies still can't walk onto them.)
    public bool IsProjectileBlocked(int col, int row)
    {
        if (!Grid.InBounds(col, row)) return true;
        var t = Grid[col, row];
        return t == Tile.Wall || t == Tile.Door || t == Tile.Void;
    }

    // Move a projectile, stopping at walls/doors but passing over generator tiles.
    public bool MoveProjectile(ref Vec2 pos, float radius, float dx, float dy) =>
        Grid.MoveCircle(ref pos, radius, dx, dy, IsProjectileBlocked);
}
