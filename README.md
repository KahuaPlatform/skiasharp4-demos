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
| [Paku](Docs/Paku/README.md) | net10.0 (wasm / desktop) | Agar.io-style cell-absorption arena with plasma background, mass-ejection thrust, progressive AI hunters, zooming camera. | Working |
| [Kiai](Docs/Kiai/DESIGN.md) | net10.0 (wasm / desktop) | Defender-style horizontally-wrapping shooter — patrol a toroidal world, gun down landers before they abduct humanoids, catch falling humanoids, ship-centred radar strip, smart bombs + hyperspace, attract mode. | Working |
| [Koa](Docs/Koa/DESIGN.md) | net10.0 (wasm / desktop) | Gauntlet-style top-down dungeon crawl — 8-way wall-sliding movement, destructible generators spawning hordes, flow-field swarm AI, draining-health clock, food / potions / treasure, multi-level progression, attract mode. | Working |
| [Launcher](Docs/Launcher/README.md) | net10.0 (wasm / desktop) | Unified neon catalog landing page — card grid of every demo with hover effects + click-to-navigate. Designed for a published static site where each game sits at `/games/<name>/`. | Working |
| [Uno3dViewer](Source/Uno3dViewer/) | net10.0 (desktop only) | OpenGL 3D model viewer using Silk.NET + Assimp, rendered into Uno's `GLCanvasElement`. | Working |

Each demo's full per-demo docs live in [Docs/](Docs/). The original source repos these were imported from (`UnoGallery`, `Pohaku`, `ProjectNebula`, `Uno3dViewer`) remain untouched at sibling paths under `C:\Repos\`; you can retire them once you've verified the consolidated copies.

## Documentation

- **[Docs/Architecture/](Docs/Architecture/README.md)** — cross-cutting architecture reference: demo anatomy, shared chassis, rendering pipeline, audio, build/deploy, launcher dispatch. Each doc has Mermaid diagrams; start with the [README](Docs/Architecture/README.md) for the reading order.
- **[Docs/<Demo>/README.md](Docs/)** — per-demo docs (mechanics, controls, file map). One per demo.

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
│   ├── Paku/
│   ├── Kiai/
│   ├── Koa/
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
- **Uno Platform workloads** — `dotnet workload install uno` once per machine. The Uno SDK version (`Uno.Sdk 6.7.0-dev.164`) is pinned in each demo's `global.json`.
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

### Launcher + unified static-site deploy

[`Builds/Publish-Site.ps1`](Builds/Publish-Site.ps1) publishes the launcher and every wasm game into a single static-site layout (`publish/site/` with games at `/games/<slug>/`). Drop the output on any plain HTTP host. Full details (path-rewriting, local serve, service-worker hygiene) live in [Docs/Launcher/README.md](Docs/Launcher/README.md).

```powershell
.\Builds\Publish-Site.ps1
python -m http.server 8080 --directory .\publish\site
```

For sub-second tile-launch on the desktop launcher, build every game Release first so the launcher resolves their prebuilt exes instead of falling back to `dotnet run`:

```powershell
.\Builds\Build-All.ps1 -Configuration Release
.\Builds\Run-Launcher.ps1
```

You can also invoke `dotnet` directly. Each demo's csproj lives at `Source/<Demo>/<Demo>/<Demo>.csproj`:

```powershell
dotnet build Source/UnoGallery/UnoGallery/UnoGallery.csproj -c Release -f net10.0-desktop
dotnet run --project Source/Pohaku/Pohaku/Pohaku.csproj -f net10.0-browserwasm
```

## Adding a new demo

1. Create `Source/<NewDemo>/<NewDemo>/<NewDemo>.csproj` plus `Directory.Build.props`, `Directory.Packages.props`, `global.json`, and `<NewDemo>.sln` in `Source/<NewDemo>/`. Don't add anything build-related at the repo root — that would cascade into the other demos' MSBuild trees and break their isolation.
2. Add `Builds/Build-<NewDemo>.ps1` and `Builds/Run-<NewDemo>.ps1` following the existing pattern.
3. Append the script name to the `$scripts` array in `Builds/Build-All.ps1`.
4. Add an `Entry(...)` to [`Source/Launcher/Launcher/Game/GameCatalog.cs`](Source/Launcher/Launcher/Game/GameCatalog.cs) so the launcher shows a tile for it, and append the slug to the `$games` array in [`Builds/Publish-Site.ps1`](Builds/Publish-Site.ps1) so it gets bundled into the static site.
5. Add `Docs/<NewDemo>/README.md`.

The fastest way is to copy an existing demo as a starting point (Pohaku if you want vector+neon, UnoGallery if you want the full SKCanvasElement scene + effects pipeline).

## Stack notes per demo

The demos deliberately use different versions and feature sets — that's the point of the repo. Don't try to unify SkiaSharp versions or Uno features across them.

