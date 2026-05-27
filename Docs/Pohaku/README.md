# Pohaku

## Elevator pitch

A vector-style Asteroids clone written as a single Uno Platform project that targets the browser (WebAssembly) and desktop (Skia / Win32). The entire game world — ship, asteroids, saucer, bullets, particles, HUD, and a scrolling marquee — is drawn directly to the Uno Skia compositor via `Uno.WinUI.Graphics2DSK.SKCanvasElement`. There is no XAML for the gameplay, no bitmap sprites, no game framework: just a `GameWorld` class that ticks on `CompositionTarget.Rendering` and a static `Renderer` that issues SkiaSharp draw calls each frame.

The interesting bits are all in two files: [Renderer.cs](../../Source/Pohaku/Pohaku/Game/Renderer.cs) (vector rendering, perspective marquee, SkiaSharp 4 path builder, neon glow pass) and [MainPage.xaml.cs](../../Source/Pohaku/Pohaku/MainPage.xaml.cs) (input, the compositor-aligned game loop). Press `V` at any time to toggle between the muted retro-green look and a synthwave neon mode with `SKMaskFilter` glow.

## Deep dive

### Project layout

- [global.json](../../Source/Pohaku/global.json) pins `Uno.Sdk` to `6.7.0-dev.64`.
- [Pohaku.csproj](../../Source/Pohaku/Pohaku/Pohaku.csproj) is a `Uno.Sdk` single-project targeting `net10.0-browserwasm;net10.0-desktop` with `<UnoFeatures>SkiaRenderer</UnoFeatures>`. The `<SkiaSharpVersion>4.147.0-preview.3.1</SkiaSharpVersion>` property overrides the Uno SDK's implicit SkiaSharp packages, pulling in SkiaSharp 4 across `SkiaSharp`, `SkiaSharp.Views.Uno.WinUI`, `SkiaSharp.Skottie`, and all native-assets packages in one shot — no individual `PackageReference` needed.
- Game state lives in [Game/](../../Source/Pohaku/Pohaku/Game/): `Entities.cs` (ship/asteroid/saucer/bullet/particle data), `GameWorld.cs` (physics, spawning, scoring, mode state machine), `Renderer.cs` (all drawing).

### Game loop

[MainPage.xaml.cs](../../Source/Pohaku/Pohaku/MainPage.xaml.cs) subscribes to `Microsoft.UI.Xaml.Media.CompositionTarget.Rendering` rather than running a `DispatcherTimer`. `Rendering` fires once per compositor frame and is vsync-aligned, so `dt` (derived from a `Stopwatch`) tracks real frame pacing and the world updates stay in lockstep with the screen. `GameCanvas.Invalidate()` at the end of each tick schedules the next paint pass. The earlier `DispatcherTimer` approach polled on a separate thread and produced visible stutter on desktop; the swap to `CompositionTarget.Rendering` is the single biggest perceived-smoothness improvement in the project.

Input is plain `KeyDown`/`KeyUp` with two latched "pressed this frame" flags (`_firePressedThisFrame`, `_hyperPressedThisFrame`) so single-shot actions don't repeat. The `V` key toggles `_world.VibrantMode`.

### Render pipeline

`MainPage.xaml` hosts a `GameSurface` element ([GameSurface.cs](../../Source/Pohaku/Pohaku/GameSurface.cs)) that subclasses `Uno.WinUI.Graphics2DSK.SKCanvasElement` and overrides `RenderOverride(SKCanvas canvas, Size area)` to call `Renderer.Render(canvas, _world, w, h)`. `SKCanvasElement` plugs directly into Uno's Skia composition tree — draw calls go straight to the shared GPU-backed surface that Uno is already rendering, with no intermediate `SKBitmap` allocation or pixel copy back into XAML the way `SKXamlCanvas` does. The cost is that `SKCanvasElement` is Skia-only (which is fine here: `<UnoFeatures>SkiaRenderer</UnoFeatures>` already enables Skia on both desktop and browserwasm).

`MainPage.xaml.cs` sets `GameCanvas.World = _world` in `Loaded` and calls `GameCanvas.Invalidate()` after every world tick to schedule the next paint. Every frame redraws the entire scene; there is no dirty-rect logic and no off-screen surface caching. With ~60 draw calls per frame (retro) or ~120 (vibrant, since each shape draws twice), this is well within budget on either target.

`Renderer.Render` branches on `world.VibrantMode` to `DrawWorldRetro` or `DrawWorldVibrant`. The canvas setup before the branch is shared: clear/background, world-fit transform (`Translate` to center, `Scale` to fit the world into the canvas while preserving aspect), then the per-mode entity pass, then `DrawHud` on the unscaled canvas.

### SkiaSharp 4 idioms

SkiaSharp 4 deprecates `SKPath.MoveTo`/`LineTo`/`Close` in favor of `SKPathBuilder`. The codebase uses three patterns:

1. **Static cached paths** for shapes whose geometry is constant — the ship body, thrust flame, life icon, and 17 vector-font glyphs. Built once at type-init via a small `BuildPath(ReadOnlySpan<SKPoint>, bool close)` helper that constructs an `SKPathBuilder`, calls `AddPoly`, and `Detach()`s an immutable `SKPath`.

2. **Per-frame `stackalloc` + `AddPoly`** for shapes whose geometry changes every frame — the asteroid (12 vertices at randomly-perturbed radii, rotated each tick) and the saucer (parameterized by `Radius`, which varies between large and small saucers). This keeps allocations zero and replaces the old N×`MoveTo`/`LineTo` call sequence with a single P/Invoke per path. The asteroid loop calls a helper rather than `stackalloc`-ing inline because CA2014 (correctly) flags `stackalloc` inside a `foreach` body — extracting the work into `DrawAsteroidVibrant`/`DrawAsteroid` gives each call its own stack frame.

