# Kia'i — Design (Defender homage)

> **Status: built.** This was the implementation plan; the game now lives at `Source/Kiai/`.
> The shared chassis pieces this game drove (`Camera2D`, `VectorShapes`, `Radar`, `SeamlessTerrain`)
> are documented in [03 – Shared Chassis](../Architecture/03-Shared-Chassis.md); the design rationale
> behind them is in [08 – Chassis Extensions](../Architecture/08-Chassis-Extensions.md).

## Elevator pitch

**KIA'I** (Hawaiian *"guardian / to watch over"*) is a **Defender** homage: a horizontally-scrolling
shooter over a planet you patrol. You fly left/right with thrust + inertia across a world several
screens wide that **wraps seamlessly** (toroidal in X), gun down Lander aliens before they abduct the
humanoids dotted along the terrain, and catch humanoids that fall when you shoot a Lander mid-carry. A
ship-centred **scanner/radar strip** at the top shows the whole wrapped world at a glance.

The architecturally novel bit — and the reason this game exists — is the **scrolling, wrapping camera**
and the **radar projection**. No existing demo scrolls; they all scale one fixed world to fit the canvas.

## What's different from the existing template

- **No Viewbox scale-to-fit.** Uses the Pohaku stretched-`GameSurface` layout (`Pohaku/MainPage.xaml:8-11`),
  and the Renderer applies a **camera translate** (via the shared `Camera2D`) instead of
  `Translate+Scale` to fit (`Pohaku/Renderer.cs:240-248`). World units are pixels; the world scrolls
  within the viewport.
- **Toroidal X world.** All positions live in `[0, WorldWidth)`; the `Entity` base wraps **X only**
  (Y is bounded by terrain below and a ceiling above), unlike Pohaku's wrap-both (`Entities.cs:20-26`).
- Everything else (game loop, neon chassis, audio plumbing, build/catalog wiring) mirrors Pohaku/HokuLele.

## Project layout — `Source/Kiai/Kiai/`

Fastest correct path: copy an existing demo wholesale and rename `Kiai` throughout, then replace `Game/`
(see `02-Demo-Anatomy.md:171`). Chassis/wiring files are near-mechanical renames:

| File | KIA'I-specific delta |
|---|---|
| `Kiai.csproj` | Copy of `Pohaku.csproj`. `ApplicationTitle=KIA'I`, `ApplicationId=com.companyname.kiai`. Keep verbatim: Common `<Compile>` glob (`:32-34`), wasm `audio.js` `EmbeddedResource` (`:39-41`), `HAS_NAUDIO`/NAudio block (`:46-51`), `UnoSplashScreen Color="#050014"` (`:56`), SkiaSharp pin. |
| `App.xaml(.cs)` | Copy of Pohaku; namespace → `Kiai`; keep `AudioEngine.Init()` in `OnLaunched`. |
| `MainPage.xaml` | **Pohaku stretched layout** (Background + stretched GameSurface, no Viewbox). |
| `MainPage.xaml.cs` | Copy of Pohaku loop (`:56-87`). Latched flags: `_thrustLeft/Right/Up/Down`, `_fire`, edge flags `_firePressedThisFrame`, `_smartBombPressedThisFrame` (`B`), `_hyperPressedThisFrame` (`H`). Keep dt clamp + Rendering wiring; invalidate both canvases. |
| `BackgroundSurface.cs` | `sealed : Arcade.Common.AmbientStarBackdrop`; optionally override `BgTop/BgBottom` for a night-flight mood. |
| `GameSurface.cs` | Copy of `Pohaku/GameSurface.cs`. `Resize` sets viewport size + `WorldHeight = canvasH`, leaves `WorldWidth`/terrain alone. |
| `GlobalUsings.cs`, `Platforms/Desktop|WebAssembly/Program.cs` | Verbatim copies, namespace → `Kiai`; `globalThis.pohakuAudio` → `globalThis.kiaiAudio` in `audio.js`. |

### Game-specific files (`Game/`)

