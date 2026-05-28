using System;
using System.Collections.Generic;

namespace Hahai.Game;

// Hahai = "to chase". Pac-Man homage: eat pellets, dodge four ghosts that
// cycle through Chase/Scatter phases, grab a power pellet to flip the ghosts
// frightened and devour them for big multipliers.
public sealed class GameWorld
{
    public float Width  => Arena.WorldW;
    public float Height => Arena.WorldH;

    public const float PacSpeed         = 110f;   // px/sec
    public const float GhostSpeed       = 100f;
    public const float FrightenedSpeed  = 70f;
    public const float EatenSpeed       = 200f;
    public const float HouseSpeed       = 50f;    // bobbing inside the house
    public const float PowerDuration    = 7.0f;   // seconds Frightened lasts
    public const float DeathDuration    = 1.6f;   // pause before respawning
    public const float ReadyDuration    = 1.4f;
    public const int   StartingLives    = 3;

    public GameMode Mode = GameMode.Title;
    public Arena Arena = new();
    public Pac Pac = new();
    public Ghost[] Ghosts = new Ghost[4];
    public List<Particle> Particles = new();
    public List<ScorePopup> ScorePopups = new();

    public int   Score;
    public int   HighScore;
    public int   Level     = 1;
    public int   Lives     = StartingLives;
    public int   GhostsEatenInPower; // for the 200/400/800/1600 chain
    public float PowerTimer;
    public float ScatterChaseTimer;
    public bool  InScatterPhase;
    public float DeathTimer;
    public float ReadyTimer;
    public string PlacardText = "";
    public float PlacardTimer;
    public float TitleIdleTimer;

    // Edge-triggered direction request from the input layer. Pac honors it on
    // the next intersection where the requested direction is open.
    public Direction Requested;
    public bool RequestedThisFrame;

    static readonly Random _rng = new();
    static readonly HighScoreStore HighScoreStore = new("Hahai");

    // Scatter/chase schedule (seconds). Classic arcade timing trimmed for demo
    // pacing — three short scatter windows interleaved with longer chase.
    static readonly float[] ScatterDurations = { 7f, 7f, 5f, 5f };
    static readonly float[] ChaseDurations   = { 20f, 20f, 20f, float.PositiveInfinity };
    int _phaseIdx;

    public GameWorld()
    {
        HighScore = HighScoreStore.Load();
        ResetForTitle();
    }

    public void Resize(float w, float h) { }

    void ResetForTitle()
    {
        Arena.ResetPellets();
        Score = 0;
        Level = 1;
        Lives = StartingLives;
        SpawnEntities();
        Particles.Clear();
        ScorePopups.Clear();
        Pac.Alive = false; // attract mode shows the maze idle
    }

    public void StartGame()
    {
        Mode = GameMode.Playing;
        Score = 0;
        Level = 1;
        Lives = StartingLives;
        Arena.ResetPellets();
        SpawnEntities();
        Particles.Clear();
        ScorePopups.Clear();
        ReadyTimer = ReadyDuration;
        Pac.Alive = true;
        ShowPlacard("READY!", ReadyDuration);
        _phaseIdx = 0;
        InScatterPhase = true;
        ScatterChaseTimer = ScatterDurations[0];
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
        ResetForTitle();
    }

    void SpawnEntities()
    {
        // Pac starts at the canonical bottom-center spawn.
        var pacSpawn = Arena.CellCenter(13, 23);
        Pac.Col = 13;
        Pac.Row = 23;
        Pac.Position = pacSpawn;
        Pac.Dir = Direction.Left;
        Pac.Pending = Direction.Left;
        Pac.MouthPhase = 0;
        Pac.Alive = true;

        // Ghost spawn cells inside the house. Blinky starts above the door
        // (already loose); the other three start inside and stagger out.
        Ghosts = new Ghost[4];
        SpawnGhost(0, GhostKind.Blinky, col: 13, row: 11, inHouse: false, releaseDelay: 0f);
        SpawnGhost(1, GhostKind.Pinky,  col: 13, row: 14, inHouse: true,  releaseDelay: 1.0f);
        SpawnGhost(2, GhostKind.Inky,   col: 11, row: 14, inHouse: true,  releaseDelay: 4.0f);
        SpawnGhost(3, GhostKind.Clyde,  col: 15, row: 14, inHouse: true,  releaseDelay: 8.0f);
        GhostsEatenInPower = 0;
        PowerTimer = 0f;
    }

