namespace Arcade.Common.Tests;

// The swarm-AI workhorse. Its whole justification over per-enemy pathfinding is
// that BFS distance IS true shortest-path-on-the-grid, so it routes correctly
// around concave geometry where greedy targeting corner-clips. That is the
// property most worth pinning down.
[TestClass]
public sealed class FlowFieldTests
{
    // Builds an isWalkable predicate from an ASCII picture ('#' = wall).
    static System.Func<int, int, bool> Walkable(string[] rows) =>
        (c, r) => r >= 0 && r < rows.Length && c >= 0 && c < rows[r].Length && rows[r][c] != '#';

    [TestMethod]
    public void Rebuild_SourceIsZero_AndDistanceGrowsWithManhattanStepsInOpenSpace()
    {
        var f = new FlowField(5, 5);
        f.Rebuild(0, 0, (_, _) => true);

        Assert.AreEqual(0, f.Dist(0, 0));
        Assert.AreEqual(1, f.Dist(1, 0));
        Assert.AreEqual(1, f.Dist(0, 1));
        Assert.AreEqual(2, f.Dist(1, 1), "4-connected flood: a diagonal neighbour is two steps");
        Assert.AreEqual(8, f.Dist(4, 4));
    }

    [TestMethod]
    public void Walls_AndOffGrid_AreUnreachable()
    {
        var rows = new[]
        {
            ".....",
            ".###.",
            ".#S#.",     // S is sealed inside a wall box
            ".###.",
            ".....",
        };
        var f = new FlowField(5, 5);
        f.Rebuild(0, 0, Walkable(rows));

        Assert.AreEqual(FlowField.Unreachable, f.Dist(1, 1), "a wall cell is never reached");
        Assert.AreEqual(FlowField.Unreachable, f.Dist(2, 2), "nor is a cell walled off from the source");
        Assert.AreEqual(FlowField.Unreachable, f.Dist(-1, 0), "nor anything off-grid");
        Assert.AreEqual(FlowField.Unreachable, f.Dist(99, 99));
    }

    [TestMethod]
    public void FlowDir_PointsDownhill_TowardTheSource()
    {
        var f = new FlowField(5, 1);
        f.Rebuild(0, 0, (_, _) => true);

        Assert.AreEqual((-1, 0), f.FlowDir(3, 0), "step toward lower distance");
        Assert.AreEqual((0, 0), f.FlowDir(0, 0), "the source itself holds position");
    }

    [TestMethod]
    public void FlowDir_OnUnreachableCell_IsZero()
    {
        var rows = new[] { "..#..", "..#..", "..#.." };
        var f = new FlowField(5, 3);
        f.Rebuild(0, 0, Walkable(rows));
        Assert.AreEqual((0, 0), f.FlowDir(4, 1), "right of the wall is unreachable, so no step");
        Assert.AreEqual((0, 0), f.FlowDir(2, 1), "and a wall cell yields no step");
    }

    [TestMethod]
    public void RoutesAroundConcaveGeometry_NotThroughIt()
    {
        // A C-shaped pocket: the greedy "walk toward the target" approach an enemy
        // would otherwise use gets stuck on the inner face. BFS must route out and
        // around, so the path length exceeds the straight-line distance.
        var rows = new[]
        {
            ".........",
            ".#######.",
            ".#.....#.",
            ".#.###.#.",
            ".#.#S#.#.",
            ".#.#####.",
            ".#.......",
            ".########",
            ".........",
        };
        var f = new FlowField(9, 9);
        f.Rebuild(4, 4, Walkable(rows));   // source inside the innermost pocket

        // The pocket is sealed, so nothing outside it should be reachable at all.
        Assert.AreEqual(0, f.Dist(4, 4));
        Assert.AreEqual(FlowField.Unreachable, f.Dist(0, 0),
            "a sealed pocket must not leak distance to the outside");
    }

