using System;
using SkiaSharp;

namespace Arcade.Common.Chassis;

// Per-axis framing mode for a Camera2D axis.
//   Free  — the axis is unbounded; the camera centre may sit anywhere.
//   Clamp — the axis is bounded to [0, WorldSize]; the viewport never shows
//           "off the edge" of the world (used by Koa's bounded dungeon).
//   Wrap  — the axis is a torus of circumference WorldSize; world coordinates
//           wrap around the seam and entities near the seam draw on both sides
//           (used by Kia'i's horizontally-wrapping Defender world).
public enum AxisMode { Free, Clamp, Wrap }

// Configuration for one camera axis. A Camera2D owns two of these (X, Y) so a
// game can wrap one axis while clamping/freeing the other — Kia'i needs exactly
// that (wrap X, free Y) and a single WrapX bool could not express it.
public struct CameraAxis
{
    // How this axis frames/wraps the world.
    public AxisMode Mode;

    // Wrap: the torus circumference (world wraps at WorldSize back to 0).
    // Clamp: the world extent; the visible window is held inside [0, WorldSize].
    // Free: unused.
    public float WorldSize;

    // Look-ahead bias, in world units. FollowLookAhead shifts the followed point
    // by this much in the sign of the tracked velocity, so the camera leads the
    // subject in its direction of travel rather than centring on it.
    public float LookAhead;

    // Exp-lerp stiffness for Follow. Larger = stiffer/snappier easing. A value
    // <= 0 means "snap" — the centre jumps straight to the target with no easing.
    public float FollowRate;
}

// A 2D scrolling/zooming camera shared by every game that needs a viewport
// larger or smaller than its world. Replaces the inline "scale world to fit
// canvas" transforms in the legacy renderers.
//
// World space is the coordinate system entities live in; screen space is the
// pixel coordinate system of the SKCanvas. Zoom converts world units to pixels
// (1 = no scale). The camera centre (CenterX/CenterY) is the world point that
// maps to the middle of the viewport.
//
// The subtle part is the Wrap axis. On a wrapping axis the world is a torus, so
// "the X distance from the camera to an entity" is the *shortest signed* path
// around the loop, not the naive difference — WrapDelta computes that. Screen
// mapping, follow easing, and seam-replica drawing all route through WrapDelta
// so an entity one pixel past the seam reads as one pixel away, not a world away.
public sealed class Camera2D
{
    // The world point mapped to the centre of the viewport.
    public float CenterX, CenterY;

    // Viewport size in pixels (typically the SKCanvasElement draw area).
    public float ViewW, ViewH;

    // World-units -> pixels. 1 = no scaling; >1 zooms in, <1 zooms out.
    public float Zoom = 1f;

    // Per-axis configuration.
    public CameraAxis X, Y;

    // Set the viewport pixel size — call from World.Resize / the canvas paint
    // handler whenever the draw area changes.
    public void SetViewport(float w, float h)
    {
        ViewW = w;
        ViewH = h;
    }

    // --- Viewport edges (world space) ---------------------------------------
    // Half the viewport in world units is ViewW/(2*Zoom); Left/Top are the world
    // coordinates of the viewport's top-left corner.
    public float Left => CenterX - ViewW / (2 * Zoom);
    public float Top  => CenterY - ViewH / (2 * Zoom);

    // --- Following -----------------------------------------------------------

    // Ease the camera centre toward (tx, ty), honouring each axis' Mode and
    // FollowRate. dt is the frame delta in seconds. Easing is frame-rate
    // independent: the blend factor is 1 - exp(-FollowRate*dt), which converges
    // at the same wall-clock rate regardless of frame time. FollowRate <= 0
    // snaps. After easing, each axis is normalised/clamped per its Mode.
    public void Follow(float tx, float ty, float dt)
    {
        CenterX = FollowAxis(in X, CenterX, tx, dt, ViewW);
        CenterY = FollowAxis(in Y, CenterY, ty, dt, ViewH);
    }

    // As Follow, but biases the target by LookAhead in the sign of the supplied
    // velocity per axis — the camera leads the subject in its travel direction.
    public void FollowLookAhead(float tx, float ty, float vx, float vy, float dt)
    {
        if (vx != 0f) tx += MathF.Sign(vx) * X.LookAhead;
        if (vy != 0f) ty += MathF.Sign(vy) * Y.LookAhead;
        CenterX = FollowAxis(in X, CenterX, tx, dt, ViewW);
        CenterY = FollowAxis(in Y, CenterY, ty, dt, ViewH);
    }

