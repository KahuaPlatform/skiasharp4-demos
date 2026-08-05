# Koa — Design (Gauntlet homage)

> **Status: built.** This was the implementation plan; the game now lives at `Source/Koa/`.
> The shared chassis pieces this game drove (`Camera2D`, `VectorShapes`, `TileGrid<T>`, `AsciiMap`,
> `FlowField`, `HudText.Bar`) are documented in
> [03 – Shared Chassis](../Architecture/03-Shared-Chassis.md); the design rationale behind them is in
> [08 – Chassis Extensions](../Architecture/08-Chassis-Extensions.md).

## Elevator pitch

**KOA** (Hawaiian *"warrior / brave"*) is a **Gauntlet** homage: a top-down tile-grid dungeon crawl.
Move 8-directionally through a maze larger than the screen, shoot endless hordes pouring from
destructible **generators**, grab **keys** to open doors and **food** to top up a health bar that
**drains continuously** ("warrior needs food badly"), and reach the exit. Optionally pick a class
(Warrior/Valkyrie/Wizard/Elf) — the seam toward the repo's first co-op game.

Architecturally closest to **Hahai** (grid + ASCII layout + chase AI) but diverging in three ways that
drive every decision: a **2-D bounded follow-camera** (dungeon ≫ screen), **continuous AABB-vs-wall
motion with wall-sliding** (not cell-snapped grid motion), and **dozens of concurrent enemies + a
draining health clock** (needs a shared flow-field, culling, and maybe pooling).

## What's different from the existing template

- **No Viewbox scale-to-fit.** Uses the Pohaku stretched-`GameSurface` layout (`Pohaku/MainPage.xaml:8-11`);
  the shared `Camera2D` (clamped, no wrap) does framing, not a Viewbox (contrast `Hahai/Renderer.cs:33-39`).
- **Continuous motion, tile walls.** Heroes/enemies are circles in continuous space; only walls are
  tiles. Movement resolves **axis-separately vs wall tiles** for free wall-sliding — not Hahai's
  center-snapped `StepEntity` (`Hahai/GameWorld.cs:435-485`).
- **Health is the clock.** Continuous drain; food restores; 0 = death.

## Project layout — `Source/Koa/Koa/`

Root namespace `Koa`, game namespace `Koa.Game`. Chassis/wiring files mirror Pohaku/Hahai:

| File | KOA-specific delta |
|---|---|
| `Koa.csproj` | Copy of `Pohaku.csproj`, `Pohaku`→`Koa`. Keep verbatim: Common `<Compile>` glob (`:32-34`), wasm `audio.js` `EmbeddedResource` (`:39-41`), `HAS_NAUDIO`/NAudio block (`:46-51`), `UnoSplashScreen` (`:56`). |
| `GlobalUsings.cs` | Verbatim copy (the `Arcade.Common*` global usings are mandatory). |
| `App.xaml(.cs)` | Copy of Hahai; namespace `Koa`; `AudioEngine.Init()` in `OnLaunched`; size desktop window landscape (~1280×800). |
| `MainPage.xaml` | **Pohaku stretched layout** (Background + stretched GameSurface, no Viewbox). |
| `BackgroundSurface.cs` | `sealed : AmbientStarBackdrop`; override `BgTop/BgBottom` to crypt-dark (`#0A0410`→`#1A0A24`); stars read as torch-dust. |
| `GameSurface.cs` | Copy of `Pohaku/GameSurface.cs`; `Resize` records viewport pixel size for the camera (world size is fixed by the level). |
| `Platforms/*` | Verbatim copies, namespace `Koa`; `globalThis.koaAudio` in `audio.js`. |

### Game-specific files (`Game/`)

- **`TileMap.cs`** — thin domain wrapper over the shared `TileGrid<Tile>` (chassis). `enum Tile : byte
  { Floor, Wall, Door, Exit, Generator, Void }`, `CellSize = 32f`. Adds `IsBlocked(col,row)` /
  `IsBlockedAt(x,y)`, `OpenDoor(c,r)`, destroyed-generator → Floor. Cell math (`CellCenter`,
  `WorldToCell`) and the `MoveCircle` wall-slide resolver come from `TileGrid<T>`.
