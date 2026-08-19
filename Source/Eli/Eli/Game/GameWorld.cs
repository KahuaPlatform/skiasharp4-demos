using System;
using System.Collections.Generic;

namespace Eli.Game;

// Eli = "to dig". Dig Dug homage: a side-on field of packed dirt, four strata
// deep, that the player REWRITES as they walk. Sim core modelled on Koa (bounded
// clamped camera, TileGrid wall-slide motion, flow-field chase), diverging per
// the four pillars in DESIGN:
//   1. terrain is mutable — carving forces a same-frame flow-field re-flood;
//   2. the weapon is a stateful extending harpoon, not a fire-and-forget bullet;
//   3. gravity applies to terrain features — boulders wobble, fall and crush;
//   4. enemy AI is two-mode — flow-field in tunnels, straight-line phasing
//      THROUGH dirt out of them (flow-field routing does not apply in that mode).
public sealed class GameWorld
{
    // --- Tunables -----------------------------------------------------------
    // Digger
    public const float WalkSpeed      = 132f;   // px/sec in tunnel or sky (~4.1 cells/s)
    public const float DigSpeed       = 64f;    // px/sec through packed dirt (~2.0 cells/s)
    public const float DiggerRadius   = Field.CellSize * 0.34f;
    public const float CarveFraction  = 0.8f;   // of radius; keeps corridors 1 cell wide
    public const int   StartingLives  = 3;
    public const float RespawnDelay   = 1.4f;

    // Harpoon
    public const float HarpoonExtendSpeed  = 420f;
    public const float HarpoonRetractSpeed = 700f;  // > extend, so a whiff costs ~0.4s total
    public const float HarpoonMaxLength    = 104f;  // 3.25 cells
    public const int   UhanePumpsToBurst   = 4;
    public const int   NohuPumpsToBurst    = 5;     // the tougher kind takes one more
    public const float PumpInterval        = 0.18f;
    public const float InflateDecayPerSec  = 0.55f;
    public const float BurstHoldTime       = 0.25f;

    // Boulders
    public const float BoulderWobbleDelay   = 0.9f;   // the player's escape window
    public const float BoulderGravity       = 900f;
    public const float BoulderMaxFallSpeed  = 520f;
    public const float BoulderShatterTime   = 0.35f;
    public const float BoulderRadius        = Field.CellSize * 0.46f;

    // Enemies
    public const float UhaneSpeed  = 96f;
    public const float NohuSpeed   = 78f;
    public const float EnemyRadius = Field.CellSize * 0.34f;
    public const int   LiveEnemyCap = 8;   // no generators; this is a safety rail

    // Phasing (the second AI mode)
    public const float GhostTriggerDistance = 240f;  // 7.5 cells
    public const float GhostCheckInterval   = 3.5f;
    public const float GhostCheckJitter     = 1.5f;
    public const float GhostSpeed           = 46f;   // < 1/3 WalkSpeed: always outrunnable
    public const float GhostMinDuration     = 0.6f;

    // Sim
    public const int   FlowRebuildEvery   = 5;    // frames — PLUS every terrain edit
    public const float TitleIdleToAttract = 12f;
    public const float AutoFireRange      = 200f;
    public const int   LevelClearBonus    = 500;  // x Level

    // --- State --------------------------------------------------------------
    public GameMode Mode = GameMode.Title;

    public Field    Field = null!;
    public Camera2D Camera = new() { Zoom = 1.25f };
    public Pathing  Pathing = null!;

    public Digger  Digger = new();
    public Harpoon Harpoon;

    public readonly List<Enemy>    Enemies   = new();
    public readonly List<Boulder>  Boulders  = new();
    public readonly List<Particle> Particles = new();

    public int Score;
    public int HighScore;
    public int Level = 1;
    public int Lives = StartingLives;

    public float ViewW, ViewH;
    public float TitleIdleTimer;
    public float LevelClearFlash;    // > 0 briefly after a field is cleared

    // Input bridge (set by MainPage each frame while Playing).
    public bool FireHeld;
    Vec2 _moveIntent;

    // Attract autopilot state (see RunAutoDigger).
    Vec2  _autoHeading;
    float _autoTimer;
    Vec2  _autoLastPos;
    bool  _autoFire;

    int _frame;
    float _speedScale = 1f;
    static readonly Random _rng = new();
    static readonly HighScoreStore HighScoreStore = new("Eli");

    public GameWorld()
    {
        HighScore = HighScoreStore.Load();
        LoadLevel(1);
        Mode = GameMode.Title;
    }

