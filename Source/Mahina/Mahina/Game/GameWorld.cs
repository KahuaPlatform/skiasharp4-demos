using System;
using System.Collections.Generic;

namespace Mahina.Game;

/// <summary>
/// The per-frame brain for Mahina, a Lunar-Lander physics game (landscape
/// 1280×720). Integrates gravity + thrust + rotation, burns fuel, runs terrain
/// collision and the landing/crash judgement, scoring, per-level terrain
/// regeneration, the mode state machine, and the attract autopilot. Touch down
/// gently on a flat pad to score; crash into anything else and lose a life.
/// </summary>
public sealed class GameWorld
{
    // --- World dimensions ---
    public const float WorldW = 1280f;
    public const float WorldH = 720f;
    public float Width  => WorldW;
    public float Height => WorldH;

    // --- Physics constants ---
    public const float Gravity            = 35f;     // px/s² downward
    public const float ThrustAccel        = 110f;    // px/s² along nose direction
    public const float FuelBurnRate       = 14f;     // kg/s while thrusting
    public const float MaxLandVerticalSpd = 32f;     // |vy| must be below this to land safely
    public const float MaxLandHorizontalSpd = 22f;   // |vx| must be below this
    public const float MaxLandAngle       = 0.18f;   // ~10 degrees off vertical
    public const float RotateSpeed        = 2.1f;    // rad/sec while keys held
    public const float StartingFuel       = 800f;    // kg
    public const float LanderSize         = 18f;     // half-extent for collision

    // --- State ---
    public GameMode Mode = GameMode.Title;
    public int Level = 1;
    public int Score;
    public int HighScore;
    public int LivesLeft = 3;
    public string PlacardText = "";
    public float PlacardTimer;

    public Lander Lander = new();
    public Terrain Terrain = new();
    public List<Particle>  Particles = new();
    public List<ScorePopup> Popups   = new();

    // Cached landing analysis (set on touch-down so the placard can show details).
    public int    LastLandingMultiplier;
    public float  LastLandingFuelBonus;
    public int    LastLandingScore;

    // Input flags driven by MainPage.
    public bool RotateLeft, RotateRight, Thrust;

    // Attract / title pacing.
    public float TitleIdleTimer;

    // Was the last attract action useful (for attract AI scheduling).
    GameMode _preWarpMode;

    static readonly Random _rng = new();
    static readonly HighScoreStore HighScoreStore = new("Mahina");

    public GameWorld()
    {
        HighScore = HighScoreStore.Load();
        Terrain = TerrainBuilder.Build(1, WorldW, WorldH, _rng);
        ResetLanderToSpawn();
    }

    /// <summary>No-op: world coords are fixed and the renderer letterboxes.</summary>
    public void Resize(float w, float h)
    {
        // Fixed world coords; nothing to do.
    }

    // --- Lifecycle ---

    /// <summary>Starts a fresh player-controlled game at level 1.</summary>
    public void StartGame()
    {
        Mode = GameMode.Playing;
        Level = 1;
        Score = 0;
        LivesLeft = 3;
        Terrain = TerrainBuilder.Build(Level, WorldW, WorldH, _rng);
        ResetLanderToSpawn();
        Particles.Clear();
        Popups.Clear();
        ShowPlacard($"LEVEL {Level}", 1.6f);
    }

    /// <summary>Starts the self-playing attract demo (homing autopilot, near-infinite lives).</summary>
    public void StartAttract()
    {
        StartGame();
        Mode = GameMode.Attract;
        LivesLeft = 9999;
    }

    /// <summary>Returns to the title screen and clears transient effects.</summary>
    public void ReturnToTitle()
    {
        Mode = GameMode.Title;
        TitleIdleTimer = 0f;
        Particles.Clear();
        Popups.Clear();
    }

