using System;
using System.Collections.Generic;
using Arcade.Common;
using Arcade.Common.Chassis;

namespace Kiai.Game;

// The per-frame brain for Kia'i. Owns the toroidal Camera2D, the seamless
// Terrain, the player Ship, all entity lists, the wave/spawn timers, the rescue
// bookkeeping, and the 4-state Title/Playing/GameOver/Attract machine.
//
// The two architecturally novel pieces this game exists to prove:
//   * The wrapping camera — Camera2D configured X = Wrap(WorldWidth), Y = Free,
//     with look-ahead in the ship's facing direction. The camera follows the ship
//     along the torus (shortest signed path, never "the long way" across the seam).
//   * Toroidal distance everywhere — collision and AI targeting use
//     Camera2D.WrapDelta so shots, hunts, and catches all work across the seam.
public class GameWorld
{
    // --- World dimensions -----------------------------------------------------

    // The visible viewport (pixels), updated by Resize. World units ARE pixels;
    // there is no fit-to-screen scale.
    public float ViewW = 1280f;
    public float ViewH = 720f;

    // WorldWidth is fixed at StartGame (~4 screens wide) so the terrain is stable
    // across resizes; only the view changes on resize. WorldHeight tracks the
    // canvas height (the world is one screen tall).
    public float WorldWidth = 1280f * 4f;
    public float WorldHeight = 720f;

    // The reference view width used to size the fixed WorldWidth — captured at the
    // first StartGame so a later resize doesn't reshape the planet.
    const float ReferenceViewW = 1280f;
    const float WorldWidthScreens = 4f;

    // --- Shared chassis pieces ------------------------------------------------

    public readonly Camera2D Camera = new();
    public Terrain Terrain = null!;     // (re)built each StartGame
    public readonly Radar Radar = new();

    // --- Entities -------------------------------------------------------------

    public Ship Ship = new();
    public List<Humanoid> Humanoids = new();
    public List<Lander>   Landers   = new();
    public List<Mutant>   Mutants   = new();
    public List<Baiter>   Baiters   = new();
    public List<Bomber>   Bombers   = new();
    public List<Pod>      Pods      = new();
    public List<Swarmer>  Swarmers  = new();
    public List<Bullet>   Bullets   = new();
    public List<Particle> Particles = new();

    // --- Mode + score ---------------------------------------------------------

    public GameMode Mode = GameMode.Title;
    public int Score;
    public int HighScore;
    public int Wave = 1;
    public int HumanoidsRemaining;

    // Visual one-shot: a brief full-screen flash (planet explosion / smart bomb).
    public float ScreenFlash;

    // --- Tuning ---------------------------------------------------------------

    const float CeilingY = 48f;          // top of flyable space (radar sits above)
    const float Clearance = 22f;         // how far above the ground the ship is held
    const float ShipShootInterval = 0.16f;
    const int   MaxShipBullets = 6;
    const float BulletSpeed = 900f;

    const int   StartHumanoids = 10;
    const int   StartLives = 3;
    const int   StartSmartBombs = 3;

    const float LanderHunt = 70f;        // lander cruise speed
    const float LanderLift = 60f;        // climb speed while carrying
    const float LanderDescend = 90f;
    const float LanderShootSpeed = 360f;
    const float MutantSpeed = 240f;
    const float BaiterSpeed = 320f;
    const float HumanoidFall = 180f;     // terminal-ish fall speed
    const float SafeFallSpeed = 130f;    // land slower than this and the colonist survives

    // Wave choreography timers.
    float _spawnTimer;
    float _baiterTimer;       // counts down; a Baiter spawns when the player lingers
    float _gameOverTimer;
    float _titleIdleTimer;
    float _attractAiTimer;
    int   _landersToSpawn;    // remaining landers queued for this wave

    const float AttractIdleThreshold = 10f;   // title idle seconds before attract
    const float BaiterLinger = 18f;            // seconds in a wave before a baiter shows

    public bool ShowAttractText => (_titleIdleTimer % 1.2f) < 0.7f;

    readonly Random _rng = new();
    static readonly HighScoreStore HighScoreStore = new("Kiai");

    public GameWorld()
    {
        HighScore = HighScoreStore.Load();
        ConfigureCamera();
        ResetForTitle();
    }

    // Configure the camera once: X wraps the torus with look-ahead in the facing
    // direction; Y is free (the world is one screen tall, no vertical scroll).
    void ConfigureCamera()
    {
        Camera.X = new CameraAxis { Mode = AxisMode.Wrap, WorldSize = WorldWidth, LookAhead = ViewW * 0.25f, FollowRate = 3.5f };
        Camera.Y = new CameraAxis { Mode = AxisMode.Free };
        Camera.Zoom = 1f;
        Camera.SetViewport(ViewW, ViewH);
    }

    // Resize changes only the *view*: viewport size + one-screen WorldHeight +
    // the look-ahead (a fraction of the view). It never touches WorldWidth or the
    // terrain, so the planet stays stable while the window resizes.
    public void Resize(float w, float h)
    {
        ViewW = MathF.Max(320f, w);
        ViewH = MathF.Max(240f, h);
        WorldHeight = ViewH;
        Camera.SetViewport(ViewW, ViewH);
        Camera.X.LookAhead = ViewW * 0.25f;
    }

