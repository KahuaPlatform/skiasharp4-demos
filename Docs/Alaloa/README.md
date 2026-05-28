# Alaloa

A Tron-Light-Cycles-style vector duel in the same neon style as the rest of the repo. Built on Uno Platform + SkiaSharp 4, targeting `net10.0-desktop` and `net10.0-browserwasm`.

> **Alaloa** — Hawaiian for "long road / long path / trail" — fits Tron's defining mechanic: the long glowing trail every cycle drags behind it.

## What it does

- **4 light cycles** spawn from the four cardinal edges of a 720×720 arena, all heading toward the centre.
- **Continuous motion + 90° turns**. Each cycle moves at constant speed; tapping a direction key turns the player's cycle at the next cell boundary (no diagonal cuts).
- **Persistent neon trails**. Every cell a cycle enters is marked with its owner; trail-vs-anything collision is per-cell so it's pixel-clean. Crash into a trail (yours or anyone else's) or the arena edge and the cycle dies in a particle burst.
- **Bot AI** for the 3 non-player cycles. Each tick, the bot scans straight + left + right with a 30-cell look-ahead and picks the longest open run. Small random jitter prevents perfectly identical bot behaviour.
- **Rounds + matches**. Last cycle alive wins the round and scores +1; first to 5 wins takes the match. Per-cycle scores shown along the top of the HUD.
- **Procedural audio** ([AudioEngine.cs](../../Source/Alaloa/Alaloa/Game/AudioEngine.cs) + [audio.js](../../Source/Alaloa/Alaloa/Platforms/WebAssembly/WasmScripts/audio.js)) — short high blip on every turn, filtered-noise + dropping-saw crash, rising-arpeggio round win, descending-arpeggio round lose. WASM uses Web Audio via JS interop; desktop uses NAudio.
- **Attract mode** after 12s idle on the title — four bots fight it out and re-spawn when nobody's left.
- **Persistent high score** — your best round-wins total across all matches, stored in `%LocalAppData%\Alaloa\` on desktop.
- **Same chassis as the other demos**: square 720×720 Viewbox, ambient `BackgroundSurface` starfield in the side bars, thin neon playfield border, perspective-tilted scrolling marquee. All chassis pieces come from `Source/Common/`.

## Run

```powershell
.\Builds\Run-Alaloa.ps1                          # desktop, Release
.\Builds\Run-Alaloa.ps1 -Configuration Debug     # desktop, Debug
.\Builds\Run-Alaloa.ps1 -Wasm                    # browser-wasm
```

Or directly: `dotnet run --project Source/Alaloa/Alaloa/Alaloa.csproj -f net10.0-desktop`.

## Controls

| Key | Action |
|---|---|
| Arrows or WASD | Turn cycle 90° in that direction (single press) |
| Space / Enter | Start match from title / game over |
| Click / Tap | Start match from title / exit attract |

## Architecture

| File | Role |
|---|---|
| [`Arena.cs`](../../Source/Alaloa/Alaloa/Game/Arena.cs) | 90×90 cell grid (8px cells), `int[,]` per-cell owner tracking, world↔cell helpers. |
| [`Entities.cs`](../../Source/Alaloa/Alaloa/Game/Entities.cs) | `Cycle`, `Direction` + helpers, `Particle`, `GameMode`. |
| [`GameWorld.cs`](../../Source/Alaloa/Alaloa/Game/GameWorld.cs) | State machine + physics. Cycle motion, turn handling, per-cell trail marking + collision, bot look-ahead AI, round/match scoring. |
| [`Renderer.cs`](../../Source/Alaloa/Alaloa/Game/Renderer.cs) | Game-specific draws: arena grid backdrop, neon trail polylines per cycle, cycle bodies + nose dots, HUD scoreboard, title + game-over. |
| [`AudioEngine.cs`](../../Source/Alaloa/Alaloa/Game/AudioEngine.cs) | NAudio voices (desktop) + JS interop bridge (WASM). Inherits plumbing from `Arcade.Common.Audio.AudioEngineBase`. |
| [`Platforms/WebAssembly/WasmScripts/audio.js`](../../Source/Alaloa/Alaloa/Platforms/WebAssembly/WasmScripts/audio.js) | Web Audio mirror — gesture-gated AudioContext, same voice set. |
| [`BackgroundSurface.cs`](../../Source/Alaloa/Alaloa/BackgroundSurface.cs) | Thin wrapper over `Arcade.Common.AmbientStarBackdrop`. |
| [`MainPage.xaml`](../../Source/Alaloa/Alaloa/MainPage.xaml) / [`.cs`](../../Source/Alaloa/Alaloa/MainPage.xaml.cs) | Viewbox layout + edge-triggered turn input + render loop. |

Shared chassis (neon paints, glyph font, marquee, gradient backdrop, playfield border, HUD text, `Vec2`, `HighScoreStore`, audio base) is included from `Source/Common/` via the csproj's `<Compile>` glob.

World coordinates are fixed at `720 × 720` square. The arena grid is `90 × 90` at `8px` cells. Continuous cycle position + per-cell collision gives crisp turns and pixel-clean collision without any line-segment intersection math.

## Stack

- Uno Platform (`SkiaRenderer` UnoFeature) + `Uno.WinUI.Graphics2DSK.SKCanvasElement`
- SkiaSharp 4.147.0-preview.3.1
- .NET 10 — `net10.0-desktop`, `net10.0-browserwasm`
- NAudio 2.x for desktop audio (Windows-only; guarded by `HAS_NAUDIO`)
