namespace UnoGalaga.Game;

public enum GameMode { Title, Playing, GameOver }
public enum WaveState { Spawning, Settling, Attacking, Placard, Cleared }

// Wave choreography skeleton. Spawns a 4×6 formation enemy-by-enemy on
// alternating left/right entry streams, settles for a beat, then launches
// pair-dives every PairAttackInterval seconds. No collisions yet — bullets
// fire and miss, enemies dive past and exit.
public class GameWorld
{
    public float Width  = 720f;
    public float Height = 1280f;

    public Player Player = new();
    public List<Enemy>    Enemies   = new();
    public List<Bullet>   Bullets   = new();
    public List<Particle> Particles = new();

    public GameMode  Mode      = GameMode.Title;
    public WaveState WaveState = WaveState.Spawning;
    public int Score;
    public int HighScore;
    public int Stage = 1;
    public bool IsChallengeStage;
    public string PlacardText = "";
    public bool MovingLeft;
    public bool MovingRight;
    public bool BulletCapEnabled = true;  // K key cheats it off — old cooldown-only firing

    public float WaveTime;   // total elapsed seconds in current wave (for breathing phase)

    // --- Player tuning ---
    const float PlayerSpeed = 380f;
    const float ShootInterval = 0.18f;
    const int   MaxPlayerBullets = 2;   // Galaga rule — forces shot discipline

    // --- Formation layout: authentic Galaga 4+8+8+10+10 = 40 enemies ---
    // Row 0 (top): 4 high-tier (snowflake + mothership + 2 bosses)
    // Rows 1-2:    8 medium-tier each (captains, then wings)
    // Rows 3-4:    10 drones each (the wider "bee" rows that stick out at the bottom)
    static readonly int[] RowCounts = { 4, 8, 8, 10, 10 };
    const float FormationColSpacing = 60f;
    const float FormationRowSpacing = 64f;
    const float FormationCenterY    = 140f;
    const int   FlightSize          = 10;   // 40 enemies / 4 flights

    // --- Choreography timing ---
    const float SpawnInterval        = 0.18f;
    const float EntryDuration        = 1.8f;
    const float SettlingDelay        = 0.8f;
    const float FirstAttackDelay     = 1.0f;
    const float PairAttackInterval   = 2.5f;
    const float DiveDuration         = 3.2f;
    const float RejoinDuration       = 1.6f;
    const float BreathingFreq        = 1.4f;
    const float BreathingAmplitude   = 7f;
    const float DeathDelay           = 1.4f;
    const float RespawnInvincibility = 2.0f;
    const float EnemyBulletSpeed     = 320f;
    const int   MaxEnemyBullets      = 4;
    const float EnemyFireMin         = 0.45f;   // shortest gap between an enemy's shots
    const float EnemyFireMax         = 1.25f;   // longest gap
    const float PlacardDuration      = 2.5f;
    const int   ChallengeWaveSize    = 40;
    const int   ChallengePerfectBonus = 10_000;
    const float ChallengeSpawnInterval = 0.16f;
    const float ChallengeFlythroughDuration = 4.2f;

    // Explosion + score data per enemy kind (indexed by Enemy.Kind 0..5).
    static readonly uint[] ExplosionColors =
    {
        0xFF99FF55u,  // drone      — acid green
        0xFFFF44AAu,  // wing       — hot pink
        0xFF33CCFFu,  // captain    — electric cyan
        0xFFFFAA33u,  // boss       — neon orange
        0xFFF85977u,  // mothership — Uno-arc pink
        0xFFF58220u,  // snowflake  — Kahua orange
    };
    static readonly int[] EnemyScores = { 50, 80, 100, 200, 300, 500 };
    const uint PlayerExplosionColor = 0xFF33F8FFu;

