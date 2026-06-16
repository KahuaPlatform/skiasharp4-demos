using System;
using System.Collections.Generic;

namespace Koa.Game;

// Koa = "warrior / brave". Gauntlet homage: a top-down tile-dungeon crawl with a
// bounded follow-camera, continuous wall-sliding motion, dozens of flow-field
// swarmers pouring from destructible generators, and a health bar that drains
// continuously ("warrior needs food badly"). Sim core modelled on Hahai +
// Pohaku, but diverging per the three pillars in DESIGN: 2-D clamped camera,
// AABB-vs-tile wall slide, and a draining health clock.
public sealed class GameWorld
{
    // --- Tunables -----------------------------------------------------------
    public const float HealthDrainPerSec = 4f;     // the "needs food" clock
    public const float FoodHeal          = 600f;    // health restored by one food
    public const int   LiveEnemyCap      = 120;     // global concurrent-enemy ceiling (frantic)
    public const float LowHealthWarnAt   = 400f;    // start the warning voice below this
    public const int   FlowRebuildEvery  = 5;       // frames between flow-field rebuilds
    public const float SeparationWeight  = 0.9f;    // how hard enemies push apart (anti-stacking)

    // --- State --------------------------------------------------------------
    public GameMode Mode = GameMode.Title;

    public TileMap  Map = null!;
    public Camera2D Camera = new();
    public Pathing  Pathing = null!;

    public Hero Hero = new();
    public readonly List<Enemy>      Enemies     = new();
    public readonly List<Generator>  Generators  = new();
    public readonly List<Projectile> Projectiles = new();
    public readonly List<Pickup>     Pickups     = new();
    public readonly List<Particle>   Particles   = new();

    public int   Score;
    public int   HighScore;
    public int   Level = 1;

    public float ViewW, ViewH;     // viewport pixels (from Resize)
    public float TitleIdleTimer;
    public float LowHealthWarnTimer;

    // Input bridge (set by MainPage each frame while Playing).
    public bool FireHeld;
    Vec2 _moveIntent;

    // Attract autopilot state (see RunAutoHero).
    Vec2  _autoHeading;     // current cardinal heading the demo bot is committed to
    float _autoTimer;       // seconds until the bot re-picks a heading
    Vec2  _autoLastPos;     // hero position at the previous RunAutoHero tick (stuck detection)

    int _frame;
    static readonly Random _rng = new();
    static readonly HighScoreStore HighScoreStore = new("Koa");

    public GameWorld()
    {
        HighScore = HighScoreStore.Load();
        LoadLevel(1);
        Hero.Health = Hero.Stats.MaxHealth;   // so the title-screen HUD bar reads full, not empty/critical
        Mode = GameMode.Title;
    }

    // Viewport size from the canvas; the camera frames the world inside it.
    public void Resize(float w, float h)
    {
        ViewW = w; ViewH = h;
        Camera.SetViewport(w, h);
        ConfigureCameraAxes();
    }

    // Clamp on both axes, snap follow (FollowRate 0). No wrap — the camera
    // hard-stops at the dungeon edges.
    void ConfigureCameraAxes()
    {
        if (Map is null) return;
        Camera.X = new CameraAxis { Mode = AxisMode.Clamp, WorldSize = Map.WorldWidth,  FollowRate = 0f };
        Camera.Y = new CameraAxis { Mode = AxisMode.Clamp, WorldSize = Map.WorldHeight, FollowRate = 0f };
    }

    // --- Input bridge -------------------------------------------------------
    public void SetMoveIntent(float mx, float my)
    {
        _moveIntent = new Vec2(mx, my);
    }