    // --- Mode transitions -----------------------------------------------------

    public void StartGame() => StartGameInternal(GameMode.Playing);
    public void StartAttract() => StartGameInternal(GameMode.Attract);

    void StartGameInternal(GameMode mode)
    {
        Mode = mode;
        Score = 0;
        Wave = 1;
        _titleIdleTimer = 0f;
        _planetLost = false;   // clear any leftover "planet lost" flag from a prior run

        // Fix the world width to ~4 reference screens, once, so terrain is stable.
        WorldWidth = ReferenceViewW * WorldWidthScreens;
        Camera.X.WorldSize = WorldWidth;
        Radar.SetWorld(WorldWidth, WorldHeight);

        Terrain = new Terrain(WorldWidth, WorldHeight, new Random(12345));

        ClearEntities();

        Ship = new Ship
        {
            Position = new Vec2(0f, WorldHeight * 0.4f),
            Velocity = Vec2.Zero,
            InvincibleTime = mode == GameMode.Attract ? 0f : 2.5f,
            Lives = mode == GameMode.Attract ? 9999 : StartLives,
            SmartBombs = StartSmartBombs,
            FacingSign = 1f,
        };
        Camera.Snap(Ship.Position.X, Ship.Position.Y);

        SpawnHumanoids(StartHumanoids);
        StartWave(1);
    }

    public void ReturnToTitle() => ResetForTitle();

    void ResetForTitle()
    {
        Mode = GameMode.Title;
        _titleIdleTimer = 0f;
        // Build a simple stand-still scene behind the title so it isn't empty.
        WorldWidth = ReferenceViewW * WorldWidthScreens;
        Camera.X.WorldSize = WorldWidth;
        Radar.SetWorld(WorldWidth, WorldHeight);
        Terrain = new Terrain(WorldWidth, WorldHeight, new Random(12345));
        ClearEntities();
        Ship = new Ship { Position = new Vec2(0f, WorldHeight * 0.4f) };
        Camera.Snap(Ship.Position.X, Ship.Position.Y);
        SpawnHumanoids(StartHumanoids);
    }

    void ClearEntities()
    {
        Humanoids.Clear(); Landers.Clear(); Mutants.Clear(); Baiters.Clear();
        Bombers.Clear(); Pods.Clear(); Swarmers.Clear(); Bullets.Clear(); Particles.Clear();
        Ship.Carrying = null;
    }

    // --- Wave setup -----------------------------------------------------------

    void SpawnHumanoids(int count)
    {
        Humanoids.Clear();
        HumanoidsRemaining = count;
        foreach (float x in Terrain.PickHumanoidSpots(count, _rng))
        {
            float gy = Terrain.HeightAt(x);
            Humanoids.Add(new Humanoid
            {
                Position = new Vec2(x, gy - 7f),
                GroundY = gy,
                State = HumanoidState.Standing,
            });
        }
    }

    void StartWave(int wave)
    {
        Wave = wave;
        _baiterTimer = BaiterLinger;
        // More landers each wave; the lander count scales but is capped.
        _landersToSpawn = Math.Min(6 + wave * 2, 22);
        _spawnTimer = 1.0f;

        // A couple of secondary threats appear from wave 2+.
        if (wave >= 2)
        {
            SpawnBomber();
            if (wave >= 3) SpawnPod();
        }
    }

    // --- Public actions (input-driven) ----------------------------------------

    public void FireBullet()
    {
        if (Ship.ShootCooldown > 0 || !Ship.Alive) return;
        int active = 0;
        foreach (var b in Bullets) if (b.Alive && b.FromShip) active++;
        if (active >= MaxShipBullets) return;

        var dir = new Vec2(Ship.FacingSign, 0f);
        Bullets.Add(new Bullet
        {
            Position = Ship.Position + dir * (Ship.Radius + 4f),
            Velocity = new Vec2(Ship.FacingSign * BulletSpeed, 0f) + new Vec2(Ship.Velocity.X * 0.3f, 0f),
            FromShip = true,
        });
        Ship.ShootCooldown = ShipShootInterval;
        AudioEngine.PlayShoot();
    }

    // Smart bomb: destroy every on-screen threat (within the visible viewport,
    // toroidally), award score, flash the screen. Falling humanoids are spared.
    public void SmartBomb()
    {
        if (Ship.SmartBombs <= 0 || !Ship.Alive) return;
        Ship.SmartBombs--;
        ScreenFlash = 0.5f;
        AudioEngine.PlaySmartBomb();

        float halfView = ViewW / 2f + 40f;
        bool OnScreen(Vec2 p) => MathF.Abs(Camera2D.WrapDelta(Camera.CenterX, p.X, WorldWidth)) <= halfView;

        foreach (var l in Landers) if (l.Alive && OnScreen(l.Position)) { DropCaptive(l); KillLander(l, scored: true); }
        foreach (var m in Mutants) if (m.Alive && OnScreen(m.Position)) { Explode(m.Position, 14); m.Alive = false; AddScore(150); }
        foreach (var b in Baiters) if (b.Alive && OnScreen(b.Position)) { Explode(b.Position, 14); b.Alive = false; AddScore(200); }
        foreach (var b in Bombers) if (b.Alive && OnScreen(b.Position)) { Explode(b.Position, 16); b.Alive = false; AddScore(250); }
        foreach (var p in Pods)    if (p.Alive && OnScreen(p.Position)) { Explode(p.Position, 12); p.Alive = false; AddScore(100); }
        foreach (var s in Swarmers) if (s.Alive && OnScreen(s.Position)) { Explode(s.Position, 8); s.Alive = false; AddScore(50); }
    }

