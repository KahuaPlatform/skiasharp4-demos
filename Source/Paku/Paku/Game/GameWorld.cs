namespace Paku.Game;

/// <summary>High-level game states. Paku has no separate Title — Attract IS the title screen.</summary>
public enum GameMode
{
    /// <summary>Title/idle screen with an autopiloted bot demoing play.</summary>
    Attract,
    /// <summary>Active gameplay reading the player's input.</summary>
    Playing,
    /// <summary>Player was eaten; shows the panel briefly then returns to Attract.</summary>
    GameOver,
}

/// <summary>
/// The per-frame brain for Paku: simulation, AI, collisions/absorption, scoring,
/// spawning, the panning/zooming camera, and the mode state machine. The
/// <see cref="Renderer"/> reads this state; <c>MainPage</c> feeds it input and
/// calls <see cref="Update"/> once per compositor frame.
/// </summary>
public class GameWorld
{
    /// <summary>Logical world width — far larger than the screen; the camera pans over it.</summary>
    public const float WorldWidth  = 5000f;
    /// <summary>Logical world height.</summary>
    public const float WorldHeight = 5000f;

    /// <summary>Current canvas (screen) width, refreshed each frame by <see cref="Resize"/>.</summary>
    public float CanvasW { get; private set; } = 1280;
    /// <summary>Current canvas (screen) height.</summary>
    public float CanvasH { get; private set; } = 720;

    /// <summary>Current game state.</summary>
    public GameMode Mode { get; private set; } = GameMode.Attract;
    /// <summary>The player (or the attract-mode bot) cell.</summary>
    public Cell Player { get; private set; } = new();
    /// <summary>All non-player cells.</summary>
    public List<Cell> Enemies { get; } = new();
    /// <summary>Edible food dots.</summary>
    public List<Spore> Spores { get; } = new();
    /// <summary>Transient visual particles.</summary>
    public List<Particle> Particles { get; } = new();

    /// <summary>Current run score.</summary>
    public int Score { get; private set; }
    /// <summary>Best score persisted via <see cref="HighScoreStore"/>.</summary>
    public int HighScore { get; private set; }
    /// <summary>Seconds since the current <see cref="GameMode.Playing"/> run began (drives difficulty).</summary>
    public float GameTime { get; private set; }
    /// <summary>Total elapsed seconds since construction (drives the attract-mode plasma animation).</summary>
    public float TotalTime { get; private set; }

    // Camera follows the player with smoothing; zoom shrinks as the player grows
    // so a huge blob still fits on screen ("zoom out as you win").
    /// <summary>World-space X the camera is centered on.</summary>
    public float CameraX { get; private set; }
    /// <summary>World-space Y the camera is centered on.</summary>
    public float CameraY { get; private set; }
    /// <summary>Current camera zoom factor (smaller = more zoomed out).</summary>
    public float Zoom { get; private set; } = 1f;

    /// <summary>Set by the input layer; true while thrust is requested (used for audio + motion).</summary>
    public bool Thrusting { get; set; }
    bool _wasThrusting; // previous-frame thrust, for thrust-loop start/stop edges

    /// <summary>Latest pointer X in screen pixels (mouse-aim fallback).</summary>
    public float PointerX { get; set; }
    /// <summary>Latest pointer Y in screen pixels.</summary>
    public float PointerY { get; set; }
    /// <summary>True once a pointer position has been seen this session.</summary>
    public bool PointerValid { get; set; }

    /// <summary>Keyboard direction flags set by the input layer each frame.</summary>
    public bool InputUp, InputDown, InputLeft, InputRight;

    const float PlayerStartMass    = 40f;
    const float ThrustForce        = 280f;
    const float ThrustMassCost     = 8f;    // mass/sec burned while thrusting (you shrink to move)
    const float MinPlayerMass      = 15f;   // can't thrust below this — prevents self-starvation
    const float AbsorbRatio        = 1.25f; // attacker must be 25% bigger to eat the defender
    const int   MaxSpores          = 350;
    const int   MaxEnemies         = 120;
    const int   MaxParticles       = 600;
    const float SporeRespawnRate   = 8f;    // spores/sec
    const float GameOverDelay      = 3f;    // seconds the game-over panel lingers before Attract

