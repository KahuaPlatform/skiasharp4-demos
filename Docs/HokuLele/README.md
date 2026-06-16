# HokuLele

A full Galaga-style vector shooter in the same neon style as [Pohaku](../Pohaku/README.md) and the rest of the repo. Built on Uno Platform + SkiaSharp 4, targeting `net10.0-desktop` and `net10.0-browserwasm`.

> **Hoku Lele** — Hawaiian for "shooting star / flying star". The enemy formation is drawn from neon vector silhouettes, with the Uno arc-mark and Kahua snowflake cameoing as high-tier ships.

## What it does

- **Authentic 40-ship formation** — `4 + 8 + 8 + 10 + 10` rows (high-tier / captains / wings / drones), spawned enemy-by-enemy on alternating left/right entry streams along cubic-Bezier entry flights, then parked in a slot with a sinusoidal "breathing" wobble.
- **Six enemy kinds** (`Enemy.Kind` 0–5): drone, wing, captain, boss, Uno mothership, Kahua snowflake — distinct neon silhouettes and explosion colors, scoring 50/80/100/200/300/500.
- **Galaga dive choreography** — `Attacking` launches pair-dives every interval: two enemies (preferring opposite sides for a pincer) loop out of formation on an Immelmann dive toward the player's position, fire downward shots, then loop back over the top and `Rejoin` their slot. Diving enemies score **2×**.
- **Tractor-beam capture → dual-fighter rescue** — high-tier enemies may, instead of diving, fly to a hover above the player and deploy a widening tractor-beam trapezoid. Caught, you're tractored up; if the boss reaches its slot you lose a life, but **shoot the boss while it holds your captive and the captive returns as a side-by-side wingman** that doubles your fire (and absorbs the next hit).
- **Stage progression + challenge stages** — stages get faster/deadlier with a difficulty ramp (dive interval, enemy fire cadence, dive duration all tighten with a floor). Every 4th stage (3, 7, 11, …) is a no-formation **challenge stage**: 40 ships fly through one of four choreographies and exit; clearing all 40 awards a 10,000 perfect bonus.
- **Mystery flyby** — periodically the Uno mothership or Kahua snowflake traverses the top of the screen (borrowed from Space Invaders' UFO) as a 1,500-point bonus target.
- **Lives, scoring, score popups, persistent high score** (`%LocalAppData%\HokuLele\` on desktop), **attract mode** after 10s idle on the title (homing bot).
- **Procedural audio** ([`AudioEngine.cs`](../../Source/HokuLele/HokuLele/Game/AudioEngine.cs) + [`audio.js`](../../Source/HokuLele/HokuLele/Platforms/WebAssembly/WasmScripts/audio.js)) — shoot, dive whoosh, explosion. Desktop NAudio; WASM Web Audio.
- **Same chassis as the others** — Viewbox-letterboxed portrait playfield, `BackgroundSurface` starfield, neon glyph-font title + scrolling marquee.

## Run

```powershell
.\Builds\Run-HokuLele.ps1                          # desktop, Release
.\Builds\Run-HokuLele.ps1 -Configuration Debug     # desktop, Debug
.\Builds\Run-HokuLele.ps1 -Wasm                    # browser-wasm
```

Or directly: `dotnet run --project Source/HokuLele/HokuLele/HokuLele.csproj -f net10.0-desktop`.

## Controls

| Key | Action |
|---|---|
| Arrows / A-D | Move left / right |
| Space / Enter | Fire (or start game from title / game over) |
| Click / Tap | Start game from title |
| K | Cheat — toggle the on-screen bullet cap (off = old cooldown-only firing) |

## Architecture

| File | Role |
|---|---|
| [`Entities.cs`](../../Source/HokuLele/HokuLele/Game/Entities.cs) | `Player` (+ dual-fighter wingman), `Enemy` (+ `EnemyState`), `Bullet`, `Particle`, `ScorePopup`. |
| [`Paths.cs`](../../Source/HokuLele/HokuLele/Game/Paths.cs) | Cubic-Bezier path helpers for every choreography: entry flights (×4), dive, rejoin, challenge-stage flythroughs (×4 patterns × 8 sub-variants), mystery flyby. |
| [`GameWorld.cs`](../../Source/HokuLele/HokuLele/Game/GameWorld.cs) | The brain: wave state machine (`Spawning→Settling→Attacking→Placard`), spawning + slot/kind layout, pair-dive + tractor-beam scheduling, per-state enemy motion + path-facing, collisions, scoring, stage/challenge progression, capture/rescue, attract AI. |
| [`Renderer.cs`](../../Source/HokuLele/HokuLele/Game/Renderer.cs) | Neon vector silhouettes per `Enemy.Kind`, player ship + wingman, bullets, tractor beam, particles, HUD (score / high score / lives / stage placards), title + marquee. |
| [`AudioEngine.cs`](../../Source/HokuLele/HokuLele/Game/AudioEngine.cs) | NAudio voices (desktop) + JS-interop bridge (WASM) over `Arcade.Common.Audio.AudioEngineBase`. |
| [`BackgroundSurface.cs`](../../Source/HokuLele/HokuLele/BackgroundSurface.cs) | Thin wrapper over `Arcade.Common.AmbientStarBackdrop`. |
| [`MainPage.xaml`](../../Source/HokuLele/HokuLele/MainPage.xaml) / [`.cs`](../../Source/HokuLele/HokuLele/MainPage.xaml.cs) | Viewbox layout, held-key input, `CompositionTarget.Rendering` loop. |

The world coordinates are portrait `720 × 1280`; the renderer letterboxes onto the canvas, and the desktop window is sized `810 × 1440` in [`App.xaml.cs`](../../Source/HokuLele/HokuLele/App.xaml.cs).

### Path-facing trick

Enemies are drawn pointing "up" (−Y) by default. `UpdatePathFacing` does a forward-difference along the current Bezier (sample `t` and `t+0.05`), takes the tangent, and sets `Rotation = atan2(dy, dx) + π/2` so the ship's nose tracks its motion through every loop and dive.

## Stack

- Uno Platform (`SkiaRenderer` UnoFeature) + `Uno.WinUI.Graphics2DSK.SKCanvasElement`
- SkiaSharp 4.147.0-preview.3.1
- .NET 10 — `net10.0-desktop`, `net10.0-browserwasm`
- NAudio 2.x for desktop audio (Windows-only; guarded by `HAS_NAUDIO`)