    // Viewport size from the canvas; the camera frames the world inside it.
    public void Resize(float w, float h)
    {
        ViewW = w; ViewH = h;
        Camera.SetViewport(w, h);
        ConfigureCameraAxes();
    }

    // Clamp on both axes, snap follow (FollowRate 0) — the field is taller and
    // wider than the viewport on both axes, and the camera hard-stops at its edges.
    void ConfigureCameraAxes()
    {
        if (Field is null) return;
        Camera.X = new CameraAxis { Mode = AxisMode.Clamp, WorldSize = Field.WorldWidth,  FollowRate = 0f };
        Camera.Y = new CameraAxis { Mode = AxisMode.Clamp, WorldSize = Field.WorldHeight, FollowRate = 0f };
    }

    // --- Input bridge -------------------------------------------------------
    public void SetMoveIntent(float mx, float my) => _moveIntent = new Vec2(mx, my);

    // --- Lifecycle ----------------------------------------------------------
    void LoadLevel(int level)
    {
        Level = Math.Max(1, level);
        _speedScale = Game.Level.SpeedScaleFor(Level);

        var loaded = Game.Level.Build(Level, _rng);
        Field = loaded.Field;
        Pathing = new Pathing(Field);
        ConfigureCameraAxes();

        Enemies.Clear();
        Boulders.Clear();
        Particles.Clear();
        Harpoon.Reset();

        Digger.Pos = loaded.DiggerSpawn;
        Digger.Vel = Vec2.Zero;
        Digger.Radius = DiggerRadius;
        Digger.Alive = true;
        Digger.Facing = Facing.Right;
        Digger.RespawnTimer = 0f;
        Digger.Digging = false;
        _diggerSpawn = loaded.DiggerSpawn;

        foreach (var (c, r, kind) in loaded.Enemies)
            SpawnEnemy(kind, Field.CellCenter(c, r));

        foreach (var (c, r) in loaded.Boulders)
        {
            Boulders.Add(new Boulder
            {
                Col = c, Row = r,
                Pos = Field.CellCenter(c, r),
                Radius = BoulderRadius,
            });
            // A settled boulder is as solid as bedrock to everything — register its
            // cell before the first flow-field flood below so the swarm never routes
            // through it.
            Field.SetBoulderCell(c, r, true);
        }

        Pathing.Rebuild(Digger.Pos);
        Camera.Snap(Digger.Pos.X, Digger.Pos.Y);
    }

    Vec2 _diggerSpawn;

    public void StartGame()
    {
        Mode = GameMode.Playing;
        Score = 0;
        Lives = StartingLives;
        LoadLevel(1);
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
        Lives = StartingLives;
        Score = 0;
        LoadLevel(1);
    }