    float _sporeAccum;
    float _enemySpawnTimer;
    float _gameOverTimer;
    readonly Random _rng = new();
    readonly HighScoreStore _hiScore = new("Paku");

    /// <summary>Loads the high score and opens straight into attract mode.</summary>
    public GameWorld()
    {
        HighScore = _hiScore.Load();
        StartAttract();
    }

    /// <summary>Records the current canvas size; called each frame by the surface.</summary>
    public void Resize(float w, float h) { CanvasW = w; CanvasH = h; }

    // Euclidean distance between two world points.
    static float Dist(Vec2 a, Vec2 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Resets into attract mode with a fresh bot-controlled player and population.</summary>
    public void StartAttract()
    {
        Mode = GameMode.Attract;
        ResetWorld();
        Player = new Cell
        {
            Pos = new Vec2(WorldWidth / 2, WorldHeight / 2),
            Mass = PlayerStartMass,
            Hue = 180,
            Behavior = CellBehavior.Passive
        };
        Player.InitShape(_rng);
        SpawnInitialPopulation();
    }

    /// <summary>Begins a fresh player-controlled run from a clean world.</summary>
    public void StartGame()
    {
        Mode = GameMode.Playing;
        GameTime = 0;
        Score = 0;
        ResetWorld();
        Player = new Cell
        {
            Pos = new Vec2(WorldWidth / 2, WorldHeight / 2),
            Mass = PlayerStartMass,
            Hue = 160, // cyan-ish
            Behavior = CellBehavior.Passive
        };
        Player.InitShape(_rng);
        SpawnInitialPopulation();
    }

    void ResetWorld()
    {
        Enemies.Clear();
        Spores.Clear();
        Particles.Clear();
        _sporeAccum = 0;
        _enemySpawnTimer = 0;
        _gameOverTimer = 0;
    }

    // Seeds the world with a food layer plus a size-tiered enemy population so the
    // arena feels alive and offers prey at every player size.
    void SpawnInitialPopulation()
    {
        for (int i = 0; i < MaxSpores; i++)
            SpawnSpore();

        // Lots of small passive cells to make the world feel alive
        for (int i = 0; i < 50; i++)
            SpawnEnemy(massMin: 8, massMax: 25, CellBehavior.Passive);

        // Medium cells
        for (int i = 0; i < 20; i++)
            SpawnEnemy(massMin: 20, massMax: 60, CellBehavior.Passive);

        // Larger ones
        for (int i = 0; i < 10; i++)
            SpawnEnemy(massMin: 40, massMax: 100, CellBehavior.Passive);

        // Early hunters
        for (int i = 0; i < 8; i++)
            SpawnEnemy(massMin: 25, massMax: 70, CellBehavior.Hunter);
    }

    void SpawnSpore()
    {
        Spores.Add(new Spore
        {
            Pos = new Vec2(_rng.NextSingle() * WorldWidth, _rng.NextSingle() * WorldHeight),
            Hue = _rng.NextSingle() * 360f
        });
    }

    // Spawns one enemy of random mass in [massMin,massMax], placed away from the
    // player (retries up to 20 times to keep at least 500 units clear so nothing
    // pops in on top of you).
    void SpawnEnemy(float massMin, float massMax, CellBehavior behavior)
    {
        float mass = massMin + _rng.NextSingle() * (massMax - massMin);
        Vec2 pos;
        int attempts = 0;
        do
        {
            pos = new Vec2(_rng.NextSingle() * WorldWidth, _rng.NextSingle() * WorldHeight);
            attempts++;
        } while (attempts < 20 && Dist(pos, Player.Pos) < 500f);

        float speed = 30f + _rng.NextSingle() * 60f;
        float angle = _rng.NextSingle() * MathF.Tau;
        var cell = new Cell
        {
            Pos = pos,
            Vel = Vec2.FromAngle(angle) * speed,
            Mass = mass,
            Hue = _rng.NextSingle() * 360f,
            Behavior = behavior,
            HuntRange = 400f + _rng.NextSingle() * 400f
        };
        cell.InitShape(_rng);
        Enemies.Add(cell);
    }

    /// <summary>
    /// Advances the whole game one frame. Dispatches on <see cref="Mode"/>: the
    /// shared simulation always runs, with input/AI and audio layered on per state.
    /// </summary>
    public void Update(float dt)
    {
        TotalTime += dt;

        switch (Mode)
        {
            case GameMode.Attract:
                UpdateAttractAI(dt);
                UpdateSimulation(dt);
                break;
            case GameMode.Playing:
                GameTime += dt;
                UpdatePlayerInput(dt);
                UpdateSimulation(dt);
                UpdateAudioState();
                break;
            case GameMode.GameOver:
                _gameOverTimer += dt;
                UpdateSimulation(dt);
                if (_gameOverTimer > GameOverDelay)
                    StartAttract();
                break;
        }
    }

    // Converts player input into thrust: pick an aim direction (keyboard first,
    // else pointer relative to screen center), then accelerate that way while
    // burning mass and emitting exhaust in the opposite direction.
    void UpdatePlayerInput(float dt)
    {
        Vec2 dir = Vec2.Zero;

        // Keyboard direction takes priority
        if (InputUp)    dir.Y -= 1;
        if (InputDown)  dir.Y += 1;
        if (InputLeft)  dir.X -= 1;
        if (InputRight) dir.X += 1;

        float dirLen = dir.Length;

        // If no keyboard direction, use pointer aim (relative to screen center)
        if (dirLen < 0.01f && PointerValid)
        {
            float screenCx = CanvasW / 2f;
            float screenCy = CanvasH / 2f;
            float dx = PointerX - screenCx;
            float dy = PointerY - screenCy;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len > 5f)
            {
                dir = new Vec2(dx / len, dy / len);
                dirLen = 1f;
            }
        }

        if (dirLen > 0.01f)
            dir = dir * (1f / dirLen);

        if (Thrusting && Player.Mass > MinPlayerMass && dirLen > 0.01f)
        {
            Player.Vel.X += dir.X * ThrustForce * dt;
            Player.Vel.Y += dir.Y * ThrustForce * dt;

            float massLost = ThrustMassCost * dt;
            Player.Mass -= massLost;

            EmitExhaust(Player, -dir.X, -dir.Y, dt);
        }
    }