- **`Level.cs`** — ASCII level data + loader using the shared `AsciiMap.Parse`. Legend extends Hahai's
  (`Arena.cs:79-96`): `#`wall `.`floor `D`door `X`exit `G`generator `K`key `F`food `P`potion `$`treasure
  `@`hero-spawn ` `void. **Static terrain** (walls/doors/exit) bakes into `TileMap`; **dynamic features**
  (generators, items) become entities. Ships 3–4 authored maps; `BuildProcedural(level,rng)` stub left
  for endless mode.
- **`Camera` usage** — shared `Camera2D` with `X/Y = Clamp(worldExtent)`, `FollowRate = 0` (snap). Uses
  `VisibleWorldRect` for culling and `Apply(canvas)` for the world→screen translate.
- **`Pathing.cs`** — thin wrapper over shared `FlowField` (chassis): multi-source BFS from the hero(es),
  rebuilt every ~4–6 frames; `FlowDir(col,row)` gives each enemy its step. One field serves all enemies.
- **`Entities.cs`** — `abstract Entity` base like `Pohaku/Entities.cs:6-27` **without** wrapping.
  Types: `Hero` (`HeroClass Class`, `Health`, `Keys`, `Potions`, `AimDir`, intent fields), `Enemy`
  (`EnemyKind {Grunt,Ghost,Demon}`, `Health`, `Speed`, `HitCooldown`), `Generator` (`Health`,
  `SpawnTimer`, `Spawns`), `Projectile` (`FromHero`, `Lifetime`, `Damage`), `Pickup`
  (`PickupKind {Key,Food,Potion,Treasure}`), `Particle`. `enum HeroClass` + `ClassStats` table
  (Speed/ShotSpeed/Cooldown/Armor/MaxHealth) is the co-op seam; v1 defaults to Warrior.
- **`GameWorld.cs`** — sim core (modeled on Hahai + Pohaku). Holds `TileMap`, `Camera2D`, `Pathing`,
  `Hero`, lists of enemies/generators/projectiles/pickups/particles (`RemoveAll(!Alive)` sweep). 
  4-state `GameMode`. `Update(dt)`: mode switch → **health drain** → hero move vs walls (slide) +
  camera follow → rebuild flow field every N frames → enemies (flow field + optional separation),
  generators (spawn cadence), projectiles, pickups, particles → `HandleCollisions` → sweep →
  level-clear/death/scoring. `HighScoreStore("Koa")`.
- **`Renderer.cs`** — `Camera2D.Apply` → cull-to-`VisibleWorldRect`: tiles (windowed double-loop, not
  full-grid like `Hahai/Renderer.cs:74-75`), pickups, generators, enemies, projectiles, hero, particles
  → restore → HUD (the draining **health bar via `HudText.Bar`**, score, keys/potions, level). Walls as
  the neon block+halo pass (`Hahai.DrawMaze:59-99`); shapes via shared `VectorShapes`.
- **`AudioEngine.cs`** — Hahai facade structure; `globalThis.koaAudio`. Voices: `PlayShoot`, `PlayHit`,
  `PlayEnemyDie`, `PlayGeneratorDie`, `PlayPickup`, `PlayDoor`, `PlayPotion`, `PlayHeroHurt`,
  `PlayDeath`, `PlayLevelClear`, and a recurring low health-low warning ("warrior needs food badly").
  Throttle shoot/hit like `PlayChomp` (`Hahai/AudioEngine.cs:22-31`).

## Tilemap

- **Representation:** `TileGrid<Tile>` backing array; `CellSize = 32f`; world is `Cols*Cell × Rows*Cell`,
  typically 2–4× the viewport each axis.
- **Authoring:** `AsciiMap.Parse(rows, onCell)` validates rectangularity then calls back per glyph; the
  game maps char → tile **and** registers features. **Terrain-vs-feature split** is the key decision:
  walls/doors/exit are static tiles (cheap to test + cull); generators/items are entities (destroyable,
  collectable, poolable). Doors are hybrid — `Tile.Door` for collision, flipped to Floor by `OpenDoor`
  when a key is spent.
- **AABB-vs-tile wall-slide** (shared `TileGrid.MoveCircle`): resolve desired displacement
  **X then Y independently** against the cells the circle's extent overlaps, clamping to the wall face on
  block. Independent axes mean pressing diagonally into a wall still advances along the free axis — the
  Gauntlet slide. Enemies use the same resolver (no corner-clipping). Returns a `hitWall` flag (used to
  expire projectiles).

## Follow-camera