    void SpawnGhost(int idx, GhostKind kind, int col, int row, bool inHouse, float releaseDelay)
    {
        Ghosts[idx] = new Ghost
        {
            Kind         = kind,
            Col          = col,
            Row          = row,
            Position     = Arena.CellCenter(col, row),
            Dir          = Direction.Up,
            State        = GhostState.Scatter,
            ReleaseDelay = releaseDelay,
            InHouse      = inHouse,
        };
    }

    public void ShowPlacard(string text, float seconds)
    {
        PlacardText = text;
        PlacardTimer = seconds;
    }

    public void Update(float dt)
    {
        if (PlacardTimer > 0) PlacardTimer -= dt;

        switch (Mode)
        {
            case GameMode.Title:
                TitleIdleTimer += dt;
                if (TitleIdleTimer > 12f) { StartAttract(); TitleIdleTimer = 0f; }
                UpdateGhostsIdle(dt);
                UpdateParticles(dt);
                UpdateScorePopups(dt);
                break;

            case GameMode.Playing:
            case GameMode.Attract:
                if (ReadyTimer > 0) { ReadyTimer -= dt; break; }
                if (DeathTimer > 0)
                {
                    DeathTimer -= dt;
                    UpdateParticles(dt);
                    if (DeathTimer <= 0) AfterDeath();
                    break;
                }
                AdvancePhase(dt);
                if (Mode == GameMode.Attract) RunHonuBot();
                AdvancePac(dt);
                AdvanceGhosts(dt);
                ResolveCollisions();
                UpdateParticles(dt);
                UpdateScorePopups(dt);
                CheckLevelClear();
                break;

            case GameMode.GameOver:
                UpdateParticles(dt);
                UpdateScorePopups(dt);
                break;
        }
    }

    void AdvancePhase(float dt)
    {
        // Frightened mode pauses scatter/chase timer — ghosts return to their
        // prior state when PowerTimer expires.
        if (PowerTimer > 0)
        {
            PowerTimer -= dt;
            if (PowerTimer <= 0)
            {
                foreach (var g in Ghosts)
                    if (g.State == GhostState.Frightened) g.State = InScatterPhase ? GhostState.Scatter : GhostState.Chase;
                GhostsEatenInPower = 0;
            }
            return;
        }

        ScatterChaseTimer -= dt;
        if (ScatterChaseTimer <= 0)
        {
            if (InScatterPhase)
            {
                InScatterPhase = false;
                ScatterChaseTimer = ChaseDurations[_phaseIdx];
                foreach (var g in Ghosts) if (g.State != GhostState.Eaten && g.State != GhostState.Frightened) g.State = GhostState.Chase;
            }
            else
            {
                _phaseIdx = Math.Min(_phaseIdx + 1, ScatterDurations.Length - 1);
                InScatterPhase = true;
                ScatterChaseTimer = ScatterDurations[_phaseIdx];
                foreach (var g in Ghosts) if (g.State != GhostState.Eaten && g.State != GhostState.Frightened) g.State = GhostState.Scatter;
            }
        }
    }

    void AdvancePac(float dt)
    {
        if (!Pac.Alive) return;

        // Mouth animation independent of grid motion.
        Pac.MouthPhase = (Pac.MouthPhase + dt * 6f) % 1f;

        // Honor input: if the requested direction is open from the CURRENT cell,
        // turn immediately even mid-segment (classic Pac feel). Otherwise queue.
        if (RequestedThisFrame)
        {
            if (Pac.Pending != Requested) Pac.Pending = Requested;
            RequestedThisFrame = false;
        }
        if (Pac.Pending != Direction.None)
        {
            // Only honor 180° reverse immediately; for orthogonal turns, wait
            // until we're close to a cell center so motion lines up with the grid.
            if (Directions.IsOpposite(Pac.Pending, Pac.Dir))
            {
                Pac.Dir = Pac.Pending;
                Pac.Pending = Direction.None;
            }
            else if (TryTurnAtIntersection(Pac, Pac.Pending))
            {
                Pac.Pending = Direction.None;
            }
        }

        StepEntity(ref Pac.Position, ref Pac.Col, ref Pac.Row, Pac.Dir, PacSpeed * dt, allowDoor: false);
    }