- **`Camera` usage** — uses the shared `Camera2D` (chassis) configured `X = Wrap(WorldWidth)`,
  `Y = Free`, `LookAhead = ViewW*0.25`, `FollowRate ≈ 3.5`. No game-local camera class needed once
  the chassis piece exists.
- **`Terrain.cs`** — thin wrapper over the shared `SeamlessTerrain` (chassis) holding the generated
  height field + humanoid spawn-point selection (flat-ground cells). Exposes `HeightAt(worldX)`.
- **`Radar` usage** — uses the shared `Radar` (chassis) in `WrapX = true` mode (ship-centred).
- **`Entities.cs`** — `abstract Entity` base (mirrors `Pohaku/Entities.cs:6-27`) with **X-only** `Wrap`.
  Types: `Ship`, `Bullet`, `Humanoid`, `Lander`, `Mutant`, `Baiter`, `Bomber`, `Pod`, `Swarmer`,
  `Particle`; enums `GameMode`, `LanderState`, `HumanoidState`.
- **`GameWorld.cs`** — per-frame brain + 4-state machine; owns `Camera2D`, `Terrain`, `Ship`, entity
  lists, wave/spawn timers, rescue bookkeeping (`HumanoidsRemaining`, `Wave`), smart-bomb, hyperspace,
  scoring (extra life / 10k like `Pohaku/GameWorld.cs:406-414`), `UpdateAudioState`. `HighScoreStore("Kiai")`.
- **`Renderer.cs`** — `NeonBackground` → **radar strip** → `Camera2D.Apply` → terrain + entities with
  toroidal seam replicas (`Camera2D.ForEachVisibleX`) → restore → HUD → (Title/Attract) `Marquee` +
  `DrawRainbowTitle("KIA'I")`. Neon-only (no retro/vibrant toggle). Entity shapes via shared `VectorShapes`.
- **`AudioEngine.cs`** — facade + `AudioEngineImpl : AudioEngineBase` (shape of `Pohaku/Game/AudioEngine.cs`).
  Voices: `PlayShoot`, `PlayExplosion`, `PlayHyperspace`, `PlaySmartBomb`, `PlayHumanoidRescued`
  (rising chime), `PlayHumanoidLost`, `PlayMutate`, looping `StartThrust/StopThrust`. Desktop NAudio
  procedural voices mirrored by `globalThis.kiaiAudio` Web-Audio in `audio.js`.

## Scrolling-camera + wraparound (core mechanic)

- **Dimensions:** `WorldHeight = canvasH` (one screen tall); `WorldWidth = ReferenceViewW * 4`
  (~4 screens), a constant fixed at `StartGame` so terrain is stable across resizes. `Resize` changes
  only the *view*, never `WorldWidth`/terrain.
- **Toroidal X:** positions in `[0,WorldWidth)`; movement wraps X only, Y clamps to `[ceiling, HeightAt(x)-clearance]`.
- **Shortest signed distance** `Camera2D.WrapDelta(a,b,WorldWidth)` is the workhorse for camera follow,
  AI targeting, and collision.
- **Follow with look-ahead:** target `= ship.X + LookAhead*facingSign`, eased on the torus with
  frame-rate-independent exp-lerp so it never scrolls "the long way" across the seam.
- **World→screen (per entity, because of replicas):** `screenX = WrapDelta(worldX, CenterX, WorldWidth) + ViewW/2`,
  `screenY = worldY`. No global canvas scale.
- **Seam rendering:** `Camera2D.ForEachVisibleX(worldX, pad, drawAtScreenX)` yields the on-screen
  replica(s) so sprites straddling the seam draw on both sides; terrain walks screen-x → world-x and
  samples `HeightAt` (wrapped + periodic), so the silhouette is continuous with zero special-casing.
- **Seamless terrain:** `SeamlessTerrain` sums sinusoids whose periods are **integer divisors of
  WorldWidth** (e.g. harmonics 3,7,13,23), so `height(0)==height(WorldWidth)` *and* the slope matches —
  the seam is mathematically invisible.

