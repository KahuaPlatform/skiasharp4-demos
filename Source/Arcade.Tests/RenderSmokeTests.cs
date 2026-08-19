using SkiaSharp;

namespace Arcade.Tests;

/// <summary>
/// Renders every demo to an offscreen surface. No window, no Uno, no GPU — just the
/// static <c>Renderer.Render(canvas, world, w, h)</c> entry point every demo exposes.
///
/// This covers the layer the Eli verification work explicitly could not reach: a
/// headless sim harness never touches the renderer, and a screenshot only proves one
/// demo drew one frame at one size. A renderer that throws on an empty entity list,
/// on a zero viewport, or on the game-over overlay is invisible until someone plays
/// that exact state.
/// </summary>
[TestClass]
public sealed class RenderSmokeTests
{
    const float Dt = 1f / 60f;

    static (SKSurface surface, SKCanvas canvas) NewSurface(int w, int h)
    {
        var s = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException($"could not create a {w}x{h} offscreen surface");
        return (s, s.Canvas);
    }

    static int DistinctColours(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(image);
        var seen = new HashSet<uint>();
        for (int y = 0; y < bmp.Height; y += 8)
            for (int x = 0; x < bmp.Width; x += 8)
                seen.Add((uint)bmp.GetPixel(x, y));
        return seen.Count;
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void TitleScreen_RendersSomething(DemoEntry demo)
    {
        var (surface, canvas) = NewSurface(960, 720);
        using (surface)
        {
            var world = DemoRegistry.CreateWorld(demo);
            DemoRegistry.Resize(demo, world, 960f, 720f);
            DemoRegistry.Update(demo, world, Dt);

            DemoRegistry.Render(demo, world, canvas, 960f, 720f);
            canvas.Flush();

            Assert.IsTrue(DistinctColours(surface) > 1,
                $"{demo.Name} rendered a uniformly blank title screen");
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void AttractMode_RendersEveryFrameOfTwentySeconds(DemoEntry demo)
    {
        // Drives the sim and draws it, so the renderer sees the same churn the sim
        // does: entities spawning and dying, level rollovers, mode transitions.
        // Twenty seconds x 12 demos keeps the whole suite around a minute; the long
        // haul is covered by the sim-only soak, which runs five minutes per demo for
        // a fraction of the cost because it never rasterises.
        var (surface, canvas) = NewSurface(800, 600);
        using (surface)
        {
            var world = DemoRegistry.CreateWorld(demo);
            DemoRegistry.Resize(demo, world, 800f, 600f);
            DemoRegistry.StartAttract(demo, world);

            for (int i = 0; i < 60 * 20; i++)
            {
                DemoRegistry.Update(demo, world, Dt);
                DemoRegistry.Render(demo, world, canvas, 800f, 600f);   // throwing here fails the test
            }
            canvas.Flush();

            Assert.IsTrue(DistinctColours(surface) > 1,
                $"{demo.Name} rendered a uniformly blank frame after twenty seconds of attract mode");
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void RendersAtAwkwardCanvasSizes(DemoEntry demo)
    {
        // The canvas size comes straight from the layout, so these all really happen:
        // 1px slivers mid-resize, extreme aspect ratios on a dragged window, and the
        // letterbox maths dividing by a small dimension.
        foreach (var (w, h) in new[] { (1, 1), (1, 600), (600, 1), (320, 240), (2560, 1440) })
        {
            var (surface, canvas) = NewSurface(w, h);
            using (surface)
            {
                var world = DemoRegistry.CreateWorld(demo);
                DemoRegistry.Resize(demo, world, w, h);
                DemoRegistry.Update(demo, world, Dt);
                DemoRegistry.Render(demo, world, canvas, w, h);   // must not throw
                canvas.Flush();
            }
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(DemoRegistry.AllRows), typeof(DemoRegistry), DynamicDataSourceType.Method)]
    public void RenderLeavesTheCanvasTransformBalanced(DemoEntry demo)
    {
        // Camera2D.Apply pushes a Save and documents that THE CALLER restores. A
        // renderer that leaks a Save would slowly nest the transform every frame -
        // which looks fine for a few seconds and then drifts the whole world.
        var (surface, canvas) = NewSurface(800, 600);
        using (surface)
        {
            var world = DemoRegistry.CreateWorld(demo);
            DemoRegistry.Resize(demo, world, 800f, 600f);
            DemoRegistry.StartAttract(demo, world);

            int depthBefore = canvas.SaveCount;
            for (int i = 0; i < 120; i++)
            {
                DemoRegistry.Update(demo, world, Dt);
                DemoRegistry.Render(demo, world, canvas, 800f, 600f);
            }

            Assert.AreEqual(depthBefore, canvas.SaveCount,
                $"{demo.Name} leaked {canvas.SaveCount - depthBefore} canvas Save levels over 120 frames");
        }
    }
}
