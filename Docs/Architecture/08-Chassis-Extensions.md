# 08 – Chassis Extensions (Kia'i + Koa)

> **Status: shipped.** This was the reconciled design for the chassis additions that two new games —
> **Kia'i** (Defender homage, [Docs/Kiai/DESIGN.md](../Kiai/DESIGN.md)) and **Koa** (Gauntlet homage,
> [Docs/Koa/DESIGN.md](../Koa/DESIGN.md)) — drove into `Source/Common/`. Every P0 and P1 piece is
> built and in use; the per-piece **reference** now lives in
> [03 – Shared Chassis](03-Shared-Chassis.md). What this doc keeps is the part 03 shouldn't carry:
> *why* the chassis has these shapes, what was deliberately **not** built, and the cross-game
> decisions the two games forced.

## Why these two games drove new chassis code

Every arcade demo before these two scales **one fixed world to fit the canvas**
(`Pohaku/Renderer.cs:250-259`, `Hahai/Renderer.cs:37-45`) — there was no scrolling viewport anywhere
in the repo. Both new games break that assumption, in *opposite* ways:

| | **Kia'i** (Defender) | **Koa** (Gauntlet) |
|---|---|---|
| Space | Continuous, world ≫ screen | Tile grid, dungeon ≫ screen |
| Camera | 1-D **wrapping** (toroidal X) | 2-D **bounded** (clamp at edges) |
| Collision | circle-circle (toroidal X) | circle-vs-wall-tile (AABB slide) |
| AI | few, smart, fast | many, dumb, dense (flow-field) |

Because the camera need is shared — and divergent only in *wrap vs clamp* — the camera was the
load-bearing reconciliation: **one `Camera2D` serves both.** The rest split cleanly by game.

## The reconciliation: one `Camera2D`, not two

Two independent planning passes each proposed a `Camera2D`. Kia'i's used a per-axis
`AxisMode {Free,Clamp,Wrap}`; Koa's used a single `WrapX` bool plus `Zoom` and an `Apply(canvas)`
convenience.

**Per-axis won.** Kia'i wraps X but leaves Y free, so a single `WrapX` bool cannot express its
configuration at all. Koa's `Zoom` and `Apply` were kept, because a bounded world wants exactly one
affine transform pushed onto the canvas rather than a per-entity mapping call.

Two further consequences of that merge, both of which survive in the shipped code:

- **The toroidal helpers are `static` on `Camera2D`.** Kia'i's plan had a separate `ToroidalMath`
  class, but collision and AI need shortest-signed-distance without holding a camera instance, and a
  second class holding the same two functions would drift from the camera's own wrap behaviour.
  `Camera2D.Wrap` / `Camera2D.WrapDelta` are the single definition; the camera's own screen mapping,
  follow easing, and seam replication all call them.
- **`Apply` does not replicate across a seam**, by design. It is one `Scale` + `Translate`, which is
  what a clamped world wants; a wrapping world draws through `ForEachVisibleX/Y` or per-entity
  `ToScreen*` instead. Trying to make one call serve both would have meant a multi-pass canvas
  transform that the bounded case pays for and never uses.

