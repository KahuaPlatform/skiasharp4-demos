using SkiaSharp;

namespace UnoGallery.Effects;

/// <summary>
/// Cinematic film grain via a high-frequency Perlin turbulence overlay,
/// SoftLight-blended at low opacity. Seed-rotates with time so the grain
/// shifts each frame.
/// </summary>
public sealed class FilmGrainPass
{
    const float Amount = 0.45f;   // 0..1

    public void Draw(SKCanvas canvas, SKSize size, float time)
    {
        // High-frequency turbulence: small base frequency gives fine grain.
        // The seed is the integer part of time*60 — gives ~60 distinct noise
        // patterns per second, which reads as "movie grain".
        int seed = (int)(time * 60f) & 0x3FF;
        using var noise = new SKPaint
        {
            Shader = SKShader.CreatePerlinNoiseTurbulence(0.85f, 0.85f, 1, seed),
            ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
            {
                // Compress noise into grey so we don't introduce hue shifts.
                0.33f, 0.33f, 0.33f, 0, 0,
                0.33f, 0.33f, 0.33f, 0, 0,
                0.33f, 0.33f, 0.33f, 0, 0,
                0,     0,     0,     1, 0,
            }),
            BlendMode = SKBlendMode.SoftLight,
            Color = SKColors.White.WithAlpha((byte)(Amount * 110f)),
        };
        canvas.DrawRect(0, 0, size.Width, size.Height, noise);
    }
}
