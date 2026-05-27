using System;
using System.Numerics;
using SkiaSharp;

namespace KahuaNetwork.Engine;

internal sealed class DataStream
{
    public Building From { get; set; } = null!;
    public Building To { get; set; } = null!;
    public DocumentKind Kind { get; set; } = DocumentKind.RFI;
    public SKColor Color { get; set; } = Theme.Cyan;
    public float Speed { get; set; } = 0.5f;
    public float Phase { get; set; }
    public float Thickness { get; set; } = 2f;
    public float Intensity { get; set; } = 1f;

    public Vector3 SamplePath(float t)
    {
        var a = From.TopCenter + new Vector3(0, 12, 0);
        var b = To.TopCenter + new Vector3(0, 12, 0);
        var mid = (a + b) * 0.5f;
        var dist = Vector3.Distance(a, b);
        mid.Y += MathF.Min(220f, 70f + dist * 0.35f);

        // Quadratic Bezier
        float u = 1 - t;
        return u * u * a + 2 * u * t * mid + t * t * b;
    }

    public void Render(SKCanvas canvas, Camera3D camera, double timeSeconds)
    {
        const int segments = 28;
        Span<SKPoint> pts = stackalloc SKPoint[segments + 1];
        Span<float> depths = stackalloc float[segments + 1];
        Span<bool> valid = stackalloc bool[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            var w = SamplePath(t);
            valid[i] = camera.Project(w, out var s, out var d);
            pts[i] = s;
            depths[i] = d;
        }

        // Draw faint base path
        using var basePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = Color.WithAlpha((byte)(35 * Intensity)),
            StrokeWidth = Thickness,
            BlendMode = SKBlendMode.Plus,
        };
        using var path = new SKPath();
        bool started = false;
        for (int i = 0; i <= segments; i++)
        {
            if (!valid[i]) { started = false; continue; }
            if (!started) { path.MoveTo(pts[i]); started = true; }
            else path.LineTo(pts[i]);
        }
        canvas.DrawPath(path, basePaint);

        // Animate pulse(s) along the path
        double phase = (timeSeconds * Speed + Phase) % 1.0;
        int pulses = 2;
        for (int k = 0; k < pulses; k++)
        {
            double pulseT = (phase + k / (double)pulses) % 1.0;
            int idx = (int)(pulseT * segments);
            int idx2 = Math.Min(segments, idx + 1);
            if (!valid[idx] || !valid[idx2]) continue;
            float lerp = (float)(pulseT * segments - idx);
            var pos = new SKPoint(
                pts[idx].X + (pts[idx2].X - pts[idx].X) * lerp,
                pts[idx].Y + (pts[idx2].Y - pts[idx].Y) * lerp);
            float d = depths[idx];
            float size = MathF.Max(2f, (1f - d) * 14f) * Intensity;

            using var glow = new SKPaint
            {
                IsAntialias = true,
                BlendMode = SKBlendMode.Plus,
                Color = Color.WithAlpha(90),
            };
            canvas.DrawCircle(pos, size * 3f, glow);
            using var core = new SKPaint
            {
                IsAntialias = true,
                BlendMode = SKBlendMode.Plus,
                Color = Color.WithAlpha(255),
            };
            canvas.DrawCircle(pos, size, core);

            // Trailing tail
            for (int t = 1; t < 8; t++)
            {
                int tidx = idx - t;
                if (tidx < 0 || !valid[tidx]) break;
                using var tail = new SKPaint
                {
                    IsAntialias = true,
                    BlendMode = SKBlendMode.Plus,
                    Color = Color.WithAlpha((byte)(120 / (t + 1))),
                };
                canvas.DrawCircle(pts[tidx], size * (1f - t * 0.12f), tail);
            }
        }
    }
}
