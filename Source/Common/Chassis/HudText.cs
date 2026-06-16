using SkiaSharp;

namespace Arcade.Common.Chassis;

public static class HudText
{
    // Standard halo-plus-sharp text rendering used for scoring, placards,
    // game-over panels, etc. Mutates NeonPaints.FillHalo/Sharp Color per call.
    public static void Draw(SKCanvas c, string text, float x, float y, SKTextAlign align, SKFont font, SKColor color)
    {
        NeonPaints.FillHalo.Color = color.WithAlpha(0xC0);
        c.DrawText(text, x, y, align, font, NeonPaints.FillHalo);
        NeonPaints.FillSharp.Color = color;
        c.DrawText(text, x, y, align, font, NeonPaints.FillSharp);
    }

    // Neon fill bar for health / fuel / shield-style HUD gauges. Draws a dim
    // track the full width, then a glowing fill spanning fill01 (0..1) of it,
    // then a crisp frame. The fill reuses the shared NeonPaints fill pool (halo
    // + sharp) so it reads as the same glowing material as the rest of the HUD.
    //
    //   (x,y)   top-left of the bar, in screen pixels
    //   w,h     bar size in pixels
    //   fill01  fraction filled, clamped to [0,1]
    //   color   fill colour (the track + frame derive from it, dimmed)
    //
    // Koa drives this with Hero.Health/MaxHealth for the continuously-draining
    // "warrior needs food" gauge; Mahina (fuel) and Heiau (shields) are the other
    // intended consumers, hence it lives in the shared chassis.
    public static void Bar(SKCanvas c, float x, float y, float w, float h, float fill01, SKColor color)
    {
        fill01 = Math.Clamp(fill01, 0f, 1f);
        var track = new SKRect(x, y, x + w, y + h);

        // Dim track behind the fill so an empty bar still reads as a gauge.
        NeonPaints.FillSharp.Color = color.WithAlpha(0x33);
        c.DrawRect(track, NeonPaints.FillSharp);

        // The glowing fill: halo pass first (so the bar blooms), then the sharp
        // core, both clipped to the filled fraction.
        if (fill01 > 0f)
        {
            var fill = new SKRect(x, y, x + w * fill01, y + h);
            NeonPaints.FillHalo.Color = color.WithAlpha(0xB0);
            c.DrawRect(fill, NeonPaints.FillHalo);
            NeonPaints.FillSharp.Color = color;
            c.DrawRect(fill, NeonPaints.FillSharp);
        }

        // Crisp frame around the whole track.
        NeonPaints.StrokeSharp.Color = color.WithAlpha(0xCC);
        c.DrawRect(track, NeonPaints.StrokeSharp);
    }
}
