namespace UnoGallery.Scene;

/// <summary>Easing curves for layout morphs and tile animations; input/output in [0,1].</summary>
public static class Easing
{
    /// <summary>Cubic ease-out: fast start, gentle settle.</summary>
    public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
    /// <summary>Quartic ease-in-out: slow ends, fast middle (used for layout transitions).</summary>
    public static float InOutQuart(float t) =>
        t < 0.5f ? 8f * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 4f) * 0.5f;
}