    void ResetLanderToSpawn()
    {
        Lander = new Lander
        {
            // Drop in from upper-left with a gentle rightward drift, like the arcade.
            Position = new Vec2(140f, 80f),
            Velocity = new Vec2(28f, 0f),
            AngleRadians = 0f,
            FuelKg = StartingFuel * MathF.Max(0.6f, 1f - (Level - 1) * 0.08f),
            Alive = true,
        };
    }

    void NextLevel()
    {
        Level++;
        Terrain = TerrainBuilder.Build(Level, WorldW, WorldH, _rng);
        ResetLanderToSpawn();
        Mode = (_preWarpMode == GameMode.Attract) ? GameMode.Attract : GameMode.Playing;
        ShowPlacard($"LEVEL {Level}", 1.6f);
    }

    void RetryLevel()
    {
        // Same terrain — player keeps trying.
        ResetLanderToSpawn();
        Mode = (_preWarpMode == GameMode.Attract) ? GameMode.Attract : GameMode.Playing;
        ShowPlacard("TRY AGAIN", 1.2f);
    }

    /// <summary>Shows a centered placard (e.g. "LEVEL 2") for <paramref name="seconds"/>.</summary>
    public void ShowPlacard(string text, float seconds)
    {
        PlacardText = text;
        PlacardTimer = seconds;
    }

    // --- Per-frame tick ---

