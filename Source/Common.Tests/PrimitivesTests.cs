namespace Arcade.Common.Tests;

[TestClass]
public sealed class Vec2Tests
{
    const float Tol = 1e-4f;

    [TestMethod]
    public void Normalized_ZeroVector_ReturnsZero_RatherThanNaN()
    {
        // Guards the divide-by-zero the docs promise is handled. Sim code normalises
        // direction vectors that are legitimately zero on any frame the player is
        // standing still, so a NaN here would poison a position permanently.
        var n = Vec2.Zero.Normalized();
        Assert.AreEqual(0f, n.X, Tol);
        Assert.AreEqual(0f, n.Y, Tol);
        Assert.IsFalse(float.IsNaN(n.X) || float.IsNaN(n.Y));
    }

    [TestMethod]
    public void Normalized_GivesUnitLength_AndKeepsDirection()
    {
        var v = new Vec2(3f, 4f);
        Assert.AreEqual(5f, v.Length, Tol);

        var n = v.Normalized();
        Assert.AreEqual(1f, n.Length, Tol);
        Assert.AreEqual(0.6f, n.X, Tol);
        Assert.AreEqual(0.8f, n.Y, Tol);
    }

    [TestMethod]
    public void FromAngle_MatchesCosSin_AndScalesByMagnitude()
    {
        var v = Vec2.FromAngle(0f);
        Assert.AreEqual(1f, v.X, Tol);
        Assert.AreEqual(0f, v.Y, Tol);

        var up = Vec2.FromAngle(System.MathF.PI / 2f, 10f);
        Assert.AreEqual(0f, up.X, 1e-3f);
        Assert.AreEqual(10f, up.Y, Tol);
    }

    [TestMethod]
    public void Operators_BehaveArithmetically()
    {
        var a = new Vec2(1f, 2f);
        var b = new Vec2(10f, 20f);

        Assert.AreEqual(11f, (a + b).X, Tol);
        Assert.AreEqual(-9f, (a - b).X, Tol);
        Assert.AreEqual(-1f, (-a).X, Tol);
        Assert.AreEqual(3f, (a * 3f).X, Tol);
        Assert.AreEqual(3f, (3f * a).X, Tol);
    }

    [TestMethod]
    public void IsAValueType_SoAssignmentCopies()
    {
        // Entities hold Vec2 as a field and code does `var p0 = e.Pos;` expecting a
        // snapshot. If this ever became a class, that idiom would silently alias.
        Assert.IsTrue(typeof(Vec2).IsValueType);

        var a = new Vec2(1f, 1f);
        var copy = a;
        a.X = 99f;
        Assert.AreEqual(1f, copy.X, Tol, "assignment must copy, not alias");
    }
}

[TestClass]
public sealed class HsvColorTests
{
    [TestMethod]
    public void PrimaryHues_MapToTheExpectedCorners()
    {
        Assert.AreEqual(new SkiaSharp.SKColor(255, 0, 0), HsvColor.HsvToRgb(0f, 1f, 1f));
        Assert.AreEqual(new SkiaSharp.SKColor(0, 255, 0), HsvColor.HsvToRgb(120f, 1f, 1f));
        Assert.AreEqual(new SkiaSharp.SKColor(0, 0, 255), HsvColor.HsvToRgb(240f, 1f, 1f));
    }

    [TestMethod]
    public void HueWrapsAtThreeSixty()
    {
        Assert.AreEqual(HsvColor.HsvToRgb(0f, 1f, 1f), HsvColor.HsvToRgb(360f, 1f, 1f));
    }

    [TestMethod]
    public void ZeroSaturation_IsGrey_AndZeroValueIsBlack()
    {
        var grey = HsvColor.HsvToRgb(200f, 0f, 0.5f);
        Assert.AreEqual(grey.Red, grey.Green);
        Assert.AreEqual(grey.Green, grey.Blue);

        var black = HsvColor.HsvToRgb(200f, 1f, 0f);
        Assert.AreEqual(0, black.Red);
        Assert.AreEqual(0, black.Green);
        Assert.AreEqual(0, black.Blue);
    }

    [TestMethod]
    public void EveryHue_ProducesAValidOpaqueColour()
    {
        // Marquee cycles hue continuously; a gap or a transparent result would show.
        for (float h = 0f; h <= 360f; h += 3f)
        {
            var c = HsvColor.HsvToRgb(h, 1f, 1f);
            Assert.AreEqual(255, c.Alpha, $"hue {h} produced a non-opaque colour");
            Assert.IsTrue(c.Red > 0 || c.Green > 0 || c.Blue > 0, $"hue {h} produced black at full value");
        }
    }
}

[TestClass]
public sealed class HighScoreStoreTests
{
    // Never touch a real demo's store: use a unique name per run and clean up.
    static string TempName() => "ArcadeTests_" + System.Guid.NewGuid().ToString("N");

    static void Cleanup(string name)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), name);
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, recursive: true);
        }
        catch { /* best effort - the store itself is fail-silent by design */ }
    }

    [TestMethod]
    public void Load_BeforeAnythingIsSaved_ReturnsZero()
    {
        var name = TempName();
        try { Assert.AreEqual(0, new HighScoreStore(name).Load()); }
        finally { Cleanup(name); }
    }

    [TestMethod]
    public void Save_ThenLoad_RoundTrips_AcrossInstances()
    {
        // "Across instances" is the part that matters: the game reads the score at
        // construction and writes it on game over, in different process lifetimes.
        var name = TempName();
        try
        {
            new HighScoreStore(name).Save(12345);
            Assert.AreEqual(12345, new HighScoreStore(name).Load());
        }
        finally { Cleanup(name); }
    }

    [TestMethod]
    public void Save_Overwrites_RatherThanAppending()
    {
        var name = TempName();
        try
        {
            var store = new HighScoreStore(name);
            store.Save(100);
            store.Save(250);
            Assert.AreEqual(250, new HighScoreStore(name).Load());
        }
        finally { Cleanup(name); }
    }

    [TestMethod]
    public void Load_OnCorruptContent_FailsSilentToZero()
    {
        // Fail-silent is the documented contract - a mangled file must never take
        // the game down on startup.
        var name = TempName();
        try
        {
            new HighScoreStore(name).Save(42);
            var path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                name, "highscore.txt");
            System.IO.File.WriteAllText(path, "not a number");

            Assert.AreEqual(0, new HighScoreStore(name).Load());
        }
        finally { Cleanup(name); }
    }
}
