# Paku

## Elevator pitch

A neon agar.io: you are a wobbling amoeba in a 5000×5000 cell arena. Thrust around eating spores and any cell smaller than you, avoid anything bigger, and grow. The whole thing is drawn directly to the Uno Skia compositor via `Uno.WinUI.Graphics2DSK.SKCanvasElement` — no XAML gameplay, no sprites: a `GameWorld` that ticks on `CompositionTarget.Rendering` and a static `Renderer` that issues SkiaSharp draw calls each frame.

> **Paku** — playful Hawaiian-flavored riff on "gobble". Consume or be consumed.

What makes Paku different from the other arcade demos in this repo: it does **not** use the fixed-Viewbox + `BackgroundSurface` chassis. Instead it renders to the full canvas and applies its own **camera transform** (translate-to-center → scale by zoom → pan by camera position) so a world far larger than the screen scrolls beneath you, and the camera **zooms out as you grow** so a giant blob still fits on screen.

## Deep dive

### Project layout

- [Paku.csproj](../../Source/Paku/Paku/Paku.csproj) is a `Uno.Sdk` single-project targeting `net10.0-browserwasm;net10.0-desktop` with `<UnoFeatures>SkiaRenderer</UnoFeatures>` and `<SkiaSharpVersion>4.147.0-preview.3.1</SkiaSharpVersion>`.
- Game state lives in [Game/](../../Source/Paku/Paku/Game/): [`Entities.cs`](../../Source/Paku/Paku/Game/Entities.cs) (`Cell`, `Spore`, `Particle` data), [`GameWorld.cs`](../../Source/Paku/Paku/Game/GameWorld.cs) (simulation, AI, absorption, scoring, camera, mode state machine), [`Renderer.cs`](../../Source/Paku/Paku/Game/Renderer.cs) (all drawing), [`AudioEngine.cs`](../../Source/Paku/Paku/Game/AudioEngine.cs) (procedural voices).
- The shared neon chassis (`HsvColor`, `Marquee`, `HudText`, `HighScoreStore`, `AudioEngineBase`, `Vec2`) is source-included from [`Source/Common/`](../../Source/Common/) via the csproj `<Compile>` glob. Paku uses these pieces but **skips** `AmbientStarBackdrop`/`NeonBackground`/`PlayfieldBorder` — its plasma backdrop and world grid replace them.

### Game loop

[MainPage.xaml.cs](../../Source/Paku/Paku/MainPage.xaml.cs) subscribes to `CompositionTarget.Rendering` (vsync-aligned, once per compositor frame) rather than a `DispatcherTimer`, so `dt` (from a `Stopwatch`) tracks real frame pacing. `dt` is clamped to `[1/60, 1/30]` so a debugger pause or GC hitch can't teleport every cell. Each frame the page copies its held-key flags + pointer state into the world, sets `Thrusting`, calls `_world.Update(dt)`, and `GameCanvas.Invalidate()`s for the next paint.

Input is held-state tracked per key (`_up/_down/_left/_right/_space/_pointerDown`) so releasing one direction key doesn't cut thrust while another is still held. Thrust is active when any direction key, space, or the pointer is held.

### Mode state machine

Paku has three modes (`Attract`, `Playing`, `GameOver`) — Attract **is** the title screen, with a greedy autopilot bot demoing play. `Update(dt)` dispatches on `Mode`: the shared `UpdateSimulation(dt)` always runs, with player input + audio layered on in `Playing`, the bot AI in `Attract`, and a 3-second timer in `GameOver` before returning to `Attract`.

```
Attract  --Space/Enter/Click-->  Playing  --eaten-->  GameOver  --3s-->  Attract
```

### Cells, mass, and absorption

A `Cell` has `Mass`; its radius is `sqrt(mass) * 2.5`, so area scales linearly with mass and eating prey grows you believably. The absorption rule everywhere (player↔enemy, enemy↔enemy, bot↔enemy) is **`AbsorbRatio = 1.25`**: the attacker must be at least 25% more massive to eat the defender, and gains a fraction of the eaten mass (80% for player-eats-enemy, 60% for enemy-eats-enemy). Touch resolves at 80% of summed radii so blobs visibly overlap first.

