using System;
using System.Collections.Generic;

namespace Alaloa.Game;

/// <summary>
/// The per-frame brain for Alaloa, a Tron light-cycle duel (720×720). Runs cycle
/// motion + queued 90° turns, per-cell trail marking and collision, the 30-cell
/// look-ahead bot AI for the three non-player cycles, round/match scoring, the
/// mode state machine, and the attract autopilot. Last cycle alive wins the round.
/// </summary>
public sealed class GameWorld
{
    public float Width  => Arena.WorldW;
    public float Height => Arena.WorldH;

    public const float CycleSpeed         = 144f;   // px/sec, 18 cells/sec at 8px cells
    public const int   StartingMatchScore = 5;      // first to 5 round wins takes the match
    public const float RoundOverDelay     = 2.5f;   // seconds between rounds
    public const float SpawnInset         = 8f * 4f; // 4 cells in from the edge

    // Per-owner palette: 0=player (cyan), 1=magenta bot, 2=yellow bot, 3=green bot.
    public static readonly uint[] CycleColors =
    {
        0xFF_33F8FF,   // cyan        — player
        0xFF_FF44AA,   // magenta
        0xFF_FFEE44,   // yellow
        0xFF_55FF77,   // green
    };

    public GameMode Mode = GameMode.Title;
    public int Round = 1;
    public int[] MatchScores = new int[4];    // round-wins per cycle (player + 3 bots)
    public string PlacardText = "";
    public float PlacardTimer;
    public float TitleIdleTimer;
    public float RoundOverTimer;
    public int   HighScore;                    // player's best round-wins total ever

    public Arena Arena = new();
    public Cycle[] Cycles = new Cycle[4];
    public List<Particle> Particles = new();

    public bool TurnUp, TurnDown, TurnLeft, TurnRight;

    static readonly Random _rng = new();
    static readonly HighScoreStore HighScoreStore = new("Alaloa");

    public GameWorld()
    {
        HighScore = HighScoreStore.Load();
        ResetForTitle();
    }

    /// <summary>No-op: world coords are fixed and the renderer letterboxes.</summary>
    public void Resize(float w, float h) { }

    void ResetForTitle()
    {
        Arena.Clear();
        SpawnCycles(playerControlled: false);
        Particles.Clear();
    }

    /// <summary>Starts a fresh match (scores zeroed) at round 1 with the player controlling cycle 0.</summary>
    public void StartGame()
    {
        Mode = GameMode.Playing;
        for (int i = 0; i < 4; i++) MatchScores[i] = 0;
        Round = 1;
        StartRound();
        ShowPlacard("ROUND 1", 1.2f);
    }

    /// <summary>Starts the self-playing attract demo (all four cycles are bots).</summary>
    public void StartAttract()
    {
        StartGame();
        Mode = GameMode.Attract;
    }

    /// <summary>Returns to the title screen and respawns the idle bot demo.</summary>
    public void ReturnToTitle()
    {
        Mode = GameMode.Title;
        TitleIdleTimer = 0f;
        ResetForTitle();
    }

    void StartRound()
    {
        Arena.Clear();
        Particles.Clear();
        SpawnCycles(playerControlled: Mode == GameMode.Playing);
    }

