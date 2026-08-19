using System;
using System.Collections.Generic;

namespace Lua.Game;

/// <summary>
/// The per-frame brain for Lua, a Tempest-style well shooter: the mode state
/// machine, enemy spawning + AI (flippers/tankers/spikers/fuseballs), bullet and
/// spike handling, collisions/scoring, super-zapper, the between-levels warp, and
/// the attract-mode autopilot. World coords are 720×1280 portrait; the
/// <see cref="Well"/> sits in the upper half so the bottom is free for the HUD and
/// the player's claw is drawn largest on the near rim.
/// </summary>
public sealed class GameWorld
{
    // --- World dimensions ---
    // World space is a fixed 720×1280 coordinate system. The renderer letterboxes
    // onto whatever canvas size it gets — this stays constant regardless of the
    // actual window dimensions so gameplay coordinates are stable.
    public const float WorldW = 720f;
    public const float WorldH = 1280f;
    public float Width  => WorldW;
    public float Height => WorldH;

    public Well Well { get; private set; }

    // --- State ---
    public GameMode Mode = GameMode.Title;
    public int Level = 1;
    public int Score;
    public int HighScore;
    public Player Player = new();
    public List<Enemy>     Enemies     = new();
    public List<Bullet>    Bullets     = new();
    public List<Spike>     Spikes      = new();
    public List<Particle>  Particles   = new();
    public List<ScorePopup> Popups     = new();

    // --- Level pacing ---
    public int   EnemiesRemainingToSpawn;
    public int   EnemiesAliveCap = 6;
    public float NextSpawnTimer;
    public float SpawnInterval = 1.6f;
    public float WarpProgress; // 0..1 during warp transition
    public float WarpDuration;

    // --- Title / attract ---
    public float TitleIdleTimer;
    public float AttractInputCooldown;
    public float PlacardTimer;
    public string PlacardText = "";

    // --- HUD helpers ---
    public bool BulletCapEnabled = true; // Tempest didn't have a cap, but keep the cheat hook
    public int  MaxPlayerBullets = 8;

    static readonly HighScoreStore HighScoreStore = new("Lua");

    public GameWorld()
    {
        HighScore = HighScoreStore.Load();
        Well = Wells.Build(WellShape.Circle, WorldW * 0.5f, WorldH * 0.5f, 290f);
        Player.Segment = Player.TargetSegment = Well.SegmentCount / 2;
    }

    /// <summary>No-op: world coords are fixed at 720×1280 and the renderer letterboxes.</summary>
    public void Resize(float w, float h)
    {
        // World coordinates are fixed at 720×1280; the renderer letterboxes onto
        // whatever canvas size it receives. Nothing to record here.
    }

    // --- Input flags driven by MainPage ---
    public bool MovingLeft;
    public bool MovingRight;

    // --- Game lifecycle ---

    /// <summary>Starts a fresh player-controlled game at level 1.</summary>
    public void StartGame()
    {
        Mode = GameMode.Playing;
        Level = 1;
        Score = 0;
        Player = new Player
        {
            SuperZapperUsesLeft = 2,
            Invuln = 1.5f,
        };
        Enemies.Clear();
        Bullets.Clear();
        Spikes.Clear();
        Particles.Clear();
        Popups.Clear();
        BuildLevel(Level);
        ShowPlacard($"LEVEL {Level}", 1.6f);
    }

    /// <summary>Starts the self-playing attract demo (autopilot, generous zapper).</summary>
    public void StartAttract()
    {
        StartGame();
        Mode = GameMode.Attract;
        Player.SuperZapperUsesLeft = 9; // attract AI uses zapper liberally
    }

    /// <summary>Returns to the title screen and clears the playfield.</summary>
    public void ReturnToTitle()
    {
        Mode = GameMode.Title;
        TitleIdleTimer = 0f;
        AttractInputCooldown = 0.5f;
        Enemies.Clear();
        Bullets.Clear();
        Spikes.Clear();
        Particles.Clear();
        Popups.Clear();
    }

