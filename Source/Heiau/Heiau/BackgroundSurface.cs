using System;
using System.Diagnostics;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Heiau;

// Ambient deep-space backdrop that fills the entire window behind the playfield
// Viewbox. Gives the side/top bars something to look at — a slow drifting,
// twinkling starfield over the same purple gradient the playfield uses — without
// competing with the gameplay's radial / warp starfield inside the rim.
//
// Animation is driven by the MainPage render loop calling Invalidate() each
// frame; the surface tracks its own dt via Stopwatch.
public sealed class BackgroundSurface : SKCanvasElement
{
    static readonly SKColor BgTop    = new(0x05, 0x00, 0x14);
    static readonly SKColor BgBottom = new(0x18, 0x02, 0x36);

    struct Star { public float X, Y, Speed, Brightness; }
    const int StarCount = 110;
    Star[]? _stars;
    float _starsW, _starsH;
    readonly Random _rng = new(31);
    readonly Stopwatch _clock = Stopwatch.StartNew();
    double _lastT;
    readonly SKPaint _starPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        float cw = (float)area.Width;
        float ch = (float)area.Height;

        using (var bg = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0), new SKPoint(0, ch),
                new[] { BgTop, BgBottom }, SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawRect(0, 0, cw, ch, bg);
        }

        EnsureStars(cw, ch);
        double now = _clock.Elapsed.TotalSeconds;
        float dt = MathF.Min(0.1f, (float)(now - _lastT));
        _lastT = now;
        float twinkle = (float)now;
        for (int i = 0; i < _stars!.Length; i++)
        {
            // Gentle downward drift — slower than the in-playfield starfield so it
            // reads as deeper, more distant space.
            _stars[i].Y += _stars[i].Speed * dt;
            if (_stars[i].Y > ch + 5f)
            {
                _stars[i].X = (float)_rng.NextDouble() * cw;
                _stars[i].Y = -5f;
            }
            float flick = 0.85f + 0.15f * MathF.Sin(twinkle * 1.7f + i * 0.31f);
            byte a = (byte)(255 * _stars[i].Brightness * flick);
            _starPaint.Color = new SKColor(255, 255, 255, a);
            float r = _stars[i].Brightness > 0.75f ? 1.5f : _stars[i].Brightness > 0.55f ? 1.0f : 0.7f;
            canvas.DrawCircle(_stars[i].X, _stars[i].Y, r, _starPaint);
        }
    }

    void EnsureStars(float cw, float ch)
    {
        if (_stars != null && Math.Abs(cw - _starsW) < 1f && Math.Abs(ch - _starsH) < 1f) return;
        _stars  = new Star[StarCount];
        _starsW = cw; _starsH = ch;
        for (int i = 0; i < _stars.Length; i++)
        {
            double r = _rng.NextDouble();
            int layer = r < 0.50 ? 0 : r < 0.85 ? 1 : 2;
            _stars[i].X = (float)_rng.NextDouble() * cw;
            _stars[i].Y = (float)_rng.NextDouble() * ch;
            _stars[i].Speed = layer switch { 0 => 12f, 1 => 28f, _ => 55f };
            _stars[i].Brightness = layer switch
            {
                0 => 0.22f + (float)_rng.NextDouble() * 0.13f,
                1 => 0.45f + (float)_rng.NextDouble() * 0.18f,
                _ => 0.75f + (float)_rng.NextDouble() * 0.18f,
            };
        }
    }
}
