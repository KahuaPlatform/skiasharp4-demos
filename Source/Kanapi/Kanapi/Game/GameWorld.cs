using System;
using System.Collections.Generic;

namespace Kanapi.Game;

/// <summary>
/// The per-frame brain for Kanapi, a Centipede clone (720×720 over a 30×30
/// mushroom grid). Runs the centipede grid AI (advance / bounce-and-drop / split
/// on hit), player movement + auto-fire, spider movement, collisions, scoring,
/// per-level field regeneration, the mode state machine, and the attract autopilot.
/// </summary>
public sealed class GameWorld
{
    public const float WorldW = 720f;
    public const float WorldH = 720f;
    public float Width  => WorldW;
    public float Height => WorldH;

    public const float PlayerZoneTop = MushroomGrid.PlayerZoneTopRow * MushroomGrid.CellSize;

    // --- Player ---
    public const float PlayerSpeed     = 280f;
    public const float PlayerRadius    = 9f;
    public const float BulletSpeed     = 720f;
    public const float BulletLife      = 1.2f;
    public const float BulletInterval  = 0.18f;

    // --- Centipede ---
    public const int   StartingSegments    = 11;
    public const float CentipedeBaseSpeed  = 130f;   // px/sec along grid

    // --- Spider ---
    public const float SpiderBaseSpeed     = 150f;
    public const float SpiderSpawnInterval = 8f;

    // --- State ---
    public GameMode Mode = GameMode.Title;
    public int Level = 1;
    public int Score;
    public int HighScore;
    public int LivesLeft = 3;
    public string PlacardText = "";
    public float PlacardTimer;
    public float TitleIdleTimer;

    public Player Player = new();
    public MushroomGrid Grid = new();
    public List<CentipedeChain> Chains = new();
    public List<Bullet>     Bullets   = new();
    public List<Spider>     Spiders   = new();
    public List<Particle>   Particles = new();
    public List<ScorePopup> Popups    = new();

    // Driven by MainPage.
    public bool MoveUp, MoveDown, MoveLeft, MoveRight, Firing;

    static readonly Random _rng = new();
    static readonly HighScoreStore HighScoreStore = new("Kanapi");

    public GameWorld()
    {
        HighScore = HighScoreStore.Load();
        ResetForTitle();
    }

    /// <summary>No-op: world coords are fixed and the renderer letterboxes.</summary>
    public void Resize(float w, float h) { /* fixed coords */ }

    void ResetForTitle()
    {
        Grid.Reset(1, _rng);
        Chains.Clear();
        Bullets.Clear();
        Spiders.Clear();
        Particles.Clear();
        Popups.Clear();
        Player = new Player
        {
            Position = new Vec2(WorldW * 0.5f, WorldH - MushroomGrid.CellSize * 2f),
            Alive = true,
        };
    }

    /// <summary>Starts a fresh player-controlled game at level 1.</summary>
    public void StartGame()
    {
        Mode = GameMode.Playing;
        Level = 1;
        Score = 0;
        LivesLeft = 3;
        BuildLevel(Level);
        ShowPlacard($"LEVEL {Level}", 1.4f);
    }

    /// <summary>Starts the self-playing attract demo (autopilot, near-infinite lives).</summary>
    public void StartAttract()
    {
        StartGame();
        Mode = GameMode.Attract;
        LivesLeft = 9999;
    }

    /// <summary>Returns to the title screen and rebuilds the idle field.</summary>
    public void ReturnToTitle()
    {
        Mode = GameMode.Title;
        TitleIdleTimer = 0f;
        ResetForTitle();
    }

    void BuildLevel(int level)
    {
        Grid.Reset(level, _rng);
        SpawnFreshCentipede(level);
        Bullets.Clear();
        Spiders.Clear();
        Particles.Clear();
        Popups.Clear();
        _spiderTimer = SpiderSpawnInterval * 0.5f;
        ResetPlayerToSpawn();
    }

    void ResetPlayerToSpawn()
    {
        Player = new Player
        {
            Position = new Vec2(WorldW * 0.5f, WorldH - MushroomGrid.CellSize * 2f),
            Alive    = true,
            Invuln   = 1.5f,
        };
    }