    // --- Lifecycle ----------------------------------------------------------
    void LoadLevel(int level)
    {
        Level = Math.Max(1, level);
        var loaded = Game.Level.Build(Level, _rng);
        Map = loaded.Map;
        Pathing = new Pathing(Map);
        ConfigureCameraAxes();

        Enemies.Clear();
        Projectiles.Clear();
        Particles.Clear();
        Pickups.Clear();
        Generators.Clear();

        // Hero keeps its class/health/score across levels; only reposition it.
        Hero.Pos = loaded.HeroSpawn;
        Hero.Vel = Vec2.Zero;
        Hero.Radius = TileMap.CellSize * 0.36f;
        Hero.Alive = true;
        Hero.ShootCooldown = 0f;

        foreach (var (c, r, _kind) in loaded.Generators)
        {
            // Generator level (1-3) ramps with dungeon depth; its spawn kind tracks
            // the level. (The authored map's per-glyph kind is superseded by the
            // arcade level model.)
            int glvl = PickGeneratorLevel(Level);
            Generators.Add(new Generator
            {
                Col = c, Row = r,
                Pos = Map.CellCenter(c, r),
                Radius = TileMap.CellSize * 0.42f,
                Level = glvl,
                Spawns = KindForLevel(glvl),
                SpawnTimer = 0.4f + (float)_rng.NextDouble() * 0.8f,
            });
        }

        foreach (var (c, r, kind) in loaded.Pickups)
        {
            Pickups.Add(new Pickup
            {
                Kind = kind,
                Pos = Map.CellCenter(c, r),
                Radius = TileMap.CellSize * 0.32f,
            });
        }

        // Snap the camera onto the hero so the first frame is framed.
        Camera.Snap(Hero.Pos.X, Hero.Pos.Y);
    }

    public void StartGame()
    {
        Mode = GameMode.Playing;
        Score = 0;
        Hero.SetClass(HeroClass.Warrior);   // v1 single-player default
        Hero.Health = Hero.Stats.MaxHealth;
        Hero.Keys = 0;
        Hero.Potions = 1;
        LoadLevel(1);
        LowHealthWarnTimer = 0f;
    }

    public void StartAttract()
    {
        StartGame();
        Mode = GameMode.Attract;
    }

    public void ReturnToTitle()
    {
        Mode = GameMode.Title;
        TitleIdleTimer = 0f;
        Hero.SetClass(HeroClass.Warrior);
        Hero.Health = Hero.Stats.MaxHealth;
        LoadLevel(1);
    }

    // --- Main update --------------------------------------------------------
    public void Update(float dt)
    {
        _frame++;
        switch (Mode)
        {
            case GameMode.Title:
                TitleIdleTimer += dt;
                if (TitleIdleTimer > 12f) { StartAttract(); TitleIdleTimer = 0f; }
                // Let generators idle-spawn a few wanderers so the title has life.
                UpdateGenerators(dt, idle: true);
                UpdateEnemies(dt);
                UpdateParticles(dt);
                Camera.Snap(Hero.Pos.X, Hero.Pos.Y);
                break;

            case GameMode.Playing:
            case GameMode.Attract:
                StepSim(dt);
                break;

            case GameMode.GameOver:
                UpdateParticles(dt);
                break;
        }
    }

    void StepSim(float dt)
    {
        if (Mode == GameMode.Attract) RunAutoHero();

        // 1. Health is the clock: continuous drain. 0 => death.
        DrainHealth(dt);
        if (!Hero.Alive) return;

        // 2. Hero movement vs walls (slide) + camera follow.
        MoveHero(dt);
        Camera.Follow(Hero.Pos.X, Hero.Pos.Y, dt);

        // 3. Hero firing.
        UpdateHeroFire(dt);

        // 4. Rebuild the shared flow field on a cadence (not every frame).
        if (_frame % FlowRebuildEvery == 0)
            Pathing.Rebuild(Hero.Pos);

        // 5. Entities.
        UpdateGenerators(dt, idle: false);
        UpdateEnemies(dt);
        ResolveCrowding();          // hard-separate overlapping bodies (enemies + hero)
        UpdateProjectiles(dt);
        UpdateParticles(dt);

        // 6. Interactions.
        HandleProjectileHits();
        HandleEnemyContact(dt);
        HandlePickups();
        HandleDoors();
        HandleExit();

        // 7. Sweep the dead.
        Enemies.RemoveAll(e => !e.Alive);
        Projectiles.RemoveAll(p => !p.Alive);
        Pickups.RemoveAll(p => !p.Alive);
        Generators.RemoveAll(g => !g.Alive);
    }