    public void HyperSpace()
    {
        if (!Ship.Alive) return;
        AudioEngine.PlayHyperspace();
        // Teleport to a random world X at mid-height; velocity zeroed.
        float x = (float)_rng.NextDouble() * WorldWidth;
        Ship.Position = new Vec2(x, WorldHeight * 0.35f);
        Ship.Velocity = Vec2.Zero;
        // 1-in-10 bad jump.
        if (_rng.Next(10) == 0) KillShip();
        else Ship.InvincibleTime = MathF.Max(Ship.InvincibleTime, 1.0f);
    }

    // --- Main update ----------------------------------------------------------

    public void Update(float dt)
    {
        if (Score > HighScore) HighScore = Score;
        if (ScreenFlash > 0f) ScreenFlash -= dt;

        if (Mode == GameMode.Title)
        {
            // Idle on the title long enough -> attract autopilot.
            _titleIdleTimer += dt;
            // Keep the camera gently drifting so the title scene isn't dead.
            Camera.FollowLookAhead(Ship.Position.X, Ship.Position.Y, Ship.FacingSign, 0f, dt);
            Radar.SetRect(0, 6, ViewW, 38);
            Radar.WrapX = true; Radar.FocusX = Ship.Position.X;
            if (_titleIdleTimer >= AttractIdleThreshold) StartAttract();
            return;
        }

        if (Mode == GameMode.GameOver)
        {
            _gameOverTimer -= dt;
            // Let particles settle behind the placard.
            UpdateParticles(dt);
            if (_gameOverTimer <= 0f)
            {
                if (Score > HighScore) HighScore = Score;
                HighScoreStore.Save(HighScore);
                ResetForTitle();
            }
            UpdateAudioState();
            return;
        }

        if (Mode == GameMode.Attract) UpdateAttractAI(dt);

        // 1) Camera follow with look-ahead in the ship's facing direction. The
        //    Wrap axis eases along the torus so it never scrolls the long way.
        Camera.FollowLookAhead(Ship.Position.X, Ship.Position.Y, Ship.FacingSign, 0f, dt);

        // 2) Integrate the ship, then clamp Y to [ceiling, terrain - clearance].
        Ship.Update(dt, WorldWidth, WorldHeight);
        ClampShipToWorld();
        EmitThrustTrail(dt);

        // 3) Enemies / humanoids / bullets / particles.
        UpdateLanders(dt);
        UpdateMutants(dt);
        UpdateBaiters(dt);
        UpdateBombers(dt);
        UpdatePods(dt);
        UpdateSwarmers(dt);
        UpdateHumanoids(dt);
        foreach (var b in Bullets) b.Update(dt, WorldWidth, WorldHeight);
        UpdateParticles(dt);

        // 4) Wave / spawn timers.
        UpdateSpawning(dt);

        // 5) Toroidal collisions.
        HandleCollisions();

        // 6) Resolve humanoid states (catch / land / splat already inline; remove dead).
        // 7) Sweep dead entities.
        RemoveDead();

        // 8) Wave-clear / lose check.
        CheckWaveAndLose();

        // 9) Audio state (looping thrust voice).
        UpdateAudioState();

        // Radar follows the ship each frame.
        Radar.SetRect(0, 6, ViewW, 38);
        Radar.WrapX = true;
        Radar.FocusX = Ship.Position.X;
    }

    // Hold the ship inside the flyable band: above the ceiling, above the ground
    // by Clearance. Bouncy-stop on contact so it doesn't tunnel through terrain.
    void ClampShipToWorld()
    {
        float ground = Terrain.HeightAt(Ship.Position.X) - Clearance;
        if (Ship.Position.Y > ground) { Ship.Position.Y = ground; if (Ship.Velocity.Y > 0) Ship.Velocity.Y = 0; }
        if (Ship.Position.Y < CeilingY) { Ship.Position.Y = CeilingY; if (Ship.Velocity.Y < 0) Ship.Velocity.Y = 0; }
    }

    void EmitThrustTrail(float dt)
    {
        if (!Ship.ThrustingAny || !Ship.Alive) return;
        if (_rng.NextDouble() < 0.7)
        {
            var back = new Vec2(-Ship.FacingSign, 0f);
            Particles.Add(new Particle(
                Ship.Position + back * Ship.Radius,
                Ship.Velocity * 0.2f + back * (60f + (float)_rng.NextDouble() * 60f) + new Vec2(0f, ((float)_rng.NextDouble() - 0.5f) * 40f),
                0.32f, isMine: false, color: 0xFFFF8833u));
        }
    }

    // --- Attract autopilot ----------------------------------------------------