Thrusting **costs mass** (`ThrustMassCost = 8` mass/sec) and is disabled below `MinPlayerMass = 15`, so moving is a real trade-off — you shrink to chase.

### Organic blob shape

Each cell carries `LobeCount = 7` sine harmonics with random per-cell amplitude and phase ([`Cell.InitShape`](../../Source/Paku/Paku/Game/Entities.cs)). `Cell.RadiusAt(angle, time)` sums them so the membrane ripples (each harmonic animates at its own rate), giving every cell a unique wobbling amoeba silhouette. The renderer's `BuildBlobPath` samples `RadiusAt` at 36 perimeter angles to build the closed outline, then draws it in layered passes: big soft outer glow + inner halo (both via scale-about-center transforms of the same path) + body fill + white nucleus + neon membrane stroke.

### Enemy AI

`Passive` cells Brownian-wander (random velocity nudges, speed-capped). `Hunter` cells chase prey (the player or any smaller enemy within `HuntRange`) and flee bigger threats. Speed caps fall as mass rises (`max ∝ 1/sqrt(mass)`) so big cells are sluggish — that's your opening. Hunters are flagged in the renderer with three orbiting red dots.

Population is seeded in tiers (many small, some medium, a few large, a few early hunters) and then **progressively spawned**: the spawn interval shortens with `GameTime` (down to 0.5s) and a `difficulty` ramp (0→1 over the first two minutes) scales enemy size and the chance a spawn is a Hunter.

### Camera

`UpdateSimulation` exponentially smooths the camera toward the player (`camSmooth = 4·dt`) and eases zoom toward `40 / max(playerRadius, 10)`, clamped to `[0.08, 1.5]`. The renderer applies it as `Translate(center) · Scale(zoom) · Translate(-camera)`. World-space content (grid, border, spores, particles, cells) is drawn inside this transform; the plasma backdrop and HUD are drawn in screen space outside it.

### Plasma backdrop

A classic demoscene plasma — three overlapping sine fields summed and mapped to a dark neon palette, rendered as coarse 24px blocks (not per-pixel) so it's cheap. Kept dark so the cells pop. It animates off `world.TotalTime`.

### Audio

[`AudioEngine.cs`](../../Source/Paku/Paku/Game/AudioEngine.cs) is a static facade over `Arcade.Common.Audio.AudioEngineBase`. Voices: a rising bubbly **absorb** chirp, a descending **death** warble (saw + filtered noise), and a looping **thrust** voice (double-filtered noise + slow wobble) with linear fade-in/out. Desktop uses NAudio `ISampleProvider`s; WASM mirrors each voice in [`Platforms/WebAssembly/WasmScripts/audio.js`](../../Source/Paku/Paku/Platforms/WebAssembly/WasmScripts/audio.js) via `globalThis.pakuAudio`. The thrust loop is started/stopped on rising/falling edges of "effective thrust" (thrusting **and** above min mass).

### Tunables at a glance

| Knob | Location | Effect |
|---|---|---|
| `WorldWidth` / `WorldHeight` | `GameWorld.cs` | arena size |
| `AbsorbRatio` | `GameWorld.cs` | how much bigger you must be to eat |
| `ThrustForce` / `ThrustMassCost` / `MinPlayerMass` | `GameWorld.cs` | movement feel + mass economy |
| `MaxSpores` / `MaxEnemies` / `SporeRespawnRate` | `GameWorld.cs` | world density |
| `RadiusScale` / `LobeCount` | `Entities.cs` | cell size mapping + wobble detail |
| `BlobSegments` | `Renderer.cs` | membrane smoothness |
| camera `targetZoom` clamp `[0.08, 1.5]` | `GameWorld.cs` | how far it zooms out as you grow |

### Running

```
dotnet run --project Source/Paku/Paku --framework net10.0-desktop
dotnet run --project Source/Paku/Paku --framework net10.0-browserwasm
```

Or use the helper: `.\Builds\Run-Paku.ps1` (desktop, Release) / `.\Builds\Run-Paku.ps1 -Wasm` (browser). The wasm target serves at `http://localhost:5000/`.

Controls: WASD or arrows to aim, or aim with the mouse; hold space or click to thrust; space/enter/click starts a game from the title or game-over screen.