    void SpawnFreshCentipede(int level)
    {
        Chains.Clear();
        var chain = new CentipedeChain { SpeedFactor = 1f + (level - 1) * 0.08f };
        int len = StartingSegments;
        // Enter from the top-right, walking left. Place segments off-screen above
        // the playfield so they march in.
        for (int i = 0; i < len; i++)
        {
            var seg = new CentipedeSegment
            {
                Position = new Vec2(MushroomGrid.CellCenter(MushroomGrid.Cols - 1, 0).X + i * MushroomGrid.CellSize,
                                    MushroomGrid.CellCenter(MushroomGrid.Cols - 1, 0).Y),
                Target   = MushroomGrid.CellCenter(MushroomGrid.Cols - 1, 0),
                IsHead   = (i == 0),
                HorizDir = -1,
                VertDir  = +1,
            };
            chain.Segments.Add(seg);
        }
        Chains.Add(chain);
    }

    /// <summary>Shows a centered placard (e.g. "LEVEL 2") for <paramref name="seconds"/>.</summary>
    public void ShowPlacard(string text, float seconds)
    {
        PlacardText = text;
        PlacardTimer = seconds;
    }

    /// <summary>Advances the game one frame; dispatches on <see cref="Mode"/>.</summary>
    public void Update(float dt)
    {
        if (PlacardTimer > 0) PlacardTimer -= dt;
        switch (Mode)
        {
            case GameMode.Title:
                TitleIdleTimer += dt;
                if (TitleIdleTimer > 12f) { StartAttract(); TitleIdleTimer = 0f; }
                break;
            case GameMode.Playing:
            case GameMode.Attract:
                UpdatePlay(dt);
                break;
            case GameMode.GameOver:
                UpdateParticles(dt);
                UpdatePopups(dt);
                break;
        }
    }

    void UpdatePlay(float dt)
    {
        if (Mode == GameMode.Attract) UpdateAttractAI(dt);
        UpdatePlayer(dt);
        UpdateBullets(dt);
        UpdateCentipedes(dt);
        UpdateSpiders(dt);
        UpdateParticles(dt);
        UpdatePopups(dt);

        // Level cleared?
        bool anyChainAlive = false;
        foreach (var c in Chains) if (c.Segments.Count > 0) { anyChainAlive = true; break; }
        if (!anyChainAlive)
        {
            // Bonus points for cleared mushrooms, then advance.
            _levelClearTimer -= dt;
            if (_levelClearTimer <= 0)
            {
                Level++;
                BuildLevel(Level);
                ShowPlacard($"LEVEL {Level}", 1.4f);
                _levelClearTimer = 1.0f;
            }
        }
        else
        {
            _levelClearTimer = 1.0f;
        }
    }
    float _levelClearTimer = 1.0f;

    void UpdatePlayer(float dt)
    {
        if (!Player.Alive)
        {
            _respawnTimer -= dt;
            if (_respawnTimer <= 0)
            {
                ResetPlayerToSpawn();
            }
            return;
        }
        if (Player.Invuln > 0) Player.Invuln -= dt;
        if (Player.ShootCooldown > 0) Player.ShootCooldown -= dt;

        float vx = 0, vy = 0;
        if (MoveLeft)  vx -= 1;
        if (MoveRight) vx += 1;
        if (MoveUp)    vy -= 1;
        if (MoveDown)  vy += 1;
        if (vx != 0 || vy != 0)
        {
            float len = MathF.Sqrt(vx * vx + vy * vy);
            vx /= len; vy /= len;
            Player.Position.X += vx * PlayerSpeed * dt;
            Player.Position.Y += vy * PlayerSpeed * dt;
        }

        // Constrain to player zone + horizontal world bounds.
        Player.Position.X = MathF.Max(PlayerRadius, MathF.Min(WorldW - PlayerRadius, Player.Position.X));
        Player.Position.Y = MathF.Max(PlayerZoneTop + PlayerRadius,
                                       MathF.Min(WorldH - PlayerRadius, Player.Position.Y));

        // Block by mushrooms in the player zone.
        var (col, row) = MushroomGrid.WorldToCell(Player.Position);
        if (Grid.Get(col, row) != null)
        {
            // Push back along last move direction.
            Player.Position.X -= vx * PlayerSpeed * dt;
            Player.Position.Y -= vy * PlayerSpeed * dt;
        }

        if (Firing) TryFireBullet();
    }

    float _respawnTimer;

    void TryFireBullet()
    {
        if (Player.ShootCooldown > 0) return;
        Bullets.Add(new Bullet
        {
            Position = new Vec2(Player.Position.X, Player.Position.Y - 12f),
            Life     = BulletLife,
        });
        Player.ShootCooldown = BulletInterval;
        AudioEngine.PlayShoot();
    }

