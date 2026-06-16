# 08 – Proposed Chassis Extensions (Kia'i + Koa)

> **Status: design / not yet built.** This doc is the reconciled output of planning two new
> games in parallel — **Kia'i** (Defender homage, [Docs/Kiai/DESIGN.md](../Kiai/DESIGN.md)) and
> **Koa** (Gauntlet homage, [Docs/Koa/DESIGN.md](../Koa/DESIGN.md)). Both designs surfaced
> candidate additions to `Source/Common/`. This file is the single source of truth for what
> goes into the shared chassis, with the overlaps de-duplicated. When a piece here ships, fold
> its reference into [03 – Shared Chassis](03-Shared-Chassis.md) and delete it from "proposed".

## Why these two games drive new chassis code

Every existing arcade demo scales **one fixed world to fit the canvas** (`Pohaku/Renderer.cs:240-248`,
`Hahai/Renderer.cs:33-39`) — there is no scrolling viewport anywhere in the repo. Both new games
break that assumption, in *opposite* ways:

| | **Kia'i** (Defender) | **Koa** (Gauntlet) |
|---|---|---|
| Space | Continuous, world ≫ screen | Tile grid, dungeon ≫ screen |
| Camera | 1-D **wrapping** (toroidal X) | 2-D **bounded** (clamp at edges) |
| Collision | circle-circle (toroidal X) | circle-vs-wall-tile (AABB slide) |
| AI | few, smart, fast | many, dumb, dense (flow-field) |

Because the camera need is shared (and divergent only in *wrap vs clamp*), the camera is the
load-bearing reconciliation: **one `Camera2D` serves both.** The rest split cleanly by game.

## The reconciliation: one `Camera2D`, not two

Both planning passes proposed a `Camera2D`. Kia'i's used a per-axis `AxisMode {Free,Clamp,Wrap}`;
Koa's used a single `WrapX` bool plus `Zoom` and an `Apply(canvas)` convenience. **Per-axis wins** —
Kia'i wraps X but clamps/frees Y, so a single bool can't express it — and we keep Koa's `Zoom` and
`Apply`. The merged API:

```csharp
namespace Arcade.Common.Chassis;

public enum AxisMode { Free, Clamp, Wrap }

public struct CameraAxis {
    public AxisMode Mode;
    public float WorldSize;    // Wrap: torus circumference. Clamp: world extent (lower bound 0).
    public float LookAhead;    // bias the followed point toward facing/velocity (world units)
    public float FollowRate;   // exp-lerp stiffness; <= 0 means snap (no easing)
}

public sealed class Camera2D {
    public float CenterX, CenterY;     // world point mapped to the viewport centre
    public float ViewW, ViewH;         // viewport size in pixels (from SKCanvasElement area)
    public float Zoom = 1f;            // world-units -> pixels (1 = no scale)
    public CameraAxis X, Y;

    public void SetViewport(float w, float h);               // call from World.Resize
    public void Follow(float tx, float ty, float dt);        // honours each axis Mode/LookAhead/FollowRate
    public void FollowLookAhead(float tx, float ty, float vx, float vy, float dt);
    public void Snap(float x, float y);

    public float Left => CenterX - ViewW / (2 * Zoom);
    public float Top  => CenterY - ViewH / (2 * Zoom);

    public float ToScreenX(float worldX);   // Wrap -> WrapDelta-based; Clamp/Free -> linear
    public float ToScreenY(float worldY);
    public Vec2  ToScreen(Vec2 world);
    public float ToWorldX(float screenX);
    public float ToWorldY(float screenY);
    public Vec2  ToWorld(Vec2 screen);

    public SKRect VisibleWorldRect(float pad = 0);                       // for tile/entity culling
    public void   ForEachVisibleX(float worldX, float pad, Action<float> drawAtScreenX); // seam replicas
    public void   ForEachVisibleY(float worldY, float pad, Action<float> drawAtScreenY);
    public void   Apply(SKCanvas c);   // canvas.Save(); Scale(Zoom); Translate(-Left,-Top). Caller Restores.

    // Toroidal helpers (fold Kia'i's proposed ToroidalMath here so collision/AI can call them
    // without holding a camera instance):
    public static float Wrap(float v, float size);          // ((v % size) + size) % size
    public static float WrapDelta(float a, float b, float size);  // shortest signed a->b on the torus
}
```

How each game configures it:

- **Kia'i:** `X = { Mode = Wrap, WorldSize = WorldWidth, LookAhead = ViewW*0.25f, FollowRate = 3.5f }`,
  `Y = { Mode = Free }` (world is one screen tall). Renders entities via `ForEachVisibleX` so sprites
  near the seam draw on both sides. Collision/AI call `Camera2D.WrapDelta` for toroidal nearest-distance.
- **Koa:** `X = { Mode = Clamp, WorldSize = dungeonW, FollowRate = 0 (snap) }`, `Y` same. Uses
  `VisibleWorldRect` to cull tiles/entities, and `Apply(canvas)` for the world→screen translate.