    /// <summary>Advances the game one frame; dispatches on <see cref="Mode"/>.</summary>
    public void Update(float dt)
    {
        if (PlacardTimer > 0) PlacardTimer -= dt;

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
            case GameMode.Landed:
            case GameMode.Crashed:
                UpdateParticles(dt);
                UpdatePopups(dt);
                _postOutcomeTimer -= dt;
                if (_postOutcomeTimer <= 0)
                {
                    if (Mode == GameMode.Landed) NextLevel();
                    else
                    {
                        LivesLeft--;
                        if (LivesLeft <= 0)
                        {
                            Mode = GameMode.GameOver;
                            HighScoreStore.Save(HighScore);
                            // No placard — the GameOver branch of DrawHud paints
                            // a dedicated full-size "GAME OVER" header. Showing the
                            // placard here would double up.
                        }
                        else RetryLevel();
                    }
                }
                break;
            case GameMode.GameOver:
                UpdateParticles(dt);
                UpdatePopups(dt);
                break;
        }
    }
    float _postOutcomeTimer;

    void UpdatePlay(float dt)
    {
        if (Mode == GameMode.Attract) UpdateAttractAI(dt);

        UpdateLander(dt);
        UpdateParticles(dt);
        UpdatePopups(dt);
        CheckCollision();
    }

    void UpdateLander(float dt)
    {
        var l = Lander;

        // Rotation
        if (RotateLeft)  l.AngularVelocity = -RotateSpeed;
        else if (RotateRight) l.AngularVelocity = RotateSpeed;
        else l.AngularVelocity = 0f;
        l.AngleRadians += l.AngularVelocity * dt;

        // Gravity
        l.Velocity.Y += Gravity * dt;

        // Thrust
        bool wantThrust = Thrust && l.FuelKg > 0f;
        if (wantThrust)
        {
            float ax = MathF.Sin(l.AngleRadians) * ThrustAccel;
            float ay = -MathF.Cos(l.AngleRadians) * ThrustAccel;
            l.Velocity.X += ax * dt;
            l.Velocity.Y += ay * dt;
            l.FuelKg = MathF.Max(0f, l.FuelKg - FuelBurnRate * dt);
            EmitThrustFlame(l, dt);
        }
        l.Thrusting = wantThrust;
        AudioState(wantThrust);

        // Integrate position
        l.Position += l.Velocity * dt;

        // Wrap horizontally (the original arcade wrapped left/right edges).
        if (l.Position.X < 0)       l.Position.X += WorldW;
        if (l.Position.X >= WorldW) l.Position.X -= WorldW;

        // Top clamp — don't let the player escape upward.
        if (l.Position.Y < 0)
        {
            l.Position.Y = 0;
            if (l.Velocity.Y < 0) l.Velocity.Y = 0;
        }
    }

    bool _prevThrustOn;
    void AudioState(bool thrusting)
    {
        if (thrusting && !_prevThrustOn) AudioEngine.StartThrust();
        if (!thrusting && _prevThrustOn) AudioEngine.StopThrust();
        _prevThrustOn = thrusting;
    }

    void EmitThrustFlame(Lander l, float dt)
    {
        // Spawn a few particles per second below the ship along the -nose vector.
        _flameTimer -= dt;
        if (_flameTimer > 0) return;
        _flameTimer = 0.012f;
        float nx =  MathF.Sin(l.AngleRadians);
        float ny = -MathF.Cos(l.AngleRadians);
        var emitPos = new Vec2(l.Position.X - nx * LanderSize * 0.6f,
                               l.Position.Y - ny * LanderSize * 0.6f);
        for (int i = 0; i < 3; i++)
        {
            float spread = ((float)_rng.NextDouble() - 0.5f) * 0.6f;
            float dx = -nx + spread * (-ny);
            float dy = -ny + spread *  nx;
            float spd = 180f + (float)_rng.NextDouble() * 80f;
            uint color = (i & 1) == 0 ? 0xFF_FFCC33 : 0xFF_FF8833;
            Particles.Add(new Particle
            {
                Pos     = emitPos,
                Vel     = new Vec2(l.Velocity.X * 0.4f + dx * spd, l.Velocity.Y * 0.4f + dy * spd),
                Life    = 0.30f,
                MaxLife = 0.30f,
                Color   = color,
                Size    = 2.6f,
            });
        }
    }
    float _flameTimer;

    // Detect collision with terrain. If the ship's bounding circle dips below the
    // terrain surface at its current X, we resolve as either a landing or a crash.
    void CheckCollision()
    {
        var l = Lander;
        if (!l.Alive) return;
        float terrainY = TerrainBuilder.HeightAt(Terrain, l.Position.X);
        if (l.Position.Y + LanderSize < terrainY) return; // still in flight

        var pad = TerrainBuilder.PadAt(Terrain, l.Position.X);
        bool entirelyOverPad = pad is not null
            && (l.Position.X - LanderSize * 0.7f) >= pad.X0
            && (l.Position.X + LanderSize * 0.7f) <= pad.X1;
        bool gentleVy   = MathF.Abs(l.Velocity.Y) <= MaxLandVerticalSpd;
        bool gentleVx   = MathF.Abs(l.Velocity.X) <= MaxLandHorizontalSpd;
        float wrappedAngle = ((l.AngleRadians % MathF.Tau) + MathF.Tau) % MathF.Tau;
        if (wrappedAngle > MathF.PI) wrappedAngle -= MathF.Tau;
        bool upright    = MathF.Abs(wrappedAngle) <= MaxLandAngle;

        if (entirelyOverPad && gentleVy && gentleVx && upright)
        {
            OnLandingSuccess(pad!);
        }
        else
        {
            OnCrash();
        }
    }

    void OnLandingSuccess(LandingPad pad)
    {
        var l = Lander;
        l.Velocity = Vec2.Zero;
        l.AngularVelocity = 0f;
        l.AngleRadians = 0f;
        l.Position.Y = pad.Y - LanderSize;
        _preWarpMode = Mode;
        Mode = GameMode.Landed;
        _postOutcomeTimer = 2.5f;
        AudioEngine.StopThrust();
        AudioEngine.PlayLandingChime();

        // Scoring: landing base 50 × multiplier + 1 point per remaining kg fuel.
        LastLandingMultiplier = pad.Multiplier;
        int baseScore = 50 * pad.Multiplier;
        LastLandingFuelBonus = MathF.Floor(l.FuelKg);
        LastLandingScore = baseScore + (int)LastLandingFuelBonus;
        Score += LastLandingScore;
        if (Score > HighScore) HighScore = Score;

        Popups.Add(new ScorePopup
        {
            Pos     = new Vec2(l.Position.X, l.Position.Y - 40f),
            Value   = LastLandingScore,
            Life    = 1.8f,
            MaxLife = 1.8f,
            Color   = 0xFF_55FF77,
        });
        ShowPlacard($"TOUCHDOWN  x{pad.Multiplier}", 1.8f);
    }

    void OnCrash()
    {
        var l = Lander;
        l.Alive = false;
        _preWarpMode = Mode;
        Mode = GameMode.Crashed;
        _postOutcomeTimer = 2.5f;
        AudioEngine.StopThrust();
        AudioEngine.PlayExplosion();
        EmitExplosion(l.Position, 36, 0xFF_FF6644);
        ShowPlacard("CRASH", 1.8f);
    }

    // --- Particles + popups ---

    void UpdateParticles(float dt)
    {
        for (int i = Particles.Count - 1; i >= 0; i--)
        {
            var p = Particles[i];
            p.Pos += p.Vel * dt;
            p.Vel.Y += Gravity * dt * 0.6f;     // particles fall faster than ship for visual punch
            p.Vel *= MathF.Pow(0.96f, dt * 60f);
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
            float spd = 60f + (float)_rng.NextDouble() * 280f;
            Particles.Add(new Particle
            {
                Pos     = origin,
                Vel     = new Vec2((float)Math.Cos(ang) * spd, (float)Math.Sin(ang) * spd),
                Life    = 1.0f,
                MaxLife = 1.0f,
                Color   = color,
                Size    = 2.5f + (float)_rng.NextDouble() * 2.0f,
            });
        }
    }

    // --- Attract AI ---
    //
    // Very simple: aim for the nearest pad horizontally, brake when below 1/3 height
    // and falling fast. Doesn't always win — that's fine, attract mode is theater.
    float _attractInputTimer;
    void UpdateAttractAI(float dt)
    {
        var l = Lander;
        _attractInputTimer -= dt;
        if (_attractInputTimer > 0) return;
        _attractInputTimer = 0.06f + (float)_rng.NextDouble() * 0.06f;

        // Pick target pad: prefer pads ahead of current horizontal position.
        LandingPad? target = null;
        float bestDist = float.PositiveInfinity;
        foreach (var p in Terrain.Pads)
        {
            float midX = (p.X0 + p.X1) * 0.5f;
            float dx   = midX - l.Position.X;
            float d    = MathF.Abs(dx) + (p.Multiplier == 5 ? 30f : 0f);
            if (d < bestDist) { bestDist = d; target = p; }
        }
        if (target == null) return;

        float padCenter = (target.X0 + target.X1) * 0.5f;
        float dxToPad = padCenter - l.Position.X;

        // Rotate toward direction that gives horizontal acceleration toward pad.
        // Sign convention: positive AngleRadians = clockwise = nose tilts right,
        // so thrust pushes ship to the right.
        float desiredAngle;
        if (l.Position.Y < WorldH * 0.55f)
        {
            // Up high: tilt to drift over the pad.
            desiredAngle = MathF.Sign(dxToPad) * 0.25f;
        }
        else
        {
            // Down low: stay upright, brake the descent.
            desiredAngle = MathF.Sign(dxToPad) * 0.1f;
        }

        RotateLeft  = l.AngleRadians > desiredAngle + 0.04f;
        RotateRight = l.AngleRadians < desiredAngle - 0.04f;

        bool falling = l.Velocity.Y > 12f;
        bool low     = l.Position.Y > WorldH * 0.55f;
        bool nearTerrain = l.Position.Y + 80f > TerrainBuilder.HeightAt(Terrain, l.Position.X);
        Thrust = (falling && low) || nearTerrain || l.Velocity.Y > MaxLandVerticalSpd * 1.5f;
    }
}
