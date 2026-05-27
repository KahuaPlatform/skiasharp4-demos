#pragma warning disable CS0618 // SKPath obsolete in v4
using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Frame-time tile: records the wall-clock interval between consecutive
/// <see cref="Draw"/> calls and plots the last 80 samples as a bar chart.
/// Reads as a "GPU load" proxy — slower frames push the bars higher.
/// A reference line marks 16.6 ms (60 fps); bars colour from green (fast)
/// through amber (60 fps) to red (slower than 30 fps).
/// </summary>
public sealed class FrameTimeTile : ILiveTile
{
    const int History = 80;
    const float MaxMs = 33.3f; // bars saturate at 30 fps

    readonly float[] _ms = new float[History];
    float _lastTime = -1f;
    int _writeIdx;

    public string Caption => "Frame";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(8, 16, 20),
        new SKColor(80, 220, 160),
        new SKColor(255, 220, 100),
        new SKColor(255, 100, 100));

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        if (_lastTime >= 0f)
        {
            float dt = t - _lastTime;
            _ms[_writeIdx] = MathF.Min(dt * 1000f, MaxMs * 1.4f);
            _writeIdx = (_writeIdx + 1) % History;
        }
        _lastTime = t;

        using var bg = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bg);

        DrawBars(canvas, dest);
        Draw60FpsLine(canvas, dest);
        DrawReadout(canvas, dest);
    }

    void DrawBars(SKCanvas canvas, SKRect dest)
    {
        float barW = dest.Width / History;
        for (int i = 0; i < History; i++)
        {
            int idx = (_writeIdx + i) % History;
            float ms = _ms[idx];
            float norm = MathF.Min(ms / MaxMs, 1f);
            float h = norm * dest.Height * 0.92f;

            var col = ColorForMs(ms);
            using var paint = new SKPaint
            {
                Color = col.WithAlpha(220),
                IsAntialias = false,
            };
            float left = dest.Left + i * barW;
            float top = dest.Bottom - h;
            canvas.DrawRect(left + 0.5f, top, barW - 1f, h, paint);
        }
    }

    void Draw60FpsLine(SKCanvas canvas, SKRect dest)
    {
        float y = dest.Bottom - (16.6f / MaxMs) * dest.Height * 0.92f;
        using var p = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 90),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0f),
        };
        canvas.DrawLine(dest.Left, y, dest.Right, y, p);
    }

    SKColor ColorForMs(float ms)
    {
        if (ms <= 12f) return Palette[1];      // <12ms: cushy
        if (ms <= 17f) return Lerp(Palette[1], Palette[2], (ms - 12f) / 5f);
        if (ms <= 33f) return Lerp(Palette[2], Palette[3], (ms - 17f) / 16f);
        return Palette[3];                      // >33ms: capped red
    }

    static SKColor Lerp(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }

    void DrawReadout(SKCanvas canvas, SKRect dest)
    {
        // Average of the most recent 10 samples for a steadier readout.
        float sum = 0f;
        int count = Math.Min(10, History);
        for (int i = 0; i < count; i++)
            sum += _ms[(_writeIdx - 1 - i + History) % History];
        float avg = sum / count;
        float fps = avg > 0.001f ? 1000f / avg : 0f;
        string label = $"{avg:F1} ms · {fps:F0} fps";

        using var font = new SKFont { Size = MathF.Max(11f, dest.Width / 22f) };
        using var fg = new SKPaint { Color = ColorForMs(avg), IsAntialias = true };
        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 180), IsAntialias = true };

        float pad = MathF.Max(6f, dest.Width * 0.04f);
        float x = dest.Left + pad;
        float y = dest.Top + pad + font.Size;
        canvas.DrawText(label, x + 1, y + 1, SKTextAlign.Left, font, shadow);
        canvas.DrawText(label, x, y, SKTextAlign.Left, font, fg);
    }
}
