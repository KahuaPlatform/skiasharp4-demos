using System;
using System.Diagnostics;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Arcade.Common;

/// <summary>
/// Ambient deep-space backdrop that fills the entire window behind the playfield
/// <c>Viewbox</c>. Paints the deep-space gradient plus a slowly drifting,
/// twinkling parallax starfield, so the letterbox/side bars revealed by the
/// Viewbox never look empty.
/// </summary>
/// <remarks>
/// Abstract base: each demo declares a thin <c>sealed</c> wrapper in its own
/// namespace (e.g. <c>public sealed class BackgroundSurface : AmbientStarBackdrop {}</c>)
/// so <c>&lt;local:BackgroundSurface&gt;</c> can be referenced from XAML — all
/// rendering stays here. Animation is driven by the host <c>MainPage</c> calling
/// <c>Invalidate()</c> each frame; the control tracks its own <c>dt</c> via an
/// internal <see cref="Stopwatch"/> rather than relying on the caller's clock.
/// </remarks>
public abstract class AmbientStarBackdrop : SKCanvasElement
{
    /// <summary>Top color of the vertical background gradient. Override to retheme.</summary>
    protected virtual SKColor BgTop    => new(0x05, 0x00, 0x14);
    /// <summary>Bottom color of the vertical background gradient. Override to retheme.</summary>
    protected virtual SKColor BgBottom => new(0x18, 0x02, 0x36);

    // A single drifting star: position, vertical speed (parallax layer), and
    // base brightness (also drives radius). Twinkle is applied per-frame, not stored.
    struct Star { public float X, Y, Speed, Brightness; }
    const int StarCount = 110;
    Star[]? _stars;
    float _starsW, _starsH;       // canvas size the current star field was laid out for
    readonly Random _rng = new(31); // fixed seed → deterministic field across runs
    readonly Stopwatch _clock = Stopwatch.StartNew();
    double _lastT;
    readonly SKPaint _starPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    /// <summary>
    /// Per-frame paint: fills the gradient, advances + twinkles every star, and
    /// recycles stars that fall off the bottom back to the top.
    /// </summary>
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
        // Clamp dt so a paused debugger or a long stall can't teleport every star.
        float dt = MathF.Min(0.1f, (float)(now - _lastT));
        _lastT = now;
        float twinkle = (float)now;
        for (int i = 0; i < _stars!.Length; i++)
        {
            _stars[i].Y += _stars[i].Speed * dt;
            // Recycle off-bottom stars to a new random column at the top for an
            // endless downward drift.
            if (_stars[i].Y > ch + 5f)
            {
                _stars[i].X = (float)_rng.NextDouble() * cw;
                _stars[i].Y = -5f;
            }
            // Per-star sine twinkle; the i*0.31 phase offset desynchronizes them.
            float flick = 0.85f + 0.15f * MathF.Sin(twinkle * 1.7f + i * 0.31f);
            byte a = (byte)(255 * _stars[i].Brightness * flick);
            _starPaint.Color = new SKColor(255, 255, 255, a);
            float r = _stars[i].Brightness > 0.75f ? 1.5f : _stars[i].Brightness > 0.55f ? 1.0f : 0.7f;
            canvas.DrawCircle(_stars[i].X, _stars[i].Y, r, _starPaint);
        }
    }

    // Lazily (re)builds the star field, but only when the canvas size actually
    // changes — so a steady-state resize-free run reuses the same array forever.
    void EnsureStars(float cw, float ch)
    {
        if (_stars != null && Math.Abs(cw - _starsW) < 1f && Math.Abs(ch - _starsH) < 1f) return;
        _stars  = new Star[StarCount];
        _starsW = cw; _starsH = ch;
        for (int i = 0; i < _stars.Length; i++)
        {
            // Three parallax layers: ~50% far/dim/slow, ~35% mid, ~15% near/bright/fast.
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