    int _spawnedCount;
    int _challengeKills;
    int _challengeStagePattern;  // 0-3, selects which of the four challenge choreographies
    float _spawnTimer;
    float _settleTimer;
    float _attackTimer;
    float _deathTimer;
    float _placardTimer;
    float _flybyTimer;           // seconds until next mystery flyby attempt
    readonly Random _rng = new(42);

    const float FlybyInterval     = 28f;  // average gap between flybys
    const float FlybyDuration     = 6f;   // seconds to traverse the screen
    const int   FlybyKillBonus    = 1500; // score for shooting down the mystery mothership

    public GameWorld()
    {
        ResetForTitle();
    }

    public void Resize(float w, float h)
    {
        Width  = MathF.Max(360f, w);
        Height = MathF.Max(480f, h);
    }

    public void StartGame()
    {
        Mode  = GameMode.Playing;
        Stage = 1;
        IsChallengeStage = false;
        Score = 0;
        Enemies.Clear();
        Bullets.Clear();
        Particles.Clear();
        Player = new Player
        {
            Position = new Vec2(Width / 2f, Height - 80f),
            InvincibleTime = 1.5f,
        };
        WaveTime    = 0f;
        _flybyTimer = 6f;  // first flyby ~6s into the attacking phase so testers see it quickly
        StartNormalWave();
    }

    void StartNormalWave()
    {
        WaveState        = WaveState.Spawning;
        IsChallengeStage = false;
        _spawnedCount    = 0;
        _challengeKills  = 0;
        _spawnTimer      = 0f;
        _settleTimer     = 0f;
        _attackTimer     = 0f;
    }

    void StartChallengeWave()
    {
        WaveState        = WaveState.Spawning;
        IsChallengeStage = true;
        _spawnedCount    = 0;
        _challengeKills  = 0;
        _spawnTimer      = 0f;
        // Pattern 0 -> stage 3, 1 -> stage 7, 2 -> stage 11, 3 -> stage 15, repeating.
        _challengeStagePattern = (Stage / 4) % 4;
    }

    void StartPlacard(string text)
    {
        WaveState     = WaveState.Placard;
        PlacardText   = text;
        _placardTimer = PlacardDuration;
    }

    static bool IsChallengeStageNumber(int stage) => stage % 4 == 3;  // stages 3, 7, 11, ...

    public void ResetForTitle()
    {
        Mode = GameMode.Title;
        WaveState = WaveState.Cleared;
        Enemies.Clear();
        Bullets.Clear();
        Particles.Clear();
        Player = new Player
        {
            Position = new Vec2(Width / 2f, Height - 80f),
        };
    }

    public void FireBullet()
    {
        if (Player.ShootCooldown > 0 || !Player.Alive) return;

        if (BulletCapEnabled)
        {
            int active = 0;
            foreach (var b in Bullets) if (b.Alive && b.FromPlayer) active++;
            if (active >= MaxPlayerBullets) return;
        }

        Bullets.Add(new Bullet
        {
            Position = new Vec2(Player.Position.X, Player.Position.Y - Player.Radius - 4f),
            Velocity = new Vec2(0f, -640f),
            FromPlayer = true,
        });
        Player.ShootCooldown = ShootInterval;
        AudioEngine.PlayShoot();
    }

    public void Update(float dt)
    {
        WaveTime += dt;
        UpdatePlayer(dt);

        switch (WaveState)
        {
            case WaveState.Spawning:  UpdateSpawning(dt);  break;
            case WaveState.Settling:  UpdateSettling(dt);  break;
            case WaveState.Attacking: UpdateAttacking(dt); break;
            case WaveState.Placard:   UpdatePlacard(dt);   break;
        }

        UpdateEnemyPositions(dt);
        UpdateBullets(dt);
        UpdateParticles(dt);

        CheckCollisions();

        Bullets.RemoveAll(b => !b.Alive);
        Enemies.RemoveAll(e => !e.Alive);
        Particles.RemoveAll(p => p.Lifetime <= 0f);
    }

    // --- Collisions ---

