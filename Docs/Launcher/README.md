# Launcher

A unified catalog landing page for every demo in the repo. Renders a card grid where each tile shows a game's name, Hawaiian-meaning gloss, original-arcade attribution, one-line tagline, and a hover-aware Play hint; clicking a tile launches that game. Same chassis (`Source/Common/`) as the games themselves — `Uno.WinUI.Graphics2DSK.SKCanvasElement` + SkiaSharp 4 + the neon paint stack. Targets `net10.0-desktop` and `net10.0-browserwasm`.

## What it does

- **Card grid** built from [`GameCatalog.cs`](../../Source/Launcher/Launcher/Game/GameCatalog.cs) — one row of metadata per demo (`Name`, `Gloss`, `OriginalGame`, `Description`, `Color`, `WasmPath`, `FolderName`). Add an entry to add a tile; layout flows on a 4-column grid.
- **Two themes**, toggled with the **T** key:
  - **Neon** (default) — deep-space gradient + glowing card borders in each game's accent color + perspective-tilted scrolling marquee, matching the games' look.
  - **Bob Ross** — painted Hawaiian sunset: pastel sky with happy little cumulus clouds, hazy sun, three layered mountain silhouettes (back layer gets snow caps), gradient ocean with a golden sun-reflection trail, and two silhouette palm trees framing the foreground. Cards become translucent cream-parchment postcards with espresso wood frames + per-game accent inner stripe.
