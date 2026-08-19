# Eli — Design (Dig Dug homage)

> **Status: built.** This was the implementation plan; the game now lives at `Source/Eli/`.
> Unlike Kia'i and Koa, Eli drove **no new shared chassis code** — it is the first game built entirely
> out of the existing pieces (`Camera2D`, `TileGrid<T>`, `AsciiMap`, `FlowField`, `VectorShapes`,
> `HudText.Bar`, `Marquee`, `AmbientStarBackdrop`, `HighScoreStore`, `AudioEngineBase`), which are
> documented in [03 – Shared Chassis](../Architecture/03-Shared-Chassis.md); the rationale behind them
> is in [08 – Chassis Extensions](../Architecture/08-Chassis-Extensions.md). The one piece this game
> did surface as a candidate — `TileGrid<T>.ForEachOverlappedCell` — is recorded as **P2 and left
> unbuilt** under [Chassis impact](#chassis-impact). `HudText.Bar` gained its third consumer here (the
> pump gauge), after Koa's health clock.
>
> Reference demo was **Koa** ([Docs/Koa/DESIGN.md](../Koa/DESIGN.md), `Source/Koa/`). The Phase 1
> prompt that produced this document is preserved alongside at [Design-prompt.md](Design-prompt.md);
> see [As built](#as-built) for where the shipped code refines what is written below, and
> [DEFECTS.md](DEFECTS.md) for the full defect history (what was found, by whom, and the fix).

## Elevator pitch

**ELI** (Hawaiian *"to dig"*) is a **Dig Dug** homage: a side-on field of packed dirt, four strata
deep, each a different hue. You are a digger who **carves tunnels wherever you walk**, rewriting the
terrain continuously. You kill the two enemy types that patrol those tunnels with a **harpoon** — a
stateful segment that extends from you along your facing, sticks into whatever it hits, and then
inflates the victim over successive pumps until it bursts. Scattered through the dirt are **boulders**
that hang suspended until you dig out the ground beneath them; they wobble, then fall, crushing
enemies and you alike. Clear the field of enemies to advance; touching an un-inflated enemy costs a
life. Levels are authored ASCII.

Architecturally closest to **Koa** — same stretched-`GameSurface` + `Camera2D` layout family, same
`TileGrid<Tile>` / `AsciiMap` / `FlowField` trio, same continuous-circle-vs-tile motion — but
diverging in four ways that drive every decision below: **mutable terrain**, a **stateful weapon**,
**gravity applied to terrain features**, and a **two-mode enemy AI** where one mode leaves the grid
entirely.

## What's different from Koa

| | **Koa** (Gauntlet) | **Eli** (Dig Dug) |
|---|---|---|
| Terrain | Authored once; only ever `Door → Floor` (`Koa/Game/TileMap.cs:72-80`) | **Rewritten every frame** — walking carves `Dirt → Tunnel`; falling boulders carve too |
| Weapon | Fire-and-forget `Projectile`, integrated then swept (`Koa/Game/GameWorld.cs:546-559`) | **Stateful extending segment** with a 4-state machine and an attached victim |
| Gravity | None — nothing in the repo has falling terrain | **Boulders fall**, crushing enemies and the player |
| Enemy AI | One mode: flow-field chase (`Koa/Game/GameWorld.cs:427-490`) | **Two modes** — flow-field in tunnels, straight-line *phasing through dirt* out of them |
| Movement | 8-directional (`Koa/MainPage.xaml.cs:67-68`) | **4-directional**, so carved corridors stay exactly one cell wide |

The four deltas are load-bearing, and each has a knock-on:

- **Mutable terrain** means every consumer of grid state has to tolerate per-frame edits. Koa rebuilds
  its flow field purely on a frame cadence (`Koa/Game/GameWorld.cs:211-212`); Eli must **also** rebuild
  on any terrain edit, or enemies path through dirt that no longer exists (or refuse to enter a tunnel
  that now does). See [The field](#the-field-tilemap--strata--digging).
- **The stateful weapon** cannot reuse `Projectile`'s "integrate, sweep the dead" shape at all. It is
  one struct on `GameWorld`, not a list.
- **Falling boulders** are entities, not tiles — a tile cannot hold a sub-cell Y position. They read
  the tilemap for support and write to it as they fall. This is the one place where the
  terrain-vs-feature split Koa established (`Koa/Game/Level.cs:6-10`) had to be re-decided rather than
  copied.
- **The ghost mode** does not route through `FlowField` at all, so `Pathing.FlowDir` is only consulted
  in one of the two modes. `Pathing.Reachable` (`Koa/Game/Pathing.cs:83-87`) becomes a *trigger*
  rather than a diagnostic.

## Project layout — `Source/Eli/Eli/`

Root namespace `Eli`, game namespace `Eli.Game`. Produced by copying `Source/Koa/` and renaming
`Koa → Eli` throughout, then replacing `Game/`. Per-demo isolation is preserved exactly: own `.sln`,
`Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `global.json`. Nothing
build-related lands at the repo root.

| File | Eli-specific delta |
|---|---|
| `Eli.csproj` | Copy of `Koa.csproj`. Keep verbatim: `TargetFrameworks` (`Koa.csproj:3`), `SkiaSharpVersion` 4.151.0 (`:7`), the Common `<Compile>` glob (`:32-34`), the wasm `audio.js` `EmbeddedResource` (`:39-41`), the `HAS_NAUDIO`/NAudio block (`:46-51`), `UnoSplashScreen … Color="#050014"` (`:56`). Change `ApplicationTitle`/`ApplicationPublisher` to `Eli`, `ApplicationId` to `com.companyname.eli`. |
| `global.json` | Verbatim — `Uno.Sdk 6.7.0-dev.164` (`Koa/global.json:4`). |
| `Directory.Packages.props` | Verbatim — `NAudio 2.2.1`. |
| `GlobalUsings.cs` | Verbatim copy; the `Arcade.Common*` global usings are mandatory (`Koa/GlobalUsings.cs:6-8`). |
| `App.xaml(.cs)` | Namespace `Eli`; `AudioEngine.Init()` in `OnLaunched` (`Koa/App.xaml.cs:41`); desktop window 1280×800 (`:32-40`). |
| `MainPage.xaml` | **Koa/Pohaku stretched layout, not a Viewbox** — the `Camera2D` performs framing (`Koa/MainPage.xaml:7-18`). Carry the explanatory comment across, retargeted to the dirt field. |
| `MainPage.xaml.cs` | Render loop from `CompositionTarget.Rendering`, `dt` from a `Stopwatch` clamped to `[1/60, 1/30]`, both canvases invalidated every tick (`Koa/MainPage.xaml.cs:55-77`). Input differs — see [Loop / input / modes](#loop--input--modes). |
| `BackgroundSurface.cs` | `sealed : Arcade.Common.AmbientStarBackdrop`; override `BgTop`/`BgBottom` to **underground browns** `#0B0603` → `#241408` (Koa overrides to crypt violet at `Koa/BackgroundSurface.cs:13-14`). The drifting stars read as suspended dust in the cavern air. |
| `GameSurface.cs` | Verbatim shape (`Koa/GameSurface.cs:12-19`) — forwards `area` to `World.Resize` then to `Renderer.Render`. |
| `Platforms/*` | Verbatim copies, namespace `Eli`; `globalThis.eliAudio` in `audio.js`. |
| `Assets/Splash/splash_screen.svg` | Copy; `<UnoSplashScreen … Color="#050014" />` unchanged. |

### Game-specific files (`Game/`)

- **`Field.cs`** — the mutable terrain. Thin domain wrapper over the shared `TileGrid<Tile>`, exactly
  as `Koa/Game/TileMap.cs:22-109` wraps it. Named *Field* rather than *TileMap* because "the field"
  is the thing the player clears. Owns `enum Tile : byte { Sky, Dirt, Tunnel, Rock }`, `CellSize = 32f`,
  the **three** solidity predicates (below), `Carve(pos, radius)`, `StratumAt(row)`, and the
  `TerrainDirty` flag the flow field consumes.
- **`Level.cs`** — authored ASCII + loader over `AsciiMap.Parse` (`Common/Chassis/AsciiMap.cs:221`),
  mirroring `Koa/Game/Level.cs:40-86`. Terrain glyphs bake into `Field`; boulders, enemy spawns and
  the digger spawn come back as features. Ships **4 authored maps** with a difficulty-ramped cycle
  past that (Koa falls through to `BuildProcedural` instead — `Koa/Game/Level.cs:30-35`; a procedural
  dirt field is meaningless, so Eli re-serves authored maps with extra enemies).
- **`Pathing.cs`** — thin wrapper over the shared `FlowField`, essentially verbatim from
  `Koa/Game/Pathing.cs:10-87`, including the diagonal-blend `FlowDir` and the corner-suppression
  rule. Sources from the digger's cell; walkable = `Tunnel` only.
- **`Entities.cs`** — `abstract Entity { Pos, Vel, Radius, Alive }` non-wrapping base, verbatim from
  `Koa/Game/Entities.cs:49-55`. Types: `Digger`, `Enemy`, `Boulder`, `Particle`, plus the `Harpoon`
  struct and the `GameMode` / `EnemyKind` / `EnemyMode` / `BoulderState` / `Facing` enums.
- **`GameWorld.cs`** — the sim core. Holds `Field`, `Camera2D`, `Pathing`, `Digger`, `Harpoon`, and
  lists of enemies / boulders / particles with the `RemoveAll(!Alive)` sweep
  (`Koa/Game/GameWorld.cs:228-232`). 4-state `GameMode`. `HighScoreStore("Eli")`.
- **`Renderer.cs`** — `static Render(SKCanvas, GameWorld, float, float)`, stateless, following the
  five-step body in [04 – Rendering Pipeline](../Architecture/04-Rendering-Pipeline.md#what-every-rendererrender-does)
  as `Koa/Game/Renderer.cs:36-49` does: background → `Camera2D.Apply` → world draws → `Restore` → HUD
  in canvas-pixel coords.
- **`AudioEngine.cs`** — static facade + `AudioEngineImpl : AudioEngineBase`, structured exactly like
  `Koa/Game/AudioEngine.cs:12-129`, with `globalThis.eliAudio` and a voice-for-voice mirror in
  `Platforms/WebAssembly/WasmScripts/audio.js`.

## The field (tilemap + strata + digging)

**Representation.** `TileGrid<Tile>` with `CellSize = 32f`, **44 cols × 30 rows** = a 1408 × 960 world.
At `Camera.Zoom = 1.25` the viewport shows 1024 × 640 world units in a 1280 × 800 window — taller
*and* wider than the view on both axes, so the camera scrolls in X and Y.

```
enum Tile : byte {
    Sky,      // rows 0-1, the horizon band ABOVE the ground: SOLID to everything.
              // Scenery, not playable space — the top dirt row is the surface.
    Dirt,     // packed earth: passable BY THE DIGGER ONLY, at dig speed, and carved on contact
    Tunnel,   // carved: freely walkable by everything
    Rock,     // bedrock frame + floor: indestructible, blocks everything including boulders
}
```

**Strata.** Rows 2–29 divide into four 7-row bands. `Field.StratumAt(row)` returns 0–3 and drives both
the render hue and the depth score multiplier (Dig Dug scores by depth — this is the mechanic that
makes the strata gameplay rather than decoration).

| Stratum | Rows | Hue | Reads as |
|---|---|---|---|
| 0 | 2–8 | `#8A5A2B` | warm topsoil ochre |
| 1 | 9–15 | `#7A4A38` | red-brown clay |
| 2 | 16–22 | `#63482E` | deep loam |
| 3 | 23–29 | `#4A3A55` | slate-violet bedrock shale |

All four are dark and desaturated, deliberately clear of the rock-fall warning red `#FF3B22` (which is
used *only* on the wobble telegraph) and of the amber accent `#FFAA33`.

**Three solidity predicates.** Koa needed two — `IsBlocked` for bodies and `IsProjectileBlocked` for
shots that must fly onto a generator tile (`Koa/Game/TileMap.cs:51-108`). Eli needs three, because
"what is solid" genuinely differs three ways:

| Predicate | `Sky` | `Dirt` | `Tunnel` | `Rock` | OOB | Used by |
|---|:-:|:-:|:-:|:-:|:-:|---|
| `IsBlockedForDigger` | solid | **open** | open | solid | solid | the digger's `MoveCircle` — it tunnels *through* dirt |
| `IsBlockedForEnemy` | solid | solid | open | solid | solid | enemy `MoveCircle`; also `IsWalkable = !IsBlockedForEnemy` for the flow field |
| `IsBlockedForHarpoon` | solid | solid | open | solid | solid | harpoon tip advance (same as enemy today; kept separate so a later "harpoon bores dirt" tweak is one predicate, not a shared edit) |

**`Sky` is solid to all three.** It is the horizon band drawn above the field, not
playable space: the topmost dirt row is the surface and the digger is pinned to it. Leaving the sky
open let the digger climb to row 0 and run the full width of the map at walk speed without digging at
all, and — because `IsWalkable` is the inverse of `IsBlockedForEnemy` — let the flow field flood
across the top and join every tunnel to every other one, which both gutted the phasing trigger and
defeated Level 4. See [As built](#as-built).

**Digging.** The digger is never blocked by `Dirt`; it is *slowed* by it. Each frame:

1. Speed = `DigSpeed` if the cell the digger's leading edge is entering is `Dirt`, else `WalkSpeed`.
2. Move via `Field.MoveCircle(ref pos, radius, dx, dy)` → `TileGrid.MoveCircle` with
   `IsBlockedForDigger` (`Common/Chassis/TileGrid.cs:108-126`). The axis-separated sub-stepped
   resolver still applies, so bedrock and the world edge stop the digger cleanly.
3. `Field.Carve(pos, radius * CarveFraction)` flips every overlapped `Dirt` cell to `Tunnel` and
   returns `true` if anything changed.

Because movement is **4-directional** and Koa's corridor-centering assist (`Koa/Game/GameWorld.cs:292-317`)
is reused verbatim, motion is always exactly cardinal and always eased onto the cell centre line — so
`CarveFraction = 0.8` produces clean one-cell-wide corridors rather than a two-cell smear when the
digger travels near a cell boundary. This is the whole reason Eli drops Koa's 8-way input.

**Terrain edits and the flow field.** `Carve` sets `Field.TerrainDirty`. `StepSim` rebuilds on
**either** trigger:

```csharp
if (_frame % FlowRebuildEvery == 0 || Field.ConsumeTerrainDirty())
    Pathing.Rebuild(Digger.Pos);
```

Koa rebuilds on the frame cadence alone (`Koa/Game/GameWorld.cs:211-212`) because its terrain only
changes on a door or generator death — rare enough that a ≤5-frame stale field is invisible. In Eli
the terrain changes *most frames the player is moving*, and a stale field is immediately visible as
enemies walking into dirt. `ConsumeTerrainDirty` clears the flag as it reads it, so a frame with no
edit costs nothing. Worst case is one `O(cells)` flood per frame over 1320 cells — the same order as
Koa's 45 × 32 = 1440 and already proven on wasm.

**Culling** is Koa's windowed tile loop (`Koa/Game/Renderer.cs:64-72`) driven by
`Camera.VisibleWorldRect(CellSize)` — ~32 × 20 visible cells out of 1320.

## Follow-camera

Shared `Camera2D`, **`Clamp` on both axes, snap follow** (`FollowRate = 0`), configured exactly as
Koa does it (`Koa/Game/GameWorld.cs:75-80`) with `WorldSize` from `TileGrid.WorldWidth`/`WorldHeight`.
`Zoom = 1.25f` — the one departure from Koa's default `Zoom = 1f`, so 32px cells read at a chunky
40px on screen and the field genuinely scrolls on both axes.

`Camera2D.Apply(canvas)` pushes the single affine transform; **the caller restores**
(`Common/Chassis/Camera2D.cs:289-294`). No wrap anywhere — the field is bounded and `NormalizeCenter`
clamps the viewport inside `[0, WorldSize]` (`Common/Chassis/Camera2D.cs:151-157`).

## The harpoon

Not a projectile. One struct on `GameWorld` with a four-state machine:

```
Idle ──fire──▶ Extending ──hits dirt/rock or MaxLength──▶ Retracting ──len 0──▶ Idle
                    │                                          ▲
                 hits enemy                                    │
                    ▼                                          │
                 Attached ───────── burst / decay / move ──────┘
```

- **`Extending`** — `Origin` = digger centre at fire time, `Dir` = the digger's `Facing` (cardinal),
  `Length` grows at `HarpoonExtendSpeed`. Each frame the **tip** (`Origin + Dir * Length`) is tested:
  a `Dirt`/`Rock` cell under `IsBlockedForHarpoon` → `Retracting`; a circle hit against a
  non-phasing enemy → `Attached`. Reaching `HarpoonMaxLength` → `Retracting`.
- **`Attached`** — the victim is anchored (its `Vel` is zeroed and its AI skipped). Each subsequent
  fire press, rate-limited to `PumpInterval`, increments `enemy.Inflation`. At `PumpsToBurst` the
  enemy bursts: score, particle burst, `Alive = false`, harpoon → `Retracting`.
- **Deflation** — while `Attached`, `Inflation` decays at `InflateDecayPerSec` if you stop pumping;
  hitting 0 detaches and the enemy resumes. **Moving detaches** (arcade-faithful: you must stand still
  to pump). This is what stops the harpoon being a free kill.
- **`Retracting`** — `Length` shrinks at `HarpoonRetractSpeed`; the digger cannot fire again until
  `Idle`. `HarpoonRetractSpeed > HarpoonExtendSpeed` so a whiff punishes you briefly, not permanently.

A **phasing** enemy is immune to the harpoon (the tip passes through it) — the arcade rule, and the
reason ghost mode is an escape rather than a free target.

## Boulders (gravity applied to terrain)

Boulders are **entities**, not tiles, because a tile has no sub-cell Y. `Boulder : Entity` carries
`Col, Row` (its seed cell), a `BoulderState`, a `WobbleTimer`, and a cached `VectorShapes.Blob`
silhouette seeded from `Col*73 + Row*131` — the same deterministic-silhouette idiom Koa uses for
generator rings (`Koa/Game/Renderer.cs:176`).

```
Settled ──support lost──▶ Wobbling ──BoulderWobbleDelay──▶ Falling ──lands──▶ Shattering ──▶ dead
```

- **`Settled`** — each frame, test the cell directly beneath the boulder's centre. Support is lost when
  that cell is `Tunnel` and no other boulder occupies it. A settled (or wobbling) boulder is **as solid
  as bedrock**: `Field` keeps a one-cell occupancy overlay that all three solidity predicates consult,
  so `MoveCircle` stops the digger and enemies flush against its face, and — because `IsWalkable` is
  the inverse of `IsBlockedForEnemy` — the flow field routes the swarm *around* it rather than through
  it. Boulders stay entities; the overlay is only how a cell-aligned entity makes itself felt by the
  tile-based predicates.
- **`Wobbling`** — `BoulderWobbleDelay` of telegraph: the renderer shakes the silhouette ±6° at ~18 Hz
  and outlines it in the warning red `#FF3B22`. This is the player's window to get out from under it.
- **`Falling`** — the boulder releases its occupancy cell the instant it tears free (from here it
  crushes rather than blocks), and the cell it vacates becomes `Tunnel` — the void it pulled out of the
  earth is a usable passage. `Vel.Y += BoulderGravity * dt`, capped at `BoulderMaxFallSpeed`. A falling
  boulder **carves the dirt it passes through** (same `Field.Carve`, so `TerrainDirty` fires and the flow field
  follows), and kills any enemy or the digger whose circle it overlaps. Enemies crushed in one fall
  chain for bonus score.
- **`Shattering`** — on reaching a `Dirt`/`Rock` cell below, hold `BoulderShatterTime` emitting debris
  particles, then `Alive = false`. A spent boulder is gone; there is no respawn.

`Level` asserts at load that every authored boulder rests on `Dirt` or `Rock`, so no boulder falls on
frame 1. (The Level-1 map below satisfies this.)

## Enemies: the two-mode AI

Two kinds, both Hawaiian-glossed in the repo's tradition (cf. Hahai's Mo'o):

| Kind | Gloss | Homage | Ghosts? | Behaviour |
|---|---|---|:-:|---|
| `Uhane` | *spirit* | Pooka | **yes** | Fast tunnel patroller. When it can't reach you through the tunnels — or on a jittered timer — it flattens, phases through the dirt on a straight line toward you, and rematerialises in open ground. |
| `Nohu` | *stonefish* | Fygar | no | Slower, tougher (one extra pump to burst), scores double. Stays in the tunnel network at all times. |

**Mode `Tunnel`** — `Pathing.FlowDir(e.Pos)` → normalise → `Field.MoveCircle` with `IsBlockedForEnemy`,
through Koa's `MoveWithCorridorAssist` so enemies thread one-cell corridors
(`Koa/Game/GameWorld.cs:292-317, 484-489`). When `FlowDir` returns zero the enemy wanders on its
`Wobble` phase, exactly as `Koa/Game/GameWorld.cs:443-447`.

**Mode `Phasing`** (`Uhane` only) — entered when **either**:
1. `!Pathing.Reachable(e.Pos)` — the flow field never reached this enemy, so there is no tunnel route
   to the digger at all; or
2. the enemy's `GhostCheckTimer` (reset to `GhostCheckInterval ± GhostCheckJitter`) expires **and**
   `|digger - e.Pos| > GhostTriggerDistance`.

While phasing: `Pos += toDigger.Normalized() * GhostSpeed * dt`, **ignoring terrain entirely** — no
`MoveCircle`, no flow field. The enemy exits phasing when it has been phasing at least
`GhostMinDuration` **and** its cell is `Tunnel`. Exiting snaps it to that cell's centre so it
re-enters the tunnel network cleanly aligned.

A phasing enemy **cannot be harpooned** but **is still lethal on contact** — the arcade rule. That is
fair because `GhostSpeed` is deliberately less than a third of `WalkSpeed`: you can always outrun a
ghost, and it must rematerialise the moment it reaches open ground.

**Crowding.** Koa's `ResolveCrowding` hard-separation pass (`Koa/Game/GameWorld.cs:497-543`) is
reused for tunnel-mode enemies only. Phasing enemies are excluded — they are not in the world's
collision space. With `LiveEnemyCap = 8` the `O(n²)` pass is trivially cheap (Koa runs it at a cap of
120).

## Progression, scoring, lives

- **Level clear** — every enemy dead. `Score += LevelClearBonus * Level`, `PlayLevelClear()`, then
  `LoadLevel(Level + 1)` with a fresh field. Contrast Koa, where clearing is stepping on an exit tile
  (`Koa/Game/GameWorld.cs:681-690`).
- **Levels** — 4 authored maps. Level `N > 4` re-serves map `((N-1) % 4)` with
  `extraEnemies = min(4, N - 4)` seeded into free `Tunnel` cells and all enemy speeds scaled by
  `1 + 0.06 * (N - 4)`, capped at 1.5×.
- **Death** — contact with any un-inflated enemy, or being under a falling boulder. `Lives--`; emit the
  explosion (`Koa/Game/GameWorld.cs:727-742`); after `RespawnDelay` the digger returns to its spawn
  and **the field keeps its tunnels** (arcade-faithful — you don't lose your excavation), while enemies
  reset to their spawn cells. `Lives == 0` → save the high score and `Mode = GameOver`, or
  `Mode = Title` if we were in Attract (Koa's transition, `Koa/Game/GameWorld.cs:265`).
- **Scoring by depth** — the strata pay off here:

| Event | Stratum 0 | 1 | 2 | 3 |
|---|--:|--:|--:|--:|
| Burst an `Uhane` | 200 | 300 | 400 | 500 |
| Burst a `Nohu` | 400 | 600 | 800 | 1000 |

| Boulder crush (per fall) | 1 enemy | 2 | 3 | 4 | 5+ |
|---|--:|--:|--:|--:|--:|
| Score | 1000 | 2500 | 4000 | 6000 | 8000 |

Level-clear bonus `500 × Level`. `HighScoreStore("Eli")` saved on game over; desktop-only persistence
is expected (`Common/HighScoreStore.cs`).

## Loop / input / modes

- **Modes** — the mandated 4-state `GameMode { Title, Playing, GameOver, Attract }`
  ([02 § the canonical game-state machine](../Architecture/02-Demo-Anatomy.md#the-canonical-game-state-machine)).
  Title idles to Attract after **12 s** (`Koa/Game/GameWorld.cs:175-176`).
- **Loop** — `CompositionTarget.Rendering`, never a `DispatcherTimer`; `dt` from a `Stopwatch` clamped
  to `[1/60, 1/30]`; **both** canvases invalidated every tick (`Koa/MainPage.xaml.cs:55-77`).
- **Input** — latched cardinal flags as in Koa (`Koa/MainPage.xaml.cs:80-111`), but composed
  **4-directionally**: the most-recently-pressed axis wins, so holding Right+Down yields one cardinal,
  not a diagonal. Arrows + WASD both bound. Space = fire/pump (held; each `PumpInterval` while
  `Attached` counts as one pump) and edge-triggered start. Enter also starts. Click starts / leaves
  Attract.
- **Attract autopilot** — modelled on Koa's `RunAutoHero` (`Koa/Game/GameWorld.cs:772-838`): commit to
  a cardinal heading, re-pick on timer expiry / stuck / blocked, with a momentum bias and tie-break
  jitter. Two Eli-specific changes: `BlockedAhead` tests `IsBlockedForDigger` (so `Dirt` is *not*
  blocking — the bot digs), and the heading score biases toward the nearest **enemy** rather than the
  nearest pickup. The bot fires when an enemy is within `AutoFireRange` along its facing axis, and
  spams the pump while `Attached`. It holds its ground rather than closing to contact, but only for a
  shot it can actually land — the target must be inside `HarpoonMaxLength` **with a clear line of
  sight** (the harpoon stops at dirt) and no phasing monster closing, since ghosts are not valid
  targets yet still kill on contact. See [As built](#as-built).

## Audio

Static `AudioEngine` facade + `AudioEngineImpl : AudioEngineBase`, NAudio `ISampleProvider` voices
under `#if HAS_NAUDIO`, mirrored voice-for-voice in
`Platforms/WebAssembly/WasmScripts/audio.js` under `globalThis.eliAudio`, with the `audio.js`
`<EmbeddedResource>` entry in the csproj. `AudioEngine.Init()` from `App.OnLaunched`.

| Voice | Shape | Throttle |
|---|---|---|
| `PlayDig` | short filtered-noise scrape | 0.09 s (as `Koa/Game/AudioEngine.cs:26-31`) |
| `PlayHarpoonFire` | rising square blip 300→900 Hz | — |
| `PlayHarpoonStick` | short click | — |
| `PlayPump` | rising triangle, pitch steps with `Inflation` | `PumpInterval` |
| `PlayBurst` | noise burst + descending saw | — |
| `PlayRockWobble` | low tremolo warning | once per wobble |
| `PlayRockFall` | descending filtered noise | — |
| `PlayRockShatter` | short bright noise burst | — |
| `PlayDeath` | descending wobble (`Koa/Game/AudioEngine.cs:222-248`) | — |
| `PlayLevelClear` | ascending arpeggio | — |

## Rendering notes

Five-step body per [04](../Architecture/04-Rendering-Pipeline.md#what-every-rendererrender-does).
Everything that glows goes through `NeonDraw` / `HudText` / `NeonPaints`; any helper that mutates a
shared paint's `StrokeWidth` restores the default before returning
(`Common/Chassis/NeonDraw.cs:239-241`).

One deliberate divergence from Koa's tile pass. Koa draws each **wall** cell as a blurred circle plus a
sharp round-rect (`Koa/Game/Renderer.cs:77-98`) — cheap, because walls are the *minority* of its grid.
In Eli, dirt is the overwhelming majority: ~640 visible cells would mean ~640 blurred fills per frame,
which is the wrong shape for the wasm frame budget. Instead:

- **Dirt** is run-length-encoded per visible row into flat `SKRect` fills in the stratum hue — a few
  rects per row, no blur. Dirt is matte earth; it is not a glowing element, so the halo+sharp rule does
  not apply to it.
- **Tunnel walls glow.** For each visible `Tunnel` cell, the faces whose neighbour is `Dirt` are stroked
  with `NeonDraw.Line` in a brightened stratum hue. That is where the neon signature lives — glowing
  excavated outlines against matte dirt — and it is bounded by the tunnel perimeter, not the field area.
- **Everything else** — digger, enemies, harpoon, boulders, particles, HUD — is halo+sharp as usual.

The digger is drawn with the pivot-at-body-centre rotation Koa established for its hero
(`Koa/Game/Renderer.cs:226-247`), so changing facing turns the sprite in place rather than orbiting the
snout around the body.

**HUD** (canvas-pixel coords, after `Restore`): `SCORE` / `HI` centred, `LEVEL` right, `LIVES` as
digger icons left, and — while the harpoon is `Attached` — a **pump gauge** via `HudText.Bar` driven by
`Inflation / PumpsToBurst` (`Common/Chassis/HudText.cs:175`). Title screen uses
`Marquee.DrawRainbowTitle(c, "ELI", …)`; `E`, `L` and `I` are all present in `GlyphFont`
(`Common/Chassis/GlyphFont.cs:57,61,63`). `Marquee.Draw` runs the scrolling strip in every mode except
`Playing`, as `Koa/Game/Renderer.cs:294-295`.

## Level authoring

Legend (extends Koa's, `Koa/Game/Level.cs:19-22`):

```
' ' sky      ':' dirt      '.' pre-carved tunnel    '#' bedrock
'O' boulder  'U' Uhane spawn   'N' Nohu spawn       '@' digger spawn
```

`AsciiMap.Parse` enforces rectangularity and throws on a ragged map
(`Common/Chassis/AsciiMap.cs:230-235`), so a mis-typed row fails at load rather than rendering wrong.
Feature glyphs write their underlying terrain *and* register the feature: `O`/`U`/`N`/`@` all sit on
`Tunnel` except `O`, which sits in `Dirt` (a boulder is embedded in the earth).

| Map | Shape | `Uhane` | `Nohu` | Boulders |
|---|---|--:|--:|--:|
| 1 — *Kahua* ("foundation") | One starter shaft, a shallow gallery per stratum joined by two vertical links. Gentle. | 3 | 1 | 3 |
| 2 — *Lua* ("pit") | Two deep vertical shafts, no horizontal galleries — you must dig every lateral route yourself. | 3 | 2 | 4 |
| 3 — *Pūnāwai* ("spring") | A dense pre-carved warren in strata 0–1 over untouched deep rock, so early enemies swarm and late ones phase. | 4 | 2 | 5 |
| 4 — *Papakū* ("bedrock") | Bedrock pillars split the field into four quadrants connected only at the surface; a boulder guards each quadrant mouth. | 4 | 3 | 5 |

Level 1, verified 44 × 30 with every boulder resting on dirt:

```
#                                          #
#                                          #
#:::::@::::::::::::::::::::::::::::::::::::#
#:::::.::::::::::::::::::::::::::::::::::::#
#:::::.:::::::::::O::::::::::::::::::::::::#
#:::::.:::::.U..:::::::::::::::::::::::::::#
#:::::...........::::::::::::::::::::::::::#
#:::::::::::::::.::::::::::::::::::::::::::#
#:::::::::::::::.::::::::::::::::::::::::::#
#:::::::::::::::.::::::::::::::::::::::::::#
#:::::::::::::::.::::::::::::::::::::::::::#
#:::::::::::::::.:::::::::::::::::O::::::::#
#:::::::.:::::::...........U....:::::::::::#
#:::::::.:::::::::::::::::::::.::::::::::::#
#:::::::.:::::::::::::::::::::.::::::::::::#
#:::::::.:::::::::::::::::::::.::::::::::::#
#:::::::.:::::::::::::::::::::.::::::::::::#
#:::::::.:::::::::::::::::::::.::::::::::::#
#:::::::.:::::::::::O:::::::::.::::::::::::#
#:::::::..N...::::::::::::::::.::::::::::::#
#:::::::::::::::::::::::::::::.::::::::::::#
#:::::::::::::::::::::::::::::.::::::::::::#
#:::::::::::::::::::::::::::::.::::::::::::#
#:::::::::::::::::::::::::::::.::::::::::::#
#:::::::::::::::::::::::::::::.::::::::::::#
#:::::::::::::::::::::::::::::....U...:::::#
#::::::::::::::::::::::::::::::::::::::::::#
#::::::::::::::::::::::::::::::::::::::::::#
#::::::::::::::::::::::::::::::::::::::::::#
############################################
```

## Tunables — starting values

Every number below is a named `const` on `GameWorld` (or `Field`), grouped as
`Koa/Game/GameWorld.cs:14-20` does. World units are pixels; `CellSize = 32`.

| Group | Name | Value | Note |
|---|---|--:|---|
| **Field** | `CellSize` | `32f` | |
| | `Cols` × `Rows` | `44 × 30` | 1408 × 960 world |
| | `SkyRows` | `2` | |
| | `StrataRows` | `7` | 4 bands × 7 = 28 |
| **Camera** | `Zoom` | `1.25f` | Clamp/Clamp, `FollowRate = 0` (snap) |
| **Digger** | `WalkSpeed` | `132f` px/s | ≈ 4.1 cells/s in a carved tunnel (measured: exactly 132) |
| | `DigSpeed` | `64f` px/s | the constant; **effective ≈ 74 px/s (2.3 cells/s)** because the body carves the cell ahead just before the centre crosses into it — the "break through" tail. Walk:dig ≈ 1.8 |
| | `DiggerRadius` | `0.34 × CellSize` | = 10.9 px |
| | `CarveFraction` | `0.8f` | of radius; keeps corridors 1 cell wide |
| | `Lives` | `3` | |
| | `RespawnDelay` | `1.4f` s | |
| **Harpoon** | `HarpoonExtendSpeed` | `420f` px/s | |
| | `HarpoonRetractSpeed` | `700f` px/s | faster than extend — a whiff costs ~0.4 s total |
| | `HarpoonMaxLength` | `104f` px | 3.25 cells |
| | `PumpsToBurst` | `4` | `Uhane`; `Nohu` needs `5` |
| | `PumpInterval` | `0.18f` s | min seconds between pumps |
| | `InflateDecayPerSec` | `0.55f` | pumps/s lost when not pumping |
| | `BurstHoldTime` | `0.25f` s | pop animation before removal |
| **Boulder** | `BoulderWobbleDelay` | `0.9f` s | the escape window |
| | `BoulderGravity` | `900f` px/s² | |
| | `BoulderMaxFallSpeed` | `520f` px/s | |
| | `BoulderShatterTime` | `0.35f` s | |
| | `BoulderRadius` | `0.46 × CellSize` | = 14.7 px |
| **Enemy** | `UhaneSpeed` | `96f` px/s | slower than the digger walking, faster than digging |
| | `NohuSpeed` | `78f` px/s | |
| | `EnemyRadius` | `0.34 × CellSize` | |
| | `LiveEnemyCap` | `8` | no generators; the cap is a safety rail |
| **Ghost** | `GhostTriggerDistance` | `240f` px | 7.5 cells |
| | `GhostCheckInterval` | `3.5f` s | ± `GhostCheckJitter` |
| | `GhostCheckJitter` | `1.5f` s | so the pair don't phase in lockstep |
| | `GhostSpeed` | `46f` px/s | **< ⅓ `WalkSpeed`** — a ghost is always outrunnable |
| | `GhostMinDuration` | `0.6f` s | stops instant re-materialise on entry |
| **Sim** | `FlowRebuildEvery` | `5` frames | *plus* every terrain edit |
| | `TitleIdleToAttract` | `12f` s | mandated |
| | `AutoFireRange` | `200f` px | attract bot |
| **Score** | `LevelClearBonus` | `500 × Level` | |

Per-level enemy counts (authored; see the map table above):

| Level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8+ |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| `Uhane` | 3 | 3 | 4 | 4 | 4 | 4 | 5 | 5 |
| `Nohu` | 1 | 2 | 2 | 3 | 4 | 5 | 5 | 6 |
| **Total** | **4** | **5** | **6** | **7** | **8** | **9** | **10** | **11** |
| Speed scale | 1.00 | 1.00 | 1.00 | 1.00 | 1.06 | 1.12 | 1.18 | 1.24 → cap 1.50 |

(Levels 5+ re-serve authored maps 1–4 in cycle with `extraEnemies` seeded into free tunnel cells. The
`LiveEnemyCap` of 8 applies to *concurrent* enemies; at level 8+ the surplus spawns as earlier ones
die.)

## Chassis impact

**No new chassis pieces are required.** Every piece Eli needs already exists, and the four
architectural deltas all land in `Source/Eli/Game/` where they belong:

| Eli need | Served by | Note |
|---|---|---|
| Mutable terrain | `TileGrid<T>` indexer setter (`Common/Chassis/TileGrid.cs:61-65`) | The grid was already mutable; nothing in it assumes a static map. |
| Re-flood on edit | `FlowField.Rebuild` (`Common/Chassis/FlowField.cs:67`) | Takes `isWalkable` as a delegate evaluated *at flood time*, so it reads current terrain for free. The scratch queue is reused, so a per-frame rebuild allocates nothing. |
| Wall-slide vs bedrock | `TileGrid.MoveCircle` (`:108`) | Sub-stepped and axis-separated; works unchanged with a per-caller predicate. |
| Bounded scrolling view | `Camera2D` Clamp/Clamp + `Zoom` (`:151-157, 289-294`) | |
| Level authoring | `AsciiMap.Parse` (`:221`) | |
| Boulder / enemy silhouettes | `VectorShapes.Blob` + `DrawAt` (`:297, 326`) | |
| Pump gauge | `HudText.Bar` (`:175`) | Third consumer after Koa's health bar — the piece earns its place in the chassis. |

Two candidates were considered and **rejected**:

- **A terrain-carving helper on `TileGrid<T>`.** Only Eli digs; a `Carve` on the shared grid would be a
  one-consumer method with a game-specific flip rule (`Dirt → Tunnel`) baked in. Stays in `Field`.
- **A falling-entity / gravity base.** Only Eli has falling terrain, and `Boulder`'s state machine is
  entirely about *support against a tilemap*, not about gravity in general. Mahina already has its own
  lander physics and would not consume it. Stays in `Entities.cs`.

One genuine chassis candidate is **proposed but deliberately left unbuilt**, per the prompt's
instruction that P2 items ship as proposals only:

| Piece | Priority | Consumers | Gate |
|---|---|---|---|
| `TileGrid<T>.ForEachOverlappedCell(Vec2 pos, float radius, Action<int,int> cb)` — enumerate the cells a body circle touches | **P2 — not built** | Eli (`Field.Carve`, boulder-vs-tile support), Kanapi (mushroom-vs-shot cell lookup), Koa (`ResolveCrowding` bucket assignment) | Build it only when a **second** demo actually needs it. Eli's version is ~8 lines inside `Field`; hoisting it now would be speculative generalisation, which is exactly what [08 § What was deliberately not built](../Architecture/08-Chassis-Extensions.md#what-was-deliberately-not-built) argues against for `Pool<T>` and `Entity2D`. |

## Out of scope

Explicitly **not** in this build:

- **No test project.** There is none in the repo ([08](../Architecture/08-Chassis-Extensions.md#camera-unit-tests--planned-never-written)); none is added and nothing is planned around one.
- **No `Pool<T>`, no `Entity2D` base.** Both are settled "no" in 08. Eli's entity counts (≤ 8 enemies,
  ≤ 5 boulders) are an order of magnitude below Koa's 120-enemy cap, so the GC-stutter argument is
  weaker here than in the demo where it was already declined.
- **No SkiaSharp version unification.** Eli pins 4.151.0. KahuaNetwork (SkiaSharp 3) and UnoGallery's
  `$(SkiaSharpVersion)` switch are untouched.
- **No mobile TFMs** — `net10.0-browserwasm;net10.0-desktop` only. **No multiplayer. No save state**
  beyond the high score.
- **No `Source/Common/` refactor** except through the P2 proposal above, which is left unbuilt.
- **No launcher card.** `Source/Launcher/Launcher/Game/GameCatalog.cs` stays at its original eight
  entries; Eli ships standalone like Paku, Kia'i and Koa
  ([08](../Architecture/08-Chassis-Extensions.md#what-was-deliberately-not-built)). Note that
  [02 § Adding a new arcade-family demo](../Architecture/02-Demo-Anatomy.md#adding-a-new-arcade-family-demo)
  step 6 and the root README's step 4 both predate that reversal.
- **Fygar's flame breath is cut.** In the arcade the second enemy breathes fire down the corridor. The
  Phase-1 brief specifies only contact death and harpoon kills, and a flame is a third damage source
  with its own telegraph, hitbox and AI state. `Nohu` is differentiated by toughness and score instead.
  Recorded here so the omission reads as a decision, not an oversight.
- **The "last enemy flees to the surface" ending is cut.** It would require a third AI mode, which
  contradicts the brief's explicit "two-mode, not one".

## As built

The implementation follows this document. The refinements below were made during the build, recorded
here so the doc and the code don't drift; [DEFECTS.md](DEFECTS.md) carries the same list as a defect
history, with how each one was found:

- **A harpooned enemy is harmless for as long as it is on the hook.** The design says an un-inflated
  enemy kills on contact. Taken literally that left a hole: on the frame the harpoon strikes, and
  again whenever `Inflation` decayed back to exactly zero just before detaching, a monster you had
  *already speared* still killed you at point-blank range. `GameWorld.HandleEnemyContact` therefore
  skips any enemy with `Pinned == true`, not `Pinned && Inflation > 0`. Detaching clears `Pinned`, so
  it turns lethal again the instant it works loose. A phasing enemy is still lethal, as designed.
- **The attract bot keeps its distance.** The design specified only "fires when an enemy is within
  `AutoFireRange` along its facing axis"; with nothing else, the bot charged the nearest monster and
  spent all three lives within twenty seconds, which made a poor demo loop. It now holds its ground
  for a shot it can actually land: the target must be inside `HarpoonMaxLength`, with a clear line of
  sight, and with no phasing monster closing. (The first cut of this stopped for anything merely in
  the right *direction* and had no ghost check — which introduced a fresh stall; see
  [DEFECTS.md](DEFECTS.md) #8.) Across five 20 s sessions the bot now scores in 5/5 and survives 5/5,
  against 3 deaths / 0 points before.

- **Settled boulders are solid.** They were entities with collision only in the *falling* branch, so
  the digger and the swarm walked straight through a resting one (measured overlap: 25.6 px, i.e.
  centre on centre). `Field` now carries a boulder-occupancy overlay folded into all three predicates.
- **`Sky` is solid, not walkable.** As first written the digger could walk up into the sky band and
  then cross the whole field at walk speed without digging — and since `IsWalkable` is the inverse of
  the enemy predicate, the flow field flooded the sky too, joining every tunnel to every other one.
  That left enemies almost never stranded (so `Uhane` rarely phased) and defeated Level 4's
  quadrant lock. All three predicates now treat `Sky` as solid; the top dirt row is the surface and
  the digger is pinned to it. The renderer's `IsSolid` follows, so a dug-open surface gets the same
  glowing edge as any other tunnel wall.
- **The dig-speed test is cell-based, not a pixel probe.** The original probe looked `Radius + 2`
  (12.9 px) ahead of the body — less than the 16 px half-cell, so it only ever re-read the cell the
  digger had *already carved*. The penalty therefore almost never applied and tunnelling ran at
  ~115 px/s, barely below the 132 px/s walk. `MoveDigger` now asks "is the next cell along my facing
  still `Dirt`?", which brings it to a measured 74 px/s.

Everything else — the harpoon state machine, boulder gravity, the two-mode AI, the rest of the
tunable table, the four authored fields — shipped as written.

## Build integration

Wire the new demo into exactly these, and nothing else:

- **`Builds/Build-Eli.ps1`** — copy of `Builds/Build-Koa.ps1` with `Koa → Eli` (project path
  `Source\Eli\Eli\Eli.csproj`).
- **`Builds/Run-Eli.ps1`** — copy of `Builds/Run-Koa.ps1`, same substitution.
- **`Builds/Build-All.ps1`** — append `'Build-Eli.ps1',` to `$scripts` (after `'Build-Koa.ps1',`,
  `Build-All.ps1:22`).
- **`Builds/Publish-Site.ps1`** — append `@{ Name = 'Eli';      Slug = 'eli'      }` to `$games`
  (`Publish-Site.ps1:41`).
- **`README.md`** — one row in the demo table after the Koa row (`README.md:21`) and one
  `│   ├── Eli/` line in the Layout tree after `Koa/` (`README.md:50`).
- **`Docs/Eli/DESIGN.md`** — this document, with the `> **Status: built.**` banner added once the game
  runs.

**Do NOT** add an entry to `Source/Launcher/Launcher/Game/GameCatalog.cs`.

## Acceptance

The build is done when all three of these pass and the five behaviours below are observed:

```powershell
.\Builds\Build-Eli.ps1 -Configuration Release
.\Builds\Build-Eli.ps1 -Configuration Release -Wasm
.\Builds\Build-All.ps1 -Configuration Release      # nothing else regressed
```

Then `.\Builds\Run-Eli.ps1` and confirm: the title screen draws; attract mode engages after 12 s idle
and the bot digs and harpoons; a game plays through at least one level clear; game over returns to
play again; the high score persists across runs.