    void CheckCollisions()
    {
        // Player bullets vs enemies. Skip enemies that are off-screen during rejoin so
        // shots don't connect with invisible targets above the playfield.
        foreach (var bullet in Bullets)
        {
            if (!bullet.Alive || !bullet.FromPlayer) continue;
            foreach (var enemy in Enemies)
            {
                if (!enemy.Alive) continue;
                if (enemy.State == EnemyState.Rejoining && enemy.Position.Y < 0f) continue;
                if (!CirclesOverlap(bullet.Position, bullet.Radius, enemy.Position, enemy.Radius)) continue;

                bullet.Alive = false;
                enemy.Alive  = false;
                int score;
                if (enemy.State == EnemyState.Flyby)
                {
                    score = FlybyKillBonus;  // mystery mothership — fixed high score
                }
                else
                {
                    score = ScoreForKind(enemy.Kind);
                    if (enemy.State == EnemyState.Diving) score *= 2;  // Galaga-style dive bonus
                }
                Score += score;
                if (IsChallengeStage && enemy.State != EnemyState.Flyby) _challengeKills++;
                SpawnExplosion(enemy.Position, ExplosionColors[Math.Clamp(enemy.Kind, 0, ExplosionColors.Length - 1)]);
                AudioEngine.PlayExplosion();
                break;
            }
        }

        // Diving enemies vs the player. Player is immune during the brief death+respawn flicker.
        if (Player.Alive && Player.InvincibleTime <= 0f)
        {
            foreach (var enemy in Enemies)
            {
                if (!enemy.Alive || enemy.State != EnemyState.Diving) continue;
                if (!CirclesOverlap(enemy.Position, enemy.Radius, Player.Position, Player.Radius)) continue;

                enemy.Alive   = false;
                KillPlayer(enemyKind: enemy.Kind);
                SpawnExplosion(enemy.Position, ExplosionColors[Math.Clamp(enemy.Kind, 0, ExplosionColors.Length - 1)]);
                break;
            }
        }

        // Enemy bullets vs the player.
        if (Player.Alive && Player.InvincibleTime <= 0f)
        {
            foreach (var bullet in Bullets)
            {
                if (!bullet.Alive || bullet.FromPlayer) continue;
                if (!CirclesOverlap(bullet.Position, bullet.Radius, Player.Position, Player.Radius)) continue;

                bullet.Alive = false;
                KillPlayer(enemyKind: -1);
                break;
            }
        }
    }

    void KillPlayer(int enemyKind)
    {
        Player.Alive = false;
        Player.Lives = Math.Max(0, Player.Lives - 1);
        _deathTimer  = DeathDelay;
        SpawnExplosion(Player.Position, PlayerExplosionColor, count: 22);
        AudioEngine.PlayExplosion();
    }