    void UpdateBullets(float dt)
    {
        for (int i = Bullets.Count - 1; i >= 0; i--)
        {
            var b = Bullets[i];
            b.Position.Y -= BulletSpeed * dt;
            b.Life -= dt;

            if (b.Life <= 0 || b.Position.Y < -10) { Bullets.RemoveAt(i); continue; }

            // Hit a mushroom?
            var (col, row) = MushroomGrid.WorldToCell(b.Position);
            var mush = Grid.Get(col, row);
            if (mush != null)
            {
                mush.Health--;
                AudioEngine.PlayMushroomHit();
                EmitExplosion(b.Position, 4, mush.Poisoned ? 0xFF_FF8855u : 0xFF_88FF88u);
                if (mush.Health <= 0)
                {
                    Grid.Remove(col, row);
                    AddScore(1, b.Position);
                }
                Bullets.RemoveAt(i);
                continue;
            }

            // Hit a centipede segment?
            bool segHit = false;
            for (int ci = 0; ci < Chains.Count && !segHit; ci++)
            {
                var chain = Chains[ci];
                for (int si = 0; si < chain.Segments.Count; si++)
                {
                    var s = chain.Segments[si];
                    float dx = s.Position.X - b.Position.X;
                    float dy = s.Position.Y - b.Position.Y;
                    float rr = 10f;
                    if (dx * dx + dy * dy <= rr * rr)
                    {
                        HitSegment(ci, si);
                        Bullets.RemoveAt(i);
                        segHit = true;
                        break;
                    }
                }
            }
            if (segHit) continue;

            // Hit a spider?
            bool spiderHit = false;
            for (int si = Spiders.Count - 1; si >= 0; si--)
            {
                var s = Spiders[si];
                if (!s.Alive) continue;
                float dx = s.Position.X - b.Position.X;
                float dy = s.Position.Y - b.Position.Y;
                if (dx * dx + dy * dy <= 14f * 14f)
                {
                    int v = SpiderScoreForDistance(s.Position);
                    AddScore(v, s.Position);
                    EmitExplosion(s.Position, 22, 0xFF_FF66FF);
                    AudioEngine.PlaySpiderKill();
                    Spiders.RemoveAt(si);
                    Bullets.RemoveAt(i);
                    spiderHit = true;
                    break;
                }
            }
            if (spiderHit) continue;
        }
    }

