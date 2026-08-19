using System;

namespace Eli.Game;

// Thin domain wrapper over the shared FlowField, essentially verbatim from Koa's
// Pathing. One field sized to the current map; rebuild floods from the digger's
// cell, and each enemy reads its step direction. One field serves the whole
// swarm — that's the point of the flow-field approach over per-enemy pathfinding.
//
// The Eli-specific part is WHEN it rebuilds: Koa re-floods on a frame cadence
// alone because its terrain is effectively static. Eli's terrain is rewritten
// every frame the digger moves, so GameWorld also re-floods on Field's dirty
// flag (see GameWorld.StepSim). The flood itself needs no change — FlowField
// takes isWalkable as a delegate evaluated AT FLOOD TIME, so it reads current
// terrain for free.
public sealed class Pathing
{
    readonly Field _field;
    readonly FlowField _flow;

    public Pathing(Field field)
    {
        _field = field;
        _flow = new FlowField(field.Cols, field.Rows);
    }

    public void Rebuild(Vec2 diggerPos)
    {
        var (c, r) = _field.WorldToCell(diggerPos);
        _flow.Rebuild(c, r, _field.IsWalkable);
    }

    // The step toward the digger from a world position, as a (possibly zero)
    // direction vector. Blends the descending horizontal and vertical neighbours
    // into a diagonal when both descend, suppressing the diagonal when the corner
    // cell is not walkable so enemies never clip a wall corner — and so they fall
    // back to single-axis movement inside the 1-tile corridors the digger carves,
    // which is nearly all of Eli's map.
    public Vec2 FlowDir(Vec2 from)
    {
        var (c, r) = _field.WorldToCell(from);
        int here = _flow.Dist(c, r);
        if (here == FlowField.Unreachable) return Vec2.Zero;

        int dl = _flow.Dist(c - 1, r), dR = _flow.Dist(c + 1, r);
        int du = _flow.Dist(c, r - 1), dd = _flow.Dist(c, r + 1);

        float fx = 0f, fy = 0f;
        if      (dl < dR && dl < here) fx = -1f;
        else if (dR < dl && dR < here) fx =  1f;
        if      (du < dd && du < here) fy = -1f;
        else if (dd < du && dd < here) fy =  1f;

        if (fx != 0f && fy != 0f && !_field.IsWalkable(c + (int)fx, r + (int)fy))
        {
            int hx = fx < 0f ? dl : dR;
            int hy = fy < 0f ? du : dd;
            if (hx <= hy) fy = 0f; else fx = 0f;
        }

        return new Vec2(fx, fy);
    }

    // Whether the last flood reached this cell — i.e. whether there is a TUNNEL
    // route from here to the digger. In Koa this was a diagnostic; in Eli it is
    // the primary trigger for an Uhane to give up on the tunnels and phase
    // straight through the dirt instead.
    public bool Reachable(Vec2 from)
    {
        var (c, r) = _field.WorldToCell(from);
        return _flow.Dist(c, r) != FlowField.Unreachable;
    }
}