    void SpawnCycles(bool playerControlled)
    {
        Cycles = new Cycle[4];
        // 4 spawn cells: bottom heading up, left heading right, top heading down,
        // right heading left. Player is index 0 (bottom). Bots are 1, 2, 3.
        // Spawn at cell coordinates and snap Position to the cell centre so the
        // initial trail point aligns with the grid — otherwise the first turn
        // would produce a diagonal first leg as the polyline kinks from the
        // off-centre spawn to a centre-aligned turn corner.
        int mid    = Arena.Cols / 2;            // 45
        int inset  = 4;                          // 4 cells in from each edge
        var spawns = new (int col, int row, Direction dir)[]
        {
            (mid,                  Arena.Rows - 1 - inset, Direction.Up),     // 0 player (bottom)
            (inset,                mid,                    Direction.Right),  // 1 magenta (left)
            (mid,                  inset,                  Direction.Down),   // 2 yellow (top)
            (Arena.Cols - 1 - inset, mid,                  Direction.Left),   // 3 green (right)
        };

        for (int i = 0; i < 4; i++)
        {
            var (col, row, dir) = spawns[i];
            var centre = Arena.CellCenter(col, row);
            var c = new Cycle
            {
                OwnerIndex = i,
                Color      = CycleColors[i],
                Position   = centre,
                HeadCol    = col,
                HeadRow    = row,
                Dir        = dir,
                PendingDir = dir,
                Alive      = true,
                IsPlayer   = (i == 0 && playerControlled),
            };
            c.Trail.Add(centre);
            Arena.Mark(col, row, i);
            Cycles[i] = c;
        }
    }

