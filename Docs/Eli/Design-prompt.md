You are adding a new arcade-family game to the UnoSkiaDemos repo. This phase is design only.

REQUIRED READING, in order:
  Docs/Architecture/01-Overview.md, 02-Demo-Anatomy.md, 03-Shared-Chassis.md,
  04-Rendering-Pipeline.md, 05-Audio.md, 06-Build-And-Deploy.md, 08-Chassis-Extensions.md,
  Docs/Koa/DESIGN.md, and all of Source/Koa/.

IDENTITY
  Display name: 'ELI   Folder/namespace: Eli   Slug: eli   ApplicationId: com.companyname.eli
  Gloss: Hawaiian "to dig"     Homage: Dig Dug
  Accent: new SKColor(0xFF, 0xAA, 0x33)  (dirt amber — keep it clear of the rock-fall warning red)

ELEVATOR PITCH
  You are a digger in a side-on field of packed dirt, four strata deep, each a different hue. You
  carve tunnels wherever you walk, harpoon-pump the two enemy types that patrol them until they
  burst, and drop the boulders suspended in the dirt onto anything underneath. Clearing the field
  advances a level; being touched by an un-inflated enemy kills you. Levels are authored ASCII.

REFERENCE DEMO: copy Source/Koa/ and rename Koa -> Eli throughout, then replace Game/.

WHAT'S DIFFERENT FROM KOA
  - Terrain is MUTABLE. Koa's TileGrid is authored once and only ever flips Door -> Floor. Here the
    player rewrites it continuously: walking carves Dirt -> Tunnel. Every consumer of grid state
    (flow field, boulder support, culling) has to tolerate per-frame terrain edits.
  - The weapon is a stateful extending segment, not a projectile. The harpoon grows from the digger
    along its facing until it hits dirt or an enemy, then holds; pumping an attached enemy inflates
    it over several presses until it bursts. Koa's Projectile is fire-and-forget — this is not that.
  - Gravity applies to terrain features, not to the player. A boulder with no dirt beneath it
    wobbles, then falls, crushing enemies and the player alike. Nothing in the repo has falling
    terrain.
  - One enemy type can leave the tunnels: it flattens into a ghost, phases through dirt on a
    straight line toward the digger, and rematerialises. Flow-field routing does not apply in that
    mode, so its AI is two-mode, not one.

CHASSIS CONTRACT
  Reuse, don't reimplement: Camera2D (Clamp both axes, snap follow — the field is taller and wider
  than the viewport), TileGrid<Tile>, AsciiMap, FlowField (rebuild on terrain edit as well as on
  the timer), VectorShapes, HudText, Marquee + DrawRainbowTitle, AmbientStarBackdrop (override
  BgTop/BgBottom to underground browns), HighScoreStore("Eli"), AudioEngineBase.
  I expect no new chassis pieces. If you conclude one is needed, propose it in the design doc with
  a P0/P1/P2 priority and the other demos that would consume it — do not build it inline, and
  leave P2 unbuilt.

NON-NEGOTIABLES
- 4-state GameMode { Title, Playing, GameOver, Attract }, with a working Attract autopilot.
  Pohaku's 3-state {Demo, Playing, GameOver} is legacy — do not copy it. Title idles to Attract
  after 12s.
- Drive the loop from CompositionTarget.Rendering, never a DispatcherTimer. dt from a Stopwatch,
  clamped to [1/60, 1/30]. Invalidate both canvases every tick.
- Two canvas layers: BackgroundSurface (sealed : Arcade.Common.AmbientStarBackdrop, override
  BgTop/BgBottom if the mood needs it) behind the GameSurface.
- Renderer is a static Render(SKCanvas, GameWorld, float canvasW, float canvasH) — stateless; game
  state lives in GameWorld. Follow the five-step body in 04: background, transform, world draws,
  restore, HUD in canvas-pixel coords.
- Every glowing element is a halo + sharp double pass, via NeonDraw / HudText / NeonPaints. Any
  helper that mutates a shared paint's StrokeWidth must restore the default before returning.
- The chassis is source-included via <Compile Include="..\..\Common\**\*.cs" .../>. Never create a
  Common.csproj or a ProjectReference to it.
- Per-demo isolation: own .sln, Directory.Build.props, Directory.Packages.props, global.json.
  Nothing build-related at the repo root. Do not modify any other demo.
- Pin SkiaSharp 4.151.0 and Uno.Sdk 6.7.0-dev.164 (match the reference demo exactly).
  TargetFrameworks: net10.0-browserwasm;net10.0-desktop. No mobile TFMs.
- Audio: static AudioEngine facade + AudioEngineImpl : AudioEngineBase. NAudio ISampleProviders
  under #if HAS_NAUDIO, mirrored voice-for-voice in Platforms/WebAssembly/WasmScripts/audio.js
  under globalThis.<slug>Audio, with the audio.js <EmbeddedResource> entry in the csproj.
  AudioEngine.Init() in App.OnLaunched.
- HighScoreStore("<Name>") for the high score (desktop-only persistence is expected).
- Splash: <UnoSplashScreen ... Color="#050014" />.

WIRING
Wire the new demo into:
- Builds/Build-Eli.ps1 and Builds/Run-Eli.ps1 (copies of the reference demo's, renamed)
- Builds/Build-All.ps1     — append 'Build-Eli.ps1' to $scripts
- Builds/Publish-Site.ps1  — append @{ Name = 'Eli'; Slug = 'eli' } to $games
- Docs/Eli/DESIGN.md    — the plan itself
- README.md                — one row in the demo table, one line in the Layout tree

Do NOT add an entry to Source/Launcher/Launcher/Game/GameCatalog.cs. The launcher stays at its
original eight cards; extra cards broke its grid layout, and new games ship standalone. See
Docs/Architecture/08-Chassis-Extensions.md. (02-Demo-Anatomy step 6 and the root README's
"Adding a new demo" step 4 both predate this reversal — ignore that part of them.)

DELIVERABLE
  Docs/Eli/DESIGN.md only — no code this phase. Mirror the section structure of Docs/Koa/DESIGN.md.
  Cite exact file:line references into Koa and Hahai for every claim about existing behaviour. Give
  starting values for every tunable: dig speed, harpoon extend speed, pumps-to-burst, boulder wobble
  delay and fall speed, ghost trigger distance and phase speed, per-level enemy counts.

OUT OF SCOPE
Out of scope — do not do these:
- No test project. There is none in the repo; don't add one and don't plan around one.
- Don't build Pool<T> or an Entity2D base. Both are settled "no" in 08.
- Don't unify SkiaSharp versions across demos, and don't touch KahuaNetwork (SkiaSharp 3) or
  UnoGallery's $(SkiaSharpVersion) switch.
- No mobile TFMs, no multiplayer, no save state beyond the high score.
- Don't refactor Source/Common/ except through the P0/P1/P2 proposal in the design doc.