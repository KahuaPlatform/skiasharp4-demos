# UnoGalaga

A vertical-shooter demo scaffold in the same vector + neon style as [UnoAsteroids](../UnoAsteroids/README.md). Built on Uno Platform + SkiaSharp, targeting `net10.0-desktop` and `net10.0-browserwasm`.

## Status

**Scaffold only.** What's in place right now:

- Project structure mirrored from UnoAsteroids
- Neon paints + perspective-tilted marquee + vector glyph font carried over verbatim from UnoAsteroids' Renderer
- Title screen rendering: hue-cycling "UNOGALAGA" rendered with the vector font, subtitle, and the marquee scrolling at the bottom
- Player ship at the bottom of the playfield, moves left/right on Arrow/A-D, fires on Space
- Skeleton `Player` / `Enemy` / `Bullet` / `Particle` types and an empty `GameWorld` that ticks them
- Build/run scripts in `Builds/` (desktop + `-Wasm` switch)

Not yet implemented:

- Enemy formations, dive attacks, AI
- Wave / level progression
- Collision detection
- Scoring, lives display, game-over flow
- Sound

Where to start adding gameplay:

| Where | Lives in |
|---|---|
| Spawn formations | [`GameWorld.cs`](../../Source/UnoGalaga/UnoGalaga/Game/GameWorld.cs) |
| Enemy AI / dive curves | [`GameWorld.cs`](../../Source/UnoGalaga/UnoGalaga/Game/GameWorld.cs) + extend [`Enemy`](../../Source/UnoGalaga/UnoGalaga/Game/Entities.cs) |
| Enemy silhouettes | [`Renderer.DrawWorld`](../../Source/UnoGalaga/UnoGalaga/Game/Renderer.cs) — switch on `Enemy.Kind` and draw distinct neon vector shapes |
| Collision + scoring | `GameWorld.Update` after `UpdateEntities` |
| Input | [`MainPage.xaml.cs`](../../Source/UnoGalaga/UnoGalaga/MainPage.xaml.cs) |

## Run

```powershell
.\Builds\Run-UnoGalaga.ps1                          # desktop, Release
.\Builds\Run-UnoGalaga.ps1 -Configuration Debug     # desktop, Debug
.\Builds\Run-UnoGalaga.ps1 -Wasm                    # browser-wasm
```

Or directly: `dotnet run --project Source/UnoGalaga/UnoGalaga/UnoGalaga.csproj -f net10.0-desktop`.

## Controls

| Key | Action |
|---|---|
| Arrows / A-D | Move left / right |
| Space / Enter | Fire (or start game from title) |
| Click / Tap | Start game from title |

## Stack

- Uno Platform (`SkiaRenderer` UnoFeature) + `Uno.WinUI.Graphics2DSK.SKCanvasElement`
- SkiaSharp 4.147.0-preview.3.1 (inherited from UnoAsteroids' csproj pin)
- .NET 10 — targets `net10.0-desktop`, `net10.0-browserwasm`
