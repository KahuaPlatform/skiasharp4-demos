# KahuaNetwork — Kahua Network Living Digital Twin Demo

## Elevator pitch

A giant holographic visualization of the Kahua Network rendered entirely in
SkiaSharp on the Uno Platform. The Kahua Network's connected
organizations — Owners, GCs, Subs, Architects, CMs, PMs — appear as glowing
3D towers in a futuristic city. Color-coded document exchanges (RFIs,
Submittals, Pay Apps, Change Orders, Daily Reports, Punch Lists, Drawings)
pulse between them along curved data streams, embodying the network's
"enter once, connect everywhere" promise.

The camera flies through the network in idle mode. Click a tower to focus
on a single organization and see its role, active projects, approval
backlog, throughput, and pending items. Hit **NETWORK VIEW** and the whole
city shatters into thousands of particles, reassembles into the live
network topology graph, then morphs back into the city. **AI: AUTO-ROUTE**
clears an organization's pending approvals on demand and reports it in the
live insight feed.

Everything — buildings, particles, data streams, glassmorphism HUD panels,
buttons, sparklines, the topology morph — is drawn through a single
`SKCanvasElement`, integrated directly into Uno's Skia render tree with no
intermediate blits.

---

## Detailed overview

### What it demonstrates

- **The Kahua Network as a living system.** Organizations are nodes;
  documents are the lifeblood flowing between them. The metaphor maps
  cleanly to the platform's core value: each piece of information is
  entered once and propagates to every connected stakeholder.
- **Cross-platform rendering performance.** Single Uno Platform codebase
  targeting Skia Desktop (Windows / macOS / Linux) and WebAssembly. The
  same Skia draw calls run unmodified across all targets.
- **AI orchestration as visual atmosphere.** A rotating insight feed
  narrates network-level events ("RFI #4218 routed from Forge Steel &
  Erection to Studio Cipher · SLA 48h", "Submittal turnaround at Northwall
  Architects trending 31% faster") to convey that intelligence is
  continuously observing and acting on the network.
- **Crowd-stopping visuals.** Bloom-style additive particle systems,
  animated procedural ground grid, parallax depth, glassmorphism HUD,
  aurora gradient sky, scanline / vignette post-pass, and a Global View
  morph designed for booth-distance impact and shareable video clips.

### Roles and exchanges

| Role chip | Color   | Display name          |
|-----------|---------|-----------------------|
| OWNER     | Cyan    | Owner                 |
| GC        | Magenta | General Contractor    |
| SUB       | Lime    | Subcontractor         |
| ARCH      | Violet  | Architect / Designer  |
| CM        | Amber   | Construction Manager  |
| PM        | Sky     | Program Manager       |

Document exchanges are wired between realistic role pairs — RFIs and
Submittals flow Sub → Architect, Pay Apps flow GC → Owner / CM, Change
Orders flow GC → Owner, Daily Reports flow Sub / GC → CM, Punch Lists
flow Architect → GC / Sub, Drawings flow Architect → GC / Sub. Each
exchange type has its own glow color and pulses traveling along a curved
3D bezier path between the two towers.

### Interaction model

| Input               | Effect                                                |
|---------------------|-------------------------------------------------------|
| Click a tower       | Camera focuses; expanded org panel appears            |
| Click empty space   | Deselect, resume idle orbit                           |
| **NETWORK VIEW** / `G` | Global View — city explodes into the topology graph, then reforms |
| **AI: AUTO-ROUTE** / `M` | Clears ~40% of selected org's pending approvals; emits a green confirmation burst and a win-flavored insight |
| **REGENERATE NETWORK** / `R` | Re-rolls a fresh procedural network                |
| **TOGGLE GRID** / `Space` | Toggles the animated ground grid                  |
| `Esc`               | Deselect                                              |

### Architecture

```
Source/KahuaNetwork/                    Uno Platform single-project solution
└─ KahuaNetwork/
   ├─ KahuaNetwork.csproj               targets net10.0-desktop, net10.0-browserwasm
   ├─ MainPage.xaml(.cs)                Hosts the SceneCanvas; wires input + frame loop
   ├─ SceneCanvas.cs                    SKCanvasElement subclass — direct Uno Skia hook
   └─ Engine/
         ├─ Theme.cs                    Palette, risk gradient
         ├─ Roles.cs                    OrgRole + DocumentKind, colors, tags
         ├─ Camera3D.cs                 Perspective camera, world→screen projection
         ├─ CameraDirector.cs           Idle orbit / focus-on-org cinematics
         ├─ Building.cs                 Organization state (role, projects, backlog)
         ├─ City.cs                     Procedural network generation
         ├─ DataStream.cs               Glowing document-exchange bezier
         ├─ Particle.cs                 Particle struct + kinds
         ├─ ParticleSystem.cs           Emitter / updater / additive renderer
         ├─ SceneRenderer.cs            Composes the scene; depth-sorted towers
         ├─ WowEffect.cs                Global View state machine
         ├─ Hud.cs                      Glassmorphism panels, buttons, sparkline
         └─ AIInsightFeed.cs            Rotating network-flow insights
```

### Rendering pipeline

All visuals are emitted into a single `SKCanvasElement`
(`Uno.WinUI.Graphics2DSK`). Because the host application uses the Uno
Skia renderer, the canvas integrates directly into the Skia composition
tree — there's no extra `SKImage` allocation or blit between the demo's
draw calls and the screen.

Each frame the page's `CompositionTarget.Rendering` handler advances
simulation state (camera director, particles, data streams, AI insights),
then calls `Canvas.Invalidate()`. The canvas's `RenderOverride(SKCanvas,
Size)` then draws, in order:

1. Sky gradient + aurora band
2. Animated procedural ground grid (world-projected)
3. Buildings, depth-sorted back-to-front, each with: ground halo,
   gradient side faces, window-light streaks, neon edges, top cap,
   selection halo, backlog risk marker, and a floating role-tag chip
4. Document exchanges (bezier pulses with trailing tails)
5. Particles (glow + core, additive blend)
6. Global View overlays (topology nodes / edges / title)
7. Scanlines and vignette
8. Glassmorphism HUD — title, stats panel, network activity feed,
   per-org inspector panel, action buttons, corner brackets

### Running it

```powershell
cd Source/KahuaNetwork
dotnet run --project KahuaNetwork -f net10.0-desktop
# or
dotnet run --project KahuaNetwork -f net10.0-browserwasm
```

### Stack

- Uno Platform with Skia renderer (`SkiaRenderer` UnoFeature)
- SkiaSharp 3.119.4
- `Uno.WinUI.Graphics2DSK` for direct `SKCanvasElement` integration
- .NET 10
- Targets: Skia Desktop (Win32 / X11 / macOS) and WebAssembly
