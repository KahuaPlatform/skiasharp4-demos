using System;
using System.Diagnostics;
using SkiaSharp;

namespace Arcade.Common.Chassis;

public static class Marquee
{
    // The Stopwatch is shared across calls — same clock drives marquee scroll
    // position, title hue cycling, and any other "time since launch" effects.
    static readonly Stopwatch _clock = Stopwatch.StartNew();
    public static double TimeSeconds => _clock.Elapsed.TotalSeconds;

    public const float DefaultScrollSpeed = 200f;
    public const float DefaultTiltDegrees = 30f;

    // Perspective-tilted scrolling marquee — letters slide right-to-left along
    // the bottom of the screen with a forward tilt that fakes a "looking into
    // the distance" feel. Used on each game's title screen.
    public static void Draw(SKCanvas c, string text, float canvasW, float canvasH,
                            float scrollSpeed = DefaultScrollSpeed,
                            float tiltDegrees = DefaultTiltDegrees,
                            float baselineFraction = 0.92f)
    {
        float advance = GlyphFont.CharAdvance;
        float totalW  = text.Length * advance;
        float loop    = totalW + canvasW;
        double time   = TimeSeconds;
        float pixelOffset = (float)((time * scrollSpeed) % loop);
        float startX    = canvasW - pixelOffset;
        float baselineY = canvasH * baselineFraction;

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

        float centerX = canvasW / 2f;
        c.Save();
        c.Translate(centerX, baselineY - h);
        c.Concat(in perspective);

        float wTop    = 1f + h * sinT / d;
        float cullPad = (canvasW / 2f) * (wTop - 1f) + GlyphFont.CharWidth;
        for (int i = 0; i < text.Length; i++)
        {
            float x = startX + i * advance;
            if (x + GlyphFont.CharWidth < -cullPad || x > canvasW + cullPad) continue;
            if (!GlyphFont.Glyphs.TryGetValue(text[i], out var glyph)) continue;

            c.Save();
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

    // Big hue-cycling vector title (e.g. "MAHINA", "HEIAU"). Centred horizontally
    // at the given vertical position, rendered using GlyphFont.
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
