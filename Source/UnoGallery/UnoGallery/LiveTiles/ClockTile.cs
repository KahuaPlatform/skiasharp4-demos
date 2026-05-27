using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Real-time analog clock face. Sweep second hand uses
/// <c>DateTime.Now.Millisecond</c> so the motion is continuous rather than
/// ticky. Stays cheap: a handful of <c>DrawLine</c>/<c>DrawCircle</c> calls
/// per frame, no allocations.
/// </summary>
public sealed class ClockTile : ILiveTile
{
    public string Caption => "Clock";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(20, 24, 36),
        new SKColor(210, 220, 235),
        new SKColor(255, 220, 110),
        new SKColor(255, 95, 95));

    public void Draw(SKCanvas canvas, SKRect dest, float wallClockSeconds)
    {
        using var bgPaint = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bgPaint);

        float cx = dest.MidX;
        float cy = dest.MidY;
        float r = MathF.Min(dest.Width, dest.Height) * 0.44f;

        // Face disc
        using (var face = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy - r * 0.3f), r * 1.1f,
                new[] { new SKColor(40, 48, 70), new SKColor(20, 22, 32) },
                SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawCircle(cx, cy, r, face);
        }

        // Rim
        using (var rim = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(1f, r * 0.045f),
            Color = Palette[1].WithAlpha(180),
        })
        {
            canvas.DrawCircle(cx, cy, r, rim);
        }

        // Hour ticks (12), with longer/bolder marks at 12/3/6/9.
        using (var tick = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            Color = Palette[1],
        })
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i * (MathF.Tau / 12f) - MathF.PI / 2f;
                bool quarter = i % 3 == 0;
                tick.StrokeWidth = quarter ? r * 0.045f : r * 0.025f;
                float r1 = quarter ? r * 0.78f : r * 0.84f;
                float r2 = r * 0.94f;
                canvas.DrawLine(
                    cx + MathF.Cos(a) * r1, cy + MathF.Sin(a) * r1,
                    cx + MathF.Cos(a) * r2, cy + MathF.Sin(a) * r2,
                    tick);
            }
        }

        var now = DateTime.Now;
        float secFrac = (now.Second + now.Millisecond / 1000f) / 60f;
        float minFrac = (now.Minute + secFrac) / 60f;
        float hourFrac = ((now.Hour % 12) + minFrac) / 12f;

        DrawHand(canvas, cx, cy, hourFrac, r * 0.50f, r * 0.065f, Palette[1]);
        DrawHand(canvas, cx, cy, minFrac,  r * 0.76f, r * 0.045f, Palette[1]);
        DrawHand(canvas, cx, cy, secFrac,  r * 0.86f, r * 0.022f, Palette[3]);

        using (var hub = new SKPaint
        {
            IsAntialias = true,
            Color = Palette[2],
        })
        {
            canvas.DrawCircle(cx, cy, r * 0.05f, hub);
        }
    }

    static void DrawHand(SKCanvas canvas, float cx, float cy, float frac, float length, float width, SKColor color)
    {
        float a = frac * MathF.Tau - MathF.PI / 2f;
        using var p = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = width,
            StrokeCap = SKStrokeCap.Round,
            Color = color,
        };
        canvas.DrawLine(cx, cy, cx + MathF.Cos(a) * length, cy + MathF.Sin(a) * length, p);
    }
}