    // Jump the centre straight to (x, y) with no easing, then normalise/clamp.
    public void Snap(float x, float y)
    {
        CenterX = NormalizeCenter(in X, x, ViewW);
        CenterY = NormalizeCenter(in Y, y, ViewH);
    }

    // Ease one axis' centre toward a target, then normalise/clamp it. On a Wrap
    // axis we ease along the torus (shortest signed path via WrapDelta) so the
    // camera takes the short way around the seam, then wrap the result back into
    // [0, WorldSize). FollowRate <= 0 (or dt <= 0) collapses to a snap.
    private float FollowAxis(in CameraAxis axis, float center, float target, float dt, float viewExtent)
    {
        if (axis.FollowRate <= 0f || dt <= 0f)
            return NormalizeCenter(in axis, target, viewExtent);

        float t = 1f - MathF.Exp(-axis.FollowRate * dt);

        float next;
        if (axis.Mode == AxisMode.Wrap && axis.WorldSize > 0f)
        {
            // Move toward the target along the shortest torus path, then wrap.
            float delta = WrapDelta(center, target, axis.WorldSize);
            next = center + delta * t;
        }
        else
        {
            next = center + (target - center) * t;
        }

        return NormalizeCenter(in axis, next, viewExtent);
    }

    // Fold a raw centre value into the legal range for the axis' Mode:
    //   Wrap  — wrap into [0, WorldSize).
    //   Clamp — hold the *viewport* inside [0, WorldSize]: the centre is bounded
    //           to [halfView, WorldSize - halfView]. If the world is narrower
    //           than the viewport, centre the world instead (WorldSize/2).
    //   Free  — pass through unchanged.
    // viewExtent is the viewport pixel size along this axis (ViewW for X,
    // ViewH for Y) — passed in by the caller so the clamp uses the right span.
    private float NormalizeCenter(in CameraAxis axis, float center, float viewExtent)
    {
        switch (axis.Mode)
        {
            case AxisMode.Wrap:
                return axis.WorldSize > 0f ? Wrap(center, axis.WorldSize) : center;

            case AxisMode.Clamp:
            {
                float halfView = viewExtent / (2f * Zoom);
                if (axis.WorldSize <= 2f * halfView)
                    return axis.WorldSize / 2f; // world smaller than view: centre it
                return Math.Clamp(center, halfView, axis.WorldSize - halfView);
            }

            default:
                return center;
        }
    }

    // --- World <-> screen transforms ----------------------------------------

    // Map a world X to a screen X. On Wrap axes the mapping is relative to the
    // camera centre via WrapDelta, so the seam is handled transparently: an
    // entity just past the seam maps just off the near edge, not a world away.
    // Clamp/Free axes use a plain linear transform. Zoom scales the result.
    public float ToScreenX(float worldX)
    {
        if (X.Mode == AxisMode.Wrap && X.WorldSize > 0f)
        {
            float delta = WrapDelta(CenterX, worldX, X.WorldSize);
            return ViewW / 2f + delta * Zoom;
        }
        return (worldX - CenterX) * Zoom + ViewW / 2f;
    }

    public float ToScreenY(float worldY)
    {
        if (Y.Mode == AxisMode.Wrap && Y.WorldSize > 0f)
        {
            float delta = WrapDelta(CenterY, worldY, Y.WorldSize);
            return ViewH / 2f + delta * Zoom;
        }
        return (worldY - CenterY) * Zoom + ViewH / 2f;
    }

    public Vec2 ToScreen(Vec2 world) => new(ToScreenX(world.X), ToScreenY(world.Y));

    // Inverse of ToScreenX/Y. For Wrap axes we wrap the result back into the
    // world range so a screen coordinate always maps to a canonical world point.
    public float ToWorldX(float screenX)
    {
        float world = (screenX - ViewW / 2f) / Zoom + CenterX;
        if (X.Mode == AxisMode.Wrap && X.WorldSize > 0f)
            return Wrap(world, X.WorldSize);
        return world;
    }

    public float ToWorldY(float screenY)
    {
        float world = (screenY - ViewH / 2f) / Zoom + CenterY;
        if (Y.Mode == AxisMode.Wrap && Y.WorldSize > 0f)
            return Wrap(world, Y.WorldSize);
        return world;
    }

