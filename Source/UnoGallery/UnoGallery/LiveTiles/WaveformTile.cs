#pragma warning disable CS0618 // SKPath mutable API obsolete in v4
using SkiaSharp;
using UnoGallery.Audio;

namespace UnoGallery.LiveTiles;

/// <summary>
/// Audio-reactive tile. Reads from <see cref="AudioSourceManager"/>:
///
///   - <b>Spectrum bars</b> across the full tile width, log-spaced bins
///     of <see cref="AudioAnalyzer.Magnitudes"/>, normalised against the
///     running peak so the visualisation stays useful at any input level.
///   - <b>Waveform trace</b> drawn on top through the middle, sampled
///     directly from the current source so quiet moments still show a
///     thin line rather than empty bars.
///
/// Picks colour from the tile palette by bin position so the spectrum
/// gradients warm→cool from low → high frequencies.
/// </summary>
public sealed class WaveformTile : ILiveTile
{
    const int DisplaySamples = 192;
    const int BarCount = 40;

    readonly float[] _samples = new float[DisplaySamples];
    float _peakSmoothed = 0.01f;

    public string Caption => "Audio";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(5, 14, 22),
        new SKColor(80, 255, 200),
        new SKColor(140, 210, 255),
        new SKColor(255, 150, 220));

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        var mgr = AudioSourceManager.Instance;
        var analyzer = mgr.Analyzer;
        var mags = analyzer.Magnitudes;
        mgr.Current.CopyLatest(_samples);

        using var bgPaint = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bgPaint);

        DrawSpectrum(canvas, dest, mags);
        DrawWaveform(canvas, dest);
        DrawBeatRing(canvas, dest, analyzer.Pulse);
    }

    void DrawSpectrum(SKCanvas canvas, SKRect dest, ReadOnlySpan<float> mags)
    {
        // Log-spaced bin sampling so low freqs (bass / vocals) get more bars
        // than the upper-end harmonics we mostly don't care about.
        int maxBin = Math.Min(mags.Length, 256);
        var heights = new float[BarCount];
        float instantPeak = 0.0001f;
        for (int b = 0; b < BarCount; b++)
        {
            float u = (b + 1) / (float)BarCount;
            int bin = 1 + (int)(Math.Pow(u, 2.2) * (maxBin - 2));
            float m = mags[bin];
            heights[b] = m;
            if (m > instantPeak) instantPeak = m;
        }

        // EMA-track the running peak so bars stay roughly full-height regardless of input level.
        _peakSmoothed = Math.Max(_peakSmoothed * 0.96f, instantPeak);

        float barGap = MathF.Max(1f, dest.Width / 280f);
        float barW = dest.Width / BarCount;
        for (int b = 0; b < BarCount; b++)
        {
            float h = Math.Min(1f, heights[b] / _peakSmoothed) * dest.Height * 0.78f;
            float left = dest.Left + b * barW + barGap;
            float right = dest.Left + (b + 1) * barW - barGap;
            if (right <= left) right = left + 0.5f;

            float top = dest.Bottom - h;
            // Bar colour shifts low→mid→high through the palette.
            float gradT = b / (float)(BarCount - 1);
            var col = LerpPalette(gradT);
            using var paint = new SKPaint
            {
                IsAntialias = false,
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, dest.Bottom),
                    new SKPoint(0, top),
                    new[] { col.WithAlpha(60), col.WithAlpha(220) },
                    SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(left, top, right - left, h, paint);
        }
    }

    void DrawWaveform(SKCanvas canvas, SKRect dest)
    {
        using var path = new SKPath();
        float amp = dest.Height * 0.30f;
        for (int i = 0; i < DisplaySamples; i++)
        {
            float px = dest.Left + (i / (float)(DisplaySamples - 1)) * dest.Width;
            float py = dest.MidY - _samples[i] * amp;
            if (i == 0) path.MoveTo(px, py);
            else path.LineTo(px, py);
        }

        float baseStroke = MathF.Max(1.4f, dest.Width / 220f);
        using (var glow = new SKPaint
        {
            Color = Palette[2].WithAlpha(140),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = baseStroke * 4.0f,
            IsAntialias = true,
            ImageFilter = SKImageFilter.CreateBlur(3.5f, 3.5f),
        })
        {
            canvas.DrawPath(path, glow);
        }
        using (var line = new SKPaint
        {
            Color = Palette[2],
            Style = SKPaintStyle.Stroke,
            StrokeWidth = baseStroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        })
        {
            canvas.DrawPath(path, line);
        }
    }

    static void DrawBeatRing(SKCanvas canvas, SKRect dest, float pulse)
    {
        if (pulse <= 0.01f) return;
        float r = MathF.Min(dest.Width, dest.Height) * 0.10f * (1f + pulse * 0.6f);
        var center = new SKPoint(dest.Right - r * 1.4f, dest.Top + r * 1.4f);
        using var paint = new SKPaint
        {
            Color = new SKColor(255, 90, 160).WithAlpha((byte)(pulse * 220)),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(1.5f, r * 0.18f),
            IsAntialias = true,
            ImageFilter = SKImageFilter.CreateBlur(2f, 2f),
        };
        canvas.DrawCircle(center, r, paint);
    }

    SKColor LerpPalette(float t)
    {
        t = Math.Clamp(t, 0f, 0.999f);
        // Use palette[1..] so the dark background colour doesn't leak into bars.
        int span = Palette.Length - 1;
        float scaled = t * (span - 1);
        int idx = 1 + (int)scaled;
        float f = scaled - (int)scaled;
        var a = Palette[idx];
        var b = Palette[Math.Min(idx + 1, Palette.Length - 1)];
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * f),
            (byte)(a.Green + (b.Green - a.Green) * f),
            (byte)(a.Blue + (b.Blue - a.Blue) * f));
    }
}