    int SpiderScoreForDistance(Vec2 spiderPos)
    {
        if (!Player.Alive) return 600;
        float dx = spiderPos.X - Player.Position.X;
        float dy = spiderPos.Y - Player.Position.Y;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 60f)  return 900;
        if (d < 140f) return 600;
        return 300;
    }

    void HitSegment(int chainIdx, int segIdx)
    {
        var chain = Chains[chainIdx];
        var s = chain.Segments[segIdx];
        var (col, row) = MushroomGrid.WorldToCell(s.Position);
        // Score: head=100, body=10
        AddScore(s.IsHead ? 100 : 10, s.Position);
        EmitExplosion(s.Position, 14, 0xFF_FF6644);
        AudioEngine.PlaySegmentKill();

        // Drop a mushroom where the segment died.
        if (Grid.Get(col, row) == null)
            Grid.Set(new Mushroom { Col = col, Row = row });

        if (segIdx == 0)
        {
            // Head killed — segment behind becomes the new head.
            chain.Segments.RemoveAt(0);
            if (chain.Segments.Count > 0)
            {
                var newHead = chain.Segments[0];
                newHead.IsHead = true;
                // Keep direction so it doesn't immediately reverse.
                newHead.HorizDir = s.HorizDir;
                newHead.VertDir  = s.VertDir;
            }
        }
        else
        {
            // Mid-body — split into two chains.
            var tail = new CentipedeChain { SpeedFactor = chain.SpeedFactor };
            for (int k = segIdx + 1; k < chain.Segments.Count; k++)
                tail.Segments.Add(chain.Segments[k]);
            chain.Segments.RemoveRange(segIdx, chain.Segments.Count - segIdx);
            if (tail.Segments.Count > 0)
            {
                var newHead = tail.Segments[0];
                newHead.IsHead = true;
                // Reverse direction so the new chain starts moving opposite to the killed segment.
                newHead.HorizDir = -s.HorizDir;
                newHead.VertDir  = s.VertDir;
                newHead.Target   = newHead.Position;
                Chains.Add(tail);
            }
        }

        if (chain.Segments.Count == 0)
        {
            Chains.RemoveAt(chainIdx);
        }
    }

    void AddScore(int v, Vec2 popupPos)
    {
        Score += v;
        if (Score > HighScore) HighScore = Score;
        Popups.Add(new ScorePopup
        {
            Pos     = popupPos,
            Value   = v,
            Life    = 0.7f,
            MaxLife = 0.7f,
            Color   = v >= 100 ? 0xFF_FFEE66u : 0xFF_88FF88u,
        });
    }

    void UpdateCentipedes(float dt)
    {
        for (int ci = 0; ci < Chains.Count; ci++)
        {
            var chain = Chains[ci];
            if (chain.Segments.Count == 0) continue;
            float speed = CentipedeBaseSpeed * chain.SpeedFactor;
            UpdateChain(chain, dt, speed);

            // Did any segment touch the player?
            if (Player.Alive && Player.Invuln <= 0)
            {
                foreach (var s in chain.Segments)
                {
                    float dx = s.Position.X - Player.Position.X;
                    float dy = s.Position.Y - Player.Position.Y;
                    float rr = PlayerRadius + 8f;
                    if (dx * dx + dy * dy <= rr * rr)
                    {
                        OnPlayerHit();
                        return;
                    }
                }
            }
        }
    }

    void UpdateChain(CentipedeChain chain, float dt, float speed)
    {
        // Head: move toward Target; when arrived, compute next target via grid logic.
        var head = chain.Segments[0];
        MoveTowards(ref head.Position, head.Target, speed * dt);
        if ((head.Position - head.Target).Length < 0.5f)
        {
            head.Position = head.Target;
            ComputeNextHeadTarget(head);
        }

        // Body segments follow the segment ahead. Each maintains ~1 cell spacing.
        for (int i = 1; i < chain.Segments.Count; i++)
        {
            var lead = chain.Segments[i - 1];
            var s = chain.Segments[i];
            var diff = lead.Position - s.Position;
            float d = diff.Length;
            if (d > MushroomGrid.CellSize * 0.85f)
            {
                var dir = diff * (1f / MathF.Max(0.001f, d));
                s.Position += dir * speed * dt;
            }
        }
    }

    static void MoveTowards(ref Vec2 pos, Vec2 target, float step)
    {
        var diff = target - pos;
        float d = diff.Length;
        if (d <= step) { pos = target; return; }
        pos += diff * (step / d);
    }

    void ComputeNextHeadTarget(CentipedeSegment head)
    {
        var (col, row) = MushroomGrid.WorldToCell(head.Position);
        // Try to continue in current horizontal direction.
        int nextCol = col + head.HorizDir;
        bool blocked = false;
        if (nextCol < 0 || nextCol >= MushroomGrid.Cols) blocked = true;
        else if (Grid.Get(nextCol, row) != null)
        {
            // Walking into a mushroom — also blocked.
            blocked = true;
            // Poisoned mushroom contact flips this chain to dive mode.
            var m = Grid.Get(nextCol, row);
            if (m!.Poisoned)
            {
                head.Poisoned = true;
                head.VertDir = +1;
            }
        }

        if (!blocked)
        {
            head.Target = MushroomGrid.CellCenter(nextCol, row);
            return;
        }

        // Drop one row in current vertical direction and reverse horizontal.
        int nextRow = row + head.VertDir;
        if (nextRow >= MushroomGrid.Rows)
        {
            // Hit floor — bounce back up.
            head.VertDir = -1;
            nextRow = row + head.VertDir;
        }
        if (nextRow < 0)
        {
            // Hit ceiling — go back down.
            head.VertDir = +1;
            nextRow = row + head.VertDir;
        }
        head.HorizDir = -head.HorizDir;
        head.Target = MushroomGrid.CellCenter(col, nextRow);
    }

    // --- Spider ---
    float _spiderTimer;
    void UpdateSpiders(float dt)
    {
        _spiderTimer -= dt;
        if (_spiderTimer <= 0 && Spiders.Count == 0)
        {
            SpawnSpider();
            _spiderTimer = SpiderSpawnInterval + (float)_rng.NextDouble() * 4f;
        }

        for (int i = Spiders.Count - 1; i >= 0; i--)
        {
            var s = Spiders[i];
            s.Position += s.Velocity * dt;
            s.DirTimer -= dt;
            if (s.DirTimer <= 0)
            {
                // Pick a new mostly-horizontal direction with a vertical bias toward
                // the player zone. Velocity magnitude stays constant.
                double ang = (_rng.NextDouble() - 0.5) * Math.PI;
                if (s.Velocity.X < 0) ang += Math.PI;
                float spd = SpiderBaseSpeed * (0.8f + 0.4f * (float)_rng.NextDouble());
                s.Velocity = new Vec2(MathF.Cos((float)ang) * spd, MathF.Sin((float)ang) * spd * 0.6f);
                s.DirTimer = 0.4f + (float)_rng.NextDouble() * 0.8f;
            }
            // Bounce within player zone vertical bounds.
            if (s.Position.Y < PlayerZoneTop + 4f) { s.Position.Y = PlayerZoneTop + 4f; s.Velocity.Y = MathF.Abs(s.Velocity.Y); }
            if (s.Position.Y > WorldH - 4f)        { s.Position.Y = WorldH - 4f;        s.Velocity.Y = -MathF.Abs(s.Velocity.Y); }
            // Despawn when off horizontal edges.
            if (s.Position.X < -30f || s.Position.X > WorldW + 30f) { Spiders.RemoveAt(i); continue; }

            // Eat mushrooms it crosses.
            var (col, row) = MushroomGrid.WorldToCell(s.Position);
            if (Grid.Get(col, row) != null) Grid.Remove(col, row);

            // Touch player = death.
            if (Player.Alive && Player.Invuln <= 0)
            {
                float dx = s.Position.X - Player.Position.X;
                float dy = s.Position.Y - Player.Position.Y;
                if (dx * dx + dy * dy <= (PlayerRadius + 10f) * (PlayerRadius + 10f))
                {
                    OnPlayerHit();
                    return;
                }
            }
        }
    }

    void SpawnSpider()
    {
        bool fromLeft = _rng.Next(2) == 0;
        float x = fromLeft ? -10f : WorldW + 10f;
        float y = PlayerZoneTop + (float)_rng.NextDouble() * (WorldH - PlayerZoneTop - 20f);
        float vx = (fromLeft ? 1f : -1f) * SpiderBaseSpeed;
        Spiders.Add(new Spider
        {
            Position = new Vec2(x, y),
            Velocity = new Vec2(vx, ((float)_rng.NextDouble() - 0.5f) * SpiderBaseSpeed * 0.4f),
            DirTimer = 0.6f,
            Alive    = true,
        });
    }

    void OnPlayerHit()
    {
        if (Mode == GameMode.Attract) { Player.Invuln = 1.5f; return; }
        Player.Alive = false;
        EmitExplosion(Player.Position, 36, 0xFF_33F8FF);
        AudioEngine.PlayPlayerDeath();
        LivesLeft--;
        if (LivesLeft <= 0)
        {
            Mode = GameMode.GameOver;
            HighScoreStore.Save(HighScore);
            ShowPlacard("GAME OVER", 2.0f);
        }
        else
        {
            _respawnTimer = 1.5f;
        }
    }

    // --- Particles + popups ---
    void UpdateParticles(float dt)
    {
        for (int i = Particles.Count - 1; i >= 0; i--)
        {
            var p = Particles[i];
            p.Pos += p.Vel * dt;
            p.Vel *= MathF.Pow(0.94f, dt * 60f);
            p.Life -= dt;
            if (p.Life <= 0) Particles.RemoveAt(i);
        }
    }
    void UpdatePopups(float dt)
    {
        for (int i = Popups.Count - 1; i >= 0; i--)
        {
            var p = Popups[i];
            p.Pos.Y -= dt * 32f;
            p.Life  -= dt;
            if (p.Life <= 0) Popups.RemoveAt(i);
        }
    }

    void EmitExplosion(Vec2 origin, int count, uint color)
    {
        for (int i = 0; i < count; i++)
        {
            double ang = _rng.NextDouble() * Math.PI * 2.0;
            float spd = 60f + (float)_rng.NextDouble() * 240f;
            Particles.Add(new Particle
            {
                Pos     = origin,
                Vel     = new Vec2((float)Math.Cos(ang) * spd, (float)Math.Sin(ang) * spd),
                Life    = 0.7f,
                MaxLife = 0.7f,
                Color   = color,
                Size    = 2.0f + (float)_rng.NextDouble() * 1.8f,
            });
        }
    }

    // --- Attract AI: track nearest enemy horizontally, hold fire ---
    void UpdateAttractAI(float dt)
    {
        Firing = true;
        // Find nearest centipede segment within reach.
        float bestDx = float.PositiveInfinity;
        float targetX = Player.Position.X;
        foreach (var c in Chains)
        {
            foreach (var s in c.Segments)
            {
                float dx = MathF.Abs(s.Position.X - Player.Position.X);
                if (dx < bestDx) { bestDx = dx; targetX = s.Position.X; }
            }
        }
        MoveLeft  = Player.Position.X > targetX + 6f;
        MoveRight = Player.Position.X < targetX - 6f;
        MoveUp = false;
        MoveDown = false;
    }
}