    // Spawns thrust-exhaust particles streaming out the back of a thrusting cell.
    // (dirX,dirY) is the exhaust direction (i.e. opposite the thrust/aim vector).
    void EmitExhaust(Cell cell, float dirX, float dirY, float dt)
    {
        int count = (int)(30 * dt) + 1; // ~30 particles/sec, at least one per frame
        for (int i = 0; i < count && Particles.Count < MaxParticles; i++)
        {
            float spread = 0.5f;
            float ex = dirX + (_rng.NextSingle() - 0.5f) * spread;
            float ey = dirY + (_rng.NextSingle() - 0.5f) * spread;
            float speed = 120f + _rng.NextSingle() * 180f;
            float life = 0.4f + _rng.NextSingle() * 0.6f;
            Particles.Add(new Particle
            {
                Pos = new Vec2(
                    cell.Pos.X + dirX * cell.Radius,
                    cell.Pos.Y + dirY * cell.Radius),
                Vel = new Vec2(
                    cell.Vel.X + ex * speed,
                    cell.Vel.Y + ey * speed),
                Life = life,
                MaxLife = life,
                Hue = cell.Hue + (_rng.NextSingle() - 0.5f) * 40f,
                Size = 2f + _rng.NextSingle() * 3f
            });
        }
    }

