# Hahai

A Pac-Man-style chase game with Hawaiian imagery substituted for the original sprites — same canonical 28×31 maze, same scatter/chase phasing, same eaten-eyes-return-to-house respawn. Built on Uno Platform + SkiaSharp 4, targeting `net10.0-desktop` and `net10.0-browserwasm`.

> **Hahai** — Hawaiian for "to chase / pursue / follow". The Honu (sea turtle) is chased by four Mo'o (water-spirit lizards); the maze is the pursuit.

## What it does

- **Honu (sea turtle)** as the player — orange tortoise-shell body with hexagonal scute pattern, four little flippers, head poking out in the direction of motion with an open/close mouth.
- **Limu** (sea-grass pellets) and **Lehua** flowers (the five-petal ohia blossom used as power pellets).
- **Four Mo'o** (water-spirit lizards) — elongated oval bodies oriented in their direction of motion, slithering wobble tails, four little legs, kind-colored:
  - Red `Blinky` — direct-pursuit
  - Pink `Pinky` — targets four cells ahead of the Honu
  - Cyan `Inky` — targets two cells ahead of the Honu mirrored through Blinky
  - Orange `Clyde` — direct-pursuit when far, scatter when close
- **Classic AI structure** — scatter / chase phase scheduling, greedy one-step lookahead at intersections, kind-specific chase targets, frightened mode flips them blue + random-wander on power pellet, eaten state reverts to eyes that return to the ghost house and drop through the door before re-emerging.
- **Lives + scoring** — pellet=10, power=50, ghost chain 200/400/800/1600. Persistent high score stored in `%LocalAppData%\Hahai\` on desktop.
- **Attract mode** after 12s idle on the title — a simple greedy bot pilots the Honu (head toward nearest pellet, steer away from any non-frightened mo'o within 6 cells).
- **Procedural audio** ([`AudioEngine.cs`](../../Source/Hahai/Hahai/Game/AudioEngine.cs) + [`audio.js`](../../Source/Hahai/Hahai/Platforms/WebAssembly/WasmScripts/audio.js)) — alternating high/low chomp blips on pellet eat, ascending power-up arpeggio, eat-ghost chime, descending death wobble, level-clear fanfare. WASM uses Web Audio via JS interop; desktop uses NAudio.
- **Same chassis as the other demos** — 672×744 maze with 50px HUD bands top + bottom, ambient `BackgroundSurface` starfield behind the Viewbox, neon glyph-font title, perspective-tilted scrolling marquee hidden during gameplay so it doesn't compete with the maze.

## Run

```powershell
.\Builds\Run-Hahai.ps1                          # desktop, Release
.\Builds\Run-Hahai.ps1 -Configuration Debug     # desktop, Debug
.\Builds\Run-Hahai.ps1 -Wasm                    # browser-wasm
```

Or directly: `dotnet run --project Source/Hahai/Hahai/Hahai.csproj -f net10.0-desktop`.

## Controls

| Key | Action |
|---|---|
| Arrows or WASD | Set the Honu's direction (queued — honored at the next intersection) |
| Space / Enter | Start game from title / game over |
| Click / Tap | Start game from title / exit attract |

OS key-repeat for held direction keys is de-duplicated in the input layer so the 30 Hz repeat pump doesn't beat against the 60 Hz render tick.

## Architecture

| File | Role |
|---|---|
| [`Arena.cs`](../../Source/Hahai/Hahai/Game/Arena.cs) | 28×31 maze grid hand-typed as ASCII. `Tile` enum (Wall, Open, GhostDoor, House, Tunnel), parallel pellet + power-dot bool grids, wall/door walkability check, tunnel-wrap helpers, per-kind scatter-corner targets. |
| [`Entities.cs`](../../Source/Hahai/Hahai/Game/Entities.cs) | `Pac`, `Ghost`, `Direction` + helpers, `Particle`, `ScorePopup`, `GameMode`. |
| [`GameWorld.cs`](../../Source/Hahai/Hahai/Game/GameWorld.cs) | State machine + AI. Honu motion with mid-segment turn-at-intersection, ghost release-from-house, scatter/chase phase scheduling, eaten ghost beelining to door + drop-back-into-house respawn, per-kind chase targeting, frightened random-wander, score / lives / level, attract-mode autopilot. |
| [`Renderer.cs`](../../Source/Hahai/Hahai/Game/Renderer.cs) | Neon-styled maze (glowing wall blocks), limu + pulsing five-petal lehua, Honu shell + scutes + head with mouth, Mo'o body + tail + legs + eyes, HUD with score / level / lives icons / placard / marquee gated to non-Playing modes. |
| [`AudioEngine.cs`](../../Source/Hahai/Hahai/Game/AudioEngine.cs) | NAudio voices (desktop) + JS interop bridge (WASM). Inherits plumbing from `Arcade.Common.Audio.AudioEngineBase`. Chomp is throttled to ~10 Hz alternating high/low for the "wakka wakka" effect. |
| [`Platforms/WebAssembly/WasmScripts/audio.js`](../../Source/Hahai/Hahai/Platforms/WebAssembly/WasmScripts/audio.js) | Web Audio mirror — gesture-gated AudioContext, same voice set. |
| [`BackgroundSurface.cs`](../../Source/Hahai/Hahai/BackgroundSurface.cs) | Thin wrapper over `Arcade.Common.AmbientStarBackdrop`. |
| [`MainPage.xaml`](../../Source/Hahai/Hahai/MainPage.xaml) / [`.cs`](../../Source/Hahai/Hahai/MainPage.xaml.cs) | Viewbox layout (672×844 — maze + 50px HUD bands), direction-queue input with key-repeat de-dup, render loop. |

Shared chassis (neon paints, glyph font, marquee, gradient backdrop, HUD text, `Vec2`, `HighScoreStore`, audio base) is included from `Source/Common/` via the csproj's `<Compile>` glob.

World coordinates are fixed at `672 × 744` for the maze (28×31 cells × 24px). The MainPage's `GameSurface` is `672 × 844` so the renderer centers the maze vertically inside the canvas with 50px top + 50px bottom HUD bands — score and lives draw in those bands without overlapping the playfield.

## Stack

- Uno Platform (`SkiaRenderer` UnoFeature) + `Uno.WinUI.Graphics2DSK.SKCanvasElement`
- SkiaSharp 4.151.0
- .NET 10 — `net10.0-desktop`, `net10.0-browserwasm`
- NAudio 2.x for desktop audio (Windows-only; guarded by `HAS_NAUDIO`)