    // --- Health clock -------------------------------------------------------
    void DrainHealth(float dt)
    {
        Hero.Health -= HealthDrainPerSec * dt;

        // Recurring "warrior needs food badly" warning when low.
        if (Hero.Health <= LowHealthWarnAt && Hero.Health > 0f)
        {
            LowHealthWarnTimer -= dt;
            if (LowHealthWarnTimer <= 0f)
            {
                if (Mode == GameMode.Playing) AudioEngine.PlayLowHealth();
                LowHealthWarnTimer = 4f;
            }
        }
        else LowHealthWarnTimer = 0f;

        if (Hero.Health <= 0f)
        {
            Hero.Health = 0f;
            KillHero();
        }
    }

    void KillHero()
    {
        Hero.Alive = false;
        EmitExplosion(Hero.Pos, 48, 0xFFFF5533);
        if (Mode == GameMode.Playing) AudioEngine.PlayDeath();
        if (Score > HighScore) { HighScore = Score; HighScoreStore.Save(HighScore); }
        Mode = (Mode == GameMode.Attract) ? GameMode.Title : GameMode.GameOver;
    }

    // --- Hero movement (continuous, wall-sliding) ---------------------------
    void MoveHero(float dt)
    {
        var dir = _moveIntent;
        if (dir.X != 0f || dir.Y != 0f)
        {
            dir = dir.Normalized();
            Hero.AimDir = dir; // aim follows last non-zero move dir
        }
        Hero.Vel = dir * Hero.Stats.Speed;

        // Axis-separated slide against wall tiles (Gauntlet feel comes from this),
        // with a corridor-centering assist so the hero glides cleanly into and
        // down 1-tile passages instead of catching on the corridor mouth.
        MoveWithCorridorAssist(ref Hero.Pos, Hero.Radius, Hero.Vel, dt);
    }

    // Move a circle via the wall-slide resolver, plus a gentle "corridor-centering
    // assist": when motion is strongly along one axis, ease the perpendicular
    // coordinate toward the current cell's centre line so the entity lines up with
    // 1-tile corridors and can reliably turn into side passages. The assist only
    // kicks in for near-cardinal motion (one axis clearly dominant), so true
    // diagonal movement across open floor is left untouched. The eased step is
    // still fed through MoveCircle, so it can never push the entity into a wall.
    void MoveWithCorridorAssist(ref Vec2 pos, float radius, Vec2 vel, float dt)
    {
        const float Dominance  = 2.0f;  // how much one axis must lead to trigger
        const float EasePerSec = 9.0f;  // how fast we slide toward the centre line
        float cs = TileMap.CellSize;

        float dx = vel.X * dt, dy = vel.Y * dt;
        float ax = MathF.Abs(vel.X), ay = MathF.Abs(vel.Y);

        if (ax > ay * Dominance)
        {
            // Predominantly horizontal: pull Y toward the row centre.
            int row = (int)MathF.Floor(pos.Y / cs);
            float target = row * cs + cs * 0.5f;
            dy += (target - pos.Y) * MathF.Min(1f, EasePerSec * dt);
        }
        else if (ay > ax * Dominance)
        {
            // Predominantly vertical: pull X toward the column centre.
            int col = (int)MathF.Floor(pos.X / cs);
            float target = col * cs + cs * 0.5f;
            dx += (target - pos.X) * MathF.Min(1f, EasePerSec * dt);
        }

        Map.MoveCircle(ref pos, radius, dx, dy);
    }

    void UpdateHeroFire(float dt)
    {
        if (Hero.ShootCooldown > 0f) Hero.ShootCooldown -= dt;
        bool wantFire = Mode == GameMode.Attract ? _autoFire : FireHeld;
        if (wantFire && Hero.ShootCooldown <= 0f)
        {
            FireBullet();
            Hero.ShootCooldown = Hero.Stats.Cooldown;
        }
    }

    void FireBullet()
    {
        var d = Hero.AimDir;
        if (d.X == 0f && d.Y == 0f) d = new Vec2(1f, 0f);
        d = d.Normalized();
        Projectiles.Add(new Projectile
        {
            FromHero = true,
            Pos = Hero.Pos + d * (Hero.Radius + 4f),
            Vel = d * Hero.Stats.ShotSpeed,
            Radius = 4f,
            Lifetime = 1.4f,
            Damage = Hero.Stats.ShotDamage,
        });
        if (Mode == GameMode.Playing) AudioEngine.PlayShoot();
    }

