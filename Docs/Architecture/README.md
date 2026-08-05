# Architecture Documentation

Architecture-level documentation for the UnoSkiaDemos repository. Per-demo specifics (controls, gameplay, file map) live in [Docs/<Demo>/README.md](../); this folder explains the cross-cutting patterns that all the demos share.

## Contents

| Doc | Covers |
|---|---|
| [01 – Overview](01-Overview.md) | What the repo is, the demo catalog, naming conventions, per-demo isolation principle, top-level system diagram. |
| [02 – Demo Anatomy](02-Demo-Anatomy.md) | Standard file layout inside each demo, the three-layer XAML page composition, the per-frame render loop, the canonical game-state machine. |
| [03 – Shared Chassis](03-Shared-Chassis.md) | What's in `Source/Common/`, the source-include mechanism, and a chassis-component-by-component reference — the core neon tier (NeonPaints / NeonDraw / GlyphFont / Marquee / HudText / HighScoreStore / AmbientStarBackdrop / AudioEngineBase / …) and the scrolling-world tier (Camera2D / VectorShapes / Radar / SeamlessTerrain / TileGrid / AsciiMap / FlowField). |
| [04 – Rendering Pipeline](04-Rendering-Pipeline.md) | The Uno + SkiaSharp chain from XAML to pixel: `SKCanvasElement` → `RenderOverride` → world-space coords → halo+sharp double-pass paint stack. |
| [05 – Audio](05-Audio.md) | Cross-platform procedural audio: `AudioEngineBase` + NAudio (desktop) + Web Audio (wasm), gesture gating, conditional compilation. |
| [06 – Build and Deploy](06-Build-And-Deploy.md) | `Builds/` scripts, multi-targeting (`net10.0-desktop` vs `net10.0-browserwasm`), and the `Publish-Site.ps1` pipeline that bundles every wasm game + the launcher into a single static site. |
| [07 – Launcher](07-Launcher.md) | Catalog-driven UI, click-dispatch (exe-direct on desktop, navigation on wasm), and the two themes (Neon and Bob Ross). |
| [08 – Chassis Extensions](08-Chassis-Extensions.md) | Why the scrolling-world chassis tier looks the way it does: the one-`Camera2D`-not-two reconciliation Kia'i and Koa forced, what was deliberately left unbuilt (`Pool<T>`, `Entity2D`), and the cross-game decisions both games settled. |

## Mermaid rendering

All diagrams in these docs use [Mermaid](https://mermaid.js.org/) fenced code blocks. GitHub renders them inline, and VS Code's built-in Markdown preview (`Ctrl+Shift+V`) renders them natively — no extension required.

## Reading order

If you're new to the repo:

1. Start with [01 – Overview](01-Overview.md) for the lay of the land.
2. Read [02 – Demo Anatomy](02-Demo-Anatomy.md) and [03 – Shared Chassis](03-Shared-Chassis.md) together — they describe the per-demo "shape" and what gets factored out.
3. [04 – Rendering Pipeline](04-Rendering-Pipeline.md) and [05 – Audio](05-Audio.md) are deeper dives into the two cross-cutting subsystems.
4. [06 – Build and Deploy](06-Build-And-Deploy.md) and [07 – Launcher](07-Launcher.md) are operational — needed when you're adding a new demo or publishing the site.
5. [08 – Chassis Extensions](08-Chassis-Extensions.md) is the design-rationale companion to 03 — read it when you're changing the camera or adding a scrolling-world game, so you don't relitigate decisions that were already made.
