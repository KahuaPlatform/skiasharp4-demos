# UnoSkiaDemos

A consolidated home for several [Uno Platform](https://platform.uno) + [SkiaSharp](https://github.com/mono/SkiaSharp) demos. Each demo is fully self-contained under `Source/<DemoName>/` with its own `.sln`, `Directory.Build.props`, `Directory.Packages.props`, and `global.json` — deliberately, because the demos use different SkiaSharp versions and feature sets. There is no shared root build infrastructure and no aggregator `.sln`.

## The demos

| Demo | TFMs | What it shows | Status |
|---|---|---|---|
| [UnoGallery](Docs/UnoGallery/README.md) | net10.0 (android / ios / wasm / desktop) | 30-tile image gallery with 16 live procedural tiles (Conway, Boids, Reaction-Diffusion, FFT, Lorenz, etc.), SKSL post-processing, EXIF-aware folder loader, microphone-reactive ambient effects. The "kitchen-sink" demo. | Working |
| [Pohaku](Docs/Pohaku/README.md) | net10.0 (wasm / desktop) | Vector Asteroids clone with retro and neon-glow visual modes, perspective-tilted scrolling marquee, SkiaSharp 4 `SKPathBuilder` patterns. | Working |
| [KahuaNetwork](Docs/KahuaNetwork/README.md) | net10.0 (wasm / desktop) | Holographic 3D city of glowing towers connected by document-exchange data streams, with a "global view" particle-explode-into-topology-graph effect. | Working |
| [HokuLele](Docs/HokuLele/README.md) | net10.0 (wasm / desktop) | Galaga-style vector shooter — authentic 5-row formation, multi-flight entries, dive choreographies, tractor-beam captures + dual-fighter rescue, challenge stages, mothership/snowflake brand enemies, procedural audio, attract mode, persistent high score. | Working |
| [Lua](Docs/Lua/README.md) | net10.0 (wasm / desktop) | Tempest-style vector well-shooter — 10 well shapes (circle / square / plus / V / bowtie / triangle / trapezoid / step / heart / infinity), 4 enemy types (Flippers / Tankers / Spikers / Fuseballs) with spike trails + rim-flipping AI, Super Zapper, warp transition, attract mode. | Working |
| [Mahina](Docs/Mahina/README.md) | net10.0 (wasm / desktop) | Lunar-Lander-style vector physics game — Apollo LM silhouette, midpoint-displaced terrain with x2/x3/x5 pad multipliers, gravity + thrust + fuel, looping rocket-rumble audio + touchdown chime, per-level difficulty curve, attract mode. | Working |
| [Heiau](Docs/Heiau/README.md) | net10.0 (wasm / desktop) | Star-Castle-style ring shooter — central pohaku turret with tracking barrel, three counter-rotating segmented energy rings, Asteroids-style player ship, per-level speed escalation, attract mode. | Working |
| [Kanapi](Docs/Kanapi/README.md) | net10.0 (wasm / desktop) | Centipede-style vector shooter — 30×30 mushroom grid, centipede chain that snakes + bounces + splits on body hits, zigzagging spiders that eat mushrooms, 4-direction player blaster with auto-fire, attract mode. | Working |
| [Alaloa](Docs/Alaloa/README.md) | net10.0 (wasm / desktop) | Tron-Light-Cycles-style duel — 4 cycles spawn from cardinal edges, continuous motion with 90° turns, per-cell trail-vs-anything collision, look-ahead bot AI, best-of-5 match scoring. | Working |
| [Hahai](Docs/Hahai/README.md) | net10.0 (wasm / desktop) | Pac-Man-style chase — Honu (sea turtle) eats limu pellets and lehua power flowers while four colored Mo'o (water-spirit lizards) pursue, with classic scatter/chase phases and eyes-return-to-house on devour. | Working |
| [Launcher](Docs/Launcher/README.md) | net10.0 (wasm / desktop) | Unified neon catalog landing page — card grid of every demo with hover effects + click-to-navigate. Designed for a published static site where each game sits at `/games/<name>/`. | Working |
| [Uno3dViewer](Source/Uno3dViewer/) | net10.0 (desktop only) | OpenGL 3D model viewer using Silk.NET + Assimp, rendered into Uno's `GLCanvasElement`. | Working |

Each demo's full per-demo docs live in [Docs/](Docs/). The original source repos these were imported from (`UnoGallery`, `Pohaku`, `ProjectNebula`, `Uno3dViewer`) remain untouched at sibling paths under `C:\Repos\`; you can retire them once you've verified the consolidated copies.

## Layout

```
UnoSkiaDemos/
├── Source/                  Per-demo solution folders, each self-contained
│   ├── UnoGallery/
│   ├── Pohaku/
│   ├── KahuaNetwork/
│   ├── HokuLele/
│   ├── Lua/
│   ├── Mahina/
│   ├── Heiau/
│   ├── Kanapi/
│   ├── Alaloa/
│   ├── Hahai/
│   ├── Launcher/
│   └── Uno3dViewer/
├── Docs/                    Per-demo READMEs, design docs, screenshots
│   └── <DemoName>/
├── Builds/                  PowerShell Build-/Run- scripts
├── .workspaces/             Linked-workspace tooling
├── .github/                 Org/AI instructions
├── .vscode/                 Workspace settings
├── .editorconfig            (per-demo .editorconfig files are also preserved)
├── .gitignore               (root + per-demo)
└── .gitattributes           LFS-configured for binary asset types
```

## Prerequisites

- **.NET 10 SDK** — all demos target `net10.0`. `dotnet --list-sdks` should show `10.0.x`.
- **Uno Platform workloads** — `dotnet workload install uno` once per machine. The Uno SDK version (`Uno.Sdk 6.7.0-dev.64`) is pinned in each demo's `global.json`.
- **Visual Studio 2022 17.12+ or VS Code with C# Dev Kit** — both work; demos build cleanly from the CLI without an IDE.
- **For wasm builds**: emscripten is downloaded automatically by the Uno SDK on first `net10.0-browserwasm` build (`~/.uno/emsdk/`). First-time builds take a few minutes; subsequent builds are fast.
- **Git + Git LFS** — `.gitattributes` LFS-tracks binary asset types. Run `git lfs install` once per machine after cloning.

## Build and run

The `Builds/` folder has one Build- and one Run- script per demo, plus a `Build-All` aggregator. All scripts accept:

- `-Configuration <Debug|Release>` (default: `Release`)
- `-Wasm` (switch) — target `net10.0-browserwasm` instead of `net10.0-desktop`

```powershell
# Build everything (Release, desktop)
.\Builds\Build-All.ps1

# Build everything (Debug, wasm — skips Uno3dViewer which is desktop-only)
.\Builds\Build-All.ps1 -Configuration Debug -Wasm

# Build a single demo (Release, desktop)
.\Builds\Build-UnoGallery.ps1

# Build a single demo (Debug, wasm)
.\Builds\Build-Pohaku.ps1 -Configuration Debug -Wasm

# Run a single demo (Release, desktop)
.\Builds\Run-HokuLele.ps1

# Run a single demo in wasm — opens a local dev server
.\Builds\Run-KahuaNetwork.ps1 -Wasm
```

`Build-All -Wasm` skips Uno3dViewer (no browserwasm TFM); the summary at the end lists what was skipped.

You can also invoke `dotnet` directly. Each demo's csproj lives at `Source/<Demo>/<Demo>/<Demo>.csproj`:

```powershell
dotnet build Source/UnoGallery/UnoGallery/UnoGallery.csproj -c Release -f net10.0-desktop
dotnet run --project Source/Pohaku/Pohaku/Pohaku.csproj -f net10.0-browserwasm
```

## Adding a new demo

1. Create `Source/<NewDemo>/<NewDemo>/<NewDemo>.csproj` plus `Directory.Build.props`, `Directory.Packages.props`, `global.json`, and `<NewDemo>.sln` in `Source/<NewDemo>/`. Don't add anything build-related at the repo root — that would cascade into the other demos' MSBuild trees and break their isolation.
2. Add `Builds/Build-<NewDemo>.ps1` and `Builds/Run-<NewDemo>.ps1` following the existing pattern.
3. Append the script name to the `$scripts` array in `Builds/Build-All.ps1`.
4. Add `Docs/<NewDemo>/README.md`.

The fastest way is to copy an existing demo as a starting point (Pohaku if you want vector+neon, UnoGallery if you want the full SKCanvasElement scene + effects pipeline).

## Stack notes per demo

The demos deliberately use different versions and feature sets — that's the point of the repo. Don't try to unify SkiaSharp versions or Uno features across them.

- UnoGallery uses a `$(SkiaSharpVersion)`-gated build (defaults to SkiaSharp 3.119.4 stable; pass `-p:SkiaSharpVersion=4.147.0-preview.3.1` to test the v4 preview)
- Pohaku, HokuLele, Lua, Mahina, Heiau, Kanapi, and Alaloa pin SkiaSharp 4.147.0-preview.3.1
- KahuaNetwork uses SkiaSharp 3.119.4 + `Uno.WinUI.Graphics2DSK`
- Uno3dViewer adds Silk.NET (OpenGL + Assimp) and uses `<UnoFeatures>...GLCanvas</UnoFeatures>`

All ten share `Uno.Sdk 6.7.0-dev.64` as the MSBuild SDK. Six of them (HokuLele, Lua, Mahina, Heiau, Kanapi, Alaloa) share a neon-game chassis from `Source/Common/` (see [Source/Common](Source/Common/)).
