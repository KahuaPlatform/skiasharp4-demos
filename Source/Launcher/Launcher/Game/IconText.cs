using System;
using System.Collections.Generic;
using Arcade.Common.Chassis;
using SkiaSharp;

namespace Launcher.Game;

// SkiaSharp wasm's fallback font lacks glyphs like ▶ → ► —, so the launcher
// builds those out of SKPath instead and lays them inline with regular text.
// Each Draw call takes alternating strings and Icon values; the helper measures
// every segment, computes a total width, and walks left-to-right drawing both
// kinds with the same neon halo/sharp double-pass HudText uses.
public static class IconText
{
    public enum Icon { Triangle, Arrow, Chevron, EmDash }

    public static void Draw(
        SKCanvas c, float x, float y, SKTextAlign align, SKFont font, SKColor color,
        params object[] segments)
    {
        float h = font.Size * 0.7f;
        float gap = font.Size * 0.15f;

        // First pass: width of each segment.
        var widths = new float[segments.Length];
        float total = 0f;
        for (int i = 0; i < segments.Length; i++)
        {
            widths[i] = segments[i] switch
            {
                string s => font.MeasureText(s),
                Icon ic  => IconWidth(ic, h),
                _ => 0f,
            };
            total += widths[i];
        }

        float left = align switch
        {
            SKTextAlign.Center => x - total / 2f,
            SKTextAlign.Right  => x - total,
            _                  => x,
        };

        float cursor = left;
        for (int i = 0; i < segments.Length; i++)
        {
            switch (segments[i])
            {
                case string s:
                    NeonPaints.FillHalo.Color  = color.WithAlpha(0xC0);
                    NeonPaints.FillSharp.Color = color;
                    c.DrawText(s, cursor, y, SKTextAlign.Left, font, NeonPaints.FillHalo);
                    c.DrawText(s, cursor, y, SKTextAlign.Left, font, NeonPaints.FillSharp);
                    break;
                case Icon ic:
                    // Icon center is one cap-height above the text baseline so it
                    // visually aligns with the middle of uppercase characters.
                    DrawIcon(c, ic, cursor + widths[i] / 2f, y - font.Size * 0.35f, h, color);
                    break;
            }
            cursor += widths[i];
            // Inter-segment gap when an icon abuts a string with no surrounding spaces.
            if (i + 1 < segments.Length && NeedsGap(segments[i], segments[i + 1]))
            {
                cursor += gap;
            }
        }
    }

    static float IconWidth(Icon ic, float h) => ic switch
    {
        Icon.Triangle => h * 0.85f,
        Icon.Arrow    => h * 1.6f,
        Icon.Chevron  => h * 0.75f,
        Icon.EmDash   => h * 1.6f,
        _             => 0f,
    };

    static bool NeedsGap(object a, object b)
    {
        // Only add a gap when an icon sits next to text without explicit padding.
        bool aText = a is string sa && sa.Length > 0 && sa[^1] != ' ';
        bool bText = b is string sb && sb.Length > 0 && sb[0]  != ' ';
        return (a is Icon && bText) || (aText && b is Icon);
    }

    static void DrawIcon(SKCanvas c, Icon ic, float cx, float cy, float h, SKColor color)
    {
        using var path = BuildIconPath(ic, cx, cy, h);
        NeonPaints.FillHalo.Color  = color.WithAlpha(0xC0);
        NeonPaints.FillSharp.Color = color;
        c.DrawPath(path, NeonPaints.FillHalo);
        c.DrawPath(path, NeonPaints.FillSharp);
    }

    static SKPath BuildIconPath(Icon ic, float cx, float cy, float h)
    {
        using var b = new SKPathBuilder();
        switch (ic)
        {
            case Icon.Triangle:
            {
                float w = h * 0.85f;
                b.MoveTo(cx - w / 2f, cy - h / 2f);
                b.LineTo(cx + w / 2f, cy);
                b.LineTo(cx - w / 2f, cy + h / 2f);
                b.Close();
                break;
            }
            case Icon.Chevron:
            {
                float w = h * 0.75f;
                b.MoveTo(cx - w / 2f, cy - h / 2f);
                b.LineTo(cx + w / 2f, cy);
                b.LineTo(cx - w / 2f, cy + h / 2f);
                b.Close();
                break;
            }
            case Icon.Arrow:
            {
                // Horizontal arrow: shaft rectangle + triangular head.
                float w        = h * 1.6f;
                float headW    = h * 0.55f;
                float headH    = h * 0.85f;
                float shaftH   = h * 0.18f;
                float shaftEnd = cx + w / 2f - headW;
                b.MoveTo(cx - w / 2f, cy - shaftH / 2f);
                b.LineTo(shaftEnd,    cy - shaftH / 2f);
                b.LineTo(shaftEnd,    cy - headH / 2f);
                b.LineTo(cx + w / 2f, cy);
                b.LineTo(shaftEnd,    cy + headH / 2f);
                b.LineTo(shaftEnd,    cy + shaftH / 2f);
                b.LineTo(cx - w / 2f, cy + shaftH / 2f);
                b.Close();
                break;
            }
            case Icon.EmDash:
            {
                float w  = h * 1.6f;
                float th = h * 0.16f;
                b.AddRect(new SKRect(cx - w / 2f, cy - th / 2f, cx + w / 2f, cy + th / 2f));
                break;
            }
        }
        return b.Detach();
    }
}
