namespace Arcade.Common.Tests;

// The tests 08 - Chassis Extensions records as "planned, never written": it called
// seam maths "the easiest thing to get subtly wrong", and noted that a future change
// to WrapDelta, NormalizeCenter or ForEachVisibleX has "nothing to catch it but
// playtesting". These are that net.
[TestClass]
public sealed class Camera2DTests
{
    const float Tol = 1e-3f;

    static Camera2D WrapCam(float world = 1000f, float viewW = 400f)
    {
        var c = new Camera2D();
        c.SetViewport(viewW, 300f);
        c.X = new CameraAxis { Mode = AxisMode.Wrap, WorldSize = world };
        c.Y = new CameraAxis { Mode = AxisMode.Free };
        return c;
    }

    static Camera2D ClampCam(float worldW = 1000f, float worldH = 800f, float viewW = 400f, float viewH = 300f)
    {
        var c = new Camera2D();
        c.SetViewport(viewW, viewH);
        c.X = new CameraAxis { Mode = AxisMode.Clamp, WorldSize = worldW };
        c.Y = new CameraAxis { Mode = AxisMode.Clamp, WorldSize = worldH };
        return c;
    }

    // --- Toroidal statics ----------------------------------------------------

    [TestMethod]
    public void Wrap_FoldsNegatives_UnlikeModulo()
    {
        // The whole reason Wrap exists rather than `%`: C#'s % keeps the sign.
        Assert.AreEqual(-10f % 100f, -10f, Tol, "sanity: % really does keep the sign");
        Assert.AreEqual(90f, Camera2D.Wrap(-10f, 100f), Tol);
        Assert.AreEqual(10f, Camera2D.Wrap(110f, 100f), Tol);
        Assert.AreEqual(0f, Camera2D.Wrap(100f, 100f), Tol);
        Assert.AreEqual(50f, Camera2D.Wrap(-250f, 100f), Tol, "multiple wraps below zero");
    }

    [TestMethod]
    public void Wrap_NonPositiveSize_PassesThrough()
    {
        Assert.AreEqual(42f, Camera2D.Wrap(42f, 0f), Tol);
        Assert.AreEqual(42f, Camera2D.Wrap(42f, -5f), Tol);
    }

    [TestMethod]
    public void WrapDelta_TakesTheShortWayRound()
    {
        // 10 -> 990 is 20 backwards, not 980 forwards.
        Assert.AreEqual(-20f, Camera2D.WrapDelta(10f, 990f, 1000f), Tol);
        Assert.AreEqual(20f, Camera2D.WrapDelta(990f, 10f, 1000f), Tol);
        Assert.AreEqual(100f, Camera2D.WrapDelta(0f, 100f, 1000f), Tol);
    }

    [TestMethod]
    public void WrapDelta_IsAntisymmetric_ExceptAtTheAntipode()
    {
        for (float b = 0f; b < 1000f; b += 37f)
        {
            float ab = Camera2D.WrapDelta(0f, b, 1000f);
            float ba = Camera2D.WrapDelta(b, 0f, 1000f);
            if (System.MathF.Abs(System.MathF.Abs(ab) - 500f) < Tol) continue; // exact antipode
            Assert.AreEqual(-ab, ba, Tol, $"delta 0->{b} should mirror {b}->0");
        }
    }

    [TestMethod]
    public void WrapDelta_StaysInHalfOpenHalfWorldRange()
    {
        // Documented range is (-size/2, size/2].
        for (float b = 0f; b < 1000f; b += 13f)
        {
            float d = Camera2D.WrapDelta(0f, b, 1000f);
            Assert.IsTrue(d > -500f && d <= 500f, $"delta {d} out of range for b={b}");
        }
    }

    // --- Clamp framing -------------------------------------------------------

    [TestMethod]
    public void Clamp_HoldsViewportInsideTheWorld()
    {
        var cam = ClampCam(worldW: 1000f, viewW: 400f);

        cam.Snap(0f, 0f);
        Assert.AreEqual(200f, cam.CenterX, Tol, "centre cannot go below half a viewport");
        Assert.AreEqual(0f, cam.Left, Tol, "so the left edge lands exactly on the world edge");

        cam.Snap(99999f, 0f);
        Assert.AreEqual(800f, cam.CenterX, Tol, "nor above WorldSize - halfView");
        Assert.AreEqual(1000f, cam.Left + 400f, Tol, "right edge lands on the far world edge");
    }

