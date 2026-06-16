using System;
using System.Diagnostics;
using SkiaSharp;

namespace Arcade.Common.Chassis;

/// <summary>
/// Renders the chassis's two big animated text effects — the perspective-tilted
/// scrolling marquee along the bottom of the title screen, and the centered
/// hue-cycling rainbow title — both drawn from <see cref="GlyphFont"/> with the
/// neon halo+sharp paints.
/// </summary>
public static class Marquee
{
    // One shared wall-clock Stopwatch drives scroll position AND hue cycling, so
    // both stay wallclock-paced (frame drops never slow the scroll) and in sync.
    static readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>Seconds since process start — the shared animation clock.</summary>
    public static double TimeSeconds => _clock.Elapsed.TotalSeconds;

    /// <summary>Default marquee scroll velocity, in pixels per second.</summary>
    public const float DefaultScrollSpeed = 200f;
    /// <summary>Default backward lean of the marquee plane, in degrees.</summary>
    public const float DefaultTiltDegrees = 30f;

    /// <summary>
    /// Draws the perspective-tilted scrolling marquee: letters slide
    /// right-to-left along the bottom of the screen, leaning back so the row
    /// recedes "into the distance". Each glyph cycles hue independently.
    /// </summary>
    /// <param name="text">String to scroll (uppercase; missing glyphs are gaps).</param>
    /// <param name="canvasW">Canvas width in pixels.</param>
    /// <param name="canvasH">Canvas height in pixels.</param>
    /// <param name="scrollSpeed">Scroll velocity in px/sec.</param>
    /// <param name="tiltDegrees">Backward lean of the plane (bigger = more recede).</param>
    /// <param name="baselineFraction">Vertical baseline as a fraction of <paramref name="canvasH"/>.</param>
    public static void Draw(SKCanvas c, string text, float canvasW, float canvasH,
                            float scrollSpeed = DefaultScrollSpeed,
                            float tiltDegrees = DefaultTiltDegrees,
                            float baselineFraction = 0.92f)
    {
        float advance = GlyphFont.CharAdvance;
        float totalW  = text.Length * advance;
        // Loop length = whole string + a full screen, so the text scrolls fully
        // off the left before reappearing from the right with no visible seam.
        float loop    = totalW + canvasW;
        double time   = TimeSeconds;
        float pixelOffset = (float)((time * scrollSpeed) % loop);
        float startX    = canvasW - pixelOffset;
        float baselineY = canvasH * baselineFraction;

        // Build the perspective matrix in marquee-local coords where y=0 is the top
        // of the glyph row and y=h is the rotation axis at the bottom. d is the
        // viewer distance (3·h); smaller d = more aggressive foreshortening.
        float h    = GlyphFont.CharHeight;
        float tilt = tiltDegrees * MathF.PI / 180f;
        float cosT = MathF.Cos(tilt);
        float sinT = MathF.Sin(tilt);
        float d    = 3f * h;
        var perspective = new SKMatrix
        {
            ScaleX = 1f, SkewX = 0f,         TransX = 0f,
            SkewY  = 0f, ScaleY = cosT,      TransY = h * (1f - cosT),
            Persp0 = 0f, Persp1 = -sinT / d, Persp2 = 1f + h * sinT / d,
        };

        // Translate so the perspective origin (local x=0) sits at canvas center —
        // that's why the receding top of the plane vanishes toward mid-screen.
        float centerX = canvasW / 2f;
        c.Save();
        c.Translate(centerX, baselineY - h);
        c.Concat(in perspective);

        // Widen the off-screen cull pad to account for perspective pulling the top
        // of foreshortened glyphs toward center: a glyph just past the right edge
        // can still have its top visible. This is the max horizontal offset.
        float wTop    = 1f + h * sinT / d;
        float cullPad = (canvasW / 2f) * (wTop - 1f) + GlyphFont.CharWidth;
        for (int i = 0; i < text.Length; i++)
        {
            float x = startX + i * advance;
            if (x + GlyphFont.CharWidth < -cullPad || x > canvasW + cullPad) continue;
            if (!GlyphFont.Glyphs.TryGetValue(text[i], out var glyph)) continue;

            c.Save();
            // Draw at x-centerX to compensate for the center-origin translate above.
            c.Translate(x - centerX, 0f);
            float hue = ((float)time * 75f + i * 18f) % 360f;
            SKColor color = HsvColor.HsvToRgb(hue, 1f, 1f);
            NeonPaints.MarqueeHalo.Color = color.WithAlpha(0xC0);
            c.DrawPath(glyph, NeonPaints.MarqueeHalo);
            NeonPaints.MarqueeSharp.Color = color;
            c.DrawPath(glyph, NeonPaints.MarqueeSharp);
            c.Restore();
        }
        c.Restore();
    }

    /// <summary>
    /// Draws a big hue-cycling vector title (e.g. "MAHINA", "HEIAU") centered
    /// horizontally at vertical offset <paramref name="yTop"/>, using
    /// <see cref="GlyphFont"/>. Each character's hue is offset so the title reads
    /// as a moving rainbow. Used on every demo's title screen.
    /// </summary>
    public static void DrawRainbowTitle(SKCanvas c, string title, float canvasW, float yTop)
    {
        float advance = GlyphFont.CharAdvance;
        float titleW = title.Length * advance - GlyphFont.CharGap;

        c.Save();
        c.Translate((canvasW - titleW) / 2f, yTop);
        float time = (float)TimeSeconds;
        for (int i = 0; i < title.Length; i++)
        {
            if (!GlyphFont.Glyphs.TryGetValue(title[i], out var glyph)) continue;
            float hue = (time * 60f + i * 22f) % 360f;
            SKColor color = HsvColor.HsvToRgb(hue, 1f, 1f);
            c.Save();
            c.Translate(i * advance, 0f);
            NeonPaints.MarqueeHalo.Color = color.WithAlpha(0xC0);
            c.DrawPath(glyph, NeonPaints.MarqueeHalo);
            NeonPaints.MarqueeSharp.Color = color;
            c.DrawPath(glyph, NeonPaints.MarqueeSharp);
            c.Restore();
        }
        c.Restore();
    }
}
