# UnoGallery

A deliberately gratuitous image-gallery showcase for Uno Platform + SkiaSharp.
Thirty tiles arranged into one of four layouts (Grid / Helix / Carousel / Detail),
animated with SKSL post-processing, EXIF-aware folder loading, and a sidebar of
audio-reactive bells and whistles. Sixteen of the thirty tiles aren't static
images at all — they're live mini-applications (Conway's Game of Life, a
flocking-boids simulation, a reaction-diffusion PDE, a wireframe polyhedron
viewer, a Lorenz attractor, an audio scope with FFT spectrum, an analog
clock, an L-system tree, etc.) updating their own state on background threads
and rendering through the same effects pipeline as the photo tiles.

## How it was made

This codebase was built one conversation turn at a time in a co-coding session
with Claude (Anthropic's coding agent). The session started from `dotnet new
unoapp -preset recommended` and ended with the working set you can see in the
running app. Every commit corresponds to one back-and-forth exchange.

The intent at the outset was *"a gratuitous showcase for the power of the Uno
platform and the rendering power of SkiaSharp 4 using the SKCanvasElement."*
What actually shipped diverged from the original plan in interesting ways:

- The original design doc assumed `SKCanvasElement` was a SkiaSharp 4 type.
  Reality: it's an **Uno** type (`Uno.WinUI.Graphics2DSK.SKCanvasElement`),
  shipped implicitly when `UnoFeatures` contains `SkiaRenderer`. Found this
  out by directly inspecting `Uno.WinUI.Graphics2DSK.dll` after a long search
  through SkiaSharp 4's type table that turned up nothing.
- SkiaSharp **4.147.0-preview.3.1** shipped a real `AccessViolation` in
  `sk_runtimeeffect_get_uniform_byte_size` the moment `SKRuntimeShaderBuilder`
  bound uniforms on first frame. Diagnosed mid-session, worked around by
  building a multi-version build property (`SkiaSharpVersion`) — default
  v3.119.4 stable, opt-in 4.x — with conditional pin block and a
  `SKIA_V4` compile symbol. Five of the six SKSL effects only compiled-and-bound
  on v3; one (the zero-uniforms colour-filter "ToneGrade") worked on both.
  **Fixed in 4.151.0 (SkiaSharp 4 stable): the gate is gone and v4 is now the
  default**, with all six effects live. A useful negative result along the way —
  a bare-SkiaSharp console harness could *not* reproduce the AV even on the
  preview that crashes, so only running it in the Uno host settled it. See
  [SkiaSharp 3 vs 4 in UnoGallery](../../README.md#skiasharp-3-vs-4-in-unogallery).
- Real photos via folder picker hit a JPEG decoder quirk: `SKCodec.GetPixels`
  with arbitrary target dimensions silently fails on JPEG (only 1/N integer
  scales supported). Found by an "is there any reason .jpg files don't open?"
  question after the initial folder-picker landed.
- Microphone input is wired through NAudio on Windows desktop (gated by a
  `HAS_NAUDIO` compile symbol on the `net10.0-desktop` TFM). The Waveform tile
  runs its own FFT for spectrum bars, plus bass-band beat detection that
  modulates the ambient background plasma so the room "breathes" with the music.
- Performance work happened twice: first round of draw-call batching
  (Conway-as-bitmap, depth-bucketed paths, paint reuse), then a second round
  that moved Reaction-Diffusion / Conway / Boids / Attractor / CurlNoise onto
  dedicated background threads when the FPS still wasn't where it needed to
  be. There's a frame-time profiler overlay in the top-right of the canvas
  for diagnosing what comes next.

The DESIGN.md in the solution folder is a snapshot of the architecture as it
*actually shipped*, rewritten mid-session after the gap between the original
plan and reality grew too wide to leave un-resolved.

If you want to see the conversation shape it took, just walk the commit log
top to bottom — every commit is one self-contained feature or fix.

---

## Deep dive

### What you see when you launch it

```
+--------------------------------------------------------------+
|  UnoGallery — SkiaSharp 4 + Uno Platform   [Grid][Helix]...  |  ← top app bar
|--------------------------------------------------------------|
|                                                              |
|  [Grid]                                       +-----------+  |
|                                               | Frame     |  |  ← profiler HUD
|  +---+ +---+ +---+ +---+ +---+ +---+          | Profiler  |  |     (top-right)
|  | 0 | | 1 | | 2 | | 3 | | 4 | | 5 |          |  16.6 ms  |  |
|  +---+ +---+ +---+ +---+ +---+ +---+          |  bloom 4  |  |
|                                               |  tiles 8  |  |
|  ... 24 more tiles in five more rows ...      +-----------+  |
|                                                              |
|  - - - - - - - reflection floor line - - - - - - - - - - -   |
|     (Y-flipped, gradient-masked, fading to bottom edge)      |
+--------------------------------------------------------------+
```

The top bar offers Grid / Helix / Carousel layouts, an "Open folder..." picker
for loading real photos, a Demo on/off toggle that controls a 5-second
auto-cycle through the three layouts, and a cog icon that opens the settings
flyout (toggle each post-process effect, pick a microphone source, toggle the
profiler overlay).

Behaviours:

- Hover any tile and its neighbours softly defocus (bokeh-blur via
  `SKImageFilter.CreateBlur`), while a pulsing SKSL halo lifts the hovered
  tile.
- Click a tile and the iris-zoom SKSL transition expands a circular reveal
  from where you clicked, taking you into a Detail view with the tile centred
  and a caption + palette swatches below.
- Click anywhere (or press Escape) and the iris collapses back to where the
  tile sat in the underlying layout.
- Switching layouts uses a noise-thresholded SKSL dissolve transition (tiles
  vanish from one arrangement and materialise in the next through a feathered
  noise mask), distinct from the lerp-interpolation used for the layout
  placements themselves.
- Demo mode auto-cycles every 5 seconds and pauses for 3 seconds after any
  user interaction, then resumes.
- Move the pointer slowly — large/close tiles drift opposite to the cursor for
  a parallax "looking around the scene" effect.

### Live tiles

Sixteen of the thirty tiles render fresh content every frame. They live in
[UnoGallery/UnoGallery/LiveTiles/](../../Source/UnoGallery/UnoGallery/LiveTiles/) and all
implement `ILiveTile.Draw(SKCanvas canvas, SKRect dest, float wallClockSeconds)`.

| Slot | Tile | What it does |
| --- | --- | --- |
| 1 | **Plasma** | Per-tile SKSL shader (sine field + polar spiral) tinted by tile palette |
| 3 | **GPU** | System GPU 3D-engine usage via PerformanceCounter, ECG-style trace |
| 4 | **Lissajous** | Parametric curve (sin(a·u), sin(b·u + φ)) with drifting frequencies |
| 6 | **Lorenz** | Classic chaotic 3D trajectory; 1400-point trail with squared-alpha fade |
| 7 | **FallingSand** | 64×80 cellular sand sim with emitter sweep, bottom-row pruning |
| 8 | **Frame** | Last 80 frame intervals as colour-graded bars with 60-fps reference |
| 9 | **CurlNoise** | 240 particles drifting through a curl-of-Perlin vector field |
| 11 | **Conway** | 56² Game of Life, ~12 generations/sec, reseeds after 600 generations |
| 13 | **Mandala** | 8-fold rotational + mirror symmetry kaleidoscope, source wedge recorded as `SKPicture` and replayed |
| 14 | **Reaction** | Gray-Scott PDE on 96² grid, 8 substeps/batch, mitosis regime |
| 16 | **Boids** | 50 boids with Reynolds separation / alignment / cohesion on a toroidal world |
| 18 | **Attractor** | Clifford strange attractor cycling 12 hand-curated `(a,b,c,d)` presets with smoothstep morphing |
| 21 | **Audio** | FFT spectrum bars + waveform overlay + beat-pulse indicator; reads from selectable mic or synthesised source |
| 23 | **Wireframe** | Rotating polyhedra (cube / tetra / octa / icosa / stellated octa / tesseract / hyperboloid / torus knot) with depth-tinted edges |
| 24 | **Tree** | L-system grammar expansion + animated turtle-graphics rendering, cycles 4 different rules |
| 26 | **Clock** | Real-time analog face with smooth-sweep second hand |

Tiles at slots 0, 2, 5, 10, 12, 15, 17, 19, 20, 22, 25, 27, 28, 29 are
static procedural images generated at startup by the
[ProceduralSampleSource](../../Source/UnoGallery/UnoGallery/Data/ProceduralSampleSource.cs)
(six generators: linear gradient, radial burst, Julia, voronoi, stripes,
curve flow).

### Architecture in one diagram

```
+--------------------+      Render(time)
|  GallerySurface    |  ───────────────────────────→  +-------------------+
|  : SKCanvasElement |                                |  SceneController  |
|  (Uno.WinUI.Gfx2DSK)|  ◄────  ITick + IRender ──── |                   |
+--------------------+                                +-------------------+
       ▲                                                       │
       │ pointer / keyboard / picker                           │ Render(SKCanvas, SKSize)
       │                                                       ▼
+--------------------+                                +-------------------+
|     MainPage       |                                |  EffectsPipeline  |
|  (XAML + handlers) |                                |                   |
+--------------------+                                +-------------------+
                                                              │
                          ┌───────────────────────────────────┼────────────────────────────┐
                          ▼                                   ▼                            ▼
                 +----------------+                  +----------------+           +----------------+
                 |  BackgroundPass|                  |  ReflectionPass|           |    BloomPass   |
                 |  (perlin + glow|                  |  (Y-flip picture           | (replay through|
                 |  + SKSL plasma)|                  |   in floor band)|           |  threshold+blur)|
                 +----------------+                  +----------------+           +----------------+

                          ▼
                 +----------------+
                 |     Tiles      |   per-tile dispatch:
                 |  (sorted by Z) |     - static  → DrawImage
                 +----------------+     - live    → ILiveTile.Draw inside ClipRect
                                        - tiny/   → fallback to static snapshot
                                          faint    (Detail-mode crumbs)
```

The pipeline records a tiles-only `SKPicture` and replays it twice — once
inside the reflection's flipped-and-masked SaveLayer, once upright for the
main render. Bloom replays the same picture a third time through a
threshold-and-blur paint with `Plus` blend. The tone-grade SKSL colour filter
and the chromatic-aberration SKSL shader compose into a single paint that
applies to the scene-picture draw.

See [UnoGallery/DESIGN.md](DESIGN.md) for the full architecture
write-up, including state-machine diagrams for transitions and a per-frame
sequence diagram.

### SkiaSharp version strategy

The project defaults to **SkiaSharp 4.151.0** (the stable v4 line). To build
against the older v3 line instead:

```bash
dotnet build -p:SkiaSharpVersion=3.119.4
```

Both lines build and run with the full six-effect SKSL pipeline. The switch is
kept so this demo can A/B the two versions, not because either is broken.

Mechanics:

- [Directory.Build.props](../../Source/UnoGallery/Directory.Build.props) defines the
  `$(SkiaSharpVersion)` property and appends `SKIA_V4` to `$(DefineConstants)`
  whenever the version starts with `4.`.
- [Directory.Packages.props](../../Source/UnoGallery/Directory.Packages.props) gates a
  17-line transitive-pin block behind that same condition. v3 needs no pinning
  — Uno's runtime libs declare `SkiaSharp >= 3.119.0` which unifies cleanly.
  v4 needs the pin to drag `SkiaSharp.Views`, `NativeAssets.*`, and
  `HarfBuzzSharp.*` up off their 3.119 floors.
- Two source sites use `#if SKIA_V4` directly: `ProceduralSampleSource.DrawCurveFlow`
  (SKPathBuilder on v4, SKPath on v3) and `ShaderLibrary` (all uniforms-bearing
  shaders only loaded on v3, where the runtime-effect path actually works).

The SKSL shaders themselves are in [UnoGallery/UnoGallery/Shaders/](../../Source/UnoGallery/UnoGallery/Shaders/),
embedded into the assembly so `ShaderLibrary` can read and compile them at
first access.

### Live reflections, threading, and the profiler

These three are the perf-relevant moving parts.

**Live reflections** — the `ReflectionPass` doesn't iterate tile placements
any more. The pipeline records the tile-loop into a sub-`SKPicture` once per
frame; the reflection pass replays it Y-flipped inside a floor-band-bounded
`SaveLayer` with a gradient `DstIn` alpha mask. Live tiles' `Draw` is invoked
exactly once per frame regardless of how many places the picture is replayed.

**Threading** — five compute-heavy tiles run their state update on dedicated
background `Thread`s (`IsBackground = true`) so the UI thread isn't blocked
by 100k+ cell/particle/boid updates per frame:

- `ReactionDiffusionTile` — 8-substep PDE batches at ~120 Hz
- `ConwayTile` — generation step at 12 Hz
- `BoidsTile` — O(N²) flocking forces at 60 Hz
- `AttractorTile` — 8000-iteration density-buffer splats at 60 Hz
- `CurlNoiseTile` — 240 particle updates at 60 Hz

The UI thread's `Draw` snapshots state under a `Lock` and renders. The
workers all use the same simple pattern (`while (_alive) { lock { Step(); }
Thread.Sleep(N); }`). Side effect worth noting: process CPU usage now
genuinely reflects the workload — multiple cores busy in parallel — instead
of pinning a single core.

**Profiler** — [Diagnostics/FrameProfiler.cs](../../Source/UnoGallery/UnoGallery/Diagnostics/FrameProfiler.cs)
is a static `Stopwatch`-backed scoped timer. Wrap any block in
`using (FrameProfiler.Measure("label")) { ... }`. Each label accumulates a
per-frame total; an exponential-moving-average smooths the displayed value
so it doesn't jitter. The pipeline wraps every stage plus each live tile's
`Draw`, and the overlay panel (top-right of canvas, toggleable in settings)
shows rows sorted by ms descending — biggest culprits float to the top.

Other perf wins that already shipped:

- Conway: bitmap render, 1 `DrawImage` instead of 1500+ `DrawRect` per frame.
- Wireframe / Lorenz / Boids / CurlNoise: depth- or colour-bucketed paths,
  one paint per bucket reused across all draws.
- Mandala: source wedge recorded into an `SKPicture` once per frame, replayed
  16 times via `DrawPicture(matrix)`.
- Tiny-tile cull: in Detail mode, the 15 perimeter "crumb" tiles (28 px wide
  at 18 % opacity) fall back to their static snapshot — live `Draw` is gated
  on size ≥ 30 px and opacity ≥ 0.20.
- Blur-filter cache: `SKImageFilter.CreateBlur` allocations memoised by sigma,
  so 29 hover-blurred tiles share one filter instance.
- Reflection layer: `SaveLayer` bounded to the floor band (bottom 20 % of
  viewport) instead of full screen.

### Build and run

Prereqs: .NET 10 SDK, Uno Platform workloads (`dotnet workload install uno`),
Visual Studio 2022 17.12+ or VS Code with C# Dev Kit.

```bash
# default: SkiaSharp 4.151.0 stable
dotnet build Source/UnoGallery/UnoGallery.sln -f net10.0-desktop -c Debug

# build against the older SkiaSharp 3 line instead
dotnet build Source/UnoGallery/UnoGallery.sln -p:SkiaSharpVersion=3.119.4
```

Run from VS or VS Code with the bundled `.run` profile, or:

```bash
dotnet run --project Source/UnoGallery/UnoGallery/UnoGallery.csproj -f net10.0-desktop
```

Other TFMs (`net10.0-android`, `net10.0-ios`, `net10.0-browserwasm`) resolve
and build cleanly but aren't regularly exercised. Mobile and WASM are
out of scope for this session's perf and feature work.

### Interaction reference

- **Click any tile** → focus into Detail (SKSL iris transition)
- **Click anywhere in Detail** → dismiss back to the previous layout
- **Esc** → dismiss Detail (via `Page.KeyboardAccelerators`, fires regardless of focus)
- **Hover a tile** → pulsing SKSL halo lifts it; neighbours bokeh-blur
- **Move pointer** → parallax shift of all tiles
- **Top-bar buttons** → manual layout pick (pauses demo cycle)
- **"Demo: on/off"** → toggle 5-second auto-cycle
- **"Open folder…"** → load real photos from a directory (JPEG/PNG/WebP/BMP/GIF, EXIF-aware)
- **⚙ flyout** → audio source dropdown + 9 effect toggles + profiler toggle

### Code-spelunking starting points

| Question | File |
| --- | --- |
| Where does a frame get painted? | [Scene/GallerySurface.cs](../../Source/UnoGallery/UnoGallery/Scene/GallerySurface.cs) → `RenderOverride` |
| Where are scene mutations coordinated? | [Scene/SceneController.cs](../../Source/UnoGallery/UnoGallery/Scene/SceneController.cs) → `Tick`, `Focus`, `RequestLayout` |
| Where does layout X live? | [Layouts/](../../Source/UnoGallery/UnoGallery/Layouts/) |
| Where do live tiles live? | [LiveTiles/](../../Source/UnoGallery/UnoGallery/LiveTiles/) |
| Where do I add a new post-process effect? | [Effects/EffectsPipeline.cs](../../Source/UnoGallery/UnoGallery/Effects/EffectsPipeline.cs) + new `*Pass.cs` |
| Where does a chrome-button intent end up? | [Presentation/MainPage.xaml.cs](../../Source/UnoGallery/UnoGallery/Presentation/MainPage.xaml.cs) → `Surface.SetLayout` / `Surface.Dismiss` / `Surface.UpdateSettings` → `SceneController` |
| Where is the folder loader? | [Data/FolderSource.cs](../../Source/UnoGallery/UnoGallery/Data/FolderSource.cs) |
| Where is the microphone source? | [Audio/NAudioMicrophoneSource.cs](../../Source/UnoGallery/UnoGallery/Audio/NAudioMicrophoneSource.cs) (and `Audio/AudioSourceManager.cs` for selection) |
| Where is the FFT + beat detector? | [Audio/AudioAnalyzer.cs](../../Source/UnoGallery/UnoGallery/Audio/AudioAnalyzer.cs) |
| Where are SKSL shaders? | [Shaders/*.sksl](../../Source/UnoGallery/UnoGallery/Shaders/) loaded by [Shaders/ShaderLibrary.cs](../../Source/UnoGallery/UnoGallery/Shaders/ShaderLibrary.cs) |
| Where is the profiler? | [Diagnostics/FrameProfiler.cs](../../Source/UnoGallery/UnoGallery/Diagnostics/FrameProfiler.cs) |
| Where is the SkiaSharp version switch? | [Directory.Build.props](../../Source/UnoGallery/Directory.Build.props) — `$(SkiaSharpVersion)` and the `SKIA_V4` define |

### Known limitations

- **WinAppSDK Windows target is unsupported.** `SKCanvasElement` is Skia-renderer-only; the native-renderer Windows path is out of scope. Windows users get the desktop Skia build (`net10.0-desktop`).
- **HEIC photos are skipped.** SKCodec has no native HEIC plugin loaded; only JPEG, PNG, WebP, BMP, GIF decode.
- **No HEIF, no RAW.** Same reason.
- **Microphone is Windows-only.** NAudio is gated to `net10.0-desktop`. The `FakeAudioSource` synth fallback works on every TFM.
- **GPU monitor is Windows-only.** Uses `PerformanceCounter` for the "GPU Engine \ Utilization Percentage" category. On other platforms the trace flatlines at zero.
- **`SKPathBuilder` is v4-only.** Verified absent from 3.119.4, so `WireframeTile`, `MandalaTile`, and `ProceduralSampleSource` keep an `#if SKIA_V4` path-construction split. (`SKSamplingOptions` is on both lines and needs no gate.) The old "a SkiaSharp 4 build loses 5 of 6 SKSL effects" limitation is **gone** — that was the 4.147-preview AV, fixed in 4.151.0.

### Co-authorship

Every commit in this repo's history is co-authored with Claude Opus 4.7
(`Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`).
The conversation that produced it is the actual design history of the
project; the commit messages are the post-hoc summaries. If you're reading
the code and wondering "why did they do X like that?" the answer is almost
always "because something earlier in the conversation required it" — most
clearly the dance around SkiaSharp 4's preview crash, which shaped the
multi-version build architecture, the SKSL shader gating, and the choice to
write the `BackgroundPass` with a non-SKSL fallback even on a path where the
SKSL version currently works.