Shared `Camera2D`, `Clamp` on both axes, snap follow:
`Center = hero`, then clamp each axis to `[half, World-half]` (or centre the axis if world < view).
Replaces scale-to-fit: renders 1:1 (or a fixed zoom) and translates by `-Left`. **No wraparound** — the
clamp hard-stops at dungeon edges (contrast Kia'i's wrapping camera). **Culling:** `VisibleWorldRect(pad)`
drives a windowed tile loop and entity early-`continue`; a 4000×3000 world draws ~40×25 cells instead of
125×94 — essential for the wasm frame budget.

## Swarm AI + performance

- **Flow field, not per-enemy search:** `FlowField.Rebuild` floods BFS from the hero cell across walkable
  cells (`int[,] Dist`); each enemy reads `FlowDir(col,row)` (lowest-distance walkable neighbour → unit
  step) and moves via `MoveCircle`. Cost is **O(cells) once per rebuild**, shared by all enemies — vs
  Hahai's greedy per-entity targeting (`ChooseDirectionTowards:389-414`) which degrades and corner-clips
  at scale. Flow fields also route around concave walls. Rebuild every 4–6 frames or on hero cell-cross.
  Multi-source flood (all heroes) gives "chase nearest" for free → co-op ready.
- **Generators:** `SpawnTimer`-gated emit while under a global live cap (~60) with a free adjacent cell;
  jittered cadence (like saucer respawn `Pohaku/GameWorld.cs:156`). Destroying one flips its tile to
  Floor and stops the stream — the core objective.
- **Separation (cheap):** uniform spatial hash (bucket per cell, rebuilt per frame); each enemy nudges
  only against its own + 8 neighbour buckets. Disable-able if wasm-heavy (overlap is acceptable).
- **Pooling:** `Projectile`/`Enemy`/`Particle` are numerous + short-lived → back with shared `Pool<T>`
  if profiling shows GC stutter (P2 — see chassis doc).

## Resource clock / pickups / progression

- **Health clock:** `Hero.Health` is health *and* timer — drains `HealthDrainPerSec*dt` (e.g. 4/s from
  2000 max) plus on-contact damage (`* Class.Armor`, gated by per-enemy `HitCooldown`); 0 → death. Food
  restores a chunk (capped). The draining neon bar is the signature HUD readout (`HudText.Bar`).
- **Pickups:** hero-vs-`Pickup` circle test. Key→`Keys++`; Food→heal; Potion→`Potions++`; Treasure→score.
- **Doors/keys:** `Tile.Door` blocks; on contact with `Keys>0` → `Keys--`, `OpenDoor`, `PlayDoor`.
- **Potion (screen-clear):** `UsePotion()` damages all enemies in `VisibleWorldRect` + particles.
- **Progression:** overlap `Tile.Exit` → next level (authored or procedural), keep health/score
  (`Hahai.CheckLevelClear:566-578`). Scoring for kills/generators/treasure/exit; `HighScoreStore` save on
  game over.

## Loop / input / modes / audio

- **Modes:** 4-state `Title/Playing/GameOver/Attract` (Attract = flow-field auto-hero — trivial, AI exists).
- **Input:** Pohaku latched flags (`MainPage.xaml.cs:89-130`) but **8-directional** — compose `Hero.MoveDir`
  from `_up/_down/_left/_right`; `AimDir` follows `MoveDir` (or last non-zero); Space fire (latched + edge),
  a key for potion, Enter/Space start; WASD + arrows bound.
- **Audio:** `Init` in `OnLaunched`; invalidate both canvases each tick (`Hahai/MainPage.xaml.cs:61-62`).

## Catalog + build integration

- **`Launcher/Game/GameCatalog.cs`** — add (mind the 9-card grid caveat at `:31`):
  ```csharp
  new("KOA", "warrior / brave", "Gauntlet", "Raid the dungeon, smash spawners, grab food before your health drains", new SKColor(0xFF, 0x55, 0x33), "/games/koa/", "Koa"),
  ```
- **`Builds/Build-Koa.ps1`** + **`Builds/Run-Koa.ps1`** — copies of the Pohaku scripts, `Pohaku`→`Koa`.
- **`Builds/Build-All.ps1`** — add `'Build-Koa.ps1',` to `$scripts`.
- **`Builds/Publish-Site.ps1`** — add `@{ Name = 'Koa'; Slug = 'koa' }` to `$games`.