    // A simple bot: head toward the nearest threatening lander/mutant, keep a
    // matching altitude, and fire when roughly lined up horizontally. Good enough
    // to look like real play.
    void UpdateAttractAI(float dt)
    {
        _attractAiTimer -= dt;
        Ship.ThrustLeft = Ship.ThrustRight = Ship.ThrustUp = Ship.ThrustDown = false;
        if (!Ship.Alive) return;

        Vec2? target = null;
        float bestD = float.MaxValue;
        void Consider(Vec2 p)
        {
            float d = MathF.Abs(Camera2D.WrapDelta(Ship.Position.X, p.X, WorldWidth));
            if (d < bestD) { bestD = d; target = p; }
        }
        foreach (var l in Landers) if (l.Alive) Consider(l.Position);
        foreach (var m in Mutants) if (m.Alive) Consider(m.Position);

        if (target is Vec2 t)
        {
            float dx = Camera2D.WrapDelta(Ship.Position.X, t.X, WorldWidth);
            if (dx < -10f) Ship.ThrustLeft = true;
            else if (dx > 10f) Ship.ThrustRight = true;
            float dy = t.Y - Ship.Position.Y;
            if (dy < -10f) Ship.ThrustUp = true;
            else if (dy > 10f) Ship.ThrustDown = true;
            // Face the target and fire when lined up.
            if (MathF.Abs(dx) > 1f) Ship.FacingSign = MathF.Sign(dx);
            if (MathF.Abs(dy) < 24f && _attractAiTimer <= 0f)
            {
                FireBullet();
                _attractAiTimer = 0.25f + (float)_rng.NextDouble() * 0.25f;
            }
        }
        else
        {
            // Nothing to shoot — drift right to keep the world scrolling.
            Ship.ThrustRight = true;
        }
    }

    // --- Spawning -------------------------------------------------------------

    void UpdateSpawning(float dt)
    {
        // Trickle landers in over the wave.
        if (_landersToSpawn > 0)
        {
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                SpawnLander();
                _landersToSpawn--;
                _spawnTimer = 1.6f + (float)_rng.NextDouble() * 1.2f;
            }
        }

