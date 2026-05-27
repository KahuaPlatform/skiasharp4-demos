#pragma warning disable CS0618 // SKPath obsolete in v4
#pragma warning disable CA1416 // PerformanceCounter is windows-only — guarded by HAS_PERFCOUNTERS already
using SkiaSharp;

#if HAS_PERFCOUNTERS
using System.Diagnostics;
#endif

namespace UnoGallery.LiveTiles;

/// <summary>
/// Live GPU utilisation as an ECG-style scrolling trace. On Windows this
/// taps the "GPU Engine \ Utilization Percentage" performance-counter
/// category, sums the "engtype_3D" instances across the system, and clamps
/// to 100 %. On other platforms (or when the counters aren't available)
/// the trace flatlines at zero — better than crashing.
/// </summary>
public sealed class GpuMonitorTile : ILiveTile
{
    const int History = 80;
    const float SampleInterval = 0.5f;   // perf counters update at ~1 Hz; oversample for smooth scroll

    readonly float[] _samples = new float[History];
    float _lastSampleTime = -1f;
    int _writeIdx;

#if HAS_PERFCOUNTERS
    readonly List<PerformanceCounter> _counters = new();
    bool _initFailed;
#endif

    public string Caption => "GPU";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(8, 12, 24),
        new SKColor(120, 200, 255),
        new SKColor(255, 220, 90),
        new SKColor(255, 100, 100));

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        Sample(t);

        using var bg = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bg);

        DrawGrid(canvas, dest);
        DrawTrace(canvas, dest);
        DrawReadout(canvas, dest);
    }

    void Sample(float t)
    {
        if (_lastSampleTime < 0f) { _lastSampleTime = t; return; }
        if (t - _lastSampleTime < SampleInterval) return;

        float usage = ReadGpu();
        _samples[_writeIdx] = Math.Clamp(usage, 0f, 1f);
        _writeIdx = (_writeIdx + 1) % History;
        _lastSampleTime = t;
    }

    float ReadGpu()
    {
#if HAS_PERFCOUNTERS
        if (_initFailed) return 0f;

        if (_counters.Count == 0)
        {
            try
            {
                var cat = new PerformanceCounterCategory("GPU Engine");
                foreach (var inst in cat.GetInstanceNames())
                {
                    if (!inst.Contains("engtype_3D", StringComparison.Ordinal)) continue;
                    var c = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, readOnly: true);
                    _ = c.NextValue(); // prime — first read is always 0
                    _counters.Add(c);
                }
                if (_counters.Count == 0) _initFailed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GpuMonitor] init failed: {ex.Message}");
                _initFailed = true;
                return 0f;
            }
        }

        try
        {
            float total = 0f;
            foreach (var c in _counters) total += c.NextValue();
            return total / 100f;
        }
        catch
        {
            return 0f;
        }
#else
        return 0f;
#endif
    }

    void DrawGrid(SKCanvas canvas, SKRect dest)
    {
        using var line = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 18),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
        };
        for (int i = 1; i < 4; i++)
        {
            float y = dest.Top + dest.Height * i / 4f;
            canvas.DrawLine(dest.Left, y, dest.Right, y, line);
        }
    }

    void DrawTrace(SKCanvas canvas, SKRect dest)
    {
        using var path = new SKPath();
        for (int i = 0; i < History; i++)
        {
            int idx = (_writeIdx + i) % History;
            float u = i / (float)(History - 1);
            float x = dest.Left + u * dest.Width;
            float y = dest.Bottom - _samples[idx] * dest.Height * 0.95f;
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }

        using (var fillPath = new SKPath())
        {
            fillPath.AddPath(path);
            fillPath.LineTo(dest.Right, dest.Bottom);
            fillPath.LineTo(dest.Left, dest.Bottom);
            fillPath.Close();
            using var fill = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, dest.Top), new SKPoint(0, dest.Bottom),
                    new[] { Palette[1].WithAlpha(140), Palette[1].WithAlpha(20) },
                    SKShaderTileMode.Clamp),
            };
            canvas.DrawPath(fillPath, fill);
        }

        using var stroke = new SKPaint
        {
            Color = Palette[1],
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(1.4f, dest.Width / 240f),
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        };
        canvas.DrawPath(path, stroke);
    }

    void DrawReadout(SKCanvas canvas, SKRect dest)
    {
        float current = _samples[(_writeIdx - 1 + History) % History];
        var color = current > 0.75f ? Palette[3] : current > 0.4f ? Palette[2] : Palette[1];

        string label = $"GPU  {current * 100f:F0}%";
        using var font = new SKFont { Size = MathF.Max(11f, dest.Width / 22f) };
        using var fg = new SKPaint { Color = color, IsAntialias = true };
        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 180), IsAntialias = true };

        float pad = MathF.Max(6f, dest.Width * 0.04f);
        float x = dest.Left + pad;
        float y = dest.Top + pad + font.Size;
        canvas.DrawText(label, x + 1, y + 1, SKTextAlign.Left, font, shadow);
        canvas.DrawText(label, x, y, SKTextAlign.Left, font, fg);
    }
}
