using System;
using SkiaSharp;

namespace Arcade.Common.Chassis;

// A minimap / scanner-strip projection: compresses a whole game world into a
// small on-screen rectangle and plots blips for entities. Built for Kia'i's
// Defender-style scanner — a full-width strip across the top that shows the
// entire wrapped world at a glance — but written generally so a bounded game
// (Koa) can drop in a fixed full-map radar with the same calls.
//
// Two projection modes, selected by WrapX:
//
//   WrapX = true  (ship-centred, toroidal):
//       The strip is centred on a focus world-X (the player ship). A blip's
//       horizontal position is its *shortest signed* toroidal distance from the
//       focus, scaled to the strip — so the focus is always dead centre and the
//       world appears to scroll under a fixed marker as the ship flies. This is
//       the classic Defender scanner behaviour.
//
//   WrapX = false (fixed):
//       A plain linear map from [0, WorldWidth] to the strip. The whole world is
//       shown statically; nothing is centred.
//
// The Y axis is always a linear [0, WorldHeight] -> [Top, Top+Height] map (no
// wrapping vertically). The caller draws the radar in *canvas* space, after any
// camera transform has been restored, since the strip is HUD, not world.
public sealed class Radar
{
    // Strip rectangle in canvas pixels.
    public float Left, Top, Width, Height;

    // The world extents the strip represents.
    public float WorldWidth, WorldHeight;

    // When true, project X toroidally and centre on FocusX (ship-centred mode).
    // When false, project X linearly across the whole world.
    public bool WrapX;

    // The world X held at the centre of the strip in WrapX mode (typically the
    // ship). Ignored when WrapX is false.
    public float FocusX;

    // Configure the strip rectangle. Call from the renderer once the canvas size
    // is known (e.g. a 40px band across the top of the playfield).
    public void SetRect(float left, float top, float width, float height)
    {
        Left = left; Top = top; Width = width; Height = height;
    }

    public void SetWorld(float worldWidth, float worldHeight)
    {
        WorldWidth = worldWidth <= 0f ? 1f : worldWidth;
        WorldHeight = worldHeight <= 0f ? 1f : worldHeight;
    }

    // Project a world X to a strip (canvas) X.
    //   WrapX:  centre + shortestSignedToroidalDistance(focus -> worldX) * scale.
    //           A point at the focus lands at the strip centre; the world wraps
    //           so the far edge of the loop folds to the strip edges.
    //   fixed:  plain linear [0, WorldWidth] -> [Left, Left+Width].
    public float ToRadarX(float worldX)
    {
        if (WrapX)
        {
            float delta = Camera2D.WrapDelta(FocusX, worldX, WorldWidth);
            return Left + Width / 2f + delta * (Width / WorldWidth);
        }
        return Left + Camera2D.Wrap(worldX, WorldWidth) / WorldWidth * Width;
    }

    // Project a world Y to a strip (canvas) Y — always a linear vertical map.
    public float ToRadarY(float worldY) => Top + (worldY / WorldHeight) * Height;

    // Map a whole world point to a strip point.
    public SKPoint ToRadar(float worldX, float worldY) =>
        new(ToRadarX(worldX), ToRadarY(worldY));

    // Draw a single entity blip. r is the blip radius in canvas pixels; the neon
    // halo+sharp pass gives it the same glow as everything else. In WrapX mode a
    // blip whose toroidal X projects outside the strip is culled (it is "off the
    // back" of the scanner), so only the visible half-world of blips draw.
    public void DrawBlip(SKCanvas c, float worldX, float worldY, float r, SKColor color)
    {
        float x = ToRadarX(worldX);
        if (x < Left - r || x > Left + Width + r) return;   // off the scanner edge
        float y = ToRadarY(worldY);
        NeonDraw.CircleFill(c, x, y, r, color);
    }

    // Stroke a faint terrain silhouette across the strip. Walks the strip in
    // `samples` steps, converts each strip-X back to a world-X (inverting the
    // projection, including the toroidal recentring), samples heightAt, and plots
    // the resulting polyline. Because the inverse of the WrapX projection is just
    // focus + (stripOffset / scale) wrapped into the world, the silhouette is
    // continuous across the seam with no special-casing — same trick as the main
    // terrain strip, at scanner scale.
    public void DrawTerrain(SKCanvas c, Func<float, float> heightAt, SKColor color, int samples = 96)
    {
        if (samples < 2) samples = 2;
        using var b = new SKPathBuilder();
        float scaleX = Width / WorldWidth;            // world units -> strip px
        for (int i = 0; i <= samples; i++)
        {
            float sx = Left + (i / (float)samples) * Width;
            // Invert ToRadarX to recover the world X this strip column represents.
            float worldX = WrapX
                ? FocusX + (sx - (Left + Width / 2f)) / scaleX
                : (sx - Left) / Width * WorldWidth;
            float worldY = heightAt(worldX);
            float sy = ToRadarY(worldY);
            if (i == 0) b.MoveTo(sx, sy); else b.LineTo(sx, sy);
        }
        using var path = b.Detach();
        // Faint: the terrain is context, not the focus of the scanner.
        NeonDraw.Stroke(c, path, color.WithAlpha(0x80));
    }

    // Draw the strip's frame (a thin neon rectangle) plus the centred ship caret
    // in WrapX mode. The caret is a small filled triangle/marker at the strip
    // centre pointing in `facingSign` (+1 right, -1 left) so the scanner always
    // shows "you are here, facing this way". Pass facingSign = 0 to skip it.
    public void DrawFrame(SKCanvas c, SKColor frameColor, SKColor shipColor, float facingSign)
    {
        // Frame: four neon edges.
        float r = Left + Width, b = Top + Height;
        NeonDraw.Line(c, Left, Top, r, Top, frameColor.WithAlpha(0x90));
        NeonDraw.Line(c, Left, b, r, b, frameColor.WithAlpha(0x90));
        NeonDraw.Line(c, Left, Top, Left, b, frameColor.WithAlpha(0x90));
        NeonDraw.Line(c, r, Top, r, b, frameColor.WithAlpha(0x90));

        if (WrapX && facingSign != 0f)
        {
            // Bright caret at the strip centre, pointing along the facing sign.
            float cx = Left + Width / 2f;
            float cy = Top + Height / 2f;
            float s = MathF.Sign(facingSign);
            float w = 6f, h = 5f;
            using var pb = new SKPathBuilder();
            pb.MoveTo(cx + s * w, cy);
            pb.LineTo(cx - s * w, cy - h);
            pb.LineTo(cx - s * w, cy + h);
            pb.Close();
            using var caret = pb.Detach();
            NeonDraw.Stroke(c, caret, shipColor);
        }
    }
}