    // --- Generators ---------------------------------------------------------
    void UpdateGenerators(float dt, bool idle)
    {
        foreach (var g in Generators)
        {
            if (!g.Alive) continue;
            g.SpawnTimer -= dt;
            if (g.SpawnTimer > 0f) continue;
            // Jittered cadence between emits (idle title spawns slower). The active
            // cadence is fast for an arcade-frantic flood; higher-level generators
            // emit a touch quicker.
            float fast = MathF.Max(0.25f, 0.55f - 0.06f * g.Level);
            g.SpawnTimer = (idle ? 2.5f : fast) + (float)_rng.NextDouble() * (idle ? 3f : 0.6f);

            if (Enemies.Count >= LiveEnemyCap) continue;

            // Need a free adjacent walkable cell to emit into.
            if (TryFreeAdjacent(g.Col, g.Row, out var spawnPos))
                SpawnEnemy(g.Spawns, spawnPos);
        }
    }

    bool TryFreeAdjacent(int col, int row, out Vec2 pos)
    {
        Span<(int, int)> around = stackalloc (int, int)[]
        {
            (col, row - 1), (col, row + 1), (col - 1, row), (col + 1, row),
            (col - 1, row - 1), (col + 1, row - 1), (col - 1, row + 1), (col + 1, row + 1),
        };
        // Start at a rotating offset so emits don't always favour "up".
        int start = _rng.Next(around.Length);
        for (int i = 0; i < around.Length; i++)
        {
            var (c, r) = around[(start + i) % around.Length];
            if (Map.IsWalkable(c, r))
            {
                pos = Map.CellCenter(c, r);
                return true;
            }
        }
        pos = default;
        return false;
    }

    // Generator level (1-3) weighted by dungeon depth: shallow dungeons lean to
    // weak generators, deep ones to strong (level-3) ones.
    int PickGeneratorLevel(int depth)
    {
        double r = _rng.NextDouble();
        if (depth >= 5) return r < 0.50 ? 3 : (r < 0.85 ? 2 : 1);
        if (depth >= 3) return r < 0.30 ? 3 : (r < 0.70 ? 2 : 1);
        return r < 0.12 ? 3 : (r < 0.50 ? 2 : 1);
    }

    // The monster a generator of the given level emits (and downgrades to as it
    // takes damage): 3=Demon, 2=Ghost, 1=Grunt.
    static EnemyKind KindForLevel(int lvl) =>
        lvl >= 3 ? EnemyKind.Demon : lvl == 2 ? EnemyKind.Ghost : EnemyKind.Grunt;

    void SpawnEnemy(EnemyKind kind, Vec2 pos)
    {
        (float hp, float speed, float radius) = kind switch
        {
            EnemyKind.Grunt => (24f,  95f, TileMap.CellSize * 0.34f),
            EnemyKind.Ghost => (14f, 135f, TileMap.CellSize * 0.32f),
            EnemyKind.Demon => (60f,  80f, TileMap.CellSize * 0.42f),
            _               => (24f,  95f, TileMap.CellSize * 0.34f),
        };
        Enemies.Add(new Enemy
        {
            Kind = kind,
            Pos = pos,
            Radius = radius,
            Health = hp,
            Speed = speed,
            Wobble = (float)_rng.NextDouble() * MathF.Tau,
        });
    }