    public Vec2 ToWorld(Vec2 screen) => new(ToWorldX(screen.X), ToWorldY(screen.Y));

    // --- Culling & seam replicas --------------------------------------------

    // The world-space rectangle covered by the viewport, optionally grown by
    // pad world units on every side (use pad to keep entities that straddle the
    // edge from popping). Accounts for Zoom. Useful for tile/entity culling.
    //
    // Note: for a Wrap axis the returned rect is expressed around the current
    // centre and may extend past [0, WorldSize]; callers culling a wrapped world
    // should test membership with WrapDelta rather than a raw Contains.
    public SKRect VisibleWorldRect(float pad = 0)
    {
        float halfW = ViewW / (2 * Zoom);
        float halfH = ViewH / (2 * Zoom);
        return new SKRect(
            CenterX - halfW - pad,
            CenterY - halfH - pad,
            CenterX + halfW + pad,
            CenterY + halfH + pad);
    }

    // Invoke drawAtScreenX once per on-screen replica of worldX. On a Wrap axis
    // an entity can be visible at its base position and/or at ±WorldSize offsets
    // (so a sprite straddling the seam draws on both sides); we emit a callback
    // for each replica whose screen X lands in [-pad, ViewW + pad]. On a
    // non-Wrap axis there is a single position, emitted only if it is visible.
    public void ForEachVisibleX(float worldX, float pad, Action<float> drawAtScreenX)
    {
        if (X.Mode == AxisMode.Wrap && X.WorldSize > 0f)
        {
            // Canonical screen X for the base position (seam-aware).
            float baseScreen = ToScreenX(worldX);
            float worldSizePx = X.WorldSize * Zoom;
            // Test the base and its two neighbouring replicas (±one world width).
            for (int k = -1; k <= 1; k++)
            {
                float sx = baseScreen + k * worldSizePx;
                if (sx >= -pad && sx <= ViewW + pad)
                    drawAtScreenX(sx);
            }
        }
        else
        {
            float sx = ToScreenX(worldX);
            if (sx >= -pad && sx <= ViewW + pad)
                drawAtScreenX(sx);
        }
    }

    public void ForEachVisibleY(float worldY, float pad, Action<float> drawAtScreenY)
    {
        if (Y.Mode == AxisMode.Wrap && Y.WorldSize > 0f)
        {
            float baseScreen = ToScreenY(worldY);
            float worldSizePx = Y.WorldSize * Zoom;
            for (int k = -1; k <= 1; k++)
            {
                float sy = baseScreen + k * worldSizePx;
                if (sy >= -pad && sy <= ViewH + pad)
                    drawAtScreenY(sy);
            }
        }
        else
        {
            float sy = ToScreenY(worldY);
            if (sy >= -pad && sy <= ViewH + pad)
                drawAtScreenY(sy);
        }
    }

    // Push the camera transform onto the canvas: world coordinates drawn after
    // this call land in the right screen pixels. Equivalent to ToScreen for
    // linear (Clamp/Free) axes. THE CALLER MUST Restore() the canvas afterwards.
    //
    // Note: this is a single affine Scale+Translate, so it does NOT replicate
    // across the seam on a Wrap axis — for wrapped worlds either draw seam
    // replicas via ForEachVisibleX/Y, or use ToScreen* per entity. Apply is the
    // convenient path for the bounded/clamped case (Koa).
    public void Apply(SKCanvas c)
    {
        c.Save();
        c.Scale(Zoom);
        c.Translate(-Left, -Top);
    }

    // --- Toroidal helpers (static so collision/AI can call without a camera) --

    // Fold v into [0, size): the canonical positive-modulo wrap. Folds negative
    // values correctly (unlike the % operator, which keeps the sign).
    public static float Wrap(float v, float size)
    {
        if (size <= 0f) return v;
        return ((v % size) + size) % size;
    }

    // Shortest signed displacement from a to b on a torus of circumference size.
    // Result is in (-size/2, size/2]: positive means "b is ahead of a going the
    // short way". This is the toroidal nearest-distance used for wrapped screen
    // mapping, follow easing, and collision/AI.
    public static float WrapDelta(float a, float b, float size)
    {
        if (size <= 0f) return b - a;
        float d = Wrap(b - a, size);   // 0 .. size
        if (d > size / 2f) d -= size;  // take the short way around the loop
        return d;
    }
}
