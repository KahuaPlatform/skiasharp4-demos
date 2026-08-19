namespace Arcade.Common.Tests;

[TestClass]
public sealed class AsciiMapTests
{
    [TestMethod]
    public void Parse_ReturnsDimensions_AndVisitsEveryGlyphLeftToRightTopToBottom()
    {
        var rows = new[] { "ab", "cd", "ef" };
        var order = new List<string>();
        var (cols, rowCount) = AsciiMap.Parse(rows, (c, r, ch) => order.Add($"{c},{r},{ch}"));

        Assert.AreEqual(2, cols);
        Assert.AreEqual(3, rowCount);
        CollectionAssert.AreEqual(
            new[] { "0,0,a", "1,0,b", "0,1,c", "1,1,d", "0,2,e", "1,2,f" },
            order);
    }

    [TestMethod]
    public void Parse_RaggedMap_ThrowsBeforeInvokingTheCallback()
    {
        // The point of the up-front check: a ragged row is a content bug, and far
        // cheaper to catch at load than to chase as a mis-rendered tile. Crucially
        // onCell must not have fired for the valid rows first.
        var rows = new[] { "####", "##", "####" };
        int calls = 0;
        var ex = Assert.ThrowsException<System.ArgumentException>(
            () => AsciiMap.Parse(rows, (_, _, _) => calls++));

        Assert.AreEqual(0, calls, "validation must run before any cell is emitted");
        StringAssert.Contains(ex.Message, "ragged");
        StringAssert.Contains(ex.Message, "row 1", System.StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Parse_EmptyInput_Throws()
    {
        Assert.ThrowsException<System.ArgumentException>(
            () => AsciiMap.Parse(System.Array.Empty<string>(), (_, _, _) => { }));
        Assert.ThrowsException<System.ArgumentException>(
            () => AsciiMap.Parse(new[] { "" }, (_, _, _) => { }));
    }

    [TestMethod]
    public void Parse_NullInput_Throws()
    {
        Assert.ThrowsException<System.ArgumentNullException>(
            () => AsciiMap.Parse((string[])null!, (_, _, _) => { }));
    }

    [TestMethod]
    public void Parse_SingleCell_IsValid()
    {
        var (cols, rows) = AsciiMap.Parse(new[] { "#" }, (_, _, _) => { });
        Assert.AreEqual(1, cols);
        Assert.AreEqual(1, rows);
    }
}

// The point of this piece is a terrain whose seam is MATHEMATICALLY invisible:
// every component sinusoid completes a whole number of cycles across the world, so
// height and slope match exactly at the wrap. There is no special-casing to lean
// on, which makes it precisely the sort of thing a later "harmless" edit breaks.
[TestClass]
public sealed class SeamlessTerrainTests
{
    static SeamlessTerrain Make(int seed = 1234, float width = 4000f) =>
        new(width, baselineY: 500f, amplitude: 120f, rng: new System.Random(seed));

    [TestMethod]
    public void HeightAtTheSeam_MatchesOnBothSides()
    {
        var t = Make();
        Assert.AreEqual(t.HeightAt(0f), t.HeightAt(t.WorldWidth), 0.01f,
            "the seam must be invisible in height");
    }

    [TestMethod]
    public void SlopeAtTheSeam_MatchesOnBothSides()
    {
        var t = Make();
        Assert.AreEqual(t.SlopeAt(0f), t.SlopeAt(t.WorldWidth), 0.001f,
            "a matching height with a mismatched slope still shows as a kink");
    }

    [TestMethod]
    public void HeightAt_WrapsArbitraryInput()
    {
        var t = Make();
        foreach (float x in new[] { 137f, 999f, 2500f })
        {
            Assert.AreEqual(t.HeightAt(x), t.HeightAt(x + t.WorldWidth), 0.01f, $"x={x} +1 lap");
            Assert.AreEqual(t.HeightAt(x), t.HeightAt(x - t.WorldWidth), 0.01f, $"x={x} -1 lap");
            Assert.AreEqual(t.HeightAt(x), t.HeightAt(x + 3f * t.WorldWidth), 0.05f, $"x={x} +3 laps");
        }
    }

    [TestMethod]
    public void HeightStaysWithinAmplitudeOfTheBaseline()
    {
        // The constructor normalises the harmonic sum so the worst case lands exactly
        // on Amplitude; drifting outside it would push terrain off the playfield.
        var t = Make();
        for (float x = 0f; x < t.WorldWidth; x += 7f)
        {
            float d = System.MathF.Abs(t.HeightAt(x) - t.BaselineY);
            Assert.IsTrue(d <= t.Amplitude + 0.5f,
                $"height at x={x} deviates {d:0.00} from baseline, past amplitude {t.Amplitude}");
        }
    }

    [TestMethod]
    public void SameSeed_ReproducesTheSamePlanet()
    {
        var a = Make(seed: 99);
        var b = Make(seed: 99);
        for (float x = 0f; x < 4000f; x += 211f)
            Assert.AreEqual(a.HeightAt(x), b.HeightAt(x), 0.001f, $"seeded terrain diverged at x={x}");
    }

    [TestMethod]
    public void DifferentSeed_GivesADifferentPlanet()
    {
        var a = Make(seed: 1);
        var b = Make(seed: 2);
        bool anyDifference = false;
        for (float x = 0f; x < 4000f; x += 211f)
            if (System.MathF.Abs(a.HeightAt(x) - b.HeightAt(x)) > 1f) { anyDifference = true; break; }
        Assert.IsTrue(anyDifference, "different seeds should not produce identical terrain");
    }

    [TestMethod]
    public void SlopeAt_TracksTheNumericalDerivative()
    {
        var t = Make();
        const float h = 0.05f;
        for (float x = 100f; x < 3000f; x += 373f)
        {
            float numeric = (t.HeightAt(x + h) - t.HeightAt(x - h)) / (2f * h);
            Assert.AreEqual(numeric, t.SlopeAt(x), 0.02f, $"analytic slope disagrees at x={x}");
        }
    }

    [TestMethod]
    public void IsFlat_AgreesWithTheHeightSpreadOverTheSpan()
    {
        var t = Make();
        int flat = 0, bumpy = 0;
        for (float x = 0f; x < t.WorldWidth; x += 53f)
        {
            const float span = 40f, maxRise = 6f;
            float lo = float.MaxValue, hi = float.MinValue;
            for (float s = -span; s <= span; s += 4f)
            {
                float y = t.HeightAt(x + s);
                lo = System.MathF.Min(lo, y);
                hi = System.MathF.Max(hi, y);
            }
            bool reallyFlat = (hi - lo) <= maxRise;
            if (t.IsFlat(x, span, maxRise)) { flat++; Assert.IsTrue(reallyFlat, $"IsFlat true at x={x} but spread is {hi - lo:0.00}"); }
            else bumpy++;
        }
        Assert.IsTrue(flat > 0, "some spots should qualify as landable");
        Assert.IsTrue(bumpy > 0, "and some should not, or the test proves nothing");
    }
}