    static bool CirclesOverlap(Vec2 a, float ra, Vec2 b, float rb)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float r  = ra + rb;
        return dx * dx + dy * dy < r * r;
    }

    int ScoreForKind(int kind) =>
        EnemyScores[Math.Clamp(kind, 0, EnemyScores.Length - 1)];

    void SpawnExplosion(Vec2 pos, uint color, int count = 14)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(i * Math.PI * 2.0 / count) + (float)(_rng.NextDouble() * 0.4 - 0.2);
            float speed = 100f + (float)_rng.NextDouble() * 160f;
            float life  = 0.45f + (float)_rng.NextDouble() * 0.5f;
            Particles.Add(new Particle
            {
                Position = pos,
                Velocity = new Vec2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
                Lifetime = life,
                MaxLife  = life,
                Color    = color,
            });
        }
    }

    // --- Wave state machine ---

    void UpdateSpawning(float dt)
    {
        _spawnTimer -= dt;
        int totalSlots  = IsChallengeStage ? ChallengeWaveSize : TotalFormationSlots();
        float interval  = IsChallengeStage ? ChallengeSpawnInterval : SpawnInterval;

        if (_spawnTimer <= 0f && _spawnedCount < totalSlots)
        {
            SpawnNextEnemy();
            _spawnTimer = interval;
        }

        if (IsChallengeStage)
        {
            // Challenge stages have no formation: each spawned enemy flies through and
            // exits. The stage clears when all 40 have either been killed or flown off.
            if (_spawnedCount >= totalSlots && Enemies.Count == 0)
            {
                FinishChallengeStage();
            }
        }
        else if (_spawnedCount >= totalSlots)
        {
            bool allSettled = true;
            foreach (var e in Enemies)
            {
                if (e.State == EnemyState.Entering) { allSettled = false; break; }
            }
            if (allSettled)
            {
                WaveState    = WaveState.Settling;
                _settleTimer = SettlingDelay;
            }
        }
    }

    void FinishChallengeStage()
    {
        bool perfect = _challengeKills >= ChallengeWaveSize;
        if (perfect) Score += ChallengePerfectBonus;
        string label = perfect ? $"PERFECT  -  BONUS {ChallengePerfectBonus:N0}" : "CHALLENGE COMPLETE";
        StartPlacard(label);
    }

    void UpdateSettling(float dt)
    {
        _settleTimer -= dt;
        if (_settleTimer <= 0f)
        {
            WaveState    = WaveState.Attacking;
            _attackTimer = FirstAttackDelay;
        }
    }

    void UpdateAttacking(float dt)
    {
        _attackTimer -= dt;
        if (_attackTimer <= 0f)
        {
            LaunchPairDive();
            _attackTimer = PairAttackInterval;
        }

        // Mystery flyby — periodic bonus target traversing the top of the screen.
        _flybyTimer -= dt;
        if (_flybyTimer <= 0f)
        {
            SpawnFlyby();
            _flybyTimer = FlybyInterval + (float)_rng.NextDouble() * 10f;
        }

        // Wave-clear requires the formation to be gone AND no flyby still in transit
        // (otherwise a flyby active at clear time would skip to the next stage early).
        if (Enemies.Count == 0 || !AnyFormationEnemyAlive())
        {
            if (!AnyFlybyAlive())
            {
                int next = Stage + 1;
                StartPlacard(IsChallengeStageNumber(next) ? "CHALLENGING STAGE" : $"STAGE {next}");
            }
        }
    }

    bool AnyFormationEnemyAlive()
    {
        foreach (var e in Enemies) if (e.Alive && e.State != EnemyState.Flyby) return true;
        return false;
    }

    bool AnyFlybyAlive()
    {
        foreach (var e in Enemies) if (e.Alive && e.State == EnemyState.Flyby) return true;
        return false;
    }

    void SpawnFlyby()
    {
        bool fromLeft = _rng.Next(2) == 0;
        // Alternate which mark flies by — gives both brands stage time.
        int kind = _rng.Next(2) == 0 ? 4 : 5;
        Enemies.Add(new Enemy
        {
            Kind          = kind,
            Position      = Paths.FlybyPath(fromLeft, Width, 0f),
            State         = EnemyState.Flyby,
            PathT         = 0f,
            PathDuration  = FlybyDuration,
            FlybyFromLeft = fromLeft,
            Radius        = 20f,
        });
    }

    void UpdatePlacard(float dt)
    {
        _placardTimer -= dt;
        if (_placardTimer <= 0f)
        {
            // Clear leftover enemy bullets so the new stage starts clean.
            Bullets.RemoveAll(b => !b.FromPlayer);
            Stage++;
            if (IsChallengeStageNumber(Stage))
                StartChallengeWave();
            else
                StartNormalWave();
        }
    }

    // --- Spawning ---

    void SpawnNextEnemy()
    {
        int idx = _spawnedCount++;

        if (IsChallengeStage)
        {
            int subIdx = idx % 8;
            int kind = idx switch
            {
                _ when idx % 10 == 0 => 4,  // sprinkle one Uno mothership per 10
                _ when idx % 10 == 5 => 5,  // and one Kahua snowflake
                _ => idx % 4 switch
                {
                    0 => 2,  // captain
                    1 => 1,  // wing
                    2 => 0,  // drone
                    _ => 3,  // boss
                },
            };
            Enemies.Add(new Enemy
            {
                Kind         = kind,
                PatternIdx   = subIdx,
                Position     = Paths.ChallengePath(_challengeStagePattern, subIdx, Width, Height, 0f),
                State        = EnemyState.Entering,
                PathT        = 0f,
                PathDuration = ChallengeFlythroughDuration,
                IsChallengeFlythrough = true,
                Phase        = idx * 0.17f,
                Radius       = 16f,
            });
            return;
        }

        var (row, col) = SlotFromIndex(idx);
        bool fromLeft = (idx % 2 == 0);
        int flightIdx = Math.Min(idx / FlightSize, 3);

        var slot = GetSlotPosition(row, col);
        Enemies.Add(new Enemy
        {
            Kind          = GetSlotKind(row, col),
            SlotPos       = slot,
            Position      = Paths.EntryPath(flightIdx, slot, fromLeft, Width, Height, 0f),
            State         = EnemyState.Entering,
            PathT         = 0f,
            PathDuration  = EntryDuration,
            EntryFromLeft = fromLeft,
            FlightIdx     = flightIdx,
            Phase         = idx * 0.21f,
            Radius        = 16f,
        });
    }

    static int TotalFormationSlots()
    {
        int n = 0;
        foreach (int rc in RowCounts) n += rc;
        return n;
    }

    // Walk RowCounts cumulatively to convert a linear spawn index into (row, col).
    static (int row, int col) SlotFromIndex(int idx)
    {
        for (int r = 0; r < RowCounts.Length; r++)
        {
            if (idx < RowCounts[r]) return (r, idx);
            idx -= RowCounts[r];
        }
        // Past the end — shouldn't happen if callers respect TotalFormationSlots.
        return (RowCounts.Length - 1, RowCounts[^1] - 1);
    }

    Vec2 GetSlotPosition(int row, int col)
    {
        int cols = RowCounts[row];
        float x = Width / 2f + (col - (cols - 1) / 2f) * FormationColSpacing;
        float y = FormationCenterY + row * FormationRowSpacing;
        return new Vec2(x, y);
    }

    int GetSlotKind(int row, int col)
    {
        // Row 0 (4 slots): bosses flanking the Kahua snowflake + Uno mothership pair.
        // Rows 1-2 (8 each): captains then wings.
        // Rows 3-4 (10 each): drones.
        return row switch
        {
            0 => col switch
            {
                1 => 5,  // Kahua snowflake
                2 => 4,  // Uno mothership
                _ => 3,  // bosses at cols 0 and 3
            },
            1 => 2,  // captains
            2 => 1,  // wings
            _ => 0,  // drones (rows 3-4)
        };
    }

    // --- Attack scheduling ---

    void LaunchPairDive()
    {
        // Collect everyone still parked in formation. Pair-dives need two.
        var pool = new List<Enemy>(Enemies.Count);
        foreach (var e in Enemies)
            if (e.State == EnemyState.InFormation) pool.Add(e);
        if (pool.Count < 2) return;

        var first = pool[_rng.Next(pool.Count)];
        pool.Remove(first);

        // Prefer a mirror partner on the opposite side of the screen for the pincer feel.
        float centerX = Width / 2f;
        Enemy second = pool[0];
        foreach (var candidate in pool)
        {
            bool firstLeft  = first.SlotPos.X  < centerX;
            bool candRight  = candidate.SlotPos.X >= centerX;
            if (firstLeft == candRight) { second = candidate; break; }  // opposite side
        }

        LaunchDive(first);
        LaunchDive(second);
    }

    void LaunchDive(Enemy e)
    {
        float centerX = Width / 2f;
        e.State          = EnemyState.Diving;
        e.PathT          = 0f;
        e.PathDuration   = DiveDuration;
        e.DiveCurlSign   = e.SlotPos.X < centerX ? -1f : +1f;
        e.DiveTarget     = Player.Position;
        e.NextFireTime   = WaveTime + EnemyFireMin + (float)_rng.NextDouble() * (EnemyFireMax - EnemyFireMin);
        AudioEngine.PlayDive();
    }

    void FireEnemyBullet(Enemy e)
    {
        // Cap concurrent enemy bullets — Galaga's pacing leans on never having more than a
        // handful of threats in the air at once.
        int enemyBullets = 0;
        foreach (var b in Bullets) if (b.Alive && !b.FromPlayer) enemyBullets++;
        if (enemyBullets >= MaxEnemyBullets) return;

        var toPlayer = Player.Position - e.Position;
        if (toPlayer.Length < 0.1f) return;
        var dir = toPlayer.Normalized();
        Bullets.Add(new Bullet
        {
            Position   = e.Position,
            Velocity   = dir * EnemyBulletSpeed,
            FromPlayer = false,
            Lifetime   = 3.5f,
        });
    }

    // --- Per-frame enemy update ---

    void UpdateEnemyPositions(float dt)
    {
        foreach (var e in Enemies)
        {
            switch (e.State)
            {
                case EnemyState.Entering:
                    e.PathT += dt / e.PathDuration;
                    if (e.PathT >= 1f)
                    {
                        if (e.IsChallengeFlythrough)
                        {
                            // Flew off the bottom of the screen — done, no rejoin.
                            e.Alive = false;
                        }
                        else
                        {
                            e.PathT = 1f;
                            e.State = EnemyState.InFormation;
                            e.Position = e.SlotPos;
                            e.Rotation = MathF.PI;  // face down toward the player
                        }
                    }
                    else if (e.IsChallengeFlythrough)
                    {
                        int sp = _challengeStagePattern;
                        var p = Paths.ChallengePath(sp, e.PatternIdx, Width, Height, e.PathT);
                        UpdatePathFacing(e, p, t => Paths.ChallengePath(sp, e.PatternIdx, Width, Height, t));
                        e.Position = p;
                    }
                    else
                    {
                        var p = Paths.EntryPath(e.FlightIdx, e.SlotPos, e.EntryFromLeft, Width, Height, e.PathT);
                        UpdatePathFacing(e, p, t => Paths.EntryPath(e.FlightIdx, e.SlotPos, e.EntryFromLeft, Width, Height, t));
                        e.Position = p;
                    }
                    break;

                case EnemyState.InFormation:
                    float wave = MathF.Sin(WaveTime * BreathingFreq + e.Phase) * BreathingAmplitude;
                    e.Position = new Vec2(e.SlotPos.X + wave, e.SlotPos.Y);
                    e.Rotation = MathF.PI;
                    break;

                case EnemyState.Diving:
                    e.PathT += dt / e.PathDuration;
                    if (e.PathT >= 1f)
                    {
                        // Dive complete — loop around the top and rejoin the formation.
                        e.State = EnemyState.Rejoining;
                        e.PathT = 0f;
                        e.PathDuration = RejoinDuration;
                    }
                    else
                    {
                        var p = Paths.DivePath(e.SlotPos, e.DiveTarget, Height, e.DiveCurlSign, e.PathT);
                        UpdatePathFacing(e, p, t => Paths.DivePath(e.SlotPos, e.DiveTarget, Height, e.DiveCurlSign, t));
                        e.Position = p;

                        // Fire downward shots while diving (only while above the player so shots
                        // actually have time to reach). Each enemy schedules its own next-fire-time.
                        if (WaveTime >= e.NextFireTime && e.Position.Y < Player.Position.Y - 40f)
                        {
                            FireEnemyBullet(e);
                            e.NextFireTime = WaveTime + EnemyFireMin + (float)_rng.NextDouble() * (EnemyFireMax - EnemyFireMin);
                        }
                    }
                    break;

                case EnemyState.Rejoining:
                    e.PathT += dt / e.PathDuration;
                    if (e.PathT >= 1f)
                    {
                        e.State = EnemyState.InFormation;
                        e.Position = e.SlotPos;
                        e.Rotation = MathF.PI;
                    }
                    else
                    {
                        var p = Paths.RejoinPath(e.SlotPos, e.DiveCurlSign, e.PathT);
                        UpdatePathFacing(e, p, t => Paths.RejoinPath(e.SlotPos, e.DiveCurlSign, t));
                        e.Position = p;
                    }
                    break;

                case EnemyState.Flyby:
                    e.PathT += dt / e.PathDuration;
                    if (e.PathT >= 1f)
                    {
                        e.Alive = false;  // exited the screen
                    }
                    else
                    {
                        e.Position = Paths.FlybyPath(e.FlybyFromLeft, Width, e.PathT);
                        e.Rotation = 0f;  // logos stay upright; rotation suppressed in renderer for kinds 4-5 anyway
                    }
                    break;
            }
        }
    }

    // Forward-difference rotation: look ahead by a small dt along the curve, point the
    // enemy's nose along the tangent. Enemies are drawn pointing "up" (-Y) by default,
    // so we add PI/2 to align that with the motion vector.
    static void UpdatePathFacing(Enemy e, Vec2 pos, Func<float, Vec2> samplePath)
    {
        float ahead = MathF.Min(1f, e.PathT + 0.05f);
        var lookahead = samplePath(ahead);
        var diff = lookahead - pos;
        if (diff.Length > 0.1f)
        {
            e.Rotation = MathF.Atan2(diff.Y, diff.X) + MathF.PI / 2f;
        }
    }

    // --- Player + projectiles + particles (unchanged from skeleton) ---

    void UpdatePlayer(float dt)
    {
        if (!Player.Alive)
        {
            // Wait out the death animation, then respawn or trigger game over.
            _deathTimer -= dt;
            if (_deathTimer <= 0f)
            {
                if (Player.Lives > 0)
                {
                    Player.Alive          = true;
                    Player.Position       = new Vec2(Width / 2f, Height - 80f);
                    Player.Velocity       = new Vec2(0f, 0f);
                    Player.InvincibleTime = RespawnInvincibility;
                }
                else
                {
                    Mode = GameMode.GameOver;
                }
            }
            return;
        }

        float vx = 0f;
        if (MovingLeft)  vx -= PlayerSpeed;
        if (MovingRight) vx += PlayerSpeed;
        Player.Velocity = new Vec2(vx, 0f);
        Player.Position += Player.Velocity * dt;

        float margin = Player.Radius + 4f;
        if (Player.Position.X < margin)         Player.Position = new Vec2(margin, Player.Position.Y);
        if (Player.Position.X > Width - margin) Player.Position = new Vec2(Width - margin, Player.Position.Y);

        if (Player.ShootCooldown  > 0) Player.ShootCooldown  -= dt;
        if (Player.InvincibleTime > 0) Player.InvincibleTime -= dt;
    }

    void UpdateBullets(float dt)
    {
        foreach (var b in Bullets)
        {
            b.Position += b.Velocity * dt;
            b.Lifetime -= dt;
            if (b.Lifetime <= 0f || b.Position.Y < -10f || b.Position.Y > Height + 10f) b.Alive = false;
        }
    }

    void UpdateParticles(float dt)
    {
        foreach (var p in Particles)
        {
            p.Lifetime -= dt;
            p.Position += p.Velocity * dt;
            p.Velocity = p.Velocity * MathF.Pow(0.5f, dt);
        }
    }
}