    // Attract-mode autopilot: steer the "player" cell like a greedy bot —
    // head for the nearest edible cell, fall back to the nearest spore if no
    // prey is close, and add a repulsion term to dodge anything big nearby.
    void UpdateAttractAI(float dt)
    {
        Cell ai = Player;
        Vec2 dir = Vec2.Zero;
        float bestDist = float.MaxValue;

        // Seek the nearest enemy small enough to absorb.
        foreach (var e in Enemies)
        {
            if (!e.Alive || e.Mass * AbsorbRatio > ai.Mass) continue;
            float d = Dist(ai.Pos, e.Pos);
            if (d < bestDist) { bestDist = d; dir = e.Pos - ai.Pos; }
        }

        // No prey within 800 units → graze on the nearest spore instead.
        if (bestDist > 800f)
        {
            foreach (var s in Spores)
            {
                if (!s.Alive) continue;
                float d = Dist(ai.Pos, s.Pos);
                if (d < bestDist) { bestDist = d; dir = new Vec2(s.Pos.X - ai.Pos.X, s.Pos.Y - ai.Pos.Y); }
            }
        }

        // Add an inverse-square repulsion away from any nearby bigger threat.
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            if (e.Mass > ai.Mass * AbsorbRatio)
            {
                float d = Dist(ai.Pos, e.Pos);
                if (d < 300f)
                {
                    Vec2 flee = ai.Pos - e.Pos;
                    float fl = flee.Length;
                    if (fl > 0.01f)
                        dir = dir + flee * (300f / (fl * fl)); // closer threats push harder
                }
            }
        }

        float len = dir.Length;
        if (len > 0.01f)
        {
            dir = dir * (1f / len);
            // The bot thrusts gently (half force, third the mass cost) so the demo
            // run lasts a long time.
            if (ai.Mass > MinPlayerMass)
            {
                ai.Vel.X += dir.X * ThrustForce * 0.5f * dt;
                ai.Vel.Y += dir.Y * ThrustForce * 0.5f * dt;
                ai.Mass -= ThrustMassCost * 0.3f * dt;
                EmitExhaust(ai, -dir.X, -dir.Y, dt);
            }
        }