The delivered API is documented in [03 – Shared Chassis → `Chassis/Camera2D`](03-Shared-Chassis.md#chassiscamera2d).

## What shipped

Priority key as originally planned: **P0** = built before the first game (shared); **P1** = built with
the owning game; **P2** = deferred.

| Piece | File (`Source/Common/...`) | Consumers | Prio | Status |
|---|---|---|---|---|
| `Camera2D` (+ `CameraAxis`, `AxisMode`, toroidal statics) | `Chassis/Camera2D.cs` | Kia'i, Koa | **P0** | ✅ shipped |
| `VectorShapes` — cached `Poly`, `DrawAt(rot/scale)`, jittered `Blob` | `Chassis/VectorShapes.cs` | Kia'i, Koa | **P0** | ✅ shipped |
| `Radar` — minimap projection + blips (wrap **or** fixed) | `Chassis/Radar.cs` | Kia'i (scanner) | **P1** | ✅ shipped |
| `SeamlessTerrain` — integer-harmonic periodic height field | `Chassis/SeamlessTerrain.cs` | Kia'i | **P1** | ✅ shipped |
| `TileGrid<T>` — cell math + `MoveCircle` wall-slide resolver | `Chassis/TileGrid.cs` | Koa | **P1** | ✅ shipped |
| `AsciiMap` — validate + per-glyph parse callback | `Chassis/AsciiMap.cs` | Koa | **P1** | ✅ shipped |
| `FlowField` — multi-source BFS distance + flow dir | `Chassis/FlowField.cs` | Koa | **P1** | ✅ shipped |
| `HudText.Bar` — neon fill bar (health/fuel/shield) | `Chassis/HudText.cs` | Koa (health) | **P1** | ✅ shipped |
| `Pool<T>` — object pool for short-lived entities | `Chassis/Pool.cs` | — | **P2** | ❌ not built — see below |
| `Entity2D` base with per-axis wrap | `Chassis/Entity2D.cs` | — | defer | ❌ not built — see below |

Two deltas between plan and delivery worth knowing:

- **`Radar` was planned as "optional full-map" for Koa too.** Koa shipped without it — the dungeon
  reads better explored than mapped. The `WrapX = false` fixed-projection mode is implemented and
  exercised by nothing yet, so treat it as untested-in-anger if a future game wants it.
- **`HudText.Bar` was planned to be retrofitted into Mahina (fuel) and Heiau (shields).** It wasn't;
  those two still draw their own gauges. The piece lives in the chassis rather than in Koa because
  that retrofit is still the right cleanup, not because it happened.

## What was deliberately not built

### `Pool<T>` (P2) — gate not met, and not evaluated with data

The plan's gate was: *"Build it only if Koa profiling shows GC stutter — otherwise the existing
list-sweep is fine."*

That gate has **not been evaluated with profiling data.** What is known: Koa's `Enemy`, `Projectile`,
`Particle`, and `Pickup` are reference types (`Source/Koa/Koa/Game/Entities.cs`) allocated per spawn
and reclaimed by the `RemoveAll(!Alive)` sweep, so particle bursts and horde spawns do allocate on the
hot path. Desktop (`net10.0-desktop`) runs smoothly, which is the weaker of the two signals — the
plan's concern was specifically **wasm**, where GC pauses are most visible.

Decision: **left unbuilt, gate unchanged.** Retrofitting pooling into three working entity lists is a
real behavioural risk to take on speculation. If someone wants to close this, the honest next step is
profiling a wasm build under a full horde, not building the pool first.

### `Entity2D` base (defer → **decided: don't build**)

Each game's `Entity` subclass carries game-specific fields and the base is trivial — Koa's is four
fields (`Pos`, `Vel`, `Radius`, `Alive`) and is explicitly documented as *non*-wrapping, the opposite
of what Pohaku's toroidal entity wants. Sharing it would couple the games' entity models for
near-zero payoff. This is now a settled "no", not a deferral: leave each game's local `Entity` base.

### Camera unit tests — planned, never written

Build order step 1 called for "a tiny wrap test and a clamp test" before wiring a game to the camera,
on the reasoning that seam math is the easiest thing to get subtly wrong. **No test project exists in
this repo**, for the camera or anything else, so this was never done. The wrap path is exercised in
anger by Kia'i (seam-straddling sprites, toroidal collision) and the clamp path by Koa (edge-clamped
dungeon), which is empirical coverage rather than regression coverage. A future change to
`WrapDelta`, `NormalizeCenter`, or `ForEachVisibleX` has nothing to catch it but playtesting.

## Cross-game consistency decisions (as built)

- **Mode state machine:** both games use the documented 4-state standard
  `Title → Playing → GameOver → Attract` ([02 – Demo Anatomy](02-Demo-Anatomy.md#the-canonical-game-state-machine)),
  with an Attract autopilot — Kia'i flies and shoots landers, Koa runs a flow-field-following
  auto-hero. Verified in both `Game/Entities.cs`. Pohaku's 3-state `{Demo, Playing, GameOver}`
  variant is legacy; do not copy it.
- **Layout:** both use the **Pohaku stretched-`GameSurface`** layout (`Pohaku/MainPage.xaml:8-11`),
  **not** the HokuLele/Hahai fixed `Viewbox` — because `Camera2D`, not a Viewbox, performs framing.
  Both `MainPage.xaml` files carry a comment saying so.
- **Accents:** Kia'i shipped on the planned `new SKColor(0x44, 0xCC, 0xFF)` (guardian sky-cyan).
  Koa shipped `new SKColor(0xFF, 0x77, 0x44)` — a lighter torch ember than the planned `0xFF5533`,
  which read too close to its damage red in play.
- **Launcher grid: reversed — do not add these games to the catalog.** The plan called for Kia'i and
  Koa to be the two additions that justified fixing the launcher card grid and re-enabling a Paku
  card. In practice the extra cards broke the launcher's visual layout, and the repo owner's
  direction is that `Launcher/Game/GameCatalog.cs` **stays at its original eight entries**
  (Pohaku…Hahai). Paku, Kia'i, and Koa ship as **standalone apps** — wired into `Builds/Build-All.ps1`,
  `Builds/Publish-Site.ps1`, and their own `Run-*.ps1` / `Build-*.ps1` scripts. Don't re-attempt the
  grid work without an explicit ask.