    /// <summary>Shows a centered placard (e.g. "ROUND 2") for <paramref name="seconds"/>.</summary>
    public void ShowPlacard(string text, float seconds)
    {
        PlacardText = text;
        PlacardTimer = seconds;
    }

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
        switch (Mode)
        {
            case GameMode.Title:
                TitleIdleTimer += dt;
                if (TitleIdleTimer > 12f) { StartAttract(); TitleIdleTimer = 0f; }
                UpdateCyclesAttract(dt);
                UpdateParticles(dt);
                break;
            case GameMode.Playing:
            case GameMode.Attract:
                ApplyPlayerInput();
                UpdateCycles(dt);
                UpdateParticles(dt);
                CheckRoundEnd();
                break;
            case GameMode.RoundOver:
                UpdateParticles(dt);
                RoundOverTimer -= dt;
                if (RoundOverTimer <= 0)
                {
                    if (MatchOver()) GoToGameOver();
                    else
                    {
                        Round++;
                        // Restore the pre-round mode BEFORE spawning so the new
                        // player cycle gets IsPlayer=true (StartRound reads Mode).
                        Mode = PreviousAttract ? GameMode.Attract : GameMode.Playing;
                        StartRound();
                        ShowPlacard($"ROUND {Round}", 1.2f);
                    }
                }
                break;
            case GameMode.GameOver:
                UpdateParticles(dt);
                break;
        }
    }
    bool PreviousAttract;

    bool MatchOver()
    {
        for (int i = 0; i < 4; i++) if (MatchScores[i] >= StartingMatchScore) return true;
        return false;
    }

    void GoToGameOver()
    {
        Mode = GameMode.GameOver;
        // Player's match score persists as their personal best.
        if (MatchScores[0] > HighScore)
        {
            HighScore = MatchScores[0];
            HighScoreStore.Save(HighScore);
        }
        // No placard — the GameOver branch of DrawHud renders the result text
        // at full size. Showing the placard here would double up.
        PlacardTimer = 0f;
    }

    void ApplyPlayerInput()
    {
        var player = Cycles[0];
        if (!player.IsPlayer || !player.Alive) return;
        Direction? want = null;
        if (TurnUp)    want = Direction.Up;
        if (TurnDown)  want = Direction.Down;
        if (TurnLeft)  want = Direction.Left;
        if (TurnRight) want = Direction.Right;
        if (want.HasValue && !Directions.IsOpposite(want.Value, player.Dir))
        {
            player.PendingDir = want.Value;
        }
    }

    void UpdateCycles(float dt)
    {
        // Run bot AI for non-player cycles before moving.
        for (int i = 0; i < 4; i++)
        {
            var c = Cycles[i];
            if (!c.Alive) continue;
            if (!c.IsPlayer) RunBot(c, dt);
        }

        for (int i = 0; i < 4; i++)
        {
            var c = Cycles[i];
            if (!c.Alive) continue;
            AdvanceCycle(c, dt);
        }
    }

    // Same as UpdateCycles but everyone's a bot (used on title screen + attract).
    void UpdateCyclesAttract(float dt)
    {
        foreach (var c in Cycles)
        {
            if (!c.Alive) continue;
            RunBot(c, dt);
            AdvanceCycle(c, dt);
        }

        // If all cycles dead, restart the attract demo.
        bool anyAlive = false;
        foreach (var c in Cycles) if (c.Alive) { anyAlive = true; break; }
        if (!anyAlive) ResetForTitle();
    }

    void AdvanceCycle(Cycle c, float dt)
    {
        // Move continuously in the current direction.
        var (dx, dy) = Directions.Delta(c.Dir);
        c.Position.X += dx * CycleSpeed * dt;
        c.Position.Y += dy * CycleSpeed * dt;

        // Did we cross into a new cell?
        var (col, row) = Arena.WorldToCell(c.Position);
        if (col == c.HeadCol && row == c.HeadRow) return;

        // Apply pending turn at the cell boundary so turns don't cut corners.
        if (c.PendingDir != c.Dir && !Directions.IsOpposite(c.PendingDir, c.Dir))
        {
            // Push a corner onto the trail at the centre of the cell we just left so
            // the rendered polyline kinks cleanly.
            var corner = Arena.CellCenter(c.HeadCol, c.HeadRow);
            c.Trail.Add(corner);
            c.Dir = c.PendingDir;
            // Re-evaluate the next cell based on the new direction.
            var (ndx, ndy) = Directions.Delta(c.Dir);
            // Snap position to the cell centre we turned at, then continue along the
            // new axis. Prevents diagonal drift when turning at speed.
            c.Position = new Vec2(corner.X, corner.Y);
            col = c.HeadCol;
            row = c.HeadRow;
            // Step into the new direction's cell by advancing one cell.
            col += ndx;
            row += ndy;
            c.Position.X += ndx * Arena.CellSize;
            c.Position.Y += ndy * Arena.CellSize;
        }

        // Collision check against arena bounds + existing trail.
        int hit = Arena.Get(col, row);
        if (hit != -1)
        {
            // Crash. Mark not-alive; emit particles.
            CrashCycle(c);
            return;
        }

        // Claim the cell and update head position.
        Arena.Mark(col, row, c.OwnerIndex);
        c.HeadCol = col;
        c.HeadRow = row;
    }

    void CrashCycle(Cycle c)
    {
        c.Alive = false;
        EmitExplosion(c.Position, 36, c.Color);
        AudioEngine.PlayCrash();
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
                Life    = 0.9f,
                MaxLife = 0.9f,
                Color   = color,
                Size    = 1.8f + (float)_rng.NextDouble() * 1.6f,
            });
        }
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

    void CheckRoundEnd()
    {
        int aliveCount = 0;
        int lastAlive  = -1;
        for (int i = 0; i < 4; i++)
        {
            if (Cycles[i].Alive) { aliveCount++; lastAlive = i; }
        }
        if (aliveCount <= 1)
        {
            if (lastAlive >= 0) MatchScores[lastAlive]++;
            PreviousAttract = (Mode == GameMode.Attract);
            Mode = GameMode.RoundOver;
            RoundOverTimer = RoundOverDelay;

            if (lastAlive == 0)
            {
                ShowPlacard("ROUND WIN", 2.0f);
                AudioEngine.PlayRoundWin();
            }
            else if (lastAlive < 0)
            {
                ShowPlacard("DRAW", 2.0f);
            }
            else
            {
                ShowPlacard("ROUND LOST", 2.0f);
                if (Mode == GameMode.Playing) AudioEngine.PlayRoundLose();
            }
        }
    }

    // --- Bot AI ---
    //
    // Each bot evaluates three options every tick: continue forward, turn left,
    // turn right. Each option gets a composite score:
    //   - base = open run ahead (defensive — distance before a wall/trail)
    //   - pursuit bonus if the move closes Manhattan distance to the nearest
    //     live opponent, penalty if it widens it
    //   - small random jitter so bots don't all act identically and 1-v-1
    //     dances stay interesting
    //
    // Reversing direction (180°) is always rejected since the cycle's own trail
    // would kill it instantly.
    void RunBot(Cycle c, float dt)
    {
        c.AiTimer -= dt;
        if (c.AiTimer > 0) return;
        c.AiTimer = 0.04f + (float)_rng.NextDouble() * 0.04f;

        Direction[] options = c.Dir switch
        {
            Direction.Up    => new[] { Direction.Up,    Direction.Left,  Direction.Right },
            Direction.Down  => new[] { Direction.Down,  Direction.Right, Direction.Left },
            Direction.Left  => new[] { Direction.Left,  Direction.Down,  Direction.Up },
            Direction.Right => new[] { Direction.Right, Direction.Up,    Direction.Down },
            _               => new[] { c.Dir, c.Dir, c.Dir },
        };

        // Find the nearest live opponent (Manhattan distance on the grid).
        Cycle? target = null;
        int targetDist = int.MaxValue;
        for (int i = 0; i < Cycles.Length; i++)
        {
            var other = Cycles[i];
            if (other == c || !other.Alive) continue;
            int d = Math.Abs(other.HeadCol - c.HeadCol) + Math.Abs(other.HeadRow - c.HeadRow);
            if (d < targetDist) { targetDist = d; target = other; }
        }

        float bestScore = float.NegativeInfinity;
        Direction best = c.Dir;
        for (int i = 0; i < options.Length; i++)
        {
            var dir = options[i];
            int run = OpenRunFromCell(c.HeadCol, c.HeadRow, dir, 30);
            if (run == 0) continue; // immediate crash

            // Defensive base: longer open run is safer. Cap at 12 so a wide-open
            // direction doesn't completely drown out the pursuit term — once
            // you have room to manoeuvre, more room isn't a lot more valuable.
            float score = MathF.Min(12, run);

            // Forward-bias: options[0] is the current direction continued. Give
            // it a fixed bonus so bots commit to straight runs unless there's
            // a real reason to turn — without this the AI flicks around because
            // small score differences between identical-looking options keep
            // flipping the winner.
            if (i == 0) score += 4f;

            // Pursuit: if this move closes distance to the target, big bonus;
            // if it widens it, penalty.
            if (target != null)
            {
                var (dx, dy) = Directions.Delta(dir);
                int nc = c.HeadCol + dx;
                int nr = c.HeadRow + dy;
                int newDist = Math.Abs(target.HeadCol - nc) + Math.Abs(target.HeadRow - nr);
                if      (newDist < targetDist) score += 6f;
                else if (newDist > targetDist) score -= 2.5f;
            }

            // Aggressive blocking: if the target's head sits in this direction's
            // open run, lean into it — we're literally driving toward them.
            if (target != null && run >= 1)
            {
                var (dx, dy) = Directions.Delta(dir);
                for (int step = 1; step <= Math.Min(run, 8); step++)
                {
                    int cc = c.HeadCol + dx * step;
                    int rr = c.HeadRow + dy * step;
                    if (cc == target.HeadCol && rr == target.HeadRow)
                    {
                        score += 8f;
                        break;
                    }
                }
            }

            // Tiny jitter so identical-score options don't always pick the
            // same one frame-to-frame. Much smaller than before to keep the
            // AI's path commitments visible to the player.
            score += (float)_rng.NextDouble() * 0.4f;

            if (score > bestScore)
            {
                bestScore = score;
                best = dir;
            }
        }
        if (!Directions.IsOpposite(best, c.Dir))
        {
            c.PendingDir = best;
        }
    }

    int OpenRunFromCell(int col, int row, Direction dir, int maxScan)
    {
        var (dx, dy) = Directions.Delta(dir);
        int cc = col + dx, rr = row + dy;
        int run = 0;
        while (run < maxScan)
        {
            if (Arena.Get(cc, rr) != -1) break;
            run++;
            cc += dx; rr += dy;
        }
        return run;
    }
}