        // Baiter-on-linger: while the wave drags on, periodically spawn a fast
        // harasser to push the player.
        _baiterTimer -= dt;
        if (_baiterTimer <= 0f)
        {
            SpawnBaiter();
            _baiterTimer = 12f + (float)_rng.NextDouble() * 8f;
        }
    }

    void SpawnLander()
    {
        float x = (float)_rng.NextDouble() * WorldWidth;
        Landers.Add(new Lander
        {
            Position = new Vec2(x, CeilingY + 6f),
            Velocity = new Vec2(0f, LanderDescend),
            State = LanderState.Descending,
            RetargetTimer = 0f,
            ShootTimer = 1.5f + (float)_rng.NextDouble(),
        });
    }

    void SpawnBaiter()
    {
        float x = Camera2D.Wrap(Ship.Position.X + WorldWidth / 2f, WorldWidth);
        Baiters.Add(new Baiter
        {
            Position = new Vec2(x, CeilingY + 40f + (float)_rng.NextDouble() * 80f),
            WavePhase = (float)_rng.NextDouble() * MathF.Tau,
        });
    }

    void SpawnBomber()
    {
        float x = (float)_rng.NextDouble() * WorldWidth;
        Bombers.Add(new Bomber
        {
            Position = new Vec2(x, CeilingY + 60f),
            Velocity = new Vec2((_rng.Next(2) == 0 ? -1f : 1f) * 70f, 0f),
            WavePhase = (float)_rng.NextDouble() * MathF.Tau,
            MineTimer = 1.5f,
        });
    }

    void SpawnPod()
    {
        float x = (float)_rng.NextDouble() * WorldWidth;
        Pods.Add(new Pod
        {
            Position = new Vec2(x, CeilingY + 90f + (float)_rng.NextDouble() * 60f),
            Velocity = new Vec2((_rng.Next(2) == 0 ? -1f : 1f) * 40f, 0f),
            WavePhase = (float)_rng.NextDouble() * MathF.Tau,
        });
    }

    // --- Lander AI + abduction state machine ----------------------------------

    void UpdateLanders(float dt)
    {
        foreach (var l in Landers)
        {
            if (!l.Alive) continue;
            switch (l.State)
            {
                case LanderState.Descending:
                    l.Position += l.Velocity * dt;
                    // Once down into the play band, start hunting.
                    if (l.Position.Y >= CeilingY + 120f)
                    {
                        l.State = LanderState.Hunting;
                        l.Velocity = Vec2.Zero;
                    }
                    break;

                case LanderState.Hunting:
                {
                    // (Re)acquire the nearest standing humanoid on the torus.
                    l.RetargetTimer -= dt;
                    if (l.Target == null || l.Target.State != HumanoidState.Standing || l.RetargetTimer <= 0f)
                    {
                        l.Target = NearestStandingHumanoid(l.Position.X);
                        l.RetargetTimer = 1.0f;
                    }
                    if (l.Target == null)
                    {
                        // No one left to grab — just cruise (still a threat / shooter).
                        l.State = LanderState.Cruising;
                        break;
                    }
                    // Move toward the target along the torus + descend to ground.
                    MoveTowardX(ref l.Position, l.Target.Position.X, LanderHunt, dt);
                    float gy = Terrain.HeightAt(l.Position.X) - 16f;
                    l.Position.Y += MathF.Sign(gy - l.Position.Y) * LanderHunt * dt;
                    // Close enough horizontally + near ground -> seize it.
                    if (MathF.Abs(Camera2D.WrapDelta(l.Position.X, l.Target.Position.X, WorldWidth)) < 14f &&
                        MathF.Abs(gy - l.Position.Y) < 24f)
                    {
                        l.Target.State = HumanoidState.Seized;
                        l.Target.Captor = l;
                        l.State = LanderState.Lifting;
                    }
                    MaybeLanderShoot(l, dt);
                    break;
                }

                case LanderState.Lifting:
                {
                    if (l.Target == null || l.Target.State != HumanoidState.Seized)
                    {
                        // Lost the captive (shot away) — back to hunting.
                        l.State = LanderState.Hunting; l.Target = null; break;
                    }
                    l.Position.Y -= LanderLift * dt;
                    // Drag the humanoid along just below the lander.
                    l.Target.Position = new Vec2(l.Position.X, l.Position.Y + 16f);
                    // Reached the ceiling with the captive: consume + mutate.
                    if (l.Position.Y <= CeilingY + 4f)
                    {
                        l.Target.State = HumanoidState.Dead;
                        l.Target.Alive = false;
                        HumanoidsRemaining = Math.Max(0, HumanoidsRemaining - 1);
                        MutateLander(l);
                    }
                    MaybeLanderShoot(l, dt);
                    break;
                }

                case LanderState.Cruising:
                    // Drift slowly; if a humanoid becomes available again, hunt.
                    l.Position.X += LanderHunt * 0.5f * dt;
                    if (NearestStandingHumanoid(l.Position.X) is Humanoid) l.State = LanderState.Hunting;
                    MaybeLanderShoot(l, dt);
                    break;
            }
            l.Position.X = Camera2D.Wrap(l.Position.X, WorldWidth);
        }
    }

    // Landers fire aimed-with-inaccuracy shots at the ship (mirrors Pohaku's
    // saucer aim logic): atan2 toward the ship +/- a small random spread.
    void MaybeLanderShoot(Lander l, float dt)
    {
        l.ShootTimer -= dt;
        if (l.ShootTimer > 0f) return;
        l.ShootTimer = 1.8f + (float)_rng.NextDouble() * 1.6f;
        if (!Ship.Alive) return;
        // Only shoot if the ship is within a screen toroidally (don't snipe from
        // the far side of the planet).
        float dx = Camera2D.WrapDelta(l.Position.X, Ship.Position.X, WorldWidth);
        if (MathF.Abs(dx) > ViewW * 0.7f) return;
        float dy = Ship.Position.Y - l.Position.Y;
        float ang = MathF.Atan2(dy, dx) + ((float)_rng.NextDouble() - 0.5f) * 0.35f;
        var dir = Vec2.FromAngle(ang);
        Bullets.Add(new Bullet
        {
            Position = l.Position + dir * (l.Radius + 2f),
            Velocity = dir * LanderShootSpeed,
            FromShip = false,
            Lifetime = 2.0f,
        });
    }

    void MutateLander(Lander l)
    {
        l.Alive = false;
        AudioEngine.PlayMutate();
        Mutants.Add(new Mutant
        {
            Position = l.Position,
            Velocity = Vec2.Zero,
            WobblePhase = (float)_rng.NextDouble() * MathF.Tau,
            ShootTimer = 1.0f + (float)_rng.NextDouble(),
        });
    }

    // --- Mutant / Baiter / Bomber / Pod / Swarmer AI --------------------------

    void UpdateMutants(float dt)
    {
        foreach (var m in Mutants)
        {
            if (!m.Alive) continue;
            m.WobblePhase += dt * 9f;
            // Aggressive homing toward the ship along the torus, plus a wobble.
            float dx = Camera2D.WrapDelta(m.Position.X, Ship.Position.X, WorldWidth);
            float dy = Ship.Position.Y - m.Position.Y;
            var to = new Vec2(dx, dy).Normalized();
            var wobble = new Vec2(MathF.Cos(m.WobblePhase), MathF.Sin(m.WobblePhase)) * 0.5f;
            m.Velocity = (to + wobble).Normalized() * MutantSpeed;
            m.Position += m.Velocity * dt;
            m.Position.X = Camera2D.Wrap(m.Position.X, WorldWidth);
            m.Position.Y = Math.Clamp(m.Position.Y, CeilingY, Terrain.HeightAt(m.Position.X) - 8f);
        }
    }

    void UpdateBaiters(float dt)
    {
        foreach (var b in Baiters)
        {
            if (!b.Alive) continue;
            b.WavePhase += dt * 2.4f;
            // Race toward the ship horizontally, bob vertically.
            float dx = Camera2D.WrapDelta(b.Position.X, Ship.Position.X, WorldWidth);
            b.Position.X += MathF.Sign(dx) * BaiterSpeed * dt;
            b.Position.Y += MathF.Sin(b.WavePhase) * 60f * dt
                          + MathF.Sign(Ship.Position.Y - b.Position.Y) * 40f * dt;
            b.Position.X = Camera2D.Wrap(b.Position.X, WorldWidth);
            b.Position.Y = Math.Clamp(b.Position.Y, CeilingY, Terrain.HeightAt(b.Position.X) - 8f);
        }
    }

    void UpdateBombers(float dt)
    {
        foreach (var b in Bombers)
        {
            if (!b.Alive) continue;
            b.WavePhase += dt;
            b.Position += b.Velocity * dt;
            b.Position.Y += MathF.Sin(b.WavePhase * 0.8f) * 20f * dt;
            b.Position.X = Camera2D.Wrap(b.Position.X, WorldWidth);
            b.Position.Y = Math.Clamp(b.Position.Y, CeilingY + 20f, Terrain.HeightAt(b.Position.X) - 30f);
            // Lay a mine periodically.
            b.MineTimer -= dt;
            if (b.MineTimer <= 0f)
            {
                b.MineTimer = 1.4f + (float)_rng.NextDouble();
                Particles.Add(new Particle(b.Position, Vec2.Zero, 6f, isMine: true, color: 0xFFFF5522u));
            }
        }
    }

    void UpdatePods(float dt)
    {
        foreach (var p in Pods)
        {
            if (!p.Alive) continue;
            p.WavePhase += dt * 0.6f;
            p.Position += p.Velocity * dt;
            p.Position.Y += MathF.Sin(p.WavePhase) * 14f * dt;
            p.Position.X = Camera2D.Wrap(p.Position.X, WorldWidth);
        }
    }

    void UpdateSwarmers(float dt)
    {
        foreach (var s in Swarmers)
        {
            if (!s.Alive) continue;
            s.WobblePhase += dt * 7f;
            float dx = Camera2D.WrapDelta(s.Position.X, Ship.Position.X, WorldWidth);
            float dy = Ship.Position.Y - s.Position.Y;
            var to = new Vec2(dx, dy).Normalized();
            var wob = new Vec2(MathF.Cos(s.WobblePhase), MathF.Sin(s.WobblePhase)) * 0.6f;
            s.Velocity = (to + wob).Normalized() * 200f;
            s.Position += s.Velocity * dt;
            s.Position.X = Camera2D.Wrap(s.Position.X, WorldWidth);
            s.Position.Y = Math.Clamp(s.Position.Y, CeilingY, Terrain.HeightAt(s.Position.X) - 6f);
        }
    }

    // Burst a Pod into several Swarmers (reuse split pattern from Pohaku).
    void SplitPod(Pod p)
    {
        p.Alive = false;
        Explode(p.Position, 10);
        int n = 3 + _rng.Next(2);
        for (int i = 0; i < n; i++)
        {
            Swarmers.Add(new Swarmer
            {
                Position = p.Position,
                Velocity = Vec2.FromAngle((float)_rng.NextDouble() * MathF.Tau, 120f),
                WobblePhase = (float)_rng.NextDouble() * MathF.Tau,
            });
        }
    }

    // --- Humanoid state resolution --------------------------------------------

    void UpdateHumanoids(float dt)
    {
        foreach (var h in Humanoids)
        {
            if (!h.Alive) continue;
            switch (h.State)
            {
                case HumanoidState.Standing:
                    // Keep glued to the ground (terrain may differ after redeposit).
                    h.Position.Y = h.GroundY - 7f;
                    break;

                case HumanoidState.Seized:
                    // Position is driven by the captor in UpdateLanders; nothing here.
                    break;

                case HumanoidState.Falling:
                    h.Velocity.Y = MathF.Min(h.Velocity.Y + 380f * dt, HumanoidFall);
                    h.Update(dt, WorldWidth, WorldHeight);
                    float gy = Terrain.HeightAt(h.Position.X);
                    if (h.Position.Y >= gy - 7f)
                    {
                        // Hit the ground. Survive a soft landing, splat a hard one.
                        if (h.Velocity.Y <= SafeFallSpeed)
                        {
                            h.State = HumanoidState.Standing;
                            h.GroundY = gy;
                            h.Position = new Vec2(h.Position.X, gy - 7f);
                            h.Velocity = Vec2.Zero;
                        }
                        else
                        {
                            h.State = HumanoidState.Dead;
                            h.Alive = false;
                            HumanoidsRemaining = Math.Max(0, HumanoidsRemaining - 1);
                            Explode(h.Position, 8);
                            AudioEngine.PlayHumanoidLost();
                        }
                    }
                    break;

                case HumanoidState.Caught:
                    // Rides just below the ship until landed.
                    h.Position = new Vec2(Ship.Position.X, Ship.Position.Y + Ship.Radius + 8f);
                    // If the ship is near the ground, deposit it (Standing + chime + score).
                    float ground = Terrain.HeightAt(Ship.Position.X);
                    if (Ship.Position.Y >= ground - Clearance - 6f)
                    {
                        h.State = HumanoidState.Standing;
                        h.GroundY = ground;
                        h.Position = new Vec2(Ship.Position.X, ground - 7f);
                        h.Velocity = Vec2.Zero;
                        Ship.Carrying = null;
                        AddScore(500);
                        AudioEngine.PlayHumanoidRescued();
                    }
                    break;
            }
        }
    }

    Humanoid? NearestStandingHumanoid(float worldX)
    {
        Humanoid? best = null;
        float bestD = float.MaxValue;
        foreach (var h in Humanoids)
        {
            if (!h.Alive || h.State != HumanoidState.Standing) continue;
            float d = MathF.Abs(Camera2D.WrapDelta(worldX, h.Position.X, WorldWidth));
            if (d < bestD) { bestD = d; best = h; }
        }
        return best;
    }

    // --- Particles ------------------------------------------------------------

    void UpdateParticles(float dt)
    {
        foreach (var p in Particles) p.Update(dt, WorldWidth, WorldHeight);
    }

    // --- Collisions (toroidal X) ----------------------------------------------

    // Circle-circle overlap with the X distance measured on the torus, so a shot
    // one pixel past the seam still connects with a target one pixel before it.
    bool Hit(Vec2 a, float ra, Vec2 b, float rb)
    {
        float dx = Camera2D.WrapDelta(a.X, b.X, WorldWidth);
        float dy = a.Y - b.Y;
        float r = ra + rb;
        return dx * dx + dy * dy < r * r;
    }

    void HandleCollisions()
    {
        // Ship bullets vs enemies + the catch/redeposit-by-touch for humanoids.
        foreach (var bullet in Bullets)
        {
            if (!bullet.Alive || !bullet.FromShip) continue;

            foreach (var l in Landers)
            {
                if (!l.Alive) continue;
                if (Hit(bullet.Position, bullet.Radius, l.Position, l.Radius))
                {
                    bullet.Alive = false;
                    DropCaptive(l);     // shot mid-lift => humanoid Falls
                    KillLander(l, scored: true);
                    break;
                }
            }
            if (!bullet.Alive) continue;

            foreach (var m in Mutants)
                if (m.Alive && Hit(bullet.Position, bullet.Radius, m.Position, m.Radius))
                { bullet.Alive = false; m.Alive = false; Explode(m.Position, 12); AddScore(150); break; }
            if (!bullet.Alive) continue;

            foreach (var b in Baiters)
                if (b.Alive && Hit(bullet.Position, bullet.Radius, b.Position, b.Radius))
                { bullet.Alive = false; b.Alive = false; Explode(b.Position, 12); AddScore(200); break; }
            if (!bullet.Alive) continue;

            foreach (var b in Bombers)
                if (b.Alive && Hit(bullet.Position, bullet.Radius, b.Position, b.Radius))
                { bullet.Alive = false; b.Alive = false; Explode(b.Position, 14); AddScore(250); break; }
            if (!bullet.Alive) continue;

            foreach (var p in Pods)
                if (p.Alive && Hit(bullet.Position, bullet.Radius, p.Position, p.Radius))
                { bullet.Alive = false; SplitPod(p); AddScore(100); break; }
            if (!bullet.Alive) continue;

            foreach (var s in Swarmers)
                if (s.Alive && Hit(bullet.Position, bullet.Radius, s.Position, s.Radius))
                { bullet.Alive = false; s.Alive = false; Explode(s.Position, 6); AddScore(50); break; }
        }

        // The ship catches falling humanoids on contact.
        if (Ship.Alive && Ship.Carrying == null)
        {
            foreach (var h in Humanoids)
            {
                if (!h.Alive || h.State != HumanoidState.Falling) continue;
                if (Hit(Ship.Position, Ship.Radius + 6f, h.Position, h.Radius))
                {
                    h.State = HumanoidState.Caught;
                    h.Velocity = Vec2.Zero;
                    Ship.Carrying = h;
                    break;
                }
            }
        }

        // Threats + enemy bullets + mines vs the ship.
        if (Ship.Alive && Ship.InvincibleTime <= 0f && Mode == GameMode.Playing)
        {
            bool dead = false;
            foreach (var l in Landers) if (l.Alive && Hit(Ship.Position, Ship.Radius, l.Position, l.Radius)) { DropCaptive(l); KillLander(l, scored: false); dead = true; break; }
            if (!dead) foreach (var m in Mutants) if (m.Alive && Hit(Ship.Position, Ship.Radius, m.Position, m.Radius)) { m.Alive = false; Explode(m.Position, 10); dead = true; break; }
            if (!dead) foreach (var b in Baiters) if (b.Alive && Hit(Ship.Position, Ship.Radius, b.Position, b.Radius)) { b.Alive = false; Explode(b.Position, 10); dead = true; break; }
            if (!dead) foreach (var b in Bombers) if (b.Alive && Hit(Ship.Position, Ship.Radius, b.Position, b.Radius)) { b.Alive = false; Explode(b.Position, 12); dead = true; break; }
            if (!dead) foreach (var s in Swarmers) if (s.Alive && Hit(Ship.Position, Ship.Radius, s.Position, s.Radius)) { s.Alive = false; Explode(s.Position, 6); dead = true; break; }
            if (!dead) foreach (var p in Particles) if (p.IsMine && Hit(Ship.Position, Ship.Radius, p.Position, p.Radius)) { p.Alive = false; Explode(p.Position, 8); dead = true; break; }
            if (!dead) foreach (var bl in Bullets) if (bl.Alive && !bl.FromShip && Hit(Ship.Position, Ship.Radius, bl.Position, bl.Radius)) { bl.Alive = false; dead = true; break; }
            if (dead) KillShip();
        }
    }

    // If the lander was carrying a humanoid, release it into a fall.
    void DropCaptive(Lander l)
    {
        if (l.Target != null && l.Target.State == HumanoidState.Seized)
        {
            l.Target.State = HumanoidState.Falling;
            l.Target.Velocity = new Vec2(0f, 40f);
            l.Target.Captor = null;
        }
        l.Target = null;
    }

    void KillLander(Lander l, bool scored)
    {
        l.Alive = false;
        Explode(l.Position, 14);
        if (scored) AddScore(150);
    }

    // --- Scoring / death --------------------------------------------------------

    void AddScore(int s)
    {
        int prev = Score;
        Score += s;
        // Extra life every 10k (like Pohaku).
        if (Mode == GameMode.Playing && Score / 10000 > prev / 10000)
        {
            Ship.Lives++;
            Ship.SmartBombs++;
        }
    }

    void KillShip()
    {
        Explode(Ship.Position, 24);
        AudioEngine.PlayExplosion();
        // Drop any carried humanoid back into a fall.
        if (Ship.Carrying != null)
        {
            Ship.Carrying.State = HumanoidState.Falling;
            Ship.Carrying.Velocity = new Vec2(0f, 60f);
            Ship.Carrying = null;
        }
        if (Mode != GameMode.Playing) { Ship.InvincibleTime = 1.5f; return; }

        Ship.Lives--;
        if (Ship.Lives <= 0)
        {
            if (Score > HighScore) HighScore = Score;
            HighScoreStore.Save(HighScore);
            Mode = GameMode.GameOver;
            _gameOverTimer = 4.5f;
        }
        else
        {
            // Respawn at a safe altitude with brief invincibility.
            Ship.Position = new Vec2(Ship.Position.X, WorldHeight * 0.35f);
            Ship.Velocity = Vec2.Zero;
            Ship.InvincibleTime = 2.5f;
        }
    }

    void Explode(Vec2 pos, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float ang = (float)_rng.NextDouble() * MathF.Tau;
            float sp = 60f + (float)_rng.NextDouble() * 160f;
            Particles.Add(new Particle(pos, Vec2.FromAngle(ang, sp), 0.5f + (float)_rng.NextDouble() * 0.4f));
        }
        AudioEngine.PlayExplosion();
    }

    // --- Sweep + wave/lose ------------------------------------------------------

    void RemoveDead()
    {
        Bullets.RemoveAll(b => !b.Alive);
        Landers.RemoveAll(l => !l.Alive);
        Mutants.RemoveAll(m => !m.Alive);
        Baiters.RemoveAll(b => !b.Alive);
        Bombers.RemoveAll(b => !b.Alive);
        Pods.RemoveAll(p => !p.Alive);
        Swarmers.RemoveAll(s => !s.Alive);
        Particles.RemoveAll(p => !p.Alive);
        Humanoids.RemoveAll(h => !h.Alive);
    }

    void CheckWaveAndLose()
    {
        // Lose-condition: every humanoid abducted/dead -> planet "explosion": the
        // remaining landers all mutate, the screen flashes, and the next wave is
        // harder with fewer humanoids. (We don't end the game here — running out
        // of ships is the only game-over, matching Defender.)
        if (HumanoidsRemaining <= 0 && Humanoids.Count == 0 && !_planetLost)
        {
            _planetLost = true;
            ScreenFlash = 0.6f;
            var snapshot = Landers.ToArray();
            foreach (var l in snapshot) if (l.Alive) MutateLander(l);
        }

        // Wave clear: all landers gone (spawned and killed) and no pending spawns.
        bool noLandersLeft = Landers.Count == 0 && _landersToSpawn <= 0;
        if (Mode != GameMode.GameOver && noLandersLeft && Mutants.Count == 0)
        {
            // Next wave: harder, and (if the planet was lost) restock fewer colonists.
            int nextWave = Wave + 1;
            if (_planetLost)
            {
                _planetLost = false;
                int restock = Math.Max(4, StartHumanoids - nextWave);
                SpawnHumanoids(restock);
            }
            StartWave(nextWave);
        }
    }

    bool _planetLost;

    // --- Audio state ------------------------------------------------------------

    bool _prevThrustOn;
    void UpdateAudioState()
    {
        bool thrustOn = Ship.Alive && Ship.ThrustingAny && Mode != GameMode.GameOver;
        if (thrustOn != _prevThrustOn)
        {
            if (thrustOn) AudioEngine.StartThrust();
            else          AudioEngine.StopThrust();
            _prevThrustOn = thrustOn;
        }
    }

    // --- Small helpers ----------------------------------------------------------

    // Step a position toward a target X along the shortest torus path at `speed`.
    void MoveTowardX(ref Vec2 pos, float targetX, float speed, float dt)
    {
        float dx = Camera2D.WrapDelta(pos.X, targetX, WorldWidth);
        float step = MathF.Sign(dx) * MathF.Min(MathF.Abs(dx), speed * dt);
        pos.X = Camera2D.Wrap(pos.X + step, WorldWidth);
    }
}