    // If pac is within a small slop of a cell center AND the cell in the requested
    // direction is walkable, snap to center and turn. Returns true if a turn happened.
    bool TryTurnAtIntersection(Pac pac, Direction want)
    {
        var center = Arena.CellCenter(pac.Col, pac.Row);
        float dx = pac.Position.X - center.X;
        float dy = pac.Position.Y - center.Y;
        // Slop: ~half a cell along the *moving* axis is fine for the perpendicular
        // turn, but we need the perpendicular offset to be near zero.
        bool axisMatch = pac.Dir == Direction.Left || pac.Dir == Direction.Right
            ? Math.Abs(dy) < 1f
            : Math.Abs(dx) < 1f;
        if (!axisMatch) return false;

        var (wdx, wdy) = Directions.Delta(want);
        int nc = pac.Col + wdx, nr = pac.Row + wdy;
        if (!Arena.IsWalkable(nc, nr)) return false;

        // Snap to center, turn.
        pac.Position = center;
        pac.Dir = want;
        return true;
    }

    void AdvanceGhosts(float dt)
    {
        foreach (var g in Ghosts) AdvanceGhost(g, dt);
    }

    void AdvanceGhost(Ghost g, float dt)
    {
        // House release: count down delay, then float up through the door.
        if (g.InHouse)
        {
            g.ReleaseDelay -= dt;
            // Bob vertically until release.
            var bobCenter = Arena.CellCenter(g.Col, g.Row);
            g.Position.X = bobCenter.X;
            g.Position.Y = bobCenter.Y + MathF.Sin((float)Environment.TickCount * 0.003f + g.Col) * 3f;
            if (g.ReleaseDelay <= 0f)
            {
                // Teleport out to the cell just above the door — released
                // ghosts can't normally use the door so we drop them outside
                // it directly. From (13, 11) they can pick Left/Right via
                // ChooseDirectionTowards at the next intersection.
                g.InHouse = false;
                g.Col = 13;
                g.Row = 11;
                g.Position = Arena.CellCenter(13, 11);
                g.Dir = Direction.Left;
            }
            return;
        }

        // Eaten ghosts beeline back to the door entrance, then drop back into
        // the house and start a fresh short release timer — the eyes visibly
        // re-emerge through the door instead of respawning wherever they died.
        if (g.State == GhostState.Eaten)
        {
            if (g.Col == 13 && g.Row == 11)
            {
                var (rc, rr) = HouseRespawnCell(g.Kind);
                g.InHouse      = true;
                g.State        = InScatterPhase ? GhostState.Scatter : GhostState.Chase;
                g.ReleaseDelay = 1.0f;
                g.Col          = rc;
                g.Row          = rr;
                g.Position     = Arena.CellCenter(rc, rr);
                g.Dir          = Direction.Up;
            }
            else
            {
                ChooseDirectionTowards(g, 13, 11, allowDoor: true);
                StepEntity(ref g.Position, ref g.Col, ref g.Row, g.Dir, EatenSpeed * dt, allowDoor: true);
            }
            return;
        }

        // Frightened ghosts pick a random valid turn at each intersection.
        if (g.State == GhostState.Frightened)
        {
            ChooseRandomTurn(g);
            StepEntity(ref g.Position, ref g.Col, ref g.Row, g.Dir, FrightenedSpeed * dt, allowDoor: false);
            return;
        }

        // Chase or Scatter: pick target by kind, head toward it via greedy
        // intersection-time decisions (no path planning — classic Pac-Man AI).
        var (tc, tr) = g.State == GhostState.Scatter
            ? Arena.ScatterCorner(g.Kind)
            : ChaseTarget(g);
        ChooseDirectionTowards(g, tc, tr, allowDoor: false);
        StepEntity(ref g.Position, ref g.Col, ref g.Row, g.Dir, GhostSpeed * dt, allowDoor: false);
    }

    (int col, int row) ChaseTarget(Ghost g)
    {
        // Per-kind chase targeting (simplified versions of the arcade behavior).
        switch (g.Kind)
        {
            case GhostKind.Blinky:
                return (Pac.Col, Pac.Row);
            case GhostKind.Pinky:
            {
                var (dx, dy) = Directions.Delta(Pac.Dir);
                return (Pac.Col + dx * 4, Pac.Row + dy * 4);
            }
            case GhostKind.Inky:
            {
                // Two cells ahead of pac, then mirrored through Blinky's position.
                var (dx, dy) = Directions.Delta(Pac.Dir);
                int px = Pac.Col + dx * 2;
                int py = Pac.Row + dy * 2;
                var b = Ghosts[0];
                return (2 * px - b.Col, 2 * py - b.Row);
            }
            case GhostKind.Clyde:
            {
                int dCol = g.Col - Pac.Col, dRow = g.Row - Pac.Row;
                int distSq = dCol * dCol + dRow * dRow;
                if (distSq > 64) return (Pac.Col, Pac.Row);
                return Arena.ScatterCorner(GhostKind.Clyde);
            }
        }
        return (Pac.Col, Pac.Row);
    }

