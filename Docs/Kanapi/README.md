# Kanapi

A Centipede-style vector shooter in the same neon style as the rest of the repo. Built on Uno Platform + SkiaSharp 4, targeting `net10.0-desktop` and `net10.0-browserwasm`.

> **Kanapī** — Hawaiian for "centipede" — the most literal name on the menu. The mushroom field becomes a glowing reef of coral-tipped neon caps, the centipede a chain of bright green segments snaking down toward you.

## What it does

- **Mushroom grid** — a 30×30 field of 4-HP mushrooms regenerated each level, drawn as a glowing cap + stem with petal dots that drop off as the mushroom takes damage. Mushrooms block the centipede (forcing it to drop a row and reverse) and block the player's movement in the bottom zone.
- **Centipede chain** — head + body segments that snake horizontally across the mushroom field. Hitting a mushroom or wall makes the head bounce down one row and reverse direction. Shoot a body segment and the chain splits in two at that point; shoot the head and the next segment behind becomes the new head. Each killed segment drops a mushroom at its cell.
- **Player blaster** — small triangular ship in the bottom 8 rows of the field with 4-direction movement and an auto-fire blaster that fires straight up. Movement is blocked by mushrooms in the player zone.
- **Spiders** — bounce diagonally through the player zone every ~8 seconds, eating any mushrooms they cross. Worth 300/600/900 depending on how close they are when you take them out.
- **Scoring** — 1 per mushroom, 10 per body segment, 100 per head, 300–900 per spider. Killing the last centipede segment advances the level (faster centipedes, denser mushroom field).
- **Procedural audio** — shoot bleep, mushroom thunk, segment crunch (filtered noise + sub-bass), spider whoosh, descending player-death drone. Desktop NAudio + WASM Web Audio mirror.
- **Attract mode** after 12s idle on the title screen with simple homing AI.
- **Persistent high score** in `%LocalAppData%\Kanapi\` on desktop.
- **Same chassis as the other demos**: square Viewbox (720×720 logical), ambient `BackgroundSurface` starfield in the side bars, thin neon playfield border, perspective-tilted scrolling marquee. All chassis pieces come from `Source/Common/`.

## Run

```powershell
.\Builds\Run-Kanapi.ps1                          # desktop, Release
.\Builds\Run-Kanapi.ps1 -Configuration Debug     # desktop, Debug
.\Builds\Run-Kanapi.ps1 -Wasm                    # browser-wasm
```

Or directly: `dotnet run --project Source/Kanapi/Kanapi/Kanapi.csproj -f net10.0-desktop`.

## Controls

| Key | Action |
|---|---|
| Arrows or WASD | Move (4-direction, confined to bottom zone) |
| Space | Auto-fire (start game from title / game over) |
| Click / Tap | Start game from title / exit attract |

## Architecture

| File | Role |
|---|---|
| [`Grid.cs`](../../Source/Kanapi/Kanapi/Game/Grid.cs) | `MushroomGrid` (30×30 cells) + cell↔world helpers + density-based level generation. |
| [`Entities.cs`](../../Source/Kanapi/Kanapi/Game/Entities.cs) | `Player`, `Mushroom`, `CentipedeSegment`, `CentipedeChain`, `Spider`, `Bullet`, `Particle`, `ScorePopup`. |
| [`GameWorld.cs`](../../Source/Kanapi/Kanapi/Game/GameWorld.cs) | State machine + physics. Centipede grid AI (continue / bounce / split), spider movement, collision, scoring, attract AI. |
| [`Renderer.cs`](../../Source/Kanapi/Kanapi/Game/Renderer.cs) | Game-specific draws: mushroom field (HP-aware), centipede chains (head + body + eyes), spider (8-leg animated), player blaster, bullets, HUD. |
| [`AudioEngine.cs`](../../Source/Kanapi/Kanapi/Game/AudioEngine.cs) | NAudio voices (desktop) + JS interop bridge (WASM). All plumbing inherited from `Arcade.Common.Audio.AudioEngineBase`. |
| [`Platforms/WebAssembly/WasmScripts/audio.js`](../../Source/Kanapi/Kanapi/Platforms/WebAssembly/WasmScripts/audio.js) | Web Audio mirror — gesture-gated AudioContext, same voice set. |
| [`BackgroundSurface.cs`](../../Source/Kanapi/Kanapi/BackgroundSurface.cs) | Thin wrapper over `Arcade.Common.AmbientStarBackdrop`. |
| [`MainPage.xaml`](../../Source/Kanapi/Kanapi/MainPage.xaml) / [`.cs`](../../Source/Kanapi/Kanapi/MainPage.xaml.cs) | Viewbox layout + 4-direction input + render loop. |

Shared chassis (neon paints, glyph font, marquee, gradients, playfield border, HUD text, `Vec2`, `HighScoreStore`, audio base) is included from `Source/Common/` via the csproj's `<Compile>` glob.

World coordinates are fixed at `720 × 720` square. The mushroom grid is `30 × 30` at `24px` cells. Player roam is rows 22-29 (bottom ~26% of the playfield).

## Stack

- Uno Platform (`SkiaRenderer` UnoFeature) + `Uno.WinUI.Graphics2DSK.SKCanvasElement`
- SkiaSharp 4.147.0-preview.3.1
- .NET 10 — `net10.0-desktop`, `net10.0-browserwasm`
- NAudio 2.x for desktop audio (Windows-only; guarded by `HAS_NAUDIO`)