        // Slowly top the bot back up so an unlucky run doesn't end the demo early.
        if (ai.Mass < PlayerStartMass * 0.7f)
            ai.Mass += 5f * dt;
    }

    // The shared physics/collision/spawn/camera step that runs in every mode.
    void UpdateSimulation(float dt)
    {
        Player.Update(dt, WorldWidth, WorldHeight);

        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            UpdateEnemyAI(e, dt);
            e.Update(dt, WorldWidth, WorldHeight);
        }

        foreach (var p in Particles)
            p.Update(dt);

        // Player vs spores
        float pr = Player.Radius;
        foreach (var s in Spores)
        {
            if (!s.Alive) continue;
            float d = Dist(Player.Pos, s.Pos);
            if (d < pr + Spore.Radius)
            {
                s.Alive = false;
                Player.Mass += Spore.Mass;
                if (Mode == GameMode.Playing)
                {
                    Score += 1;
                    AudioEngine.PlayAbsorb();
                }
                SpawnAbsorbBurst(s.Pos, s.Hue, 4);
            }
        }

        // Player vs enemies. Touch is at 80% of summed radii so blobs visibly
        // overlap before an absorption resolves.
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            var e = Enemies[i];
            if (!e.Alive) continue;
            float d = Dist(Player.Pos, e.Pos);
            float touchDist = Player.Radius + e.Radius;
            if (d < touchDist * 0.8f)
            {
                if (Player.Mass >= e.Mass * AbsorbRatio)
                {
                    // Player is big enough → eat the enemy (gain 80% of its mass).
                    e.Alive = false;
                    Player.Mass += e.Mass * 0.8f;
                    if (Mode == GameMode.Playing)
                    {
                        Score += (int)(e.Mass);
                        AudioEngine.PlayAbsorb();
                    }
                    SpawnAbsorbBurst(e.Pos, e.Hue, (int)(e.Mass / 5) + 4);
                }
                else if (e.Mass >= Player.Mass * AbsorbRatio)
                {
                    // Enemy is big enough → it eats the player.
                    e.Mass += Player.Mass * 0.8f;
                    SpawnAbsorbBurst(Player.Pos, Player.Hue, 20);
                    if (Mode == GameMode.Playing)
                    {
                        // Real run: end it, persist a new high score.
                        AudioEngine.PlayDeath();
                        if (Score > HighScore)
                        {
                            HighScore = Score;
                            _hiScore.Save(HighScore);
                        }
                        Mode = GameMode.GameOver;
                        _gameOverTimer = 0;
                        AudioEngine.StopThrust();   // UpdateAudioState won't run again in GameOver
                        _wasThrusting = false;
                    }
                    else
                    {
                        // Attract bot got eaten: just respawn it so the demo continues.
                        Player.Pos = new Vec2(
                            _rng.NextSingle() * WorldWidth,
                            _rng.NextSingle() * WorldHeight);
                        Player.Mass = PlayerStartMass;
                        Player.Vel = Vec2.Zero;
                    }
                }
            }
        }

        // Enemy vs enemy — bigger absorbs smaller (gains 60% of mass), so the
        // population naturally consolidates into a few large cells over time.
        for (int i = 0; i < Enemies.Count; i++)
        {
            var a = Enemies[i];
            if (!a.Alive) continue;
            for (int j = i + 1; j < Enemies.Count; j++)
            {
                var b = Enemies[j];
                if (!b.Alive) continue;
                float d = Dist(a.Pos, b.Pos);
                if (d < (a.Radius + b.Radius) * 0.8f)
                {
                    if (a.Mass >= b.Mass * AbsorbRatio)
                    {
                        a.Mass += b.Mass * 0.6f;
                        b.Alive = false;
                        SpawnAbsorbBurst(b.Pos, b.Hue, 4);
                    }
                    else if (b.Mass >= a.Mass * AbsorbRatio)
                    {
                        b.Mass += a.Mass * 0.6f;
                        a.Alive = false;
                        SpawnAbsorbBurst(a.Pos, a.Hue, 4);
                    }
                }
            }
        }

        // Enemies eat spores
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            float er = e.Radius;
            foreach (var s in Spores)
            {
                if (!s.Alive) continue;
                float d = Dist(e.Pos, s.Pos);
                if (d < er + Spore.Radius)
                {
                    s.Alive = false;
                    e.Mass += Spore.Mass;
                }
            }
        }

        // Reap everything that died this frame in one pass.
        Enemies.RemoveAll(e => !e.Alive);
        Spores.RemoveAll(s => !s.Alive);
        Particles.RemoveAll(p => !p.Alive);

        // Respawn spores at a steady rate using a fractional accumulator (so sub-1
        // spores/frame still add up correctly), capped at MaxSpores.
        _sporeAccum += SporeRespawnRate * dt;
        while (_sporeAccum >= 1f && Spores.Count < MaxSpores)
        {
            SpawnSpore();
            _sporeAccum -= 1f;
        }

        // Progressive enemy spawning: the spawn interval shortens with GameTime
        // (down to 0.5s) so the arena gets busier the longer you survive.
        _enemySpawnTimer += dt;
        float spawnInterval = MathF.Max(0.5f, 2.0f - GameTime * 0.015f);
        if (_enemySpawnTimer >= spawnInterval && Enemies.Count < MaxEnemies)
        {
            _enemySpawnTimer = 0;
            // difficulty ramps 0→1 over the first two minutes and scales enemy
            // size and the chance that a spawn is an aggressive Hunter.
            float difficulty = MathF.Min(GameTime / 120f, 1f);
            float massMin = 10f + difficulty * 40f;
            float massMax = 30f + difficulty * Player.Mass * 0.8f;
            var behavior = _rng.NextSingle() < 0.3f + difficulty * 0.4f
                ? CellBehavior.Hunter
                : CellBehavior.Passive;
            SpawnEnemy(massMin, MathF.Max(massMin + 5, massMax), behavior);
        }

        // Camera: exponential-smoothed follow of the player; target zoom shrinks
        // as the player's radius grows so a giant blob still fits on screen.
        float targetZoom = 40f / MathF.Max(Player.Radius, 10f);
        targetZoom = Math.Clamp(targetZoom, 0.08f, 1.5f);
        Zoom += (targetZoom - Zoom) * 2f * dt;

        float camSmooth = 4f * dt;
        CameraX += (Player.Pos.X - CameraX) * camSmooth;
        CameraY += (Player.Pos.Y - CameraY) * camSmooth;
    }

    // Steers one enemy cell. Passive cells random-walk; Hunters chase prey
    // (incl. the player) and flee bigger threats. Both are speed-capped, with the
    // cap falling as mass rises so big cells are sluggish.
    void UpdateEnemyAI(Cell e, float dt)
    {
        if (e.Behavior == CellBehavior.Passive)
        {
            // Brownian wander: nudge velocity randomly each frame, then clamp speed.
            e.Vel.X += (_rng.NextSingle() - 0.5f) * 40f * dt;
            e.Vel.Y += (_rng.NextSingle() - 0.5f) * 40f * dt;
            float speed = e.Vel.Length;
            float maxSpeed = 80f / MathF.Sqrt(MathF.Max(e.Mass, 1f)) * 10f;
            if (speed > maxSpeed)
            {
                e.Vel.X *= maxSpeed / speed;
                e.Vel.Y *= maxSpeed / speed;
            }
            return;
        }

        // Hunter: first react to the player if within sensing range...
        float dist = Dist(e.Pos, Player.Pos);
        if (dist < e.HuntRange)
        {
            Vec2 toPlayer = Player.Pos - e.Pos;
            float len = toPlayer.Length;
            if (len > 0.01f)
            {
                toPlayer = toPlayer * (1f / len);
                if (e.Mass >= Player.Mass * AbsorbRatio)
                {
                    float chaseForce = 150f;
                    e.Vel.X += toPlayer.X * chaseForce * dt;
                    e.Vel.Y += toPlayer.Y * chaseForce * dt;
                }
                else if (Player.Mass >= e.Mass * AbsorbRatio)
                {
                    float fleeForce = 200f;
                    e.Vel.X -= toPlayer.X * fleeForce * dt;
                    e.Vel.Y -= toPlayer.Y * fleeForce * dt;
                }
            }
        }

        // ...then also hunt the nearest smaller enemy cell within range.
        Cell? target = null;
        float bestDist = e.HuntRange;
        foreach (var other in Enemies)
        {
            if (other == e || !other.Alive) continue;
            if (e.Mass < other.Mass * AbsorbRatio) continue; // not edible — skip
            float d = Dist(e.Pos, other.Pos);
            if (d < bestDist) { bestDist = d; target = other; }
        }
        if (target != null)
        {
            Vec2 toTarget = target.Pos - e.Pos;
            float len = toTarget.Length;
            if (len > 0.01f)
            {
                toTarget = toTarget * (1f / len);
                e.Vel.X += toTarget.X * 100f * dt;
                e.Vel.Y += toTarget.Y * 100f * dt;
            }
        }

        float sp = e.Vel.Length;
        float max = 120f / MathF.Sqrt(MathF.Max(e.Mass, 1f)) * 12f;
        if (sp > max) { e.Vel.X *= max / sp; e.Vel.Y *= max / sp; }
    }

    // Emits a radial burst of particles at an absorption site for visual punch.
    void SpawnAbsorbBurst(Vec2 pos, float hue, int count)
    {
        for (int i = 0; i < count && Particles.Count < MaxParticles; i++)
        {
            float angle = _rng.NextSingle() * MathF.Tau;
            float speed = 60f + _rng.NextSingle() * 180f;
            float life = 0.3f + _rng.NextSingle() * 0.5f;
            Particles.Add(new Particle
            {
                Pos = pos,
                Vel = Vec2.FromAngle(angle) * speed,
                Life = life,
                MaxLife = life,
                Hue = hue + (_rng.NextSingle() - 0.5f) * 60f,
                Size = 2f + _rng.NextSingle() * 4f
            });
        }
    }

    // Drives the looping thrust voice off rising/falling edges of "effective
    // thrust" (thrusting AND above the minimum mass), so the loop starts once when
    // thrust begins and stops once when it ends.
    void UpdateAudioState()
    {
        if (Thrusting && Player.Mass > MinPlayerMass && !_wasThrusting)
            AudioEngine.StartThrust();
        else if ((!Thrusting || Player.Mass <= MinPlayerMass) && _wasThrusting)
            AudioEngine.StopThrust();
        _wasThrusting = Thrusting && Player.Mass > MinPlayerMass;
    }
}
