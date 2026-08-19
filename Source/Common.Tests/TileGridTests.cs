namespace Arcade.Common.Tests;

// TileGrid<T> carries the motion resolver every top-down tile game in the repo
// depends on. The two properties worth protecting are the ones its own remarks
// call out: axes resolve INDEPENDENTLY (that separation *is* the wall slide), and
// the move is SUB-STEPPED (so a fast body cannot tunnel a one-tile wall).
[TestClass]
public sealed class TileGridTests
{
    const float Cell = 32f;
    const float Tol = 1e-3f;

    enum Tile : byte { Floor, Wall }

    // A 10x10 grid with a solid border, plus whatever extra walls the caller adds.
    static TileGrid<Tile> Walled(params (int col, int row)[] walls)
    {
        var g = new TileGrid<Tile>(10, 10, Cell);
        for (int i = 0; i < 10; i++)
        {
            g[i, 0] = Tile.Wall; g[i, 9] = Tile.Wall;
            g[0, i] = Tile.Wall; g[9, i] = Tile.Wall;
        }
        foreach (var (c, r) in walls) g[c, r] = Tile.Wall;
        return g;
    }

    static System.Func<int, int, bool> Solid(TileGrid<Tile> g) =>
        (c, r) => !g.InBounds(c, r) || g[c, r] == Tile.Wall;

    // --- Cell maths ----------------------------------------------------------

    [TestMethod]
    public void Constructor_RejectsDegenerateDimensions()
    {
        Assert.ThrowsException<System.ArgumentOutOfRangeException>(() => new TileGrid<Tile>(0, 5, Cell));
        Assert.ThrowsException<System.ArgumentOutOfRangeException>(() => new TileGrid<Tile>(5, 0, Cell));
        Assert.ThrowsException<System.ArgumentOutOfRangeException>(() => new TileGrid<Tile>(5, 5, 0f));
    }

    [TestMethod]
    public void WorldToCell_Floors_SoOffGridPointsGoNegativeAndFailInBounds()
    {
        var g = new TileGrid<Tile>(10, 10, Cell);
        Assert.AreEqual((0, 0), g.WorldToCell(0f, 0f));
        Assert.AreEqual((0, 0), g.WorldToCell(31.9f, 31.9f));
        Assert.AreEqual((1, 1), g.WorldToCell(32f, 32f));

        // The documented guarantee: a point just off the top-left maps to -1, which
        // InBounds then rejects - callers never get a false in-bounds.
        Assert.AreEqual((-1, -1), g.WorldToCell(-0.1f, -0.1f));
        Assert.IsFalse(g.InBounds(-1, -1));
    }

    [TestMethod]
    public void Indexer_OutOfBounds_ReadsDefaultAndSwallowsWrites()
    {
        var g = new TileGrid<Tile>(4, 4, Cell);
        Assert.AreEqual(default(Tile), g[-1, 0]);
        Assert.AreEqual(default(Tile), g[99, 99]);
        g[99, 99] = Tile.Wall;                    // must not throw
        Assert.AreEqual(default(Tile), g[99, 99]);
    }

    [TestMethod]
    public void CellCenter_And_CellRect_Agree()
    {
        var g = new TileGrid<Tile>(10, 10, Cell);
        var c = g.CellCenter(2, 3);
        Assert.AreEqual(2 * Cell + Cell / 2, c.X, Tol);
        Assert.AreEqual(3 * Cell + Cell / 2, c.Y, Tol);

        var r = g.CellRect(2, 3);
        Assert.AreEqual(c.X, r.MidX, Tol);
        Assert.AreEqual(c.Y, r.MidY, Tol);
        Assert.AreEqual(Cell, r.Width, Tol);
    }

    [TestMethod]
    public void WorldSize_IsCellsTimesCellSize()
    {
        var g = new TileGrid<Tile>(44, 30, Cell);
        Assert.AreEqual(44 * Cell, g.WorldWidth, Tol);
        Assert.AreEqual(30 * Cell, g.WorldHeight, Tol);
    }

    // --- The wall slide ------------------------------------------------------