## Radar / scanner

A full-width strip (~40 px) at the top showing the **entire** world compressed into `canvasW`,
**centred on the ship**. Uses shared `Radar` with `WrapX=true`:
- `radarX(worldX) = canvasW/2 + WrapDelta(worldX, ship.X, WorldWidth) * (canvasW / WorldWidth)`
- `radarY(worldY) = radarTop + (worldY / WorldHeight) * radarH`
- Renders a faint terrain silhouette polyline, then blips via `NeonDraw.CircleFill`: humanoids small
  white, landers green, mutants magenta, baiters yellow, bombers orange, ship a bright cyan caret at
  centre. Carried humanoids rise in the captor's column (telegraphs abduction). Drawn in canvas space
  after the camera transform is restored.

## Entities + AI

- **Collision:** circle-circle distance (`Pohaku/GameWorld.cs:468-472`) but X distance uses `WrapDelta`
  so shots near the seam hit across it.
- **Ship:** directional thrust + inertia + drag; `FacingSign` flips on reverse; fast forward bullets;
  Y clamped to terrain/ceiling; smart-bombs + hyperspace.
- **Humanoid** `enum HumanoidState { Standing, Seized, Falling, Caught, Dead }`: standing on terrain →
  Seized (tracks captor) → Falling (shot mid-carry; high fall = splat/Dead, low = survives; ship touch =
  Caught) → Caught (rides ship; deposited on landing → Standing + score + chime) → Dead (decrement count).
- **Lander** `enum LanderState { Descending, Hunting, Lifting, Cruising }`: hunts nearest Standing
  humanoid (toroidal), grabs + climbs; reaching the ceiling consumes the humanoid and **mutates** into a
  Mutant; shot while Lifting drops the humanoid to Falling. Aimed shots at the ship reuse the saucer
  aim-with-inaccuracy logic (`Pohaku/GameWorld.cs:315-330`).
- **Mutant** fast erratic homing swarmer; **Baiter** spawns when the player lingers (wave timer); 
  **Bomber** lays mine particles; **Pod** bursts into **Swarmers** when shot (reuse split pattern
  `Pohaku/GameWorld.cs:437-455`).
- **Lose condition:** `HumanoidsRemaining == 0` → planet-explosion: remaining landers become mutants,
  screen flash, next wave resets harder (more/faster enemies, fewer humanoids).

## Loop / input / modes / audio

- **Modes:** 4-state `Title/Playing/GameOver/Attract` (Attract = autopilot bot that flies + shoots).
- **Loop:** Pohaku structure (`MainPage.xaml.cs:56-87`) — clamped dt, push input → ship, fire one-shots,
  `Update(dt)`, invalidate both canvases.
- **`Update` order:** camera follow → integrate ship (Y-clamp to terrain) → enemies/humanoids/bullets/
  particles → wave/spawn timers (lander cadence, baiter-on-linger) → toroidal `HandleCollisions` →
  resolve humanoid states → `RemoveAll(!Alive)` → wave-clear/lose check → `UpdateAudioState`.
- **Input:** Arrows/WASD directional thrust (L/R flip facing), Space fire (+ start), `B` smart bomb,
  `H` hyperspace, Enter start.

## Catalog + build integration

- **`Launcher/Game/GameCatalog.cs`** — add (mind the disabled Paku note at `:31-32`):
  ```csharp
  new("KIA'I", "guardian / to watch over", "Defender", "Patrol a wrapping world, shoot landers, catch falling humanoids", new SKColor(0x44, 0xCC, 0xFF), "/games/kiai/", "Kiai"),
  ```
- **`Builds/Build-Kiai.ps1`** + **`Builds/Run-Kiai.ps1`** — copies of the Pohaku scripts, `Pohaku`→`Kiai`.
- **`Builds/Build-All.ps1`** — add `'Build-Kiai.ps1',` to `$scripts`.
- **`Builds/Publish-Site.ps1`** — add `@{ Name = 'Kiai'; Slug = 'kiai' }` to `$games`.
