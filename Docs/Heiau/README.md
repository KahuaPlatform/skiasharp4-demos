# Heiau

A Star-Castle-style vector game in the same neon style as the rest of the repo. Built on Uno Platform + SkiaSharp 4, targeting `net10.0-desktop` and `net10.0-browserwasm`.

> **Heiau** — Hawaiian for "sacred stone temple" — fits Cinematronics' 1980 Star Castle: a central sacred stone (the *pohaku*) guarded by three rotating energy walls. Breach the walls to destroy the heart.

## What it does

- **Central turret** in the middle of the playfield with a rotating barrel that tracks the player and fires aimed shots periodically.
- **Three concentric energy rings**, counter-rotating at different speeds, each broken into 12 angular segments. Each segment is independently destructible.
- **Asteroids-style player ship** orbits the playfield (rotate + thrust with inertia + screen wrap + bullet fire).
- **Bullets and rings interact**: player shots destroy ring segments and pass through any gap. The turret's own shots also break its rings — faithful to the original.
- **Score model**: 10 per ring segment, +500 bonus for clearing all rings, +1000 for destroying the turret. Killing the turret advances the level with fresh, faster rings.
- **Difficulty curve**: each level increases ring rotation speed and turret fire rate.
- **Procedural audio** ([AudioEngine.cs](../../Source/Heiau/Heiau/Game/AudioEngine.cs) + [audio.js](../../Source/Heiau/Heiau/Platforms/WebAssembly/WasmScripts/audio.js)) — square-wave shot, metallic two-sine ring ping, saw-wave turret thump, descending turret-kill swell, filtered-noise ship explosion, looping rocket thrust. WASM uses Web Audio via JS interop; desktop uses NAudio.
- **Attract mode** after 12s idle with a simple homing AI that aims for the turret and orbits.
- **Persistent high score** in `%LocalAppData%\Heiau\` on desktop.
- **Same chassis as the other demos**: square Viewbox (900×900 logical), ambient `BackgroundSurface` starfield in the side bars, thin neon playfield border, perspective-tilted scrolling marquee.

## Run

```powershell
.\Builds\Run-Heiau.ps1                          # desktop, Release
.\Builds\Run-Heiau.ps1 -Configuration Debug     # desktop, Debug
.\Builds\Run-Heiau.ps1 -Wasm                    # browser-wasm
```

Or directly: `dotnet run --project Source/Heiau/Heiau/Heiau.csproj -f net10.0-desktop`.

## Controls

| Key | Action |
|---|---|
| Left / Right or A / D | Rotate ship |
| Up / W | Thrust (uses inertia) |
| Space | Fire (or start game from title / game over) |
| Click / Tap | Start game from title / exit attract |

## Architecture

| File | Role |
|---|---|
| [`RingGeometry.cs`](../../Source/Heiau/Heiau/Game/RingGeometry.cs) | Builds 3 concentric segmented rings, plus `HitSegment` collision and angle-wrap helpers. |
| [`Entities.cs`](../../Source/Heiau/Heiau/Game/Entities.cs) | `Ship`, `Turret`, `Ring`, `Bullet`, `Particle`, `ScorePopup`. |
| [`GameWorld.cs`](../../Source/Heiau/Heiau/Game/GameWorld.cs) | State machine + physics. Ship inertia, ring rotation, turret aim/fire, bullet/segment collision, scoring, attract AI. |
| [`Renderer.cs`](../../Source/Heiau/Heiau/Game/Renderer.cs) | All draws: rings as arc segments with hue-shifting halos, central pohaku turret with rotating barrel, vector ship, particles, HUD. |
| [`AudioEngine.cs`](../../Source/Heiau/Heiau/Game/AudioEngine.cs) | Procedural NAudio voices + WASM JS interop bridge. |
| [`Platforms/WebAssembly/WasmScripts/audio.js`](../../Source/Heiau/Heiau/Platforms/WebAssembly/WasmScripts/audio.js) | Web Audio mirror — gesture-gated AudioContext, same voice set. |
| [`BackgroundSurface.cs`](../../Source/Heiau/Heiau/BackgroundSurface.cs) | Ambient deep-space backdrop behind the playfield Viewbox. |
| [`MainPage.xaml`](../../Source/Heiau/Heiau/MainPage.xaml) / [`.cs`](../../Source/Heiau/Heiau/MainPage.xaml.cs) | Square Viewbox layout + input + render loop. |

World coordinates are fixed at `900 × 900` square. Renderer letterboxes; Viewbox preserves the 1:1 ratio at any window size. High-score persistence is shared chassis ([`Source/Common/HighScoreStore.cs`](../../Source/Common/HighScoreStore.cs)), source-included via the csproj `<Compile>` glob.

## Stack

- Uno Platform (`SkiaRenderer` UnoFeature) + `Uno.WinUI.Graphics2DSK.SKCanvasElement`
- SkiaSharp 4.147.0-preview.3.1
- .NET 10 — `net10.0-desktop`, `net10.0-browserwasm`
- NAudio 2.x for desktop audio (Windows-only; guarded by `HAS_NAUDIO`)