- UnoGallery uses a `$(SkiaSharpVersion)`-gated build (defaults to SkiaSharp 3.119.4 stable; pass `-p:SkiaSharpVersion=4.151.0` to build the v4 path — but its uniforms-bearing SKSL effects stay disabled there; see [SkiaSharp 4 limitations](#skiasharp-4-limitations) below)
- Pohaku, HokuLele, Lua, Mahina, Heiau, Kanapi, Alaloa, Hahai, Paku, Kiai, Koa, and Launcher pin SkiaSharp 4.151.0
- KahuaNetwork uses SkiaSharp 3.119.4 + `Uno.WinUI.Graphics2DSK`
- Uno3dViewer adds Silk.NET (OpenGL + Assimp) and uses `<UnoFeatures>...GLCanvas</UnoFeatures>`

All fifteen share `Uno.Sdk 6.7.0-dev.164` as the MSBuild SDK. Eleven of them (HokuLele, Lua, Mahina, Heiau, Kanapi, Alaloa, Hahai, Paku, Kiai, Koa, Launcher) share a neon-game chassis from `Source/Common/` (see [Source/Common](Source/Common/)).

## SkiaSharp 4 limitations

Most demos run **SkiaSharp 4.151.0** — as of that release SkiaSharp 4 is a **stable** line, not a preview. **UnoGallery** is still the exception, and it documents the one thing that kept SkiaSharp 4 from being universal here: its programmable **SKSL runtime-effect** pipeline hit a native crash, so it defaults to **SkiaSharp 3.119.4** and gates the v4 path behind a build property.

### The crash

Measured on **SkiaSharp 4.147.0-preview.3.1**: any uniforms-bearing SKSL shader — `SKRuntimeShaderBuilder`, or `SKRuntimeEffect.CreateShader(...)` followed by `ToShader(uniforms, children)` — threw an **`AccessViolation` inside native `sk_runtimeeffect_get_uniform_byte_size` on the first frame**. It built cleanly; it died at runtime the moment a uniform block was constructed.

| SKSL path | v3 (3.119.4) | v4.147.0-preview.3.1 |
|---|---|---|
| Uniforms-bearing shader (`ToShader(uniforms, …)`) | ✅ | ❌ AccessViolation |
| Parameterless color filter (`ToColorFilter()`) | ✅ | ✅ |

The parameterless `ToColorFilter()` route survived — which is why UnoGallery's tone-grade pass works on both versions while its six uniform-driven SKSL effects (plasma, dissolve, iris, chroma-shift, hover-glow, …) are v3-only.

### Status on 4.151.0: unverified

**The crash has not been re-tested on 4.151.0 inside the Uno host, so treat the table above as a 4.147-preview measurement, not a current one.**

An isolated console probe — bare `SkiaSharp` + `SkiaSharp.NativeAssets.Win32`, no Uno, CPU raster surface — exercised `SKRuntimeEffectUniforms`, `ToShader(uniforms)`, and `SKRuntimeShaderBuilder.Build()` and **passed on both 4.147.0-preview.3.1 and 4.151.0**. Because the control did not reproduce, that probe says nothing about whether 4.151.0 fixes anything; what it does show is that the trigger is not the bare managed API. It needs something the console probe lacks — most likely Uno's GPU-backed surface, or the specific uniform layouts in UnoGallery's shaders.

Settling it means flipping the `#if SKIA_V4` gates in `Shaders/ShaderLibrary.cs` and `Data/ProceduralSampleSource.cs`, building UnoGallery at `-p:SkiaSharpVersion=4.151.0`, and running it. That's a source change plus a manual visual pass, so it hasn't been done.

### How UnoGallery handles it

- Defaults to **SkiaSharp 3.119.4 stable**; opt into v4 with `dotnet build -p:SkiaSharpVersion=4.151.0`.
- `Directory.Build.props` defines an `SKIA_V4` compile constant when the version starts with `4.`; uniforms-bearing shaders load only when `!SKIA_V4`, and consumers fall back to non-SKSL primitives (the ambient plasma becomes a dual radial gradient, etc.).
- When `SkiaSharpVersion` names a 4.x build, `Directory.Packages.props` force-pins the transitive `SkiaSharp.*` and `HarfBuzzSharp.*` packages. **`HarfBuzzSharp` tracks a version line of its own** — SkiaSharp 4.151.0 declares HarfBuzzSharp **14.2.1**, where the 4.147 previews wanted 8.3.1.6-preview.3.1. Read the target `SkiaSharp.HarfBuzz` nuspec when changing versions; the numbers are not comparable across the two lines.
- The v4-only APIs that *would* justify moving UnoGallery (`SKPathBuilder`, `SKSamplingOptions`, the new `DrawImage` overloads) are already available in SkiaSharp 3.116+, so there is still no forcing reason to switch its default.

Full audit: [Docs/UnoGallery/DESIGN.md](Docs/UnoGallery/DESIGN.md) §2.2–2.3.