    [TestMethod]
    public void DetourAroundAWall_CostsMoreThanTheStraightLine()
    {
        // Open row 0, wall across the middle with one gap at the far end: reaching the
        // cell directly below the source must cost the detour, not 1.
        var rows = new[]
        {
            "........",
            "#######.",
            "........",
        };
        var f = new FlowField(8, 3);
        f.Rebuild(0, 0, Walkable(rows));

        Assert.AreEqual(1, f.Dist(1, 0));
        Assert.IsTrue(f.Dist(0, 2) > 8,
            $"expected a long way round through the gap, got {f.Dist(0, 2)}");
    }

    [TestMethod]
    public void MultiSource_GivesDistanceToTheNEAREST_Source()
    {
        // This is what makes co-op "chase whoever is closest" free.
        var f = new FlowField(11, 1);
        System.Span<(int, int)> sources = stackalloc (int, int)[] { (0, 0), (10, 0) };
        f.Rebuild(sources, (_, _) => true);

        Assert.AreEqual(0, f.Dist(0, 0));
        Assert.AreEqual(0, f.Dist(10, 0));
        Assert.AreEqual(1, f.Dist(9, 0), "nearest source is the right-hand one");
        Assert.AreEqual(5, f.Dist(5, 0), "the midpoint is five from either");
    }

    [TestMethod]
    public void MultiSource_FlowSplitsTowardTheNearerSource()
    {
        var f = new FlowField(11, 1);
        System.Span<(int, int)> sources = stackalloc (int, int)[] { (0, 0), (10, 0) };
        f.Rebuild(sources, (_, _) => true);

        Assert.AreEqual((-1, 0), f.FlowDir(2, 0), "left of centre heads left");
        Assert.AreEqual((1, 0), f.FlowDir(8, 0), "right of centre heads right");
    }

    [TestMethod]
    public void UnwalkableSource_IsSkipped_RatherThanSeedingTheFlood()
    {
        var rows = new[] { "#....", ".....", "....." };
        var f = new FlowField(5, 3);
        f.Rebuild(0, 0, Walkable(rows));   // (0,0) is itself a wall
        Assert.AreEqual(FlowField.Unreachable, f.Dist(0, 0));
        Assert.AreEqual(FlowField.Unreachable, f.Dist(4, 2), "no source means no flood at all");
    }

    [TestMethod]
    public void Rebuild_IsRepeatable_AndClearsThePreviousFlood()
    {
        // The field is reused every few frames, so a stale distance surviving a
        // rebuild would be a real bug (and in Eli, terrain edits force extra rebuilds).
        var f = new FlowField(5, 1);
        f.Rebuild(0, 0, (_, _) => true);
        Assert.AreEqual(4, f.Dist(4, 0));

        f.Rebuild(4, 0, (_, _) => true);
        Assert.AreEqual(0, f.Dist(4, 0), "re-flooding from a new source resets distances");
        Assert.AreEqual(4, f.Dist(0, 0));
    }

    [TestMethod]
    public void Rebuild_PicksUpTerrainThatChangedBetweenFloods()
    {
        // Eli's case: the predicate is evaluated AT FLOOD TIME, so carving new
        // tunnels between rebuilds must open new routes with no other bookkeeping.
        var rows = new[] { "..#..", "..#..", "..#.." };
        var f = new FlowField(5, 3);
        f.Rebuild(0, 0, Walkable(rows));
        Assert.AreEqual(FlowField.Unreachable, f.Dist(4, 1), "walled off to begin with");

        rows[1] = ".....";                            // carve a gap
        f.Rebuild(0, 0, Walkable(rows));
        Assert.AreNotEqual(FlowField.Unreachable, f.Dist(4, 1), "the new route is picked up");
    }

    [TestMethod]
    public void Constructor_RejectsDegenerateDimensions()
    {
        Assert.ThrowsException<System.ArgumentOutOfRangeException>(() => new FlowField(0, 5));
        Assert.ThrowsException<System.ArgumentOutOfRangeException>(() => new FlowField(5, 0));
    }
}