    // --- Main update --------------------------------------------------------
    public void Update(float dt)
    {
        _frame++;
        if (LevelClearFlash > 0f) LevelClearFlash -= dt;

        switch (Mode)
        {
            case GameMode.Title:
                TitleIdleTimer += dt;
                if (TitleIdleTimer > TitleIdleToAttract) { StartAttract(); TitleIdleTimer = 0f; }
                // Let the monsters mill about so the title screen has life.
                UpdateEnemies(dt);
                UpdateParticles(dt);
                Camera.Snap(Digger.Pos.X, Digger.Pos.Y);
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
        // Death pause: the field keeps its tunnels while the digger is off it.
        if (Digger.RespawnTimer > 0f)
        {
            Digger.RespawnTimer -= dt;
            UpdateBoulders(dt);
            UpdateParticles(dt);
            if (Digger.RespawnTimer <= 0f) RespawnDigger();
            return;
        }

        if (Mode == GameMode.Attract) RunAutoDigger(dt);

        // 1. Digger movement — carves as it goes.
        MoveDigger(dt);
        Camera.Follow(Digger.Pos.X, Digger.Pos.Y, dt);

        // 2. Re-flood the shared flow field on the frame cadence OR whenever the
        //    terrain changed. The second trigger is the one Koa doesn't need: Eli
        //    rewrites terrain most frames the player is moving, and a stale field
        //    reads immediately as enemies walking into dirt that no longer exists.
        if (_frame % FlowRebuildEvery == 0 || Field.ConsumeTerrainDirty())
            Pathing.Rebuild(Digger.Pos);

        // 3. The weapon — a state machine, not a projectile list.
        UpdateHarpoon(dt);

        // 4. Entities.
        UpdateEnemies(dt);
        ResolveCrowding();
        UpdateBoulders(dt);
        UpdateParticles(dt);

        // 5. Interactions.
        HandleEnemyContact();

        // 6. Sweep the dead.
        Enemies.RemoveAll(e => !e.Alive);
        Boulders.RemoveAll(b => !b.Alive);

        // 7. Field cleared?
        CheckFieldClear();
    }

    // --- Digger -------------------------------------------------------------
    void MoveDigger(float dt)
    {
        var dir = _moveIntent;
        if (dir.X != 0f || dir.Y != 0f)
        {
            dir = dir.Normalized();
            Digger.Facing = FacingFor(dir);
        }
        Digger.MoveDir = dir;

        // Speed depends on whether there is undug earth in the way: packed dirt
        // cuts the digger to DigSpeed, an open tunnel lets it walk. The digger is
        // never *blocked* by dirt, only slowed by it.
        //
        // The test is CELL-BASED — "is the next cell along my facing still dirt?"
        // — not a fixed pixel probe ahead of the body. A pixel probe has to reach
        // past the half-cell (16px) to see the next cell at all, which a probe of
        // radius+2 (12.9px) never does: it just re-reads the cell the digger has
        // already carved, so the penalty almost never applied and tunnelling ran
        // at ~115 px/s instead of 64.
        var f = Facings.ToVec(Digger.Facing);
        var (dcol, drow) = Field.WorldToCell(Digger.Pos);
        int ncol = dcol + (int)f.X, nrow = drow + (int)f.Y;
        Digger.Digging = (dir.X != 0f || dir.Y != 0f)
                       && Field.InBounds(ncol, nrow)
                       && Field[ncol, nrow] == Tile.Dirt;

        float speed = Digger.Digging ? DigSpeed : WalkSpeed;
        Digger.Vel = dir * speed;

        MoveWithCorridorAssist(ref Digger.Pos, Digger.Radius, Digger.Vel, dt, digger: true);

        // Carve what the body now overlaps. The shrunk radius is what keeps the
        // corridor one cell wide (see Field.Carve).
        if (Field.Carve(Digger.Pos, Digger.Radius * CarveFraction) && Mode == GameMode.Playing)
            AudioEngine.PlayDig();
    }

    static Facing FacingFor(Vec2 dir) =>
        MathF.Abs(dir.X) >= MathF.Abs(dir.Y)
            ? (dir.X >= 0f ? Facing.Right : Facing.Left)
            : (dir.Y >= 0f ? Facing.Down  : Facing.Up);

    // Koa's corridor-centering assist, reused verbatim in shape: when motion is
    // strongly along one axis, ease the perpendicular coordinate toward the cell's
    // centre line so the body lines up with 1-tile corridors. Because Eli's motion
    // is 4-DIRECTIONAL, the assist always has a dominant axis and therefore always
    // applies — which is exactly why carved tunnels come out one cell wide.
    void MoveWithCorridorAssist(ref Vec2 pos, float radius, Vec2 vel, float dt, bool digger)
    {
        const float Dominance  = 2.0f;
        const float EasePerSec = 9.0f;
        float cs = Field.CellSize;

        float dx = vel.X * dt, dy = vel.Y * dt;
        float ax = MathF.Abs(vel.X), ay = MathF.Abs(vel.Y);

        if (ax > ay * Dominance)
        {
            int row = (int)MathF.Floor(pos.Y / cs);
            float target = row * cs + cs * 0.5f;
            dy += (target - pos.Y) * MathF.Min(1f, EasePerSec * dt);
        }
        else if (ay > ax * Dominance)
        {
            int col = (int)MathF.Floor(pos.X / cs);
            float target = col * cs + cs * 0.5f;
            dx += (target - pos.X) * MathF.Min(1f, EasePerSec * dt);
        }

        if (digger) Field.MoveDigger(ref pos, radius, dx, dy);
        else        Field.MoveEnemy(ref pos, radius, dx, dy);
    }

    // --- The harpoon --------------------------------------------------------
    //
    // A stateful extending segment. Contrast Koa's Projectile, which is spawned,
    // integrated and swept: this one thing persists, holds, and pumps.
    void UpdateHarpoon(float dt)
    {
        bool fire = Mode == GameMode.Attract ? _autoFire : FireHeld;
        if (Harpoon.PumpTimer > 0f) Harpoon.PumpTimer -= dt;

        switch (Harpoon.State)
        {
            case HarpoonState.Idle:
                if (fire) FireHarpoon();
                break;

            case HarpoonState.Extending:
                AdvanceHarpoon(dt);
                break;

            case HarpoonState.Attached:
                PumpVictim(dt, fire);
                break;

            case HarpoonState.Retracting:
                Harpoon.Length -= HarpoonRetractSpeed * dt;
                if (Harpoon.Length <= 0f) Harpoon.Reset();
                break;
        }
    }

    void FireHarpoon()
    {
        Harpoon.State  = HarpoonState.Extending;
        Harpoon.Origin = Digger.Pos;
        Harpoon.Dir    = Facings.ToVec(Digger.Facing);
        Harpoon.Length = Digger.Radius;
        Harpoon.Victim = null;
        if (Mode == GameMode.Playing) AudioEngine.PlayHarpoonFire();
    }

    void AdvanceHarpoon(float dt)
    {
        Harpoon.Length += HarpoonExtendSpeed * dt;

        // Tip hit an enemy? A PHASING enemy is immune — the tip passes straight
        // through it, which is what makes ghost mode an escape and not a free target.
        var tip = Harpoon.Tip;
        foreach (var e in Enemies)
        {
            if (!e.Alive || e.Mode == EnemyMode.Phasing || e.BurstTimer > 0f) continue;
            if (CircleHit(tip, 3f, e.Pos, e.Radius))
            {
                Harpoon.State  = HarpoonState.Attached;
                Harpoon.Victim = e;
                e.Pinned = true;
                e.Vel = Vec2.Zero;
                if (Mode == GameMode.Playing) AudioEngine.PlayHarpoonStick();
                return;
            }
        }

        // Tip buried in dirt or bedrock, or fully extended? Retract.
        var (tc, tr) = Field.WorldToCell(tip);
        if (Harpoon.Length >= HarpoonMaxLength || Field.IsBlockedForHarpoon(tc, tr))
            Harpoon.State = HarpoonState.Retracting;
    }

    void PumpVictim(float dt, bool fire)
    {
        var v = Harpoon.Victim;
        if (v is null || !v.Alive) { DetachHarpoon(); return; }

        // Moving breaks the pump — you have to stand still to work the bellows.
        if (Digger.MoveDir.X != 0f || Digger.MoveDir.Y != 0f) { DetachHarpoon(); return; }

        // Keep the segment anchored to the victim as it swells.
        Harpoon.Origin = Digger.Pos;
        Harpoon.Length = (v.Pos - Digger.Pos).Length;

        if (fire && Harpoon.PumpTimer <= 0f)
        {
            v.Inflation += 1f;
            Harpoon.PumpTimer = PumpInterval;
            if (Mode == GameMode.Playing) AudioEngine.PlayPump(v.Inflation / v.PumpsToBurst);

            if (v.Inflation >= v.PumpsToBurst)
            {
                BurstEnemy(v);
                DetachHarpoon();
                return;
            }
        }
        else
        {
            // Stop pumping and it deflates; reaching zero shakes the harpoon loose.
            v.Inflation -= InflateDecayPerSec * dt;
            if (v.Inflation <= 0f) { v.Inflation = 0f; DetachHarpoon(); }
        }
    }

    void DetachHarpoon()
    {
        if (Harpoon.Victim is { } v) { v.Pinned = false; v.Inflation = 0f; }
        Harpoon.Victim = null;
        Harpoon.State = HarpoonState.Retracting;
    }

    void BurstEnemy(Enemy e)
    {
        e.Alive = false;
        e.BurstTimer = BurstHoldTime;
        Score += EnemyScore(e.Kind, Field.StratumAtWorld(e.Pos.Y));
        EmitExplosion(e.Pos, 16, EnemyColor(e.Kind));
        if (Mode == GameMode.Playing) AudioEngine.PlayBurst();
    }

    // --- Enemies (two-mode AI) ----------------------------------------------
    void SpawnEnemy(EnemyKind kind, Vec2 pos)
    {
        if (Enemies.Count >= LiveEnemyCap) return;
        Enemies.Add(new Enemy
        {
            Kind = kind,
            Pos = pos,
            SpawnPos = pos,
            Radius = EnemyRadius,
            Speed = (kind == EnemyKind.Nohu ? NohuSpeed : UhaneSpeed) * _speedScale,
            Wobble = (float)_rng.NextDouble() * MathF.Tau,
            GhostCheckTimer = GhostCheckInterval + (float)_rng.NextDouble() * GhostCheckJitter,
        });
    }

    void UpdateEnemies(float dt)
    {
        foreach (var e in Enemies)
        {
            if (!e.Alive) continue;

            // Harpooned: anchored, no AI. It only deflates or bursts.
            if (e.Pinned) continue;

            if (e.Mode == EnemyMode.Phasing) StepPhasing(e, dt);
            else                             StepTunnel(e, dt);
        }
    }

    // Mode 1 — flow-field chase through the tunnel network.
    void StepTunnel(Enemy e, float dt)
    {
        MaybeBeginPhasing(e, dt);
        if (e.Mode == EnemyMode.Phasing) return;

        Vec2 dir = Pathing.FlowDir(e.Pos);
        if (dir.X == 0f && dir.Y == 0f)
        {
            // No downhill neighbour: mill about rather than freeze.
            e.Wobble += dt * 2f;
            dir = new Vec2(MathF.Cos(e.Wobble), MathF.Sin(e.Wobble));
        }
        dir = dir.Normalized();

        MoveWithCorridorAssist(ref e.Pos, e.Radius, dir * e.Speed, dt, digger: false);
    }

    // Mode 2 — flattened, phasing straight through the dirt. Deliberately does NOT
    // use the flow field or MoveCircle: it ignores terrain entirely, which is the
    // whole point. Only Uhane can do this.
    void StepPhasing(Enemy e, float dt)
    {
        e.PhaseElapsed += dt;

        var toDigger = Digger.Pos - e.Pos;
        if (toDigger.Length > 0.001f)
            e.Pos += toDigger.Normalized() * GhostSpeed * dt;

        // Rematerialise as soon as it reaches open ground (after a minimum so it
        // can't pop straight back out of the cell it entered from). Open ground
        // means a carved tunnel — Sky is solid scenery above the field, not a
        // place anything can stand.
        if (e.PhaseElapsed < GhostMinDuration) return;

        var (c, r) = Field.WorldToCell(e.Pos);
        if (!Field.InBounds(c, r)) return;
        if (Field[c, r] != Tile.Tunnel) return;

        e.Mode = EnemyMode.Tunnel;
        e.PhaseElapsed = 0f;
        e.GhostCheckTimer = GhostCheckInterval + (float)_rng.NextDouble() * GhostCheckJitter;
        e.Pos = Field.CellCenter(c, r);   // snap so it re-enters the network aligned
        EmitSpark(e.Pos, EnemyColor(e.Kind));
    }

    // Decide whether an Uhane gives up on the tunnels. Two triggers: no tunnel
    // route to the digger at all (the flood never reached this cell), or a jittered
    // timer while the digger is far away.
    void MaybeBeginPhasing(Enemy e, float dt)
    {
        if (e.Kind != EnemyKind.Uhane) return;

        e.GhostCheckTimer -= dt;
        float dist = (Digger.Pos - e.Pos).Length;

        bool stranded = !Pathing.Reachable(e.Pos);
        bool bored    = e.GhostCheckTimer <= 0f && dist > GhostTriggerDistance;

        if (!stranded && !bored) return;

        e.Mode = EnemyMode.Phasing;
        e.PhaseElapsed = 0f;
        e.GhostCheckTimer = GhostCheckInterval + (float)_rng.NextDouble() * GhostCheckJitter;
        if (Mode == GameMode.Playing) AudioEngine.PlayPhase();
    }

    // Hard body separation, as Koa's ResolveCrowding. Phasing enemies are excluded
    // — they are not in the world's collision space while inside the dirt.
    void ResolveCrowding()
    {
        int n = Enemies.Count;
        for (int i = 0; i < n; i++)
        {
            var a = Enemies[i];
            if (!a.Alive || a.Mode == EnemyMode.Phasing || a.Pinned) continue;
            for (int j = i + 1; j < n; j++)
            {
                var b = Enemies[j];
                if (!b.Alive || b.Mode == EnemyMode.Phasing || b.Pinned) continue;
                float dx = b.Pos.X - a.Pos.X, dy = b.Pos.Y - a.Pos.Y;
                float min = a.Radius + b.Radius;
                float d2 = dx * dx + dy * dy;
                if (d2 >= min * min) continue;
                float d = MathF.Sqrt(d2);
                float nx, ny;
                if (d < 1e-3f) { nx = 1f; ny = 0f; d = 0f; }
                else           { nx = dx / d; ny = dy / d; }
                float half = (min - d) * 0.5f;
                Field.MoveEnemy(ref a.Pos, a.Radius, -nx * half, -ny * half);
                Field.MoveEnemy(ref b.Pos, b.Radius,  nx * half,  ny * half);
            }
        }
    }

    // --- Boulders (gravity applied to terrain) ------------------------------
    void UpdateBoulders(float dt)
    {
        foreach (var b in Boulders)
        {
            if (!b.Alive) continue;
            switch (b.State)
            {
                case BoulderState.Settled:
                    if (!HasSupport(b))
                    {
                        b.State = BoulderState.Wobbling;
                        b.StateTimer = BoulderWobbleDelay;
                        if (Mode == GameMode.Playing) AudioEngine.PlayRockWobble();
                    }
                    break;

                case BoulderState.Wobbling:
                    b.StateTimer -= dt;
                    if (b.StateTimer <= 0f)
                    {
                        b.State = BoulderState.Falling;
                        b.Vel = Vec2.Zero;
                        b.Crushed = 0;
                        // It is no longer a wall — from here it crushes instead. Free
                        // its cell, and leave the void it tore out of the earth behind
                        // as a usable passage.
                        Field.SetBoulderCell(b.Col, b.Row, false);
                        if (Field[b.Col, b.Row] == Tile.Dirt) Field[b.Col, b.Row] = Tile.Tunnel;
                        if (Mode == GameMode.Playing) AudioEngine.PlayRockFall();
                    }
                    // A boulder can regain support if the player refills... it can't,
                    // but it CAN be re-supported by another boulder landing beneath.
                    else if (HasSupport(b)) b.State = BoulderState.Settled;
                    break;

                case BoulderState.Falling:
                    StepFallingBoulder(b, dt);
                    break;

                case BoulderState.Shattering:
                    b.StateTimer -= dt;
                    if (b.StateTimer <= 0f) b.Alive = false;
                    break;
            }
        }
    }

    // Support is the cell directly beneath the boulder's centre: dirt or bedrock
    // holds it up, a carved tunnel or open sky does not.
    bool HasSupport(Boulder b)
    {
        var (c, r) = Field.WorldToCell(b.Pos.X, b.Pos.Y + b.Radius + 2f);
        if (!Field.InBounds(c, r)) return true;      // world floor
        var t = Field[c, r];
        if (t == Tile.Dirt || t == Tile.Rock) return true;

        // Another settled boulder underneath also holds it.
        foreach (var o in Boulders)
        {
            if (o == b || !o.Alive || o.State == BoulderState.Falling) continue;
            if (MathF.Abs(o.Pos.X - b.Pos.X) < b.Radius &&
                o.Pos.Y > b.Pos.Y && o.Pos.Y - b.Pos.Y < b.Radius * 2.4f)
                return true;
        }
        return false;
    }

    void StepFallingBoulder(Boulder b, float dt)
    {
        b.Vel.Y = MathF.Min(BoulderMaxFallSpeed, b.Vel.Y + BoulderGravity * dt);
        b.Pos.Y += b.Vel.Y * dt;

        // A falling boulder carves its way down — same edit path as the digger, so
        // the flow field follows it.
        Field.Carve(b.Pos, b.Radius * 0.7f);

        // Crush anything it passes through.
        foreach (var e in Enemies)
        {
            if (!e.Alive || e.BurstTimer > 0f) continue;
            if (!CircleHit(b.Pos, b.Radius, e.Pos, e.Radius)) continue;
            e.Alive = false;
            if (Harpoon.Victim == e) DetachHarpoon();
            b.Crushed++;
            Score += CrushScore(b.Crushed);
            EmitExplosion(e.Pos, 14, EnemyColor(e.Kind));
            if (Mode == GameMode.Playing) AudioEngine.PlayBurst();
        }

        if (Digger.Alive && Digger.RespawnTimer <= 0f &&
            CircleHit(b.Pos, b.Radius, Digger.Pos, Digger.Radius))
            KillDigger();

        // Landed?
        var (c, r) = Field.WorldToCell(b.Pos.X, b.Pos.Y + b.Radius + 1f);
        bool onFloor = !Field.InBounds(c, r);
        if (onFloor || Field[c, r] == Tile.Dirt || Field[c, r] == Tile.Rock)
        {
            b.State = BoulderState.Shattering;
            b.StateTimer = BoulderShatterTime;
            EmitExplosion(b.Pos, 18, 0xFF9A7A55);
            if (Mode == GameMode.Playing) AudioEngine.PlayRockShatter();
        }
    }

    // --- Interactions -------------------------------------------------------
    void HandleEnemyContact()
    {
        if (!Digger.Alive || Digger.RespawnTimer > 0f) return;
        foreach (var e in Enemies)
        {
            if (!e.Alive || e.BurstTimer > 0f) continue;
            // A harpooned enemy is neutralised for as long as it stays on the
            // hook — including the frame it is struck, before the first pump has
            // landed. (Gating this on Inflation > 0 left a one-frame hole where a
            // point-blank hit still killed you, and reopened it every time the
            // inflation decayed back to zero just before detaching.) Detaching
            // clears Pinned, so it turns lethal again the instant it works loose.
            if (e.Pinned) continue;
            // Anything else that is un-inflated kills on contact — including a
            // phasing one, which is fair because GhostSpeed is under a third of
            // WalkSpeed and it must rematerialise the moment it reaches open ground.
            if (CircleHit(e.Pos, e.Radius, Digger.Pos, Digger.Radius)) { KillDigger(); return; }
        }
    }

    void KillDigger()
    {
        EmitExplosion(Digger.Pos, 40, 0xFFFFAA33);
        if (Mode == GameMode.Playing) AudioEngine.PlayDeath();
        DetachHarpoon();
        Harpoon.Reset();
        Lives--;

        if (Lives <= 0)
        {
            Digger.Alive = false;
            if (Score > HighScore) { HighScore = Score; HighScoreStore.Save(HighScore); }
            Mode = (Mode == GameMode.Attract) ? GameMode.Title : GameMode.GameOver;
            return;
        }
        Digger.RespawnTimer = RespawnDelay;
    }

    // The field KEEPS its tunnels across a death — you don't lose your excavation.
    // Only the digger and the monsters go back to their marks.
    void RespawnDigger()
    {
        Digger.Pos = _diggerSpawn;
        Digger.Vel = Vec2.Zero;
        Digger.Facing = Facing.Right;
        Digger.Alive = true;
        foreach (var e in Enemies)
        {
            e.Pos = e.SpawnPos;
            e.Mode = EnemyMode.Tunnel;
            e.Inflation = 0f;
            e.Pinned = false;
            e.PhaseElapsed = 0f;
        }
        Pathing.Rebuild(Digger.Pos);
        Camera.Snap(Digger.Pos.X, Digger.Pos.Y);
    }

    void CheckFieldClear()
    {
        foreach (var e in Enemies) if (e.Alive) return;
        Score += LevelClearBonus * Level;
        LevelClearFlash = 1.2f;
        if (Mode == GameMode.Playing) AudioEngine.PlayLevelClear();
        LoadLevel(Level + 1);
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
    //
    // Modelled on Koa's RunAutoHero: commit to a cardinal heading, re-pick on timer
    // expiry / stuck / blocked, with a momentum bias and tie-break jitter. Two
    // Eli-specific changes: BlockedAhead tests the DIGGER predicate, so dirt is not
    // blocking (the bot digs its own tunnels), and the heading score biases toward
    // the nearest ENEMY rather than a pickup.
    void RunAutoDigger(float dt)
    {
        float moved = (Digger.Pos - _autoLastPos).Length;
        bool stuck = moved < 0.25f;
        _autoTimer -= dt;

        bool headingBlocked = _autoHeading.Length < 0.01f || BlockedAhead(_autoHeading);

        // Nearest enemy drives both aim and the fire decision.
        Enemy? target = null;
        float best = float.MaxValue;
        foreach (var e in Enemies)
        {
            if (!e.Alive || e.Mode == EnemyMode.Phasing) continue;
            float d = (e.Pos - Digger.Pos).Length;
            if (d < best) { best = d; target = e; }
        }

        // Stand still to work the pump once something is on the hook.
        if (Harpoon.State == HarpoonState.Attached)
        {
            _moveIntent = Vec2.Zero;
            _autoFire = true;
            _autoLastPos = Digger.Pos;
            return;
        }

        // Aim and fire when a target is close, roughly along the facing axis, AND
        // actually shootable — the harpoon stops at dirt, so a monster lined up
        // behind an undug wall is not a shot, it is a waste of time.
        _autoFire = false;
        bool shootable = false;
        if (target is not null && best < AutoFireRange)
        {
            var f = Facings.ToVec(Digger.Facing);
            var to = (target.Pos - Digger.Pos).Normalized();
            bool aligned = f.X * to.X + f.Y * to.Y > 0.7f;
            shootable = aligned && best < HarpoonMaxLength && HarpoonPathClear(target.Pos);
            if (aligned) _autoFire = true;
        }

        // Standoff: an un-inflated monster kills on contact, so once one is lined
        // up and genuinely shootable the bot holds its ground and fires instead of
        // walking into it. Without this it charges the nearest target and throws
        // away all three lives in seconds, which makes for a poor demo loop.
        //
        // Two guards keep the halt from becoming a trap. It only stands still for a
        // shot it can actually land (see `shootable` — an earlier version stopped
        // for anything merely in the right *direction*, so it would stall forever
        // firing into a dirt wall). And it never stands still with a ghost closing,
        // because phasing monsters are excluded from targeting yet still kill on
        // contact — standing put was how the bot got picked off.
        if (shootable && !GhostClosing())
        {
            _moveIntent = Vec2.Zero;
            _autoLastPos = Digger.Pos;
            return;
        }

        if (_autoTimer <= 0f || stuck || headingBlocked)
        {
            _autoHeading = PickAutoHeading(target);
            _autoTimer = 0.6f + (float)_rng.NextDouble() * 1.0f;
        }
        _moveIntent = _autoHeading;
        _autoLastPos = Digger.Pos;
    }

    // Can the harpoon actually reach `to` from the digger? Walks the ray in
    // half-cell steps and fails on the first cell that would stop the tip.
    bool HarpoonPathClear(Vec2 to)
    {
        var delta = to - Digger.Pos;
        float dist = delta.Length;
        if (dist < 0.01f) return true;
        var step = delta * (Field.CellSize * 0.5f / dist);
        int steps = (int)(dist / (Field.CellSize * 0.5f));
        var p = Digger.Pos;
        for (int i = 0; i < steps; i++)
        {
            p += step;
            var (c, r) = Field.WorldToCell(p);
            if (Field.IsBlockedForHarpoon(c, r)) return false;
        }
        return true;
    }

    // Is a phasing monster near enough that holding still would be fatal? Ghosts
    // are not valid harpoon targets but they still kill on contact, so the bot has
    // to keep moving when one is closing rather than stand and shoot.
    bool GhostClosing()
    {
        float danger = (Digger.Radius + EnemyRadius) * 3f;
        foreach (var e in Enemies)
        {
            if (!e.Alive || e.Mode != EnemyMode.Phasing) continue;
            if ((e.Pos - Digger.Pos).Length < danger) return true;
        }
        return false;
    }

    static readonly Vec2[] _cardinals =
    {
        new Vec2( 1f, 0f), new Vec2(-1f, 0f), new Vec2(0f,  1f), new Vec2(0f, -1f),
    };

    // True if a step in `dir` would run the digger into BEDROCK (dirt is fine —
    // the bot digs through it).
    bool BlockedAhead(Vec2 dir)
    {
        float probe = Digger.Radius + Field.CellSize * 0.6f;
        return Field.IsBlockedForDiggerAt(Digger.Pos.X + dir.X * probe, Digger.Pos.Y + dir.Y * probe);
    }

    Vec2 PickAutoHeading(Enemy? target)
    {
        Vec2 toTarget = Vec2.Zero;
        if (target is not null) toTarget = (target.Pos - Digger.Pos).Normalized();

        Vec2 bestDir = Vec2.Zero;
        float bestScore = float.NegativeInfinity;
        foreach (var dir in _cardinals)
        {
            if (BlockedAhead(dir)) continue;
            float score = (float)_rng.NextDouble() * 0.3f;
            if (toTarget.Length > 0f) score += dir.X * toTarget.X + dir.Y * toTarget.Y;
            if (dir.X == _autoHeading.X && dir.Y == _autoHeading.Y) score += 0.5f;
            // Prefer an existing tunnel over fresh digging so the demo keeps moving.
            float probe = Digger.Radius + Field.CellSize * 0.6f;
            if (!Field.IsDirtAt(Digger.Pos.X + dir.X * probe, Digger.Pos.Y + dir.Y * probe))
                score += 0.35f;
            if (score > bestScore) { bestScore = score; bestDir = dir; }
        }
        if (bestDir.Length > 0f) return bestDir;
        return new Vec2(-_autoHeading.X, -_autoHeading.Y);
    }

    // --- Helpers ------------------------------------------------------------
    static bool CircleHit(Vec2 a, float ra, Vec2 b, float rb)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        float rr = ra + rb;
        return dx * dx + dy * dy <= rr * rr;
    }

    // Score by DEPTH — the strata are a scoring mechanic, not decoration.
    public static int EnemyScore(EnemyKind k, int stratum)
    {
        stratum = Math.Clamp(stratum, 0, Field.StrataCount - 1);
        int uhane = 200 + 100 * stratum;              // 200 / 300 / 400 / 500
        return k == EnemyKind.Nohu ? uhane * 2 : uhane;
    }

    // Chain bonus for enemies crushed by a single falling boulder.
    public static int CrushScore(int chainIndex) => chainIndex switch
    {
        1 => 1000,
        2 => 2500,
        3 => 4000,
        4 => 6000,
        _ => 8000,
    };

    public static uint EnemyColor(EnemyKind k) => k switch
    {
        EnemyKind.Uhane => 0xFFFF6655,   // pooka red
        EnemyKind.Nohu  => 0xFF66DD77,   // scaled green
        _               => 0xFFFF6655,
    };
}
