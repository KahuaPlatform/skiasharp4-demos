namespace UnoGalaga.Game;

public enum GameMode { Title, Playing, GameOver }
public enum WaveState { Spawning, Settling, Attacking, Cleared }

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
    public bool MovingLeft;
    public bool MovingRight;

    public float WaveTime;   // total elapsed seconds in current wave (for breathing phase)

    // --- Player tuning ---
    const float PlayerSpeed = 380f;
    const float ShootInterval = 0.18f;

    // --- Formation layout ---
    const int   FormationCols    = 6;
    const int   FormationRows    = 4;
    const float FormationColSpacing = 80f;
    const float FormationRowSpacing = 70f;
    const float FormationCenterY    = 140f;

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
    float _spawnTimer;
    float _settleTimer;
    float _attackTimer;
    float _deathTimer;
    readonly Random _rng = new(42);

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
        Mode      = GameMode.Playing;
        WaveState = WaveState.Spawning;
        Score = 0;
        Enemies.Clear();
        Bullets.Clear();
        Particles.Clear();
        Player = new Player
        {
            Position = new Vec2(Width / 2f, Height - 80f),
            InvincibleTime = 1.5f,
        };
        _spawnedCount = 0;
        _spawnTimer   = 0f;
        _settleTimer  = 0f;
        _attackTimer  = 0f;
        WaveTime      = 0f;
    }

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
                int score = ScoreForKind(enemy.Kind);
                if (enemy.State == EnemyState.Diving) score *= 2;  // Galaga-style dive bonus
                Score += score;
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
                Player.Alive  = false;
                Player.Lives  = Math.Max(0, Player.Lives - 1);
                _deathTimer   = DeathDelay;
                SpawnExplosion(Player.Position, PlayerExplosionColor, count: 22);
                SpawnExplosion(enemy.Position,  ExplosionColors[Math.Clamp(enemy.Kind, 0, ExplosionColors.Length - 1)]);
                AudioEngine.PlayExplosion();
                break;
            }
        }
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
        int totalSlots = FormationRows * FormationCols;
        if (_spawnTimer <= 0f && _spawnedCount < totalSlots)
        {
            SpawnNextEnemy();
            _spawnTimer = SpawnInterval;
        }

        if (_spawnedCount >= totalSlots)
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

        if (Enemies.Count == 0)
        {
            WaveState = WaveState.Cleared;
        }
    }

    // --- Spawning ---

    void SpawnNextEnemy()
    {
        int idx = _spawnedCount++;
        int row = idx / FormationCols;
        int col = idx % FormationCols;
        bool fromLeft = (idx % 2 == 0);  // alternating side streams

        var slot = GetSlotPosition(row, col);
        var enemy = new Enemy
        {
            Kind     = GetSlotKind(row, col),
            SlotPos  = slot,
            Position = Paths.EntryPath(slot, fromLeft, Width, Height, 0f),
            State    = EnemyState.Entering,
            PathT    = 0f,
            PathDuration   = EntryDuration,
            EntryFromLeft  = fromLeft,
            Phase    = idx * 0.21f,
            Radius   = 16f,
        };
        Enemies.Add(enemy);
    }

    Vec2 GetSlotPosition(int row, int col)
    {
        float x = Width / 2f + (col - (FormationCols - 1) / 2f) * FormationColSpacing;
        float y = FormationCenterY + row * FormationRowSpacing;
        return new Vec2(x, y);
    }

    int GetSlotKind(int row, int col)
    {
        // Top row reserves the middle two slots for motherships, with snowflake bonuses
        // at the very center if the column count is large enough; everything else fills
        // the standard drone/wing/captain/boss progression.
        return row switch
        {
            0 => (col == FormationCols / 2 - 1 || col == FormationCols / 2) ? 4 : 3,  // mothership pair + bosses
            1 => 2,  // captains
            2 => 1,  // wings
            _ => 0,  // drones
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
        AudioEngine.PlayDive();
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
                        e.PathT = 1f;
                        e.State = EnemyState.InFormation;
                        e.Position = e.SlotPos;
                        e.Rotation = MathF.PI;  // face down toward the player
                    }
                    else
                    {
                        var p = Paths.EntryPath(e.SlotPos, e.EntryFromLeft, Width, Height, e.PathT);
                        UpdatePathFacing(e, p, t => Paths.EntryPath(e.SlotPos, e.EntryFromLeft, Width, Height, t));
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