    void BuildLevel(int level)
    {
        var shape = Wells.ForLevel(level);
        Well = Wells.Build(shape, WorldW * 0.5f, WorldH * 0.5f, 290f);

        int n = Well.SegmentCount;
        if (Player.Segment >= n) Player.Segment = n / 2;
        Player.TargetSegment = Player.Segment;
        Player.SegmentT = 0f;
        Player.SuperZapperUsesLeft = 2;

        // Scale difficulty with level.
        EnemiesRemainingToSpawn = 12 + level * 4;
        EnemiesAliveCap         = Math.Min(10, 4 + level / 2);
        SpawnInterval           = MathF.Max(0.45f, 1.4f - level * 0.08f);
        NextSpawnTimer          = 0.9f;
    }

    /// <summary>Shows a centered placard (e.g. "LEVEL 3") for <paramref name="seconds"/>.</summary>
    public void ShowPlacard(string text, float seconds)
    {
        PlacardText = text;
        PlacardTimer = seconds;
    }

    // --- Per-frame update ---

    // --- Attract cycle ------------------------------------------------------
    // An unattended cabinet has to come back round. Without this the game parks on
    // the GAME OVER panel forever, because the Title -> Attract idle timer only ever
    // advances on the Title screen, so attract mode was unreachable after a death
    // until somebody pressed a key. Paku was the only demo in the family that
    // already cycled.
    public const float GameOverIdleSeconds = 8f;   // long enough to read your score
    float _gameOverIdle;

    /// <summary>Advances the game one frame; dispatches on <see cref="Mode"/>.</summary>
    public void Update(float dt)
    {
        // Drop back to the idle screen after a game over so the attract loop
        // comes round again. Self-resetting: the timer is held at zero in every
        // other mode, so re-entering GameOver always starts from a clean count.
        if (Mode == GameMode.GameOver)
        {
            _gameOverIdle += dt;
            if (_gameOverIdle > GameOverIdleSeconds) ReturnToTitle();
        }
        else _gameOverIdle = 0f;

        if (PlacardTimer > 0) PlacardTimer -= dt;
        if (AttractInputCooldown > 0) AttractInputCooldown -= dt;

        switch (Mode)
        {
            case GameMode.Title:
                TitleIdleTimer += dt;
                if (TitleIdleTimer > 12f)
                {
                    StartAttract();
                    TitleIdleTimer = 0f;
                }
                break;

            case GameMode.Playing:
            case GameMode.Attract:
                UpdatePlay(dt);
                break;

            case GameMode.Warp:
                UpdateWarp(dt);
                break;

            case GameMode.GameOver:
                // Just animate residual particles.
                UpdateParticles(dt);
                UpdatePopups(dt);
                break;
        }
    }

    void UpdatePlay(float dt)
    {
        if (Mode == GameMode.Attract)
        {
            UpdateAttractAI(dt);
        }

        UpdatePlayer(dt);
        UpdateBullets(dt);
        UpdateEnemies(dt);
        UpdateSpawn(dt);
        UpdateParticles(dt);
        UpdatePopups(dt);
        CheckLevelCleared(dt);
    }

    void UpdatePlayer(float dt)
    {
        if (Player.Invuln > 0)    Player.Invuln    -= dt;
        if (Player.SpawnAnim < 1) Player.SpawnAnim  = MathF.Min(1f, Player.SpawnAnim + dt * 3f);
        if (Player.ShootCooldown > 0) Player.ShootCooldown -= dt;

        // Rim movement: pressing left/right advances TargetSegment, SegmentT slides.
        if (Player.Segment == Player.TargetSegment)
        {
            if (MovingLeft)
            {
                int next = Well.Step(Player.Segment, -1);
                if (next >= 0)
                {
                    Player.TargetSegment = next;
                    Player.SegmentT = 0f;
                }
            }
            else if (MovingRight)
            {
                int next = Well.Step(Player.Segment, +1);
                if (next >= 0)
                {
                    Player.TargetSegment = next;
                    Player.SegmentT = 0f;
                }
            }
        }
        else
        {
            // Slide ~8 segments/sec (Tempest's rotary spinner is faster; this feels
            // right on keyboard without making it feel laggy).
            Player.SegmentT += dt * 8f;
            if (Player.SegmentT >= 1f)
            {
                Player.Segment = Player.TargetSegment;
                Player.SegmentT = 0f;
            }
        }
    }