    [TestMethod]
    public void Clamp_WorldNarrowerThanView_CentresTheWorld()
    {
        var cam = ClampCam(worldW: 200f, viewW: 400f);
        cam.Snap(0f, 0f);
        Assert.AreEqual(100f, cam.CenterX, Tol, "a world smaller than the viewport is centred, not clamped");
        cam.Snap(9999f, 0f);
        Assert.AreEqual(100f, cam.CenterX, Tol, "and stays centred regardless of the target");
    }

    [TestMethod]
    public void Clamp_AccountsForZoom()
    {
        var cam = ClampCam(worldW: 1000f, viewW: 400f);
        cam.Zoom = 2f;                       // half as much world visible
        cam.Snap(0f, 0f);
        Assert.AreEqual(100f, cam.CenterX, Tol, "halfView in world units is ViewW/(2*Zoom)");
    }

    // --- Following -----------------------------------------------------------

    [TestMethod]
    public void Follow_WithNonPositiveRate_Snaps()
    {
        var cam = ClampCam();
        cam.X = new CameraAxis { Mode = AxisMode.Clamp, WorldSize = 1000f, FollowRate = 0f };
        cam.Snap(200f, 400f);
        cam.Follow(600f, 400f, 1f / 60f);
        Assert.AreEqual(600f, cam.CenterX, Tol, "FollowRate <= 0 means jump straight to the target");
    }

    [TestMethod]
    public void Follow_EasingIsFrameRateIndependent()
    {
        // The documented property: blend is 1 - exp(-rate*dt), so the same wall-clock
        // span converges to the same place no matter how it is subdivided.
        static float RunAt(float dt, float seconds)
        {
            var cam = new Camera2D();
            cam.SetViewport(400f, 300f);
            cam.X = new CameraAxis { Mode = AxisMode.Free, FollowRate = 3.5f };
            cam.Y = new CameraAxis { Mode = AxisMode.Free };
            cam.Snap(0f, 0f);
            for (int i = 0; i < (int)(seconds / dt); i++) cam.Follow(1000f, 0f, dt);
            return cam.CenterX;
        }

        float at60 = RunAt(1f / 60f, 1f);
        float at30 = RunAt(1f / 30f, 1f);
        float at120 = RunAt(1f / 120f, 1f);

        // In exact arithmetic these are identical: (1-t)^n == exp(-rate*dt)^n ==
        // exp(-rate*T), independent of how T was subdivided. In float32 they differ
        // by ~0.2% over a 1000-unit move, because the blend compounds through a
        // different number of rounded multiplies. 1% is loose enough for that and
        // still ~50x tighter than any frame-rate-DEPENDENT easing would manage.
        const float OnePercent = 10f;
        Assert.AreEqual(at60, at30, OnePercent, "60Hz and 30Hz must converge to the same place");
        Assert.AreEqual(at60, at120, OnePercent, "60Hz and 120Hz must converge to the same place");

        // All three must actually have converged most of the way, or the assertions
        // above would pass trivially on three values that all barely moved.
        foreach (var (hz, v) in new[] { (60, at60), (30, at30), (120, at120) })
            Assert.IsTrue(v > 900f, $"{hz}Hz only reached {v:0.0} of 1000 in one second");
    }

    [TestMethod]
    public void Follow_NaiveFixedBlend_WouldFailTheAboveTest()
    {
        // Guards the guard: shows the previous test can actually distinguish a
        // frame-rate-independent implementation from the obvious wrong one
        // (a constant per-frame blend), which differs by hundreds of units.
        static float FixedBlend(float dt, float seconds)
        {
            float center = 0f;
            for (int i = 0; i < (int)(seconds / dt); i++) center += (1000f - center) * 0.056663f;
            return center;
        }

        float naive60 = FixedBlend(1f / 60f, 1f);
        float naive30 = FixedBlend(1f / 30f, 1f);
        Assert.IsTrue(System.MathF.Abs(naive60 - naive30) > 100f,
            "a fixed per-frame blend should diverge badly across frame rates; " +
            $"got {naive60:0.0} vs {naive30:0.0}");
    }

