namespace UnoGallery.Scene;

public static class Easing
{
    public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
    public static float InOutQuart(float t) =>
        t < 0.5f ? 8f * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 4f) * 0.5f;
}
