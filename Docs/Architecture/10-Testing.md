# 10 – Testing

How this repo is tested, why the suites are split the way they are, and what is
deliberately *not* covered.

> **This reverses a previous position.** Until 2026-08-19 the repo had no tests, and
> [09 – Authoring a New-Game Prompt](09-Authoring-A-New-Game-Prompt.md) told agents
> "don't add one and don't plan around one". That rule was never a decision — see
> [Where the no-tests rule came from](#where-the-no-tests-rule-came-from) below.

## Layout

```
Source/
├── Common.Tests/          ← the shared chassis. No demo dependency.
└── Arcade.Tests/          ← all twelve arcade demos, driven headlessly.

Builds/
├── Test-Common.ps1
├── Test-Arcade.ps1
└── Test-All.ps1           ← aggregator, mirrors Build-All.ps1
```

Both are **MSTest** on plain `net10.0` — no Uno SDK, no window, no GPU. That works
because of a property worth stating plainly: **every demo's `Game/` folder is
UI-free.** It is pure simulation plus SkiaSharp types. Everything that touches Uno
lives outside it (`App`, `MainPage`, `GameSurface`, `BackgroundSurface`), so the
entire game family can be driven from an ordinary test host.

```powershell
.\Builds\Test-All.ps1
.\Builds\Test-Common.ps1 -Filter "FullyQualifiedName~Camera2DTests"
.\Builds\Test-Arcade.ps1 -Filter "FullyQualifiedName~AttractSoak"
```

`Test-All.ps1` is deliberately **not** part of `Build-All.ps1`: a failing test should
never block a build.

## Why two projects, not one

The repo's central constraint is [per-demo isolation](01-Overview.md#per-demo-isolation)
— each demo owns its `.sln`, its `global.json`, and its SkiaSharp pin, and the chassis
is source-included rather than project-referenced precisely so those pins can differ.

A single test project referencing every demo would force one SkiaSharp version on all
of them and quietly undo that. So:

- **`Common.Tests`** source-includes `Source/Common/**/*.cs` with the same `<Compile>`
  glob the demos use, and references no demo at all. It tests the chassis as the games
  actually compile it.
- **`Arcade.Tests`** source-includes the chassis *plus* every demo's `Game/` folder,
  against a single SkiaSharp reference.

That second one is only legitimate while the demos agree on a version — and today all
twelve pin `4.151.0`. `ArcadeConventionTests.SkiaSharpPinIsUniform_AcrossEveryArcadeDemo`
fails the moment one diverges, with an explicit instruction **not** to just bump the
number: a diverging demo needs its own test project instead. The coupling is real, so
it is guarded rather than assumed.

`AmbientStarBackdrop.cs` is excluded from both — it derives from Uno's
`SKCanvasElement` and is the only chassis file with a UI dependency.

## What each suite covers

### `Common.Tests` — the chassis

These are the tests [08 – Chassis Extensions](08-Chassis-Extensions.md#camera-unit-tests--planned-never-written)
recorded as "planned, never written", having called the camera seam maths "the easiest
thing to get subtly wrong".

| Area | Notable invariants |
|---|---|
| `Camera2D` | `Wrap` folds negatives (unlike `%`); `WrapDelta` takes the short way round and stays in `(-size/2, size/2]`; `Clamp` holds the viewport inside the world and centres a world smaller than the view; follow easing is frame-rate independent |
| `TileGrid<T>` | Axis-separated resolution *is* the wall slide; sub-stepping stops a 320px step tunnelling a 32px wall; a body already flush does not creep through; a 1-cell corridor does not snag |
| `FlowField` | BFS distance is true shortest-path; walls and sealed pockets are `Unreachable`; multi-source gives distance-to-nearest; a rebuild picks up terrain that changed since the last flood |
| `AsciiMap` | A ragged map throws **before** any cell is emitted |
| `SeamlessTerrain` | Height *and slope* match across the seam — the whole point of the integer-harmonic construction |
| `Vec2`, `HsvColor`, `HighScoreStore` | Zero-vector normalisation returns zero rather than NaN; hue cycling never yields a transparent colour; a corrupt score file fails silent to 0 |

The frame-rate-independence test is paired with a deliberate counter-test
(`Follow_NaiveFixedBlend_WouldFailTheAboveTest`) proving the assertion can actually
distinguish the correct implementation from the obvious wrong one — otherwise a loose
tolerance would let both pass.

### `Arcade.Tests` — all twelve demos

Driven through `DemoRegistry`, which names each demo's types with `typeof` so the
registry fails to **compile** if a `GameWorld` or `Renderer` is renamed or dropped from
the glob. A silently-skipped demo would be worse than a broken build.

| Suite | What it does |
|---|---|
| **Attract soak** | Every demo ships an autopilot, so every demo can play itself with no input. Two minutes idling on the title screen, five minutes of attract mode, both clamped frame times, and a sweep of degenerate viewports (`0x0`, `1x1`, `1920x1`) |
| **Attract cycle** | Forces each world into `GameOver` and asserts it leaves unattended — the cabinet has to come back round. Paku publishes `Mode` as `{ get; private set; }` so it cannot be driven; `PakuStillCyclesOutOfGameOver` guards its mechanism structurally instead |
| **Render smoke** | Renders each demo to an offscreen `SKSurface`: title screen, twenty seconds of attract, awkward canvas sizes, and a check that `Render` leaves the canvas `SaveCount` balanced (`Camera2D.Apply` pushes a `Save` the caller must restore) |
| **Conventions** | Chassis source-included and never project-referenced; exactly the two TFMs; `CompositionTarget.Rendering` and no `DispatcherTimer`; dt clamped; every declared canvas invalidated; wired into both `Build-All` *and* `Publish-Site`; the launcher catalog still at eight entries |

The soak assertions are black-box on purpose — no exception, no NaN, mode never leaves
its own enum. That is what survives refactoring, and it is precisely the class of fault
a green build says nothing about.

### Documented exceptions, asserted rather than skipped

Two demos legitimately differ from the standard, and the tests encode the difference
**exactly** rather than exempting them — so if either is ever modernised, the suite
tells you to delete the exception:

- **Pohaku** predates the 4-state machine and uses `{Demo, Playing, GameOver}`.
- **Paku** uses `{Attract, Playing, GameOver}`; its own enum documents why — *"Paku has
  no separate Title — Attract IS the title screen."*

Pohaku and Paku are also single-canvas (Pohaku hand-rolls its backdrop, Paku draws an
animated plasma), so `Demo_InvalidatesEveryCanvasItDeclares` derives its expectation
from each demo's XAML instead of hardcoding two canvases.

## What is NOT covered

Worth being explicit, because the suite's green light should not be read as more than
it is:

- **The input layer.** `MainPage.xaml.cs` — key handling, the 4-directional latch in
  Eli, pointer events — is not in either project and needs a real window. This is the
  largest untested surface in the repo.
- **Uno/XAML wiring.** `App`, `GameSurface`, `BackgroundSurface`, splash, and the wasm
  bootstrap. `Build-All -Wasm` is what covers those.
- **Visual correctness.** The render smoke tests prove a frame is drawn and is not
  uniformly blank. They say nothing about whether it looks right — use
  [`Capture-Demo.ps1`](06-Build-And-Deploy.md#capturing-a-screenshot) for that.
- **Audio.** Both `HAS_NAUDIO` and `__WASM__` are undefined under the test host, so
  `AudioEngine` compiles to no-ops. That is what makes headless driving possible; it
  also means the voices themselves are unexercised.

## Where the no-tests rule came from

Recorded because the rule looked authoritative and was not:

1. The repo has **never** contained a test file or test project —
   `git log --all --diff-filter=A -- "*Test*"` returns nothing. No commit ever removed
   tests; there was never anything to remove.
2. [08](08-Chassis-Extensions.md#camera-unit-tests--planned-never-written) does not
   endorse the absence — it *regrets* it. The build plan called for camera wrap and
   clamp tests; they were never written **because no test project existed**, leaving
   "empirical coverage rather than regression coverage".
3. The prohibition itself ("don't add one and don't plan around one") appeared only in
   09, whose stated rationale — "ask for tests and you get a new test-infrastructure
   decision you didn't want to make" — rationalised the status quo rather than
   recording a decision.

An observation about the repo's state had hardened into a rule. The infrastructure
decision it feared turned out to be small: two csproj files, three PowerShell wrappers,
and no change to how any demo builds.

## Adding a demo

`Arcade.Tests` covers a new game the moment two things are true:

1. Its `Game/**/*.cs` is added to the `<Compile>` list in `Arcade.Tests.csproj`.
2. It is added to `DemoRegistry.All`.

Everything else — soak, render smoke, and every convention check — is data-driven off
that registry and applies automatically. Step 2 will not compile until step 1 is done,
which is the intended order.
