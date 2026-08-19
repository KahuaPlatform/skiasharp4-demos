namespace Arcade.Tests;

/// <summary>
/// Guards the repo conventions that are invisible from a cold read of one demo and
/// which 09 – Authoring a New-Game Prompt exists to stop an agent re-deciding. These
/// are file-level assertions rather than behavioural ones, and they catch the class
/// of mistake that builds perfectly: a demo wired into Build-All but forgotten in
/// Publish-Site, a stray ProjectReference to a Common.csproj, a new game quietly
/// added to the launcher catalog.
/// </summary>
[TestClass]
public sealed class ArcadeConventionTests
{
    static string RepoRoot => _repoRoot ??= FindRepoRoot();
    static string? _repoRoot;

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(dir.FullName, "Source")) &&
                Directory.Exists(Path.Combine(dir.FullName, "Builds")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not locate the repo root from " + AppContext.BaseDirectory);
    }

    static string DemoCsproj(string name) =>
        Path.Combine(RepoRoot, "Source", name, name, name + ".csproj");

    static string ReadDemoCsproj(string name)
    {
        var path = DemoCsproj(name);
        Assert.IsTrue(File.Exists(path), $"{name}: expected a csproj at {path}");
        return File.ReadAllText(path);
    }

    // --- The pin that lets Arcade.Tests be ONE project ------------------------

    [TestMethod]
    public void SkiaSharpPinIsUniform_AcrossEveryArcadeDemo()
    {
        // Arcade.Tests compiles all twelve demos into a single assembly against one
        // SkiaSharp reference. That is only faithful while the demos agree. If this
        // fails, do NOT just bump the number here - the repo's per-demo isolation
        // means the diverging demo needs its own test project instead.
        var pins = DemoRegistry.All.ToDictionary(
            d => d.Name,
            d =>
            {
                var xml = ReadDemoCsproj(d.Name);
                var m = System.Text.RegularExpressions.Regex.Match(xml, @"<SkiaSharpVersion>([^<]+)</SkiaSharpVersion>");
                return m.Success ? m.Groups[1].Value.Trim() : "(unset)";
            });

        var distinct = pins.Values.Distinct().ToList();
        Assert.AreEqual(1, distinct.Count,
            "arcade demos no longer share one SkiaSharp version: " +
            string.Join(", ", pins.Select(kv => $"{kv.Key}={kv.Value}")));

        Assert.AreEqual("4.151.0", distinct[0],
            "the pin moved; update Arcade.Tests.csproj and Common.Tests.csproj to match");
    }

    // --- Structural conventions ----------------------------------------------

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void Demo_SourceIncludesTheChassis_AndDoesNotProjectReferenceIt(DemoEntry demo)
    {
        var xml = ReadDemoCsproj(demo.Name);
        StringAssert.Contains(xml, @"..\..\Common\**\*.cs",
            $"{demo.Name} must source-include the chassis; a Common.csproj would break per-demo pinning");
        Assert.IsFalse(xml.Contains("Common.csproj"),
            $"{demo.Name} references a Common.csproj - the chassis is source-included, never project-referenced");
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void Demo_TargetsExactlyDesktopAndWasm(DemoEntry demo)
    {
        var xml = ReadDemoCsproj(demo.Name);
        StringAssert.Contains(xml, "net10.0-browserwasm", $"{demo.Name} must target wasm");
        StringAssert.Contains(xml, "net10.0-desktop", $"{demo.Name} must target desktop");
        foreach (var mobile in new[] { "net10.0-ios", "net10.0-android", "net10.0-maccatalyst" })
            Assert.IsFalse(xml.Contains(mobile), $"{demo.Name} picked up a mobile TFM ({mobile})");
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void Demo_DrivesTheLoopFromCompositionTarget_NotADispatcherTimer(DemoEntry demo)
    {
        var page = Path.Combine(RepoRoot, "Source", demo.Name, demo.Name, "MainPage.xaml.cs");
        Assert.IsTrue(File.Exists(page), $"{demo.Name}: no MainPage.xaml.cs at {page}");
        var src = File.ReadAllText(page);

        StringAssert.Contains(src, "CompositionTarget.Rendering",
            $"{demo.Name} must drive its loop from the render tick");
        Assert.IsFalse(src.Contains("DispatcherTimer"),
            $"{demo.Name} uses a DispatcherTimer - the render tick is the standard");
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void Demo_ClampsFrameTime(DemoEntry demo)
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot, "Source", demo.Name, demo.Name, "MainPage.xaml.cs"));
        StringAssert.Contains(src, "1.0f / 30.0f",
            $"{demo.Name} must clamp dt at the slow end, or a debugger pause dislocates the physics");
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void Demo_InvalidatesEveryCanvasItDeclares(DemoEntry demo)
    {
        // The rule from 04 is "invalidate both canvases every tick" - but only the
        // two-layer demos HAVE two. Pohaku predates the chassis and hand-rolls its
        // own backdrop; Paku draws an animated plasma instead of AmbientStarBackdrop.
        // So derive the expectation from the XAML rather than hardcoding it: whatever
        // canvases a demo declares, it must invalidate.
        var dir = Path.Combine(RepoRoot, "Source", demo.Name, demo.Name);
        var xaml = File.ReadAllText(Path.Combine(dir, "MainPage.xaml"));
        var src = File.ReadAllText(Path.Combine(dir, "MainPage.xaml.cs"));

        foreach (var canvas in new[] { "GameCanvas", "BackgroundCanvas" })
        {
            if (!xaml.Contains($"x:Name=\"{canvas}\"")) continue;
            StringAssert.Contains(src, $"{canvas}.Invalidate()",
                $"{demo.Name} declares {canvas} but never invalidates it - it would render once and freeze");
        }

        StringAssert.Contains(xaml, "x:Name=\"GameCanvas\"", $"{demo.Name} must have a playfield canvas");
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void Demo_IsWiredIntoTheBuildScripts(DemoEntry demo)
    {
        // The failure this catches: a game that builds and plays perfectly but never
        // ships, because it was added to Build-All and forgotten in Publish-Site.
        foreach (var script in new[] { $"Build-{demo.Name}.ps1", $"Run-{demo.Name}.ps1" })
            Assert.IsTrue(File.Exists(Path.Combine(RepoRoot, "Builds", script)),
                $"missing Builds/{script}");

        var buildAll = File.ReadAllText(Path.Combine(RepoRoot, "Builds", "Build-All.ps1"));
        StringAssert.Contains(buildAll, $"Build-{demo.Name}.ps1", $"{demo.Name} is not in Build-All.ps1");

        var publish = File.ReadAllText(Path.Combine(RepoRoot, "Builds", "Publish-Site.ps1"));
        StringAssert.Contains(publish, $"Name = '{demo.Name}'",
            $"{demo.Name} is not in Publish-Site.ps1 - it would build but never reach the site");
    }

    // --- The mode machine -----------------------------------------------------

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void Demo_UsesTheFourStateModeMachine(DemoEntry demo)
    {
        var mode = DemoRegistry.Mode(demo, DemoRegistry.CreateWorld(demo));
        Assert.IsNotNull(mode, $"{demo.Name} exposes no Mode");
        var names = Enum.GetNames(mode!.GetType());

        // Two demos legitimately differ, and both are asserted EXACTLY rather than
        // skipped - so if either is ever modernised, this test tells you to delete
        // the exception instead of silently continuing to allow it.
        if (demo.Name == "Pohaku")
        {
            // Predates the 4-state standard entirely.
            CollectionAssert.AreEquivalent(new[] { "Demo", "Playing", "GameOver" }, names,
                "Pohaku's legacy 3-state machine changed; update this exception");
            return;
        }

        if (demo.Name == "Paku")
        {
            // By design, and documented on the enum itself: Attract IS the title
            // screen, so there is no separate Title state to reach.
            CollectionAssert.AreEquivalent(new[] { "Attract", "Playing", "GameOver" }, names,
                "Paku's 3-state machine changed; update this exception");
            return;
        }

        foreach (var required in new[] { "Title", "Playing", "GameOver", "Attract" })
            CollectionAssert.Contains(names, required,
                $"{demo.Name}.GameMode is missing {required} - the 4-state machine is the standard");
    }

    // --- The launcher reversal ------------------------------------------------

    [TestMethod]
    public void LauncherCatalog_StaysAtItsOriginalEightEntries()
    {
        // Adding cards broke the launcher's grid layout, so newer games ship
        // standalone. This is the one convention an agent reliably violates, because
        // two docs told it to for months.
        var catalog = File.ReadAllText(Path.Combine(
            RepoRoot, "Source", "Launcher", "Launcher", "Game", "GameCatalog.cs"));
        int entries = System.Text.RegularExpressions.Regex.Matches(catalog, @"\bnew\s*\(").Count;

        Assert.AreEqual(8, entries,
            $"GameCatalog has {entries} entries; it stays at eight - see 08 - Chassis Extensions");

        foreach (var standalone in new[] { "Paku", "Kiai", "Koa", "Eli" })
            Assert.IsFalse(catalog.Contains(standalone), $"{standalone} must not be in the launcher catalog");
    }
}
