namespace Arcade.Tests;

/// <summary>
/// The cheapest broad coverage this repo can have. Every arcade demo ships an
/// attract-mode autopilot, which means every game can play itself with no input and
/// no UI — so one soak body drives all twelve, and a new demo is covered the moment
/// it is added to <see cref="DemoRegistry"/>.
///
/// These are black-box: they assert only that a game left running does not fall
/// over, go numerically insane, or leave its own state machine. That is deliberate —
/// it is what survives refactoring, and it is exactly the class of fault that a
/// green build says nothing about.
/// </summary>
[TestClass]
public sealed class AttractSoakTests
{
    const float Dt = 1f / 60f;
    const float ViewW = 1280f, ViewH = 800f;

    static object NewWorld(DemoEntry d)
    {
        var w = DemoRegistry.CreateWorld(d);
        DemoRegistry.Resize(d, w, ViewW, ViewH);
        return w;
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void IdleOnTitle_RunsForTwoMinutesWithoutThrowing(DemoEntry demo)
    {
        // Nobody has pressed anything. Every demo idles into its attract loop after a
        // timeout, so this also exercises the Title -> Attract transition and whatever
        // the demo does on its own from a cold start.
        var world = NewWorld(demo);
        for (int i = 0; i < 60 * 120; i++)
        {
            DemoRegistry.Update(demo, world, Dt);
            if (i % 600 == 0 && DemoRegistry.FindNonFiniteState(demo, world) is { } bad)
                Assert.Fail($"{bad} after {i / 60f:0.0}s idling on the title screen");
        }
        Assert.IsNull(DemoRegistry.FindNonFiniteState(demo, world));
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void AttractAutopilot_SurvivesFiveSimulatedMinutes(DemoEntry demo)
    {
        // Five minutes at 60Hz is 18,000 ticks of a bot playing itself: level rollovers,
        // deaths, respawns, wave escalation, entity churn. If a demo is going to divide
        // by zero or spawn its way into an unbounded list, this is where it shows.
        var world = NewWorld(demo);
        DemoRegistry.StartAttract(demo, world);

        for (int i = 0; i < 60 * 300; i++)
        {
            DemoRegistry.Update(demo, world, Dt);
            if (i % 1800 == 0 && DemoRegistry.FindNonFiniteState(demo, world) is { } bad)
                Assert.Fail($"{bad} after {i / 60f:0.0}s of attract mode");
        }

        Assert.IsNull(DemoRegistry.FindNonFiniteState(demo, world),
            $"{demo.Name} ended the soak with non-finite state");
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void Mode_NeverLeavesItsOwnEnum(DemoEntry demo)
    {
        // A mode holding a value outside its enum means something assigned a cast int,
        // and the state machine's switch will silently fall through from then on.
        var world = NewWorld(demo);
        DemoRegistry.StartAttract(demo, world);

        var first = DemoRegistry.Mode(demo, world);
        if (first is null) Assert.Inconclusive($"{demo.Name} exposes no Mode to check");
        var modeType = first.GetType();

        for (int i = 0; i < 60 * 120; i++)
        {
            DemoRegistry.Update(demo, world, Dt);
            var m = DemoRegistry.Mode(demo, world)!;
            Assert.IsTrue(Enum.IsDefined(modeType, m),
                $"{demo.Name}.Mode became {m} (not a defined {modeType.Name}) at {i / 60f:0.0}s");
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void ClampedFrameTimes_AreAllTolerated(DemoEntry demo)
    {
        // MainPage clamps dt to [1/60, 1/30] before handing it over, so those are the
        // only two extremes a demo ever sees in practice. Both must be safe: a demo
        // that only holds together at exactly 60Hz breaks on any loaded machine.
        foreach (float dt in new[] { 1f / 60f, 1f / 30f })
        {
            var world = NewWorld(demo);
            DemoRegistry.StartAttract(demo, world);
            for (int i = 0; i < 60 * 60; i++) DemoRegistry.Update(demo, world, dt);

            Assert.IsNull(DemoRegistry.FindNonFiniteState(demo, world),
                $"{demo.Name} went non-finite at dt={dt:0.0000}");
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void Resize_ToOddViewports_IsHandled(DemoEntry demo)
    {
        // Resize is driven straight from the canvas draw area, so a demo will genuinely
        // see a zero or 1px viewport mid-layout on startup and while a window is being
        // dragged. Dividing by a zero viewport is an easy way to poison the camera.
        var world = DemoRegistry.CreateWorld(demo);
        foreach (var (w, h) in new[] { (0f, 0f), (1f, 1f), (1920f, 1f), (320f, 240f), (3840f, 2160f) })
        {
            DemoRegistry.Resize(demo, world, w, h);
            for (int i = 0; i < 10; i++) DemoRegistry.Update(demo, world, Dt);
            Assert.IsNull(DemoRegistry.FindNonFiniteState(demo, world),
                $"{demo.Name} went non-finite at viewport {w}x{h}");
        }
    }
}