    [TestMethod]
    public void Follow_OnWrapAxis_EasesTheShortWayAcrossTheSeam()
    {
        var cam = WrapCam(world: 1000f);
        cam.X = new CameraAxis { Mode = AxisMode.Wrap, WorldSize = 1000f, FollowRate = 5f };
        cam.Snap(10f, 0f);
        // Target at 990 is 20 units BACKWARDS across the seam; the centre must move
        // down through 0 and wrap, never climb up through 500.
        for (int i = 0; i < 10; i++)
        {
            cam.Follow(990f, 0f, 1f / 60f);
            Assert.IsFalse(cam.CenterX > 100f && cam.CenterX < 900f,
                $"camera unwound the long way round (centre {cam.CenterX})");
        }
    }

    // --- Screen mapping ------------------------------------------------------

    [TestMethod]
    public void ToScreen_ToWorld_RoundTrips_OnClampAxes()
    {
        var cam = ClampCam();
        cam.Zoom = 1.25f;
        cam.Snap(500f, 400f);
        foreach (float wx in new[] { 350f, 500f, 660f })
            Assert.AreEqual(wx, cam.ToWorldX(cam.ToScreenX(wx)), Tol);
        foreach (float wy in new[] { 300f, 400f, 520f })
            Assert.AreEqual(wy, cam.ToWorldY(cam.ToScreenY(wy)), Tol);
    }

    [TestMethod]
    public void ToScreen_OnWrapAxis_MapsAcrossTheSeamAsNearby()
    {
        var cam = WrapCam(world: 1000f, viewW: 400f);
        cam.Snap(10f, 0f);
        // An entity at 990 is 20 units to the LEFT of a camera centred at 10 - it must
        // land just off the near edge, not a world away.
        Assert.AreEqual(200f - 20f, cam.ToScreenX(990f), Tol);
    }

    [TestMethod]
    public void ForEachVisibleX_EmitsSeamReplicaSoStraddlingSpritesDrawTwice()
    {
        var cam = WrapCam(world: 1000f, viewW: 400f);
        cam.Snap(0f, 0f);                    // viewport spans world 800..1000..200
        var hits = new List<float>();
        cam.ForEachVisibleX(995f, pad: 32f, drawAtScreenX: hits.Add);
        Assert.IsTrue(hits.Count >= 1, "a sprite inside the view must be drawn at least once");
        foreach (var x in hits)
            Assert.IsTrue(x >= -32f && x <= 432f, $"replica at {x} is outside the padded viewport");
    }

    [TestMethod]
    public void ForEachVisibleX_OnClampAxis_EmitsAtMostOnce()
    {
        var cam = ClampCam();
        cam.Snap(500f, 400f);
        int calls = 0;
        cam.ForEachVisibleX(500f, 0f, _ => calls++);
        Assert.AreEqual(1, calls, "a bounded axis has no replicas");
        calls = 0;
        cam.ForEachVisibleX(-9999f, 0f, _ => calls++);
        Assert.AreEqual(0, calls, "and nothing is emitted for an off-screen point");
    }

    // --- Culling -------------------------------------------------------------

    [TestMethod]
    public void VisibleWorldRect_MatchesViewportAndGrowsByPad()
    {
        var cam = ClampCam(viewW: 400f, viewH: 300f);
        cam.Snap(500f, 400f);

        var r = cam.VisibleWorldRect();
        Assert.AreEqual(300f, r.Left, Tol);
        Assert.AreEqual(700f, r.Right, Tol);
        Assert.AreEqual(250f, r.Top, Tol);
        Assert.AreEqual(550f, r.Bottom, Tol);

        var padded = cam.VisibleWorldRect(50f);
        Assert.AreEqual(r.Left - 50f, padded.Left, Tol);
        Assert.AreEqual(r.Right + 50f, padded.Right, Tol);
    }

    [TestMethod]
    public void VisibleWorldRect_ShrinksAsZoomIncreases()
    {
        var cam = ClampCam();
        cam.Snap(500f, 400f);
        float wide = cam.VisibleWorldRect().Width;
        cam.Zoom = 2f;
        Assert.AreEqual(wide / 2f, cam.VisibleWorldRect().Width, Tol);
    }
}