    [TestMethod]
    public void MoveCircle_DiagonalIntoWall_SlidesAlongTheFreeAxis()
    {
        // THE Gauntlet behaviour, and the reason the resolver splits the axes.
        var g = Walled();
        var pos = g.CellCenter(5, 1);          // row 1, directly under the top wall
        float startX = pos.X;

        bool blocked = g.MoveCircle(ref pos, 10f, dx: 8f, dy: -8f, Solid(g));

        Assert.IsTrue(blocked, "pushing into the wall reports blocked");
        Assert.IsTrue(pos.X > startX + 7f, "the free axis keeps moving - that is the slide");
        Assert.AreEqual(Cell + 10f, pos.Y, 0.5f, "the blocked axis is clamped flush to the wall face");
    }

    [TestMethod]
    public void MoveCircle_HeadOnIntoWall_StopsFlushAndReportsBlocked()
    {
        var g = Walled();
        var pos = g.CellCenter(5, 1);
        bool blocked = g.MoveCircle(ref pos, 10f, 0f, -100f, Solid(g));
        Assert.IsTrue(blocked);
        Assert.AreEqual(Cell + 10f, pos.Y, 0.5f, "leading face rests exactly on the wall's near face");
    }

    [TestMethod]
    public void MoveCircle_AlreadyFlush_DoesNotDriftIntoTheWall()
    {
        // The remark that motivated dropping the "only when newly entering" gate.
        var g = Walled();
        var pos = new Vec2(g.CellCenter(5, 1).X, Cell + 10f);   // flush already
        for (int i = 0; i < 60; i++) g.MoveCircle(ref pos, 10f, 0f, -5f, Solid(g));
        Assert.AreEqual(Cell + 10f, pos.Y, 0.5f, "sixty frames of pressing must not creep through");
    }

    [TestMethod]
    public void MoveCircle_FastMove_CannotTunnelAOneTileWall()
    {
        // Sub-stepping exists for exactly this: a single frame's delta far larger
        // than a cell must still be stopped by a one-tile-thick wall.
        var g = Walled((5, 5));
        var pos = g.CellCenter(5, 2);
        bool blocked = g.MoveCircle(ref pos, 8f, 0f, dy: 10 * Cell, Solid(g));

        Assert.IsTrue(blocked, "a 320px step into a 32px wall must register as blocked");
        Assert.IsTrue(pos.Y < 5 * Cell, $"tunnelled through the wall to y={pos.Y}");
    }

    [TestMethod]
    public void MoveCircle_UnobstructedMove_AppliesInFull()
    {
        var g = Walled();
        var pos = g.CellCenter(5, 5);
        var before = pos;
        bool blocked = g.MoveCircle(ref pos, 8f, 12f, 7f, Solid(g));
        Assert.IsFalse(blocked);
        Assert.AreEqual(before.X + 12f, pos.X, Tol);
        Assert.AreEqual(before.Y + 7f, pos.Y, Tol);
    }

    [TestMethod]
    public void MoveCircle_ThreadsAOneTileCorridorWithoutSnagging()
    {
        // The Epsilon shrink: a body the same width as a 1-cell corridor must not
        // catch on the seam where the two wall rows meet its edge.
        var g = new TileGrid<Tile>(20, 5, Cell);
        for (int c = 0; c < 20; c++) { g[c, 1] = Tile.Wall; g[c, 3] = Tile.Wall; }  // corridor along row 2

        var pos = g.CellCenter(1, 2);
        float startX = pos.X;
        for (int i = 0; i < 120; i++) g.MoveCircle(ref pos, Cell * 0.34f, 2f, 0f, Solid(g));

        Assert.IsTrue(pos.X > startX + 200f, $"snagged in the corridor at x={pos.X} (started {startX})");
        Assert.AreEqual(g.CellCenter(1, 2).Y, pos.Y, 1f, "and stayed in the corridor");
    }

    [TestMethod]
    public void MoveCircle_ZeroDelta_IsANoOp()
    {
        var g = Walled();
        var pos = g.CellCenter(5, 5);
        var before = pos;
        Assert.IsFalse(g.MoveCircle(ref pos, 8f, 0f, 0f, Solid(g)));
        Assert.AreEqual(before.X, pos.X, Tol);
        Assert.AreEqual(before.Y, pos.Y, Tol);
    }
}
