# Mahina

A Lunar-Lander-style vector physics game in the same vector + neon style as the rest of the repo. Built on Uno Platform + SkiaSharp 4, targeting `net10.0-desktop` and `net10.0-browserwasm`.

> **Mahina** — Hawaiian for "moon" — fits the original 1979 Lunar Lander: vector silhouette of the Apollo LM, descending under gravity, fuel meter ticking down, looking for somewhere flat to set down.

## What it does

- **Vector lunar module** with a recognisable Apollo LM silhouette: ascent stage with triangle viewport, descent stage, four splayed landing legs with foot pads, engine bell. Rotates around its own centre with `A/D` or arrows; main thruster on `Space`/`Up`/`W` consumes fuel.
- **Procedurally generated terrain** — midpoint-displaced polyline along the bottom of the world, gets craggier with each level.
- **Landing pads** with three multipliers (`x2`, `x3`, `x5`). Wider pads are safer; narrow ones score more. Pad mix progresses per level: levels 1–2 have a wide+medium pair, 3–4 add a narrow pad, 5+ drop the wide pad entirely.
- **Landing rules** (matching the arcade): touchdown counts if `|vy| ≤ 32`, `|vx| ≤ 22`, ship rotation within ~10° of vertical, and the ship's full footprint is over a pad. Anything else is a crash.
- **Scoring**: 50 × pad multiplier on touchdown, plus 1 point per remaining kg of fuel.
- **Per-level difficulty**: each level rebuilds terrain, decreases starting fuel, narrows pad mix.
- **Procedural audio** ([AudioEngine.cs](../../Source/Mahina/Mahina/Game/AudioEngine.cs) + [audio.js](../../Source/Mahina/Mahina/Platforms/WebAssembly/WasmScripts/audio.js)) — looping rocket thrust (bandpass noise with LFO pulse), C-major arpeggio touchdown chime, filtered-noise crash explosion. WASM uses Web Audio via JS interop; desktop uses NAudio.
- **Attract mode** after 12s idle on the title screen with simple homing AI.
- **Persistent high score** in `%LocalAppData%\Mahina\` on desktop.
- **Same chassis as Lua / HokuLele**: portrait-or-landscape Viewbox layout, BackgroundSurface for ambient starfield in the side bars, thin neon playfield border, perspective-tilted scrolling marquee.

## Run

```powershell
.\Builds\Run-Mahina.ps1                          # desktop, Release
.\Builds\Run-Mahina.ps1 -Configuration Debug     # desktop, Debug
.\Builds\Run-Mahina.ps1 -Wasm                    # browser-wasm
```

Or directly: `dotnet run --project Source/Mahina/Mahina/Mahina.csproj -f net10.0-desktop`.

## Controls

| Key | Action |
|---|---|
| Left / Right or A / D | Rotate lander left / right |
| Up / W / Space | Main thruster (uses fuel) |
| Space / Enter | Start game from title / game over |
| Click / Tap | Start game from title / exit attract |

## Architecture

| File | Role |
|---|---|
| [`Terrain.cs`](../../Source/Mahina/Mahina/Game/Terrain.cs) | Midpoint-displaced polyline + landing pad placement + height-at-X / pad-at-X queries. |
| [`Entities.cs`](../../Source/Mahina/Mahina/Game/Entities.cs) | `Lander`, `LandingPad`, `Terrain`, `Particle`, `ScorePopup`. |
| [`GameWorld.cs`](../../Source/Mahina/Mahina/Game/GameWorld.cs) | Physics + state machine. Gravity, thrust, rotation, fuel burn, collision, landing scoring, attract AI. |
| [`Renderer.cs`](../../Source/Mahina/Mahina/Game/Renderer.cs) | All draws: terrain polyline, pads with multipliers, vector lander, thrust flame, particles, HUD (score / fuel gauge / VX-VY readouts / altitude / lives). |
| [`AudioEngine.cs`](../../Source/Mahina/Mahina/Game/AudioEngine.cs) | Procedural NAudio voices + WASM JS bridge. Thrust loop, landing chime, explosion. |
| [`Platforms/WebAssembly/WasmScripts/audio.js`](../../Source/Mahina/Mahina/Platforms/WebAssembly/WasmScripts/audio.js) | Web Audio mirror — gesture-gated AudioContext, same voice set. |
| [`BackgroundSurface.cs`](../../Source/Mahina/Mahina/BackgroundSurface.cs) | Ambient deep-space backdrop behind the playfield Viewbox. |
| [`MainPage.xaml`](../../Source/Mahina/Mahina/MainPage.xaml) / [`.cs`](../../Source/Mahina/Mahina/MainPage.xaml.cs) | Viewbox layout + input + render loop. |

World coordinates are fixed at `1280 × 720` landscape. Renderer letterboxes; Viewbox preserves the 16:9 ratio at any window size. High-score persistence is shared chassis ([`Source/Common/HighScoreStore.cs`](../../Source/Common/HighScoreStore.cs)), source-included via the csproj `<Compile>` glob.

## Stack

- Uno Platform (`SkiaRenderer` UnoFeature) + `Uno.WinUI.Graphics2DSK.SKCanvasElement`
- SkiaSharp 4.151.0
- .NET 10 — `net10.0-desktop`, `net10.0-browserwasm`
- NAudio 2.x for desktop audio (Windows-only; guarded by `HAS_NAUDIO`)
