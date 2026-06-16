using System.Collections.Generic;
using SkiaSharp;

namespace Arcade.Common.Chassis;

/// <summary>
/// Hand-coded stroke-vector font used by the title screens and scrolling marquee
/// (NOT for SKFont text like the score — that goes through <c>HudText</c>). Each
/// character is a list of line-segment endpoints baked into an <see cref="SKPath"/>
/// fitted to a <see cref="CharWidth"/> × <see cref="CharHeight"/> box; the retro
/// 5×7-ish look is intentional.
/// </summary>
/// <remarks>
/// Only characters actually used by the games are defined; any missing char just
/// renders as a gap. Currently: A–N (no J), O–W (no Q, X), Y, the digit 4, plus
/// <c>·</c>, <c>-</c>, <c>+</c>, <c>'</c>. Extend by adding entries to the
/// dictionary in <c>Build</c>.
/// </remarks>
public static class GlyphFont
{
    /// <summary>Nominal glyph cell width, in world units.</summary>
    public const float CharWidth  = 40f;
    /// <summary>Nominal glyph cell height, in world units.</summary>
    public const float CharHeight = 56f;
    /// <summary>Horizontal gap between adjacent glyph cells.</summary>
    public const float CharGap    = 12f;
    /// <summary>Per-character horizontal step (<see cref="CharWidth"/> + <see cref="CharGap"/>).</summary>
    public const float CharAdvance = CharWidth + CharGap;

    /// <summary>Compiled glyph paths, keyed by character. Built once at type init.</summary>
    public static readonly Dictionary<char, SKPath> Glyphs = Build();

    static Dictionary<char, SKPath> Build()
    {
        // Glyphs are authored on a 4-wide × 6-tall integer grid; sx/sy scale that
        // design grid up to the CharWidth × CharHeight cell.
        float sx = CharWidth  / 4f;
        float sy = CharHeight / 6f;
        // Turns a flat (x1,y1,x2,y2,...) list of segment endpoints into a path of
        // disjoint MoveTo/LineTo strokes (these are not deprecated on SKPathBuilder).
        SKPath G(params float[] coords)
        {
            using var b = new SKPathBuilder();
            for (int i = 0; i + 3 < coords.Length; i += 4)
            {
                b.MoveTo(coords[i] * sx, coords[i + 1] * sy);
                b.LineTo(coords[i + 2] * sx, coords[i + 3] * sy);
            }
            return b.Detach();
        }
        return new Dictionary<char, SKPath>
        {
            ['A'] = G(0,6, 2,0,  2,0, 4,6,  1,4, 3,4),
            ['B'] = G(0,0, 0,6,  0,0, 3,0,  3,0, 4,1,  4,1, 4,2,  4,2, 3,3,  0,3, 3,3,  3,3, 4,4,  4,4, 4,5,  4,5, 3,6,  3,6, 0,6),
            ['C'] = G(4,1, 3,0,  3,0, 1,0,  1,0, 0,1,  0,1, 0,5,  0,5, 1,6,  1,6, 3,6,  3,6, 4,5),
            ['D'] = G(0,0, 0,6,  0,0, 3,0,  3,0, 4,1,  4,1, 4,5,  4,5, 3,6,  3,6, 0,6),
            ['E'] = G(0,0, 0,6,  0,0, 4,0,  0,3, 3,3,  0,6, 4,6),
            ['F'] = G(0,0, 0,6,  0,0, 4,0,  0,3, 3,3),
            ['G'] = G(4,1, 3,0,  3,0, 1,0,  1,0, 0,1,  0,1, 0,5,  0,5, 1,6,  1,6, 3,6,  3,6, 4,5,  4,5, 4,3,  4,3, 2,3),
            ['H'] = G(0,0, 0,6,  4,0, 4,6,  0,3, 4,3),
            ['I'] = G(1,0, 3,0,  2,0, 2,6,  1,6, 3,6),
            ['K'] = G(0,0, 0,6,  0,3, 4,0,  0,3, 4,6),
            ['L'] = G(0,0, 0,6,  0,6, 4,6),
            ['M'] = G(0,6, 0,0,  0,0, 2,3,  2,3, 4,0,  4,0, 4,6),
            ['N'] = G(0,6, 0,0,  0,0, 4,6,  4,6, 4,0),
            ['O'] = G(1,0, 3,0,  3,0, 4,1,  4,1, 4,5,  4,5, 3,6,  3,6, 1,6,  1,6, 0,5,  0,5, 0,1,  0,1, 1,0),
            ['P'] = G(0,0, 0,6,  0,0, 3,0,  3,0, 4,1,  4,1, 4,2,  4,2, 3,3,  3,3, 0,3),
            ['R'] = G(0,0, 0,6,  0,0, 3,0,  3,0, 4,1,  4,1, 4,2,  4,2, 3,3,  3,3, 0,3,  2,3, 4,6),
            ['S'] = G(4,1, 3,0,  3,0, 1,0,  1,0, 0,1,  0,1, 0,2,  0,2, 1,3,  1,3, 3,3,  3,3, 4,4,  4,4, 4,5,  4,5, 3,6,  3,6, 1,6,  1,6, 0,5),
            ['T'] = G(0,0, 4,0,  2,0, 2,6),
            ['U'] = G(0,0, 0,5,  0,5, 1,6,  1,6, 3,6,  3,6, 4,5,  4,5, 4,0),
            ['V'] = G(0,0, 2,6,  2,6, 4,0),
            ['W'] = G(0,0, 1,6,  1,6, 2,2,  2,2, 3,6,  3,6, 4,0),
            ['Y'] = G(0,0, 2,3,  4,0, 2,3,  2,3, 2,6),
            ['·'] = G(1.7f,3, 2.3f,3),
            ['4'] = G(3,0, 0,4,  0,4, 4,4,  3,0, 3,6),
            ['-'] = G(1,3, 3,3),
            ['+'] = G(1,3, 3,3,  2,1, 2,5),
            ['\''] = G(2,0, 2,2),
        };
    }
}
