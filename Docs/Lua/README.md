# Lua

A Tempest-style vector shooter in the same vector + neon style as [Pohaku](../Pohaku/README.md) and [HokuLele](../HokuLele/README.md). Built on Uno Platform + SkiaSharp 4, targeting `net10.0-desktop` and `net10.0-browserwasm`.

> **Lua** — Hawaiian for "pit" or "well" — fits the original Tempest: a luminous vector tube you defend by walking the rim and shooting down the well.

## What it does

- **3D well projection.** A polyline rim (16 segments per level, depending on shape) is projected toward a vanishing point at the centre. Player walks the rim, enemies climb out of the well at the player.
- **10 well shapes** that cycle as you advance — circle, square, plus, V, bowtie, triangle, trapezoid, step, heart, infinity loop. Each level rebuilds the well geometry; level number increments forever.
- **4 enemy types:**
  | Enemy | Behavior | Score |
  |---|---|---|
  | Flipper | Climbs a single segment, occasionally flips to adjacent ones, walks the rim toward the player on arrival. | 150 |
  | Tanker | Heavier, climbs slower. On hit OR when reaching the rim, splits into two Flippers. | 100 (+ 2x Flippers) |
  | Spiker | Stays in one segment, climbs from the far end while extending a green spike along the well. Spikes kill you during warp. Retreats before reaching the rim. | 50 |
  | Fuseball | Animated energy ball that bounces along segment edges. Faster than other enemies; chaotic motion. | 250 |
- **Super Zapper.** 2 uses per level — first clears every enemy on screen, second kills one random enemy. Press `Z`.
- **Warp transition between levels.** Stars accelerate, the player zooms down the well at the vanishing point. Any spikes left in the player's column will hit during warp.
- **Procedural audio.** Shoot, explosion, flipper-flip click, super zapper sweep, warp whoosh — all synthesised in code, no audio assets bundled. Desktop uses NAudio; WASM uses Web Audio via JS interop.
- **Attract mode** after 12s idle on the title screen — autoplaying AI demos the game.
- **Persistent high score** via file in `%LocalAppData%\Lua\` on desktop (WASM is in-memory only).
- **Branding.** Title screen shows the Uno Platform mark (rendered from SVG path data) alongside the Kahua snowflake (embedded high-res PNG).

## Run

```powershell
.\Builds\Run-Lua.ps1                          # desktop, Release
.\Builds\Run-Lua.ps1 -Configuration Debug     # desktop, Debug
.\Builds\Run-Lua.ps1 -Wasm                    # browser-wasm
```

Or directly: `dotnet run --project Source/Lua/Lua/Lua.csproj -f net10.0-desktop`.

## Controls

| Key | Action |
|---|---|
| Arrows / A-D | Rotate left / right around the rim |
| Space / Enter | Fire (or start game from title / game-over) |
| Z / Shift | Super Zapper (2 uses per level) |
| Click / Tap | Start game from title |
| K | Cheat — toggle the 8-bullet cap |

## Architecture

| File | Role |
|---|---|
| [`WellGeometry.cs`](../../Source/Lua/Lua/Game/WellGeometry.cs) | Defines `Well` (rim polyline + perspective projection) and `Wells.Build()` for the 10 shapes. |
| [`Entities.cs`](../../Source/Lua/Lua/Game/Entities.cs) | `Player`, `Enemy` (+ `EnemyKind`, `EnemyState`), `Bullet`, `Spike`, `Particle`, `ScorePopup`. |
| [`GameWorld.cs`](../../Source/Lua/Lua/Game/GameWorld.cs) | State machine + per-frame `Update()`. Drives spawning, enemy AI, collisions, warp, attract mode. |
| [`Renderer.cs`](../../Source/Lua/Lua/Game/Renderer.cs) | Neon paint chassis (carried over from Pohaku/HokuLele) + Tempest-specific draws (well, claw, enemies, spikes, warp). |
| [`AudioEngine.cs`](../../Source/Lua/Lua/Game/AudioEngine.cs) | Procedural NAudio synth voices (desktop) + JS interop bridge (WASM). |
| [`Platforms/WebAssembly/WasmScripts/audio.js`](../../Source/Lua/Lua/Platforms/WebAssembly/WasmScripts/audio.js) | Web Audio voices mirroring the NAudio ones. |
| [`BackgroundSurface.cs`](../../Source/Lua/Lua/BackgroundSurface.cs) | Thin wrapper over `Arcade.Common.AmbientStarBackdrop`. |
| [`MainPage.xaml.cs`](../../Source/Lua/Lua/MainPage.xaml.cs) | Input + render loop. |

High-score persistence is shared chassis ([`Source/Common/HighScoreStore.cs`](../../Source/Common/HighScoreStore.cs)), source-included via the csproj `<Compile>` glob — there is no per-game copy.

The world coordinates are fixed at `720 × 1280` (portrait 9:16); the renderer letterboxes onto whatever canvas size it gets. The desktop window is sized to `810 × 1440` in [`App.xaml.cs`](../../Source/Lua/Lua/App.xaml.cs).

## Stack

- Uno Platform (`SkiaRenderer` UnoFeature) + `Uno.WinUI.Graphics2DSK.SKCanvasElement`
- SkiaSharp 4.151.0 (pinned via the `$(SkiaSharpVersion)` MSBuild property)
- .NET 10 — `net10.0-desktop`, `net10.0-browserwasm`
- NAudio 2.x for desktop audio (Windows-only; guarded by `HAS_NAUDIO`)