- **Vector glyph icons** ([`IconText.cs`](../../Source/Launcher/Launcher/Game/IconText.cs)) for ▶ → ► — drawn as `SKPath` shapes instead of Unicode characters, so they render correctly on SkiaSharp's wasm fallback font (which lacks those codepoints).
- **Click-to-launch** with per-target dispatch:
  - **Desktop**: probes `Source/<Folder>/<Folder>/bin/Release/net10.0-desktop/<Folder>.exe`, falls back to `.../Debug/...`, and finally to `dotnet run` on the csproj. Sub-second launch once Release exes exist; works on fresh clones too.
  - **WASM**: navigates `window.location.href` to `WasmPath` (`/games/<slug>/`). 404s unless the games have been published alongside the launcher — see [Publishing the static site](#publishing-the-static-site).

## Run (desktop)

```powershell
.\Builds\Run-Launcher.ps1                          # desktop, Release
.\Builds\Run-Launcher.ps1 -Configuration Debug     # desktop, Debug
.\Builds\Run-Launcher.ps1 -Wasm                    # browser-wasm
```

Or directly: `dotnet run --project Source/Launcher/Launcher/Launcher.csproj -f net10.0-desktop`.

For sub-second click-to-launch on desktop, build every game Release first so the launcher can find the prebuilt exes:

```powershell
.\Builds\Build-All.ps1 -Configuration Release
.\Builds\Run-Launcher.ps1
```

Without prebuilt exes the launcher falls back to `dotnet run`, which works but takes several seconds per click.

## Controls

| Input | Action |
|---|---|
| Click / Tap a tile | Launch that game (desktop: `Process.Start` on the exe; wasm: navigate) |
| Mouse move | Highlights the card under the cursor + shows a tooltip below |
| **T** | Toggle between Neon and Bob Ross themes |

## Publishing the static site

`Builds\Publish-Site.ps1` bundles the launcher and every wasm game into a single static-site layout suitable for any plain HTTP host (Azure Static Web Apps, GitHub Pages, Netlify, S3 + CloudFront, or local `dotnet serve` / `python -m http.server`):

```powershell
.\Builds\Publish-Site.ps1                          # publishes to .\publish\site\
.\Builds\Publish-Site.ps1 -OutputDir D:\deploy     # custom path
```

The resulting layout:

```
publish/site/
├── index.html               ← Launcher (catalog)
├── _framework/              ← Launcher's wasm runtime
├── package_<hash>/          ← Launcher's bundled assets
├── service-worker.js
├── manifest.webmanifest
└── games/
    ├── pohaku/              ← Pohaku wasm app
    ├── hokulele/
    ├── lua/
    ├── mahina/
    ├── heiau/
    ├── kanapi/
    ├── alaloa/
    ├── hahai/
    └── paku/
```

(One `games/<slug>/` subfolder per [`GameCatalog.cs`](../../Source/Launcher/Launcher/Game/GameCatalog.cs) entry; the set grows as games are added.)

Serve `publish/site/` from any static host and the launcher's tile clicks navigate to `/games/<slug>/`.

### Why post-publish path rewriting

Uno's wasm bootstrap bakes site-root-absolute paths (`/package_<hash>/...`, `/_framework/...`, `/manifest.webmanifest`, `/service-worker.js`) into each game's `index.html`, `package_<hash>/uno-config.js`, `service-worker.js`, and `manifest.webmanifest`. Those 404 when the game is served from `/games/<slug>/` rather than the site root. `Publish-Site.ps1` post-processes each game's tree and rewrites every `"/<path>"` inside string literals to `"./<path>"` so paths resolve relative to the game's subfolder. The launcher itself sits at the site root and needs no rewriting.

The script is idempotent: it deletes `publish/site/` before each run, so you can re-publish freely.

### Local serve + service-worker hygiene

```powershell
.\Builds\Publish-Site.ps1
python -m http.server 8080 --directory .\publish\site
# Then open http://localhost:8080/
```

If you've previously loaded the site, browsers will have cached the launcher's service worker. After republishing, **clear site data** (F12 → Application → Storage → Clear site data → hard-refresh) so the new build is picked up — otherwise the SW will keep serving stale assets.

## Architecture

| File | Role |
|---|---|
| [`GameCatalog.cs`](../../Source/Launcher/Launcher/Game/GameCatalog.cs) | Static array of `Entry(Name, Gloss, OriginalGame, Description, Color, WasmPath, FolderName)`. Add a row to add a tile. |
| [`LauncherWorld.cs`](../../Source/Launcher/Launcher/Game/LauncherWorld.cs) | Lightweight state: pointer position, hover/press indices, card hit-rects, current `LauncherTheme`. |
| [`Renderer.cs`](../../Source/Launcher/Launcher/Game/Renderer.cs) | Card grid layout + per-theme draw branches (`DrawCardNeon` / `DrawCardBobRoss`). Hosts the shared title/subtitle/tooltip/marquee chrome with theme-aware variants. |
| [`BobRossBackground.cs`](../../Source/Launcher/Launcher/Game/BobRossBackground.cs) | Painted sunset scene: sky gradient, clouds, sun + halo, three mountain layers with snow caps on the back, ocean gradient + sun-reflection trail, silhouette palms. |
| [`IconText.cs`](../../Source/Launcher/Launcher/Game/IconText.cs) | Vector-drawn ▶ → ► — glyphs as `SKPath` shapes + a single-call helper that lays out alternating text/icon segments with the neon halo+sharp paint stack. |
| [`MainPage.xaml`](../../Source/Launcher/Launcher/MainPage.xaml) / [`.cs`](../../Source/Launcher/Launcher/MainPage.xaml.cs) | Pointer + key input, Viewbox layout (1280×720), render loop, T-key theme toggle, exe-direct desktop launch + wasm navigate dispatcher. |
| [`BackgroundSurface.cs`](../../Source/Launcher/Launcher/BackgroundSurface.cs) | Thin wrapper around `Arcade.Common.AmbientStarBackdrop` — drifting starfield behind the Viewbox in side bars. Only visible in the Neon theme; the Bob Ross painting fills its own background. |

Shared chassis (neon paints, glyph font, marquee, gradient backdrop, HUD text, `Vec2`) is included from `Source/Common/` via the csproj's `<Compile>` glob, same pattern as the games.

World coordinates are fixed at `1280 × 720`. The grid lays out 4 columns × `ceil(N / 4)` rows automatically based on the catalog length.

## Adding a new game to the catalog

1. Add a row to [`GameCatalog.cs`](../../Source/Launcher/Launcher/Game/GameCatalog.cs) — `FolderName` must match `Source/<FolderName>/<FolderName>/<FolderName>.csproj`, and `WasmPath` should be `/games/<lowercase-folder>/`.
2. Add the slug to the `$games` array in [`Builds/Publish-Site.ps1`](../../Builds/Publish-Site.ps1) so the publish step picks it up.
3. Add the build script to [`Builds/Build-All.ps1`](../../Builds/Build-All.ps1) (and add per-game `Build-<Name>.ps1` / `Run-<Name>.ps1` matching the existing pattern).
4. Rebuild Release (`.\Builds\Build-All.ps1 -Configuration Release`) so the launcher finds the new exe, then rerun the launcher.

## Stack

- Uno Platform (`SkiaRenderer` UnoFeature) + `Uno.WinUI.Graphics2DSK.SKCanvasElement`
- SkiaSharp 4.151.0
- .NET 10 — `net10.0-desktop`, `net10.0-browserwasm`
- No audio (launcher is silent on purpose — clicking a tile hands off to the game's own audio)
