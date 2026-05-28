using System;
using System.Collections.Generic;

namespace Alaloa.Game;

// Tron-Light-Cycles-style game. Four cycles spawn from the four cardinal edges
// of a 720×720 arena and head toward the centre, laying neon trails. Cycles
// can only turn 90°; crashing into any trail (your own or anyone else's) or
// the arena edge kills the cycle. Last cycle alive wins the round.
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

    public void Resize(float w, float h) { }

    void ResetForTitle()
    {
        Arena.Clear();
        SpawnCycles(playerControlled: false);
        Particles.Clear();
    }

    public void StartGame()
    {
        Mode = GameMode.Playing;
        for (int i = 0; i < 4; i++) MatchScores[i] = 0;
        Round = 1;
        StartRound();
        ShowPlacard("ROUND 1", 1.2f);
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

    void StartRound()
    {
        Arena.Clear();
        Particles.Clear();
        SpawnCycles(playerControlled: Mode == GameMode.Playing);
    }

    void SpawnCycles(bool playerControlled)
    {
        Cycles = new Cycle[4];
        // 4 spawn positions: top heading down, right heading left, bottom heading up,
        // left heading right. Player is index 0 (bottom). Bots are 1, 2, 3.
        var spawns = new (Vec2 pos, Direction dir)[]
        {
            (new Vec2(Arena.WorldW * 0.5f,  Arena.WorldH - SpawnInset), Direction.Up),    // 0 player (bottom)
            (new Vec2(SpawnInset,           Arena.WorldH * 0.5f),        Direction.Right), // 1 magenta (left)
            (new Vec2(Arena.WorldW * 0.5f,  SpawnInset),                 Direction.Down),  // 2 yellow (top)
            (new Vec2(Arena.WorldW - SpawnInset, Arena.WorldH * 0.5f),   Direction.Left),  // 3 green (right)
        };

        for (int i = 0; i < 4; i++)
        {
            var (col, row) = Arena.WorldToCell(spawns[i].pos);
            var c = new Cycle
            {
                OwnerIndex = i,
                Color      = CycleColors[i],
                Position   = spawns[i].pos,
                HeadCol    = col,
                HeadRow    = row,
                Dir        = spawns[i].dir,
                PendingDir = spawns[i].dir,
                Alive      = true,
                IsPlayer   = (i == 0 && playerControlled),
            };
            c.Trail.Add(spawns[i].pos);
            Arena.Mark(col, row, i);
            Cycles[i] = c;
        }
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
                    else { Round++; StartRound(); Mode = (MatchScores[0] > 0 || _bestBot() > 0) ? Mode : Mode; Mode = (PreviousAttract ? GameMode.Attract : GameMode.Playing); ShowPlacard($"ROUND {Round}", 1.2f); }
                }
                break;
            case GameMode.GameOver:
                UpdateParticles(dt);
                break;
        }
    }
    bool PreviousAttract;
    int _bestBot()
    {
        int b = 0; for (int i = 1; i < 4; i++) if (MatchScores[i] > b) b = MatchScores[i];
        return b;
    }

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
        ShowPlacard(MatchScores[0] >= StartingMatchScore ? "YOU WIN" : "GAME OVER", 3.0f);
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
    // Each bot looks at three options (continue / turn-left / turn-right) and
    // picks the one with the longest open run ahead. Adds slight randomness so
    // bots don't all act identically and the chase feels alive.
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

        // Run is the number of empty cells ahead in each direction.
        int bestRun = -1;
        Direction best = c.Dir;
        for (int i = 0; i < options.Length; i++)
        {
            int run = OpenRunFromCell(c.HeadCol, c.HeadRow, options[i], 30);
            // Slight randomness: give a small bonus to direction changes when
            // current run is short, so bots break out of straight lines.
            float wiggle = (float)_rng.NextDouble() * 1.5f;
            if (run + wiggle > bestRun)
            {
                bestRun = (int)(run + wiggle);
                best = options[i];
            }
        }
        // Avoid 180° reversals (which would be lethal anyway).
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