    // --- Enemies (flow-field swarm) -----------------------------------------
    void UpdateEnemies(float dt)
    {
        int n = Enemies.Count;

        // Pass 1: compute each enemy's steering direction = flow toward the hero
        // (or a wander when unreachable), plus a SEPARATION push away from any
        // other enemy whose body circle overlaps, so the swarm spreads out and
        // queues in corridors instead of stacking on one tile. Computed up front
        // (off current positions) so separation is symmetric within the frame.
        for (int i = 0; i < n; i++)
        {
            var e = Enemies[i];
            if (!e.Alive) continue;
            if (e.HitCooldown > 0f) e.HitCooldown -= dt;

            Vec2 dir = Pathing.FlowDir(e.Pos);
            if (dir.X == 0f && dir.Y == 0f)
            {
                e.Wobble += dt * 2f;
                dir = new Vec2(MathF.Cos(e.Wobble), MathF.Sin(e.Wobble));
            }
            dir = dir.Normalized();

            // Ghosts jitter a little for a less mechanical swarm; demons are steady.
            if (e.Kind == EnemyKind.Ghost)
            {
                e.Wobble += dt * 6f;
                dir = (dir + new Vec2(MathF.Cos(e.Wobble), MathF.Sin(e.Wobble)) * 0.25f).Normalized();
            }

            // Separation: sum a push away from every overlapping neighbour, scaled
            // by how deep the overlap is. (O(n^2) over the live cap, which is cheap.)
            Vec2 sep = Vec2.Zero;
            for (int j = 0; j < n; j++)
            {
                if (j == i) continue;
                var o = Enemies[j];
                if (!o.Alive) continue;
                float dx = e.Pos.X - o.Pos.X, dy = e.Pos.Y - o.Pos.Y;
                float min = e.Radius + o.Radius;
                float d2 = dx * dx + dy * dy;
                if (d2 < min * min && d2 > 0.0001f)
                {
                    float d = MathF.Sqrt(d2);
                    float push = (min - d) / min;          // 0..1, stronger when more overlapped
                    sep.X += dx / d * push;
                    sep.Y += dy / d * push;
                }
            }

            dir += sep * SeparationWeight;
            if (dir.X != 0f || dir.Y != 0f) dir = dir.Normalized();
            e.StepDir = dir;
        }

        // Pass 2: move along the precomputed dir. Same wall-slide + corridor-centering
        // path the hero uses, so enemies stay out of walls and thread 1-tile corridors.
        for (int i = 0; i < n; i++)
        {
            var e = Enemies[i];
            if (!e.Alive) continue;
            MoveWithCorridorAssist(ref e.Pos, e.Radius, e.StepDir * e.Speed, dt);
        }
    }