    /// <summary>Fires a player shot from the current segment, subject to cooldown + bullet cap.</summary>
    public void FireBullet()
    {
        if (Mode != GameMode.Playing && Mode != GameMode.Attract) return;
        if (Player.ShootCooldown > 0) return;
        if (BulletCapEnabled && CountPlayerBullets() >= MaxPlayerBullets) return;
        var b = new Bullet
        {
            Segment    = Player.Segment,
            Depth      = 0f,
            Speed      = 2.2f,  // depth/sec (covers well in ~0.5s)
            FromPlayer = true,
            Life       = 1.5f,
        };
        Bullets.Add(b);
        Player.ShootCooldown = 0.07f; // fast finger firing
        AudioEngine.PlayShoot();
    }

    int CountPlayerBullets()
    {
        int n = 0;
        foreach (var b in Bullets) if (b.FromPlayer) n++;
        return n;
    }

    /// <summary>
    /// Uses one super-zapper charge (2 per level): the first use clears all
    /// on-screen enemies, the second kills one at random.
    /// </summary>
    public void TriggerSuperZapper()
    {
        if (Mode != GameMode.Playing && Mode != GameMode.Attract) return;
        if (Player.SuperZapperUsesLeft <= 0) return;
        Player.SuperZapperUsesLeft--;
        AudioEngine.PlayZapper();
        if (Player.SuperZapperUsesLeft == 1)
        {
            // First press clears everything on screen.
            foreach (var e in Enemies)
            {
                if (e.State == EnemyState.Dead) continue;
                AddScore(ScoreFor(e), e);
                EmitExplosion(EnemyPos(e), 24, 0xFF_FFFFFF);
                e.State = EnemyState.Dead;
            }
        }
        else
        {
            // Second press kills one random enemy.
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.State == EnemyState.Dead) continue;
                AddScore(ScoreFor(e), e);
                EmitExplosion(EnemyPos(e), 24, 0xFF_FFFFFF);
                e.State = EnemyState.Dead;
                break;
            }
        }
    }

    void UpdateBullets(float dt)
    {
        for (int i = Bullets.Count - 1; i >= 0; i--)
        {
            var b = Bullets[i];
            b.Depth += b.Speed * dt;
            b.Life -= dt;
            if (b.Depth >= 1f || b.Depth < 0f || b.Life <= 0f)
            {
                Bullets.RemoveAt(i);
                continue;
            }

            if (b.FromPlayer)
            {
                // Check collision against enemies in same segment OR adjacent
                // segments at small SegmentT offset (flipping enemies).
                for (int e = 0; e < Enemies.Count; e++)
                {
                    var en = Enemies[e];
                    if (en.State == EnemyState.Dead) continue;
                    if (!EnemyInSegmentForHit(en, b.Segment)) continue;
                    if (Math.Abs(en.Depth - b.Depth) > 0.04f) continue;
                    OnEnemyHit(en);
                    Bullets.RemoveAt(i);
                    break;
                }
            }
            else
            {
                // Enemy bullets travel from far end UP to rim (depth shrinks toward 0).
                // Hit the player if same segment and near rim.
                if (b.Depth <= 0.03f && b.Segment == Player.Segment && Player.Invuln <= 0)
                {
                    OnPlayerHit();
                    Bullets.RemoveAt(i);
                }
                // Player bullets travelling in same column can intercept enemy bullets
                // (rare in Tempest; we skip it to keep it readable).
            }
        }
    }

    bool EnemyInSegmentForHit(Enemy e, int seg)
    {
        if (e.Segment == seg) return true;
        if (e.State == EnemyState.Flipping)
        {
            // Mid-flip enemies sit between two segments — be generous on the hit.
            if (e.TargetSegment == seg) return true;
        }
        return false;
    }

    void UpdateEnemies(float dt)
    {
        int aliveOnRim = 0;
        for (int i = 0; i < Enemies.Count; i++)
        {
            var e = Enemies[i];
            if (e.State == EnemyState.Dead) continue;
            UpdateEnemy(e, dt);
            if (e.State == EnemyState.OnRim) aliveOnRim++;
        }
        // Reap dead enemies. (Iterate backward to allow in-place removal.)
        for (int i = Enemies.Count - 1; i >= 0; i--)
            if (Enemies[i].State == EnemyState.Dead) Enemies.RemoveAt(i);
    }

    void UpdateEnemy(Enemy e, float dt)
    {
        if (e.SpawnDelay > 0)
        {
            e.SpawnDelay -= dt;
            return;
        }

        switch (e.Kind)
        {
            case EnemyKind.Flipper:  UpdateFlipper(e, dt);  break;
            case EnemyKind.Tanker:   UpdateTanker(e, dt);   break;
            case EnemyKind.Spiker:   UpdateSpiker(e, dt);   break;
            case EnemyKind.Fuseball: UpdateFuseball(e, dt); break;
        }

        // Universal: enemy contact with player when on the rim in same segment.
        if (e.State == EnemyState.OnRim && e.Segment == Player.Segment && Player.Invuln <= 0)
        {
            OnPlayerHit();
            // Don't kill the enemy — Tempest's flippers grab and pull the player.
        }
    }

    void UpdateFlipper(Enemy e, float dt)
    {
        if (e.State == EnemyState.Climbing)
        {
            e.Depth -= e.ClimbSpeed * dt;
            // Random chance to flip between adjacent segments mid-climb.
            e.StateTimer -= dt;
            if (e.StateTimer <= 0 && e.Depth > 0.05f)
            {
                int dir = (_rng.Next(2) == 0) ? -1 : 1;
                int next = Well.Step(e.Segment, dir);
                if (next >= 0)
                {
                    e.TargetSegment = next;
                    e.State = EnemyState.Flipping;
                    e.SegmentT = 0f;
                    AudioEngine.PlayFlip();
                }
                e.StateTimer = 0.6f + (float)_rng.NextDouble() * 0.8f;
            }
            if (e.Depth <= 0f)
            {
                e.Depth = 0f;
                e.State = EnemyState.OnRim;
                e.StateTimer = 0.4f;
            }
        }
        else if (e.State == EnemyState.Flipping)
        {
            e.SegmentT += dt * 2.5f; // flip takes ~0.4s
            if (e.SegmentT >= 1f)
            {
                e.Segment = e.TargetSegment;
                e.SegmentT = 0f;
                e.State = EnemyState.Climbing;
            }
        }
        else if (e.State == EnemyState.OnRim)
        {
            // On rim: walk toward player. Use Tempest's "flip to next segment" feel.
            e.StateTimer -= dt;
            if (e.StateTimer <= 0)
            {
                int dir = Math.Sign(SignedSegmentDelta(Player.Segment, e.Segment));
                if (dir == 0) dir = 1;
                int next = Well.Step(e.Segment, dir);
                if (next >= 0)
                {
                    e.TargetSegment = next;
                    e.State = EnemyState.Flipping;
                    e.SegmentT = 0f;
                    AudioEngine.PlayFlip();
                }
                e.StateTimer = 0.5f;
            }

            // On-rim flippers occasionally shoot the player.
            if (_rng.NextDouble() < 0.005)
            {
                FireEnemyBullet(e);
            }
        }
    }

    void UpdateTanker(Enemy e, float dt)
    {
        if (e.State == EnemyState.Climbing)
        {
            e.Depth -= e.ClimbSpeed * dt * 0.65f; // tankers climb slower
            if (e.Depth <= 0f)
            {
                e.Depth = 0f;
                // Split into 2 flippers when reaching rim.
                SplitTanker(e);
                e.State = EnemyState.Dead;
            }
        }
    }

    void UpdateSpiker(Enemy e, float dt)
    {
        if (e.State == EnemyState.Climbing)
        {
            e.Depth -= e.ClimbSpeed * dt * 0.5f;
            // Grow / extend the spike in this segment up to the spiker's current depth.
            var spike = FindOrCreateSpike(e.Segment);
            spike.MinDepth = MathF.Min(spike.MinDepth, e.Depth);

            // Occasional zig-zag fire upward toward the player.
            if (_rng.NextDouble() < 0.004)
            {
                FireEnemyBullet(e);
            }
            if (e.Depth <= 0.05f)
            {
                // Spikers don't reach the rim — they retreat back down once near the top.
                e.Depth = 0.05f;
                e.ClimbSpeed = -MathF.Abs(e.ClimbSpeed) * 0.6f; // retreat
            }
            if (e.Depth >= 1f)
            {
                e.Depth = 1f;
                e.ClimbSpeed = MathF.Abs(e.ClimbSpeed);
            }
        }
    }

    Spike FindOrCreateSpike(int segment)
    {
        for (int i = 0; i < Spikes.Count; i++)
            if (Spikes[i].Segment == segment) return Spikes[i];
        var s = new Spike { Segment = segment, MinDepth = 1f };
        Spikes.Add(s);
        return s;
    }

    void UpdateFuseball(Enemy e, float dt)
    {
        // Travel up/down a single segment edge, occasionally jumping to adjacent.
        if (e.State == EnemyState.Climbing)
        {
            // ClimbSpeed sign already encodes direction.
            e.Depth -= e.ClimbSpeed * dt;
            if (e.Depth <= 0.05f)
            {
                // Jump to an adjacent segment (vertex hop) instead of going onto rim.
                int dir = (_rng.Next(2) == 0) ? -1 : 1;
                int next = Well.Step(e.Segment, dir);
                if (next >= 0) e.Segment = next;
                e.ClimbSpeed = -MathF.Abs(e.ClimbSpeed);
            }
            else if (e.Depth >= 1f)
            {
                e.ClimbSpeed = MathF.Abs(e.ClimbSpeed);
            }
            // Animate hue for the green/yellow energy ball look.
            e.Hue = (e.Hue + dt * 240f) % 360f;
        }
    }

    void FireEnemyBullet(Enemy e)
    {
        Bullets.Add(new Bullet
        {
            Segment    = e.Segment,
            Depth      = MathF.Max(0.05f, e.Depth),
            Speed      = -1.2f, // up toward rim
            FromPlayer = false,
            Life       = 2.5f,
        });
    }

    void SplitTanker(Enemy tanker)
    {
        for (int k = -1; k <= 1; k += 2)
        {
            int seg = Well.Step(tanker.Segment, k);
            if (seg < 0) seg = tanker.Segment;
            Enemies.Add(new Enemy
            {
                Kind        = EnemyKind.Flipper,
                Segment     = seg,
                Depth       = 0.10f,
                State       = EnemyState.Climbing,
                ClimbSpeed  = 0.14f,
                StateTimer  = 0.4f,
            });
        }
        EmitExplosion(EnemyPos(tanker), 14, 0xFF_AA66FF);
        AudioEngine.PlayExplosion();
    }

    void OnEnemyHit(Enemy e)
    {
        if (e.Kind == EnemyKind.Tanker)
        {
            // Tankers split on hit too (classic Tempest behavior).
            SplitTanker(e);
            AddScore(ScoreFor(e), e);
            e.State = EnemyState.Dead;
            return;
        }
        AddScore(ScoreFor(e), e);
        EmitExplosion(EnemyPos(e), 18, EnemyExplosionColor(e.Kind));
        AudioEngine.PlayExplosion();
        e.State = EnemyState.Dead;
    }

    int ScoreFor(Enemy e) => e.Kind switch
    {
        EnemyKind.Flipper  => 150,
        EnemyKind.Tanker   => 100,
        EnemyKind.Spiker   => 50,
        EnemyKind.Fuseball => 250,
        _ => 0,
    };

    uint EnemyExplosionColor(EnemyKind k) => k switch
    {
        EnemyKind.Flipper  => 0xFF_FF4466,
        EnemyKind.Tanker   => 0xFF_AA66FF,
        EnemyKind.Spiker   => 0xFF_FFEE33,
        EnemyKind.Fuseball => 0xFF_55FF77,
        _ => 0xFF_FFFFFF,
    };

    void AddScore(int v, Enemy e)
    {
        Score += v;
        Popups.Add(new ScorePopup
        {
            Pos      = EnemyPos(e),
            Value    = v,
            Life     = 0.9f,
            MaxLife  = 0.9f,
            Color    = EnemyExplosionColor(e.Kind),
        });
        if (Score > HighScore) HighScore = Score;
    }

    void OnPlayerHit()
    {
        if (Mode == GameMode.Attract)
        {
            // Attract mode never dies; reset invuln so it keeps showcasing.
            Player.Invuln = 1.0f;
            return;
        }
        if (Player.Invuln > 0) return;
        EmitExplosion(PlayerPos(), 50, 0xFF_33F8FF);
        AudioEngine.PlayExplosion();
        // Lives are tracked via a separate field; Tempest reserves 3 at game start.
        _livesLeft--;
        if (_livesLeft <= 0)
        {
            Mode = GameMode.GameOver;
            HighScoreStore.Save(HighScore);
            ShowPlacard("GAME OVER", 3.0f);
            return;
        }
        Player.Invuln = 2.0f;
        Player.SpawnAnim = 0f;
    }

    int _livesLeft = 3;
    public int LivesLeft => _livesLeft;

    void UpdateSpawn(float dt)
    {
        if (EnemiesRemainingToSpawn <= 0) return;
        if (Enemies.Count >= EnemiesAliveCap) return;
        NextSpawnTimer -= dt;
        if (NextSpawnTimer > 0) return;
        SpawnOneEnemy();
        NextSpawnTimer = SpawnInterval * (0.7f + (float)_rng.NextDouble() * 0.6f);
    }

    void SpawnOneEnemy()
    {
        int n = Well.SegmentCount;
        int seg = _rng.Next(n);

        // Enemy mix scales with level. Below level 3: only flippers. 3..5: + tankers.
        // 6+: + spikers. 9+: + fuseballs.
        int mix = _rng.Next(100);
        EnemyKind kind;
        if (Level <= 2)
            kind = EnemyKind.Flipper;
        else if (Level <= 5)
            kind = (mix < 70) ? EnemyKind.Flipper : EnemyKind.Tanker;
        else if (Level <= 8)
            kind = (mix < 55) ? EnemyKind.Flipper
                 : (mix < 80) ? EnemyKind.Tanker
                              : EnemyKind.Spiker;
        else
            kind = (mix < 45) ? EnemyKind.Flipper
                 : (mix < 70) ? EnemyKind.Tanker
                 : (mix < 85) ? EnemyKind.Spiker
                              : EnemyKind.Fuseball;

        var e = new Enemy
        {
            Kind       = kind,
            Segment    = seg,
            Depth      = 1f,
            State      = EnemyState.Climbing,
            ClimbSpeed = 0.085f + Level * 0.012f,
            StateTimer = 0.4f + (float)_rng.NextDouble() * 0.8f,
            SpawnDelay = 0.0f,
        };
        if (kind == EnemyKind.Spiker)
        {
            e.ClimbSpeed = 0.06f + Level * 0.008f;
        }
        if (kind == EnemyKind.Fuseball)
        {
            e.ClimbSpeed = 0.18f + Level * 0.015f;
            e.Hue = (float)_rng.NextDouble() * 360f;
        }
        Enemies.Add(e);
        EnemiesRemainingToSpawn--;
    }

    void CheckLevelCleared(float dt)
    {
        if (EnemiesRemainingToSpawn > 0) return;
        if (Enemies.Count > 0) return;
        // Level cleared; start warp.
        BeginWarp();
    }

    public float WarpSpikeHit; // 0..1 fraction along warp where a spike struck

    GameMode _preWarpMode;

    void BeginWarp()
    {
        _preWarpMode = Mode;
        Mode = GameMode.Warp;
        WarpProgress = 0f;
        WarpDuration = 2.5f;
        AudioEngine.PlayWarp();
    }

    void UpdateWarp(float dt)
    {
        WarpProgress += dt / WarpDuration;
        UpdateParticles(dt);
        UpdatePopups(dt);

        // While warping the camera zooms down the well. Spikes in the player's
        // current segment will hit when WarpProgress reaches them.
        if (Player.Alive && Mode == GameMode.Warp)
        {
            float depthFromRim = WarpProgress; // 0..1
            for (int i = 0; i < Spikes.Count; i++)
            {
                var s = Spikes[i];
                if (s.Segment != Player.Segment) continue;
                if (depthFromRim >= s.MinDepth && depthFromRim <= 1f)
                {
                    // Hit a spike! In real Tempest this kills you.
                    if (Mode == GameMode.Attract)
                    {
                        // Attract skips it.
                        continue;
                    }
                    EmitExplosion(PlayerPos(), 40, 0xFF_33F8FF);
                    AudioEngine.PlayExplosion();
                    _livesLeft--;
                    if (_livesLeft <= 0)
                    {
                        Mode = GameMode.GameOver;
                        HighScoreStore.Save(HighScore);
                        ShowPlacard("GAME OVER", 3.0f);
                        return;
                    }
                    Player.Invuln = 2.0f;
                    Spikes.RemoveAt(i); // clear the spike that hit you, ouch
                    break;
                }
            }
        }

        if (WarpProgress >= 1f)
        {
            Level++;
            Spikes.Clear();
            Bullets.Clear();
            BuildLevel(Level);
            ShowPlacard($"LEVEL {Level}", 1.6f);
            // Restore whichever mode we entered warp from so attract loops
            // through levels instead of dropping into real Playing.
            Mode = _preWarpMode == GameMode.Attract ? GameMode.Attract : GameMode.Playing;
        }
    }

    // --- Attract mode AI: simple "shoot anything climbing in my column or adjacent" ---
    void UpdateAttractAI(float dt)
    {
        if (Enemies.Count == 0) { MovingLeft = false; MovingRight = false; return; }

        // Find lowest-Depth enemy and steer toward it.
        Enemy? target = null;
        float best = float.PositiveInfinity;
        foreach (var e in Enemies)
        {
            if (e.State == EnemyState.Dead) continue;
            if (e.Depth < best) { best = e.Depth; target = e; }
        }
        if (target == null) return;
        int delta = SignedSegmentDelta(target.Segment, Player.Segment);
        MovingLeft  = delta < 0;
        MovingRight = delta > 0;

        // Fire approximately every 0.12s.
        _attractFireTimer -= dt;
        if (_attractFireTimer <= 0)
        {
            FireBullet();
            _attractFireTimer = 0.10f + (float)_rng.NextDouble() * 0.08f;
        }
        // Use super zapper if 3+ enemies are near the rim.
        int nearRim = 0;
        foreach (var e in Enemies) if (e.Depth < 0.20f && e.State != EnemyState.Dead) nearRim++;
        if (nearRim >= 3 && Player.SuperZapperUsesLeft > 0)
            TriggerSuperZapper();
    }
    float _attractFireTimer;

    // Returns the signed segment delta to go from `from` to `to` along the shorter
    // direction. Positive = move via +1, negative = move via -1.
    int SignedSegmentDelta(int to, int from)
    {
        int n = Well.SegmentCount;
        if (!Well.Closed)
        {
            return to - from;
        }
        int d = to - from;
        if (d >  n / 2) d -= n;
        if (d < -n / 2) d += n;
        return d;
    }

    // --- Particles + popups ---

    void UpdateParticles(float dt)
    {
        for (int i = Particles.Count - 1; i >= 0; i--)
        {
            var p = Particles[i];
            p.Pos += p.Vel * dt;
            p.Vel *= MathF.Pow(0.92f, dt * 60f);
            p.Life -= dt;
            if (p.Life <= 0) Particles.RemoveAt(i);
        }
    }

    void UpdatePopups(float dt)
    {
        for (int i = Popups.Count - 1; i >= 0; i--)
        {
            var p = Popups[i];
            p.Pos.Y -= dt * 30f;
            p.Life  -= dt;
            if (p.Life <= 0) Popups.RemoveAt(i);
        }
    }

    void EmitExplosion(Vec2 origin, int count, uint color)
    {
        for (int i = 0; i < count; i++)
        {
            double ang = _rng.NextDouble() * Math.PI * 2.0;
            float spd = 50f + (float)_rng.NextDouble() * 250f;
            Particles.Add(new Particle
            {
                Pos = origin,
                Vel = new Vec2((float)Math.Cos(ang) * spd, (float)Math.Sin(ang) * spd),
                Life    = 0.8f,
                MaxLife = 0.8f,
                Color   = color,
                Size    = 2f + (float)_rng.NextDouble() * 2f,
            });
        }
    }

    // --- Position helpers in world space ---
    public Vec2 PlayerPos()
    {
        if (Player.Segment == Player.TargetSegment)
        {
            // At rest: position at mid of current segment plus outward normal nudge.
            var mid = Well.SegmentMid(Player.Segment, 0f);
            var n = Well.SegmentNormal(Player.Segment);
            return mid + n * 14f;
        }
        var midA = Well.SegmentMid(Player.Segment,       0f);
        var midB = Well.SegmentMid(Player.TargetSegment, 0f);
        var nA = Well.SegmentNormal(Player.Segment);
        var nB = Well.SegmentNormal(Player.TargetSegment);
        float t = Player.SegmentT;
        var p = new Vec2(midA.X + (midB.X - midA.X) * t,
                         midA.Y + (midB.Y - midA.Y) * t);
        var nrm = new Vec2(nA.X + (nB.X - nA.X) * t,
                           nA.Y + (nB.Y - nA.Y) * t).Normalized();
        return p + nrm * 14f;
    }

    public Vec2 EnemyPos(Enemy e)
    {
        if (e.State == EnemyState.Flipping)
        {
            // Mid-flip: lerp segment-midpoints at current depth.
            var mA = Well.SegmentMid(e.Segment,       e.Depth);
            var mB = Well.SegmentMid(e.TargetSegment, e.Depth);
            float t = e.SegmentT;
            return new Vec2(mA.X + (mB.X - mA.X) * t,
                            mA.Y + (mB.Y - mA.Y) * t);
        }
        return Well.SegmentMid(e.Segment, e.Depth);
    }

    public Vec2 BulletPos(Bullet b) => Well.SegmentMid(b.Segment, b.Depth);

    // Compute the screen-space (after warp zoom) projection of the player.
    // During warp, the player visually accelerates down the well.
    public Vec2 WarpPlayerPos()
    {
        var rim = PlayerPos();
        return Well.Project(rim, WarpProgress);
    }

    static readonly Random _rng = new();
}