- **Legacy fit-to-screen** (if ever retrofitted) is just `Zoom = min(view/world)` with `Snap` to centre.

This single class replaces the inline transforms in every Renderer and is the **first thing to build**,
before either game.

## Full proposed-piece inventory

Priority key: **P0** = build before/with the first game (shared); **P1** = build with the owning game;
**P2** = nice-to-have, safe to defer.

| Piece | File (`Source/Common/...`) | New / Ext | Consumers | Prio |
|---|---|---|---|---|
| `Camera2D` (+ `CameraAxis`, `AxisMode`, toroidal statics) | `Chassis/Camera2D.cs` | New | Kia'i, Koa, future scrollers | **P0** |
| `VectorShapes` — cached `Poly`, `DrawAt(rot/scale)`, jittered `Blob` | `Chassis/VectorShapes.cs` | New (generalises Pohaku `BuildPath`/asteroid idiom) | Kia'i, Koa, all | **P0** |
| `Radar` — minimap projection + blips (wrap **or** fixed) | `Chassis/Radar.cs` | New | Kia'i (scanner); Koa (optional full-map) | **P1** (Kia'i) |
| `SeamlessTerrain` — integer-harmonic periodic height field | `Chassis/SeamlessTerrain.cs` | New | Kia'i; future side-scrollers | **P1** (Kia'i) |
| `TileGrid<T>` — cell math + `MoveCircle` wall-slide resolver | `Chassis/TileGrid.cs` | New (generalises `Arena`/`Grid`) | Koa; future top-down/platformer | **P1** (Koa) |
| `AsciiMap` — validate + per-glyph parse callback | `Chassis/AsciiMap.cs` | New (generalises `Arena()` parse) | Koa; any authored-level game | **P1** (Koa) |
| `FlowField` — multi-source BFS distance + flow dir | `Chassis/FlowField.cs` | New | Koa; any chase-the-player game | **P1** (Koa) |
| `HudText.Bar` — neon fill bar (health/fuel/shield) | extend `Chassis/HudText.cs` | Ext | Koa (health), Mahina (fuel), Heiau (shields) | **P1** (Koa) |
| `Pool<T>` — object pool for short-lived entities | `Chassis/Pool.cs` | New | Koa (hordes); retrofit others | **P2** |
| `Entity2D` base with per-axis wrap | `Chassis/Entity2D.cs` | Ext | both | **P2 / defer** |

### Notes on the deferred pieces
- **`Pool<T>` (P2):** Koa's dozens-of-enemies + projectiles + particles will allocate heavily on
  wasm where GC pauses show most. The pool keeps the existing `Add` / `RemoveAll(!Alive)` ergonomics
  (`Pohaku/GameWorld.cs:173-175`) while recycling. Build it only if Koa profiling shows GC stutter —
  otherwise the existing list-sweep is fine.
- **`Entity2D` base (defer):** Each game's `Entity` subclass carries game-specific fields and the base
  is trivial; sharing it couples the games for little payoff. Leave each game's local `Entity` base.

## Build order

1. **P0 chassis first:** `Camera2D` + `VectorShapes`. Unit-exercise the camera both ways (a tiny
   wrap test and a clamp test) before wiring a game to it — the seam math is the easiest thing to get
   subtly wrong.
2. **Kia'i** end-to-end (adds `Radar`, `SeamlessTerrain`). This proves the wrapping camera path.
3. **Koa** end-to-end (adds `TileGrid<T>`, `AsciiMap`, `FlowField`, `HudText.Bar`). This proves the
   bounded/clamped camera path and tile collision.
4. Re-evaluate `Pool<T>` from Koa profiling.

## Cross-game consistency decisions (resolved here)

- **Mode state machine:** both games use the documented 4-state standard `Title → Playing → GameOver → Attract`
  (`02-Demo-Anatomy.md:100-121`), with an Attract autopilot (Kia'i: a bot that flies + shoots landers;
  Koa: a flow-field-following auto-hero). Pohaku's 3-state variant is legacy; do not copy it.
- **Layout:** both use the **Pohaku stretched-`GameSurface`** layout (`Pohaku/MainPage.xaml:8-11`),
  **not** the Hokulele/Hahai fixed Viewbox — because `Camera2D`, not a Viewbox, performs framing.
- **Catalog accents** (match the colours already socialised; agents proposed alternates, overridden here
  for consistency with the original pitch):
  - Kia'i → `new SKColor(0x44, 0xCC, 0xFF)` (guardian sky-cyan)
  - Koa → `new SKColor(0xFF, 0x55, 0x33)` (torch ember)
- **Launcher grid:** Kia'i + Koa are the **two more games** whose addition unblocks re-enabling the
  commented-out Paku card and fixing the launcher grid for the larger count — do that grid work once,
  for all three, when these land (`Launcher/Game/GameCatalog.cs`).