3. **`SKPathBuilder.MoveTo`/`LineTo`** for the vector-font glyphs, which are disjoint line-segment paths (`M`/`L` pairs with no `Close`). These methods are *not* deprecated on `SKPathBuilder` — only on `SKPath`.

### Vector-font marquee

The "RUNNING ON UNO PLATFORM AND SKIASHARP 4" scroller at the bottom of the attract screen is a stroke font baked from a 4×6 grid. Each glyph is defined as a flat `float[]` of `x1,y1,x2,y2` segment quads and compiled once into an `SKPath` at the target character size (44×64 px). Lookup is a `Dictionary<char, SKPath>`.

Per frame, `DrawMarquee` computes a `pixelOffset` from a `Stopwatch` so position is wallclock-driven (frame drops don't slow the scroll). The cycle length is `stringWidth + canvasWidth`, so the entire string scrolls fully off the left before reappearing from the right.

### Perspective tilt

The marquee plane is tilted back around its bottom edge using an explicit perspective `SKMatrix`. The math, in marquee-local coordinates where `y = 0` is the top of the glyph row and `y = h` is the rotation axis at the bottom:

```
x' = x
y' = h·(1 − cos θ) + cos θ · y
w' = 1 + sin θ · (h − y) / d
screen = (x'/w', y'/w')
```

Encoded as `SKMatrix` fields:

```csharp
ScaleX = 1,  SkewX = 0,        TransX = 0,
SkewY  = 0,  ScaleY = cos θ,   TransY = h · (1 − cos θ),
Persp0 = 0,  Persp1 = −sin θ/d, Persp2 = 1 + h · sin θ / d
```

The canvas is translated to `(canvasW/2, baselineY − h)` before `Concat`-ing the matrix, which puts the perspective origin (`x = 0`) at the horizontal center of the canvas — that's why glyphs at the top of the tilted plane vanish toward the middle of the screen rather than the left edge. Each glyph then draws at `x − canvasW/2` to stay positioned correctly in the shifted local space.

A subtle gotcha: pre-perspective off-screen culling has to account for horizontal compression at the top of foreshortened glyphs. A glyph whose pre-perspective `x` is slightly past the right edge can still have its top visibly on screen because perspective pulls `x` toward `centerX` by a factor of `1/w'_top`. The cull pad is widened to `(canvasW/2) · (w'_top − 1) + charWidth` on each side, which is the maximum horizontal offset perspective can introduce.

`TiltDegrees` (default 30°) and the viewer distance `d` (3·h) are the two knobs — smaller `d` or larger tilt makes the vanishing more aggressive.

### Vibrant mode

Toggled by `V`, vibrant mode keeps the entire game running but swaps the look:

- **Background**: black + scanline overlay → vertical linear gradient (`SKShader.CreateLinearGradient`, deep purple top to slightly-lighter purple bottom).
- **Palette**: cyan ship, hot-pink asteroids, lime saucer, electric-yellow bullets, orange thrust flame, cyan HUD, magenta "ASTEROIDS" title. Particle and marquee colors cycle through HSV (`HsvToRgb` helper inside `Renderer`).
- **Glow pass**: every stroke and fill renders twice. The "halo" paint has `MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, σ)` with a thicker stroke (or, for fills, a slightly larger radius); the "sharp" paint draws on top with the inner color at full alpha. There are four static neon paints (`NeonStrokeHalo`, `NeonStrokeSharp`, `NeonFillHalo`, `NeonFillSharp`) — color is mutated per shape, mask filter and stroke width stay put, so the per-frame allocation cost is zero. Helpers `NeonStroke(path, color)`, `NeonLine(...)`, `NeonCircleFill(...)`, and `DrawHudText(...)` wrap the two-pass pattern.
- The marquee uses its own wider/blurrier neon paints (σ=7, 11px halo stroke) since the perspective-tilted glyphs read larger on screen than the in-world entities, and cycles hue per glyph via `(time·75 + i·18) mod 360` for a rainbow effect.

Performance: ~60 extra `DrawPath`/`DrawCircle` calls per frame (the halo pass), each with a mask-filter blur. Skia's GPU pipeline caches blur kernels so cost scales with shape count rather than blur radius. Comfortable on desktop; the codebase is desktop-focused for the vibrant path, though wasm runs it without issue at default world densities.

### Tunables at a glance

| Knob | Location | Effect |
|---|---|---|
| `MarqueeSpeed` | `Renderer.cs` | scroll velocity (px/sec) |
| `MarqueeCharHeight` / `MarqueeCharWidth` / `MarqueeCharGap` | `Renderer.cs` | marquee glyph size and spacing |
| `TiltDegrees` (inside `DrawMarquee`) | `Renderer.cs` | how far the marquee leans back |
| `d` (inside `DrawMarquee`) | `Renderer.cs` | viewer distance — smaller is more aggressive |
| `NeonStrokeHalo` / `NeonFillHalo` mask-filter sigma | `Renderer.cs` | glow softness |
| Halo alpha (`0xC0`, `0xB0`) inside `NeonStroke`/`NeonLine`/`NeonCircleFill` | `Renderer.cs` | glow intensity |
| `NeonBgTop` / `NeonBgBottom` | `Renderer.cs` | vibrant background gradient |

### Running

```
dotnet run --project Source/Pohaku/Pohaku --framework net10.0-desktop
dotnet run --project Source/Pohaku/Pohaku --framework net10.0-browserwasm
```

The wasm target serves at `http://localhost:5000/`. Controls: arrows or WASD to fly, space to fire, H for hyperspace, V to toggle vibrant mode.