    void ChooseDirectionTowards(Ghost g, int tCol, int tRow, bool allowDoor)
    {
        // Only choose at cell centers. Direction is locked between centers so
        // motion stays grid-aligned and prevents flickering decisions.
        var center = Arena.CellCenter(g.Col, g.Row);
        if (Math.Abs(g.Position.X - center.X) > 0.5f || Math.Abs(g.Position.Y - center.Y) > 0.5f) return;

        Direction best = g.Dir;
        float bestDist = float.PositiveInfinity;
        Direction[] options = { Direction.Up, Direction.Left, Direction.Down, Direction.Right };
        foreach (var d in options)
        {
            if (Directions.IsOpposite(d, g.Dir)) continue;
            var (dx, dy) = Directions.Delta(d);
            int nc = g.Col + dx, nr = g.Row + dy;
            if (!Arena.IsWalkable(nc, nr, allowDoor)) continue;
            float ddx = nc - tCol, ddy = nr - tRow;
            float dist = ddx * ddx + ddy * ddy;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = d;
            }
        }
        g.Dir = best;
    }

    void ChooseRandomTurn(Ghost g)
    {
        var center = Arena.CellCenter(g.Col, g.Row);
        if (Math.Abs(g.Position.X - center.X) > 0.5f || Math.Abs(g.Position.Y - center.Y) > 0.5f) return;
        var opts = new List<Direction>(4);
        Direction[] all = { Direction.Up, Direction.Left, Direction.Down, Direction.Right };
        foreach (var d in all)
        {
            if (Directions.IsOpposite(d, g.Dir)) continue;
            var (dx, dy) = Directions.Delta(d);
            if (Arena.IsWalkable(g.Col + dx, g.Row + dy)) opts.Add(d);
        }
        if (opts.Count == 0) return;
        g.Dir = opts[_rng.Next(opts.Count)];
    }

    // Continuous motion within a cell + step at cell boundaries. Sets Col/Row
    // when crossing a center, then verifies the next cell is walkable; if not,
    // snap back to the previous cell center (clean wall-stop).
    void StepEntity(ref Vec2 pos, ref int col, ref int row, Direction dir, float dist, bool allowDoor)
    {
        if (dir == Direction.None || dist == 0f) return;
        var (dx, dy) = Directions.Delta(dir);

        // Tunnel wrap if currently sitting on a tunnel cell and moving off-edge.
        // We do this BEFORE the move so the head doesn't run off the world.
        if (Arena.IsTunnel(col, row))
        {
            if (dir == Direction.Left  && col == 0)             { col = Arena.Cols - 1; pos = Arena.CellCenter(col, row); }
            if (dir == Direction.Right && col == Arena.Cols - 1) { col = 0;             pos = Arena.CellCenter(col, row); }
        }

        // Check the NEXT cell up front — if it's a wall, we can only travel up
        // to the current cell's center and then stop.
        var nextCol = col + dx;
        var nextRow = row + dy;
        bool nextOpen = Arena.IsWalkable(nextCol, nextRow, allowDoor);

        var center = Arena.CellCenter(col, row);

        if (!nextOpen)
        {
            // How much travel along motion axis remains before we hit center?
            float remain = dir switch
            {
                Direction.Left  => pos.X - center.X,
                Direction.Right => center.X - pos.X,
                Direction.Up    => pos.Y - center.Y,
                Direction.Down  => center.Y - pos.Y,
                _ => 0f,
            };
            if (remain <= 0f) return; // Already at center facing wall.
            float clamped = Math.Min(dist, remain);
            pos.X += dx * clamped;
            pos.Y += dy * clamped;
            return;
        }

        // Otherwise apply the full step.
        pos.X += dx * dist;
        pos.Y += dy * dist;

        // Update cell if we crossed center.
        var (nc, nr) = Arena.WorldToCell(pos);
        if (nc != col || nr != row)
        {
            col = nc;
            row = nr;
        }
    }

    void ResolveCollisions()
    {
        // Pellet eating: check the cell pac is in.
        if (Pac.Col >= 0 && Pac.Col < Arena.Cols && Pac.Row >= 0 && Pac.Row < Arena.Rows)
        {
            if (Arena.Pellets[Pac.Col, Pac.Row])
            {
                Arena.Pellets[Pac.Col, Pac.Row] = false;
                Arena.RemainingPellets--;
                Score += 10;
                if (Mode == GameMode.Playing) AudioEngine.PlayChomp();
            }
            else if (Arena.PowerDot[Pac.Col, Pac.Row])
            {
                Arena.PowerDot[Pac.Col, Pac.Row] = false;
                Arena.RemainingPellets--;
                Score += 50;
                PowerTimer = PowerDuration;
                GhostsEatenInPower = 0;
                foreach (var g in Ghosts)
                {
                    if (g.State == GhostState.Eaten || g.InHouse) continue;
                    g.State = GhostState.Frightened;
                    // Reverse on power activation (classic behavior).
                    if (g.Dir != Direction.None) g.Dir = OppositeDir(g.Dir);
                }
                if (Mode == GameMode.Playing) AudioEngine.PlayPower();
            }
        }

        // Ghost touch.
        foreach (var g in Ghosts)
        {
            if (g.InHouse || g.State == GhostState.Eaten) continue;
            float dxp = g.Position.X - Pac.Position.X;
            float dyp = g.Position.Y - Pac.Position.Y;
            if (dxp * dxp + dyp * dyp > 16f * 16f) continue;

            if (g.State == GhostState.Frightened)
            {
                GhostsEatenInPower++;
                int value = 200 * (1 << (Math.Min(GhostsEatenInPower, 4) - 1)); // 200/400/800/1600
                Score += value;
                AddScorePopup(g.Position, value, GhostBaseColor(g.Kind));
                g.State = GhostState.Eaten;
                if (Mode == GameMode.Playing) AudioEngine.PlayEatGhost();
            }
            else
            {
                // Pac dies.
                KillPac();
                return;
            }
        }
    }

    void KillPac()
    {
        Pac.Alive = false;
        DeathTimer = DeathDuration;
        EmitExplosion(Pac.Position, 40, 0xFFFFEE44);
        if (Mode == GameMode.Playing) AudioEngine.PlayDeath();
    }

    void AfterDeath()
    {
        Lives--;
        if (Lives <= 0)
        {
            Mode = (Mode == GameMode.Attract) ? GameMode.Title : GameMode.GameOver;
            if (Score > HighScore) { HighScore = Score; HighScoreStore.Save(HighScore); }
            return;
        }
        // Respawn pac + put ghosts back home for next life.
        SpawnEntities();
        ReadyTimer = ReadyDuration;
        ShowPlacard("READY!", ReadyDuration);
    }

    void CheckLevelClear()
    {
        if (Arena.RemainingPellets > 0) return;
        Level++;
        Arena.ResetPellets();
        SpawnEntities();
        ReadyTimer = ReadyDuration;
        ShowPlacard($"LEVEL {Level}", 1.6f);
        _phaseIdx = 0;
        InScatterPhase = true;
        ScatterChaseTimer = ScatterDurations[0];
        if (Mode == GameMode.Playing) AudioEngine.PlayLevelClear();
    }

    void UpdateGhostsIdle(float dt)
    {
        // On title screen, ghosts just bob inside the house.
        foreach (var g in Ghosts) AdvanceGhost(g, dt);
    }

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

    void UpdateScorePopups(float dt)
    {
        for (int i = ScorePopups.Count - 1; i >= 0; i--)
        {
            var s = ScorePopups[i];
            s.Pos.Y -= 18f * dt;
            s.Life -= dt;
            if (s.Life <= 0) ScorePopups.RemoveAt(i);
        }
    }

    void EmitExplosion(Vec2 origin, int count, uint color)
    {
        for (int i = 0; i < count; i++)
        {
            double ang = _rng.NextDouble() * Math.PI * 2.0;
            float spd = 60f + (float)_rng.NextDouble() * 220f;
            Particles.Add(new Particle
            {
                Pos     = origin,
                Vel     = new Vec2((float)Math.Cos(ang) * spd, (float)Math.Sin(ang) * spd),
                Life    = 0.8f,
                MaxLife = 0.8f,
                Color   = color,
                Size    = 1.6f + (float)_rng.NextDouble() * 1.6f,
            });
        }
    }

    void AddScorePopup(Vec2 pos, int value, uint color)
    {
        ScorePopups.Add(new ScorePopup
        {
            Pos     = pos,
            Value   = value,
            Life    = 0.9f,
            MaxLife = 0.9f,
            Color   = color,
        });
    }

    static Direction OppositeDir(Direction d) => d switch
    {
        Direction.Up    => Direction.Down,
        Direction.Down  => Direction.Up,
        Direction.Left  => Direction.Right,
        Direction.Right => Direction.Left,
        _               => Direction.None,
    };

    // Auto-pilot for the honu during attract mode. Scans for the nearest pellet
    // and (if any are loose and dangerous) the nearest unfrightened mo'o, then
    // at cell centers sets Pac.Pending to the direction that closes pellet
    // distance while avoiding immediate danger. No path-planning — just greedy
    // one-step lookahead, plenty for a demo loop.
    void RunHonuBot()
    {
        var center = Arena.CellCenter(Pac.Col, Pac.Row);
        // Only re-decide when we're at a cell center, otherwise we'd flip the
        // pending direction continuously between cells and never honor any.
        if (Math.Abs(Pac.Position.X - center.X) > 1f || Math.Abs(Pac.Position.Y - center.Y) > 1f) return;

        // Nearest remaining pellet / power dot.
        int targetCol = Pac.Col, targetRow = Pac.Row;
        int bestDist = int.MaxValue;
        for (int r = 0; r < Arena.Rows; r++)
            for (int c = 0; c < Arena.Cols; c++)
            {
                if (!Arena.Pellets[c, r] && !Arena.PowerDot[c, r]) continue;
                int d = Math.Abs(c - Pac.Col) + Math.Abs(r - Pac.Row);
                if (d < bestDist) { bestDist = d; targetCol = c; targetRow = r; }
            }

        // Nearest loose, hostile mo'o.
        int dangerDist = int.MaxValue;
        int dangerCol = -1, dangerRow = -1;
        foreach (var g in Ghosts)
        {
            if (g.InHouse || g.State == GhostState.Frightened || g.State == GhostState.Eaten) continue;
            int d = Math.Abs(g.Col - Pac.Col) + Math.Abs(g.Row - Pac.Row);
            if (d < dangerDist) { dangerDist = d; dangerCol = g.Col; dangerRow = g.Row; }
        }

        Direction[] options = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
        Direction best = Pac.Dir;
        float bestScore = float.NegativeInfinity;
        foreach (var dir in options)
        {
            var (dx, dy) = Directions.Delta(dir);
            int nc = Pac.Col + dx, nr = Pac.Row + dy;
            if (!Arena.IsWalkable(nc, nr)) continue;

            int newPelletDist = Math.Abs(nc - targetCol) + Math.Abs(nr - targetRow);
            float score = -newPelletDist;
            if (dir == Pac.Dir) score += 0.5f;
            if (Directions.IsOpposite(dir, Pac.Dir)) score -= 3f;
            if (dangerCol >= 0 && dangerDist < 6)
            {
                int newDanger = Math.Abs(nc - dangerCol) + Math.Abs(nr - dangerRow);
                if (newDanger < dangerDist) score -= 12f;
                else if (newDanger > dangerDist) score += 4f;
            }
            if (score > bestScore) { bestScore = score; best = dir; }
        }
        Pac.Pending = best;
    }

    // Where each ghost kind drops back into the ghost house after being eaten.
    // Cells match the initial spawn layout in SpawnEntities — Blinky goes
    // through the middle since he doesn't have his own slot.
    static (int col, int row) HouseRespawnCell(GhostKind k) => k switch
    {
        GhostKind.Blinky => (13, 14),
        GhostKind.Pinky  => (13, 14),
        GhostKind.Inky   => (11, 14),
        GhostKind.Clyde  => (15, 14),
        _                => (13, 14),
    };

    public static uint GhostBaseColor(GhostKind k) => k switch
    {
        GhostKind.Blinky => 0xFFFF3344, // red
        GhostKind.Pinky  => 0xFFFFAACC, // pink
        GhostKind.Inky   => 0xFF33EEFF, // cyan
        GhostKind.Clyde  => 0xFFFF9933, // orange
        _                => 0xFFFFFFFF,
    };
}