    // Hard body separation: the steering separation in UpdateEnemies only nudges,
    // so bodies can still end a frame overlapping. This resolves actual overlaps by
    // pushing the pair apart (half each) and pushes any enemy out of the hero
    // (hero holds its ground). Every push goes through MoveCircle, so a body can
    // never be shoved into a wall. A couple of relaxation passes settle dense piles.
    void ResolveCrowding()
    {
        int n = Enemies.Count;
        const int Iterations = 2;
        for (int it = 0; it < Iterations; it++)
        {
            // enemy vs enemy
            for (int i = 0; i < n; i++)
            {
                var a = Enemies[i];
                if (!a.Alive) continue;
                for (int j = i + 1; j < n; j++)
                {
                    var b = Enemies[j];
                    if (!b.Alive) continue;
                    float dx = b.Pos.X - a.Pos.X, dy = b.Pos.Y - a.Pos.Y;
                    float min = a.Radius + b.Radius;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= min * min) continue;
                    float d = MathF.Sqrt(d2);
                    float nx, ny;
                    if (d < 1e-3f) { nx = 1f; ny = 0f; d = 0f; }   // coincident: split on an arbitrary axis
                    else           { nx = dx / d; ny = dy / d; }
                    float half = (min - d) * 0.5f;
                    Map.MoveCircle(ref a.Pos, a.Radius, -nx * half, -ny * half);
                    Map.MoveCircle(ref b.Pos, b.Radius,  nx * half,  ny * half);
                }
            }

            // enemy vs hero — push the enemy fully out; the hero stays put.
            for (int i = 0; i < n; i++)
            {
                var a = Enemies[i];
                if (!a.Alive) continue;
                float dx = a.Pos.X - Hero.Pos.X, dy = a.Pos.Y - Hero.Pos.Y;
                float min = a.Radius + Hero.Radius;
                float d2 = dx * dx + dy * dy;
                if (d2 >= min * min) continue;
                float d = MathF.Sqrt(d2);
                float nx, ny;
                if (d < 1e-3f) { nx = 1f; ny = 0f; d = 0f; }
                else           { nx = dx / d; ny = dy / d; }
                float push = min - d;
                Map.MoveCircle(ref a.Pos, a.Radius, nx * push, ny * push);
            }
        }
    }

    // --- Projectiles --------------------------------------------------------
    void UpdateProjectiles(float dt)
    {
        foreach (var p in Projectiles)
        {
            if (!p.Alive) continue;
            p.Lifetime -= dt;
            if (p.Lifetime <= 0f) { p.Alive = false; continue; }
            // A wall hit expires the bullet. Use the projectile solidity test so the
            // shot passes onto generator tiles (generators are shootable — the hit is
            // applied in HandleProjectileHits) and only real walls/doors stop it.
            bool hit = Map.MoveProjectile(ref p.Pos, p.Radius, p.Vel.X * dt, p.Vel.Y * dt);
            if (hit) p.Alive = false;
        }
    }

    // --- Interactions -------------------------------------------------------
    void HandleProjectileHits()
    {
        foreach (var p in Projectiles)
        {
            if (!p.Alive || !p.FromHero) continue;

            // vs enemies
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                if (CircleHit(p.Pos, p.Radius, e.Pos, e.Radius))
                {
                    e.Health -= p.Damage;
                    p.Alive = false;
                    EmitSpark(p.Pos, EnemyColor(e.Kind));
                    if (e.Health <= 0f)
                    {
                        e.Alive = false;
                        Score += EnemyScore(e.Kind);
                        EmitExplosion(e.Pos, 10, EnemyColor(e.Kind));
                        if (Mode == GameMode.Playing) AudioEngine.PlayEnemyDie();
                    }
                    else if (Mode == GameMode.Playing) AudioEngine.PlayHit();
                    break;
                }
            }
            if (!p.Alive) continue;

            // vs generators
            foreach (var g in Generators)
            {
                if (!g.Alive) continue;
                if (CircleHit(p.Pos, p.Radius, g.Pos, g.Radius))
                {
                    // Arcade model: each hit knocks the generator down one level
                    // (HP == level); its spawn weakens with it; level 0 destroys it.
                    g.Level -= 1;
                    p.Alive = false;
                    EmitSpark(p.Pos, 0xFFFFAA33);
                    if (g.Level <= 0)
                    {
                        g.Alive = false;
                        Map.ClearGenerator(g.Col, g.Row);
                        Score += 200;                       // bounty for finishing it
                        EmitExplosion(g.Pos, 24, 0xFFFFAA33);
                        if (Mode == GameMode.Playing) AudioEngine.PlayGeneratorDie();
                    }
                    else
                    {
                        g.Spawns = KindForLevel(g.Level);   // now emits the weaker monster
                        Score += 50;                        // partial credit per downgrade
                        if (Mode == GameMode.Playing) AudioEngine.PlayHit();
                    }
                    break;
                }
            }
        }
    }

    void HandleEnemyContact(float dt)
    {
        foreach (var e in Enemies)
        {
            if (!e.Alive || e.HitCooldown > 0f) continue;
            if (CircleHit(e.Pos, e.Radius, Hero.Pos, Hero.Radius))
            {
                float dmg = EnemyContactDamage(e.Kind) * Hero.Stats.Armor;
                Hero.Health -= dmg;
                e.HitCooldown = 0.6f;
                EmitSpark(Hero.Pos, 0xFFFF4444);
                if (Mode == GameMode.Playing) AudioEngine.PlayHeroHurt();
                if (Hero.Health <= 0f) { Hero.Health = 0f; KillHero(); return; }
            }
        }
    }

    void HandlePickups()
    {
        foreach (var p in Pickups)
        {
            if (!p.Alive) continue;
            if (!CircleHit(p.Pos, p.Radius, Hero.Pos, Hero.Radius)) continue;
            p.Alive = false;
            switch (p.Kind)
            {
                case PickupKind.Key:      Hero.Keys++;    if (Mode == GameMode.Playing) AudioEngine.PlayPickup(); break;
                case PickupKind.Potion:   Hero.Potions++; if (Mode == GameMode.Playing) AudioEngine.PlayPickup(); break;
                case PickupKind.Treasure: Score += 100;   if (Mode == GameMode.Playing) AudioEngine.PlayPickup(); break;
                case PickupKind.Food:
                    Hero.Health = MathF.Min(Hero.Stats.MaxHealth, Hero.Health + FoodHeal);
                    if (Mode == GameMode.Playing) AudioEngine.PlayPotion();
                    break;
            }
        }
    }

    // Open an adjacent door when the hero touches one and holds a key.
    void HandleDoors()
    {
        if (Hero.Keys <= 0) return;
        var (hc, hr) = Map.WorldToCell(Hero.Pos);
        Span<(int, int)> around = stackalloc (int, int)[] { (hc, hr - 1), (hc, hr + 1), (hc - 1, hr), (hc + 1, hr) };
        foreach (var (c, r) in around)
        {
            if (Map.InBounds(c, r) && Map[c, r] == Tile.Door)
            {
                // Only open if the hero is actually adjacent (close to the face).
                var center = Map.CellCenter(c, r);
                if ((center - Hero.Pos).Length <= TileMap.CellSize * 0.9f)
                {
                    Map.OpenDoor(c, r);
                    Hero.Keys--;
                    if (Mode == GameMode.Playing) AudioEngine.PlayDoor();
                    return;
                }
            }
        }
    }

    void HandleExit()
    {
        var (hc, hr) = Map.WorldToCell(Hero.Pos);
        if (Map.InBounds(hc, hr) && Map[hc, hr] == Tile.Exit)
        {
            Score += 500;
            if (Mode == GameMode.Playing) AudioEngine.PlayLevelClear();
            LoadLevel(Level + 1);   // keep health + score
        }
    }

    // Smite potion: damage every enemy in view + spark particles. Consumes one.
    public void UsePotion()
    {
        if (Hero.Potions <= 0) return;
        Hero.Potions--;
        var view = Camera.VisibleWorldRect(64f);
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            if (e.Pos.X < view.Left || e.Pos.X > view.Right || e.Pos.Y < view.Top || e.Pos.Y > view.Bottom) continue;
            e.Health -= 80f;
            EmitSpark(e.Pos, 0xFFCC66FF);
            if (e.Health <= 0f)
            {
                e.Alive = false;
                Score += EnemyScore(e.Kind);
                EmitExplosion(e.Pos, 8, 0xFFCC66FF);
            }
        }
        if (Mode == GameMode.Playing) AudioEngine.PlayPotion();
    }

    // --- Particles ----------------------------------------------------------
    void UpdateParticles(float dt)
    {
        foreach (var p in Particles)
        {
            p.Pos += p.Vel * dt;
            p.Vel *= MathF.Pow(0.92f, dt * 60f);
            p.Life -= dt;
            if (p.Life <= 0f) p.Alive = false;
        }
        Particles.RemoveAll(p => !p.Alive);
    }

    void EmitExplosion(Vec2 origin, int count, uint color)
    {
        for (int i = 0; i < count; i++)
        {
            double ang = _rng.NextDouble() * Math.PI * 2.0;
            float spd = 60f + (float)_rng.NextDouble() * 240f;
            Particles.Add(new Particle
            {
                Pos = origin,
                Vel = new Vec2((float)Math.Cos(ang) * spd, (float)Math.Sin(ang) * spd),
                Life = 0.7f, MaxLife = 0.7f,
                Color = color,
                Size = 1.6f + (float)_rng.NextDouble() * 1.8f,
            });
        }
    }

    void EmitSpark(Vec2 origin, uint color)
    {
        for (int i = 0; i < 4; i++)
        {
            double ang = _rng.NextDouble() * Math.PI * 2.0;
            float spd = 40f + (float)_rng.NextDouble() * 120f;
            Particles.Add(new Particle
            {
                Pos = origin,
                Vel = new Vec2((float)Math.Cos(ang) * spd, (float)Math.Sin(ang) * spd),
                Life = 0.3f, MaxLife = 0.3f,
                Color = color, Size = 1.4f,
            });
        }
    }

    // --- Attract autopilot --------------------------------------------------
    bool _autoFire;

    // A demo bot that actually navigates the maze so the hero looks alive on the
    // title loop. It commits to a CARDINAL heading down a corridor and only
    // re-picks when (a) the commit timer expires, (b) it gets stuck against a
    // wall, or (c) its current heading is now blocked. When re-picking it scores
    // the open cardinal directions, biasing toward the nearest pickup and toward
    // keeping its current heading (so it doesn't jitter at junctions). It aims and
    // pulse-fires at the nearest enemy independently. (The earlier version steered
    // in a straight line toward the nearest pickup ignoring walls, so on the
    // wall-dense arcade maps it drove into a wall and stalled — looking frozen.)
    void RunAutoHero()
    {
        float moved = (Hero.Pos - _autoLastPos).Length;
        bool stuck = moved < 0.5f;                 // barely moved since last tick
        _autoTimer -= 1f / 60f;

        bool headingBlocked = _autoHeading.Length < 0.01f || BlockedAhead(_autoHeading);
        if (_autoTimer <= 0f || stuck || headingBlocked)
        {
            _autoHeading = PickAutoHeading();
            _autoTimer = 0.6f + (float)_rng.NextDouble() * 1.0f;
        }
        _moveIntent = _autoHeading;
        _autoLastPos = Hero.Pos;

        // Aim at the nearest enemy and pulse fire (independent of movement).
        Vec2 aim = Hero.AimDir;
        float bestE = float.MaxValue;
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;
            float d = (e.Pos - Hero.Pos).Length;
            if (d < bestE) { bestE = d; aim = (e.Pos - Hero.Pos).Normalized(); }
        }
        if (bestE < 9999f) Hero.AimDir = aim;
        _autoFire = bestE < 360f;
    }

    static readonly Vec2[] _cardinals =
    {
        new Vec2( 1f, 0f), new Vec2(-1f, 0f), new Vec2(0f,  1f), new Vec2(0f, -1f),
    };

    // True if a step in `dir` would run the hero into a wall (probe just over one
    // cell ahead of the hero's leading edge).
    bool BlockedAhead(Vec2 dir)
    {
        float probe = Hero.Radius + TileMap.CellSize * 0.6f;
        return Map.IsBlockedAt(Hero.Pos.X + dir.X * probe, Hero.Pos.Y + dir.Y * probe);
    }

    // Choose an open cardinal heading, biased toward the nearest pickup and toward
    // the current heading; fall back to any open direction, else reverse.
    Vec2 PickAutoHeading()
    {
        Vec2 toTarget = Vec2.Zero;
        float best = float.MaxValue;
        foreach (var p in Pickups)
        {
            if (!p.Alive) continue;
            float d = (p.Pos - Hero.Pos).Length;
            if (d < best) { best = d; toTarget = (p.Pos - Hero.Pos).Normalized(); }
        }

        Vec2 bestDir = Vec2.Zero;
        float bestScore = float.NegativeInfinity;
        foreach (var dir in _cardinals)
        {
            if (BlockedAhead(dir)) continue;
            float score = (float)_rng.NextDouble() * 0.3f;                 // jitter to break ties
            if (toTarget.Length > 0f) score += dir.X * toTarget.X + dir.Y * toTarget.Y; // head toward pickup
            if (dir.X == _autoHeading.X && dir.Y == _autoHeading.Y) score += 0.5f;       // momentum
            if (score > bestScore) { bestScore = score; bestDir = dir; }
        }
        if (bestDir.Length > 0f) return bestDir;
        return new Vec2(-_autoHeading.X, -_autoHeading.Y); // boxed in: turn around
    }

    // --- Helpers ------------------------------------------------------------
    static bool CircleHit(Vec2 a, float ra, Vec2 b, float rb)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        float rr = ra + rb;
        return dx * dx + dy * dy <= rr * rr;
    }

    public static int EnemyScore(EnemyKind k) => k switch
    {
        EnemyKind.Grunt => 10,
        EnemyKind.Ghost => 20,
        EnemyKind.Demon => 40,
        _               => 10,
    };

    static float EnemyContactDamage(EnemyKind k) => k switch
    {
        EnemyKind.Grunt => 120f,
        EnemyKind.Ghost => 80f,
        EnemyKind.Demon => 240f,
        _               => 120f,
    };

    public static uint EnemyColor(EnemyKind k) => k switch
    {
        EnemyKind.Grunt => 0xFF55DD55, // sickly green
        EnemyKind.Ghost => 0xFFAACCFF, // pale spectral blue
        EnemyKind.Demon => 0xFFFF4422, // ember red
        _               => 0xFF55DD55,
    };
}
