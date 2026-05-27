# UnoSkiaDemos

A consolidated home for several [Uno Platform](https://platform.uno) + [SkiaSharp](https://github.com/mono/SkiaSharp) demos. Each demo is fully self-contained under `Source/<DemoName>/` with its own `.sln`, `Directory.Build.props`, `Directory.Packages.props`, and `global.json` — deliberately, because the demos use different SkiaSharp versions and feature sets. There is no shared root build infrastructure and no aggregator `.sln`.

## The demos

| Demo | TFMs | What it shows | Status |
|---|---|---|---|
| [UnoGallery](Docs/UnoGallery/README.md) | net10.0 (android / ios / wasm / desktop) | 30-tile image gallery with 16 live procedural tiles (Conway, Boids, Reaction-Diffusion, FFT, Lorenz, etc.), SKSL post-processing, EXIF-aware folder loader, microphone-reactive ambient effects. The "kitchen-sink" demo. | Working |
| [UnoAsteroids](Docs/UnoAsteroids/README.md) | net10.0 (wasm / desktop) | Vector Asteroids clone with retro and neon-glow visual modes, perspective-tilted scrolling marquee, SkiaSharp 4 `SKPathBuilder` patterns. | Working |
| [KahuaNetwork](Docs/KahuaNetwork/README.md) | net10.0 (wasm / desktop) | Holographic 3D city of glowing towers connected by document-exchange data streams, with a "global view" particle-explode-into-topology-graph effect. | Working |
| [UnoGalaga](Docs/UnoGalaga/README.md) | net10.0 (wasm / desktop) | Vertical-shooter scaffold in the UnoAsteroids vector + neon style. Title screen + player movement; enemy formations / dive logic not implemented yet. | Scaffold |
| [Uno3dViewer](Source/Uno3dViewer/) | net10.0 (desktop only) | OpenGL 3D model viewer using Silk.NET + Assimp, rendered into Uno's `GLCanvasElement`. | Working |

Each demo's full per-demo docs live in [Docs/](Docs/). The original source repos these were imported from (`UnoGallery`, `UnoAsteroids`, `ProjectNebula`, `Uno3dViewer`) remain untouched at sibling paths under `C:\Repos\`; you can retire them once you've verified the consolidated copies.

## Layout

```
UnoSkiaDemos/
├── Source/                  Per-demo solution folders, each self-contained
│   ├── UnoGallery/
│   ├── UnoAsteroids/
│   ├── KahuaNetwork/
│   ├── UnoGalaga/
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
.\Builds\Build-UnoAsteroids.ps1 -Configuration Debug -Wasm

# Run a single demo (Release, desktop)
.\Builds\Run-UnoGalaga.ps1

# Run a single demo in wasm — opens a local dev server
.\Builds\Run-KahuaNetwork.ps1 -Wasm
```

`Build-All -Wasm` skips Uno3dViewer (no browserwasm TFM); the summary at the end lists what was skipped.

You can also invoke `dotnet` directly. Each demo's csproj lives at `Source/<Demo>/<Demo>/<Demo>.csproj`:

```powershell
dotnet build Source/UnoGallery/UnoGallery/UnoGallery.csproj -c Release -f net10.0-desktop
dotnet run --project Source/UnoAsteroids/UnoAsteroids/UnoAsteroids.csproj -f net10.0-browserwasm
```

## Adding a new demo

1. Create `Source/<NewDemo>/<NewDemo>/<NewDemo>.csproj` plus `Directory.Build.props`, `Directory.Packages.props`, `global.json`, and `<NewDemo>.sln` in `Source/<NewDemo>/`. Don't add anything build-related at the repo root — that would cascade into the other demos' MSBuild trees and break their isolation.
2. Add `Builds/Build-<NewDemo>.ps1` and `Builds/Run-<NewDemo>.ps1` following the existing pattern.
3. Append the script name to the `$scripts` array in `Builds/Build-All.ps1`.
4. Add `Docs/<NewDemo>/README.md`.

The fastest way is to copy an existing demo as a starting point (UnoAsteroids if you want vector+neon, UnoGallery if you want the full SKCanvasElement scene + effects pipeline).

## Stack notes per demo

The demos deliberately use different versions and feature sets — that's the point of the repo. Don't try to unify SkiaSharp versions or Uno features across them.

- UnoGallery uses a `$(SkiaSharpVersion)`-gated build (defaults to SkiaSharp 3.119.4 stable; pass `-p:SkiaSharpVersion=4.147.0-preview.3.1` to test the v4 preview)
- UnoAsteroids and UnoGalaga pin SkiaSharp 4.147.0-preview.3.1
- KahuaNetwork uses SkiaSharp 3.119.4 + `Uno.WinUI.Graphics2DSK`
- Uno3dViewer adds Silk.NET (OpenGL + Assimp) and uses `<UnoFeatures>...GLCanvas</UnoFeatures>`

All five share `Uno.Sdk 6.7.0-dev.64` as the MSBuild SDK.
