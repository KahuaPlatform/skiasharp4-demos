using System;
using System.Collections.Generic;

namespace Heiau.Game;

/// <summary>
/// The per-frame brain for Heiau, a Star-Castle ring shooter (900×900 square,
/// action centered on the turret). Runs ship inertia + screen wrap, ring rotation,
/// turret aim/fire, bullet-vs-segment collisions, Spark mines, scoring, per-level
/// ring rebuilds, the mode state machine, and the attract autopilot.
/// </summary>
public sealed class GameWorld
{
    // --- World ---
    public const float WorldW = 900f;
    public const float WorldH = 900f;
    public float Width  => WorldW;
    public float Height => WorldH;
    public Vec2 Center => new(WorldW * 0.5f, WorldH * 0.5f);

    // --- Physics ---
    public const float ShipThrustAccel   = 280f;
    public const float ShipMaxSpeed      = 320f;
    public const float ShipDrag          = 0.55f;     // exponential damping per second
    public const float ShipRotateSpeed   = 3.4f;      // rad/sec while held
    public const float PlayerBulletSpeed = 540f;
    public const float PlayerBulletLife  = 1.2f;
    public const int   PlayerBulletCap   = 4;
    public const float TurretBulletSpeed = 240f;
    public const float TurretBulletLife  = 3.5f;
    public const float TurretBulletTurnRate = 2.6f;   // rad/sec — homing rate for turret bullets
    // Sparx — homing energy mines that emerge from destroyed ring segments.
    public const float SparkSpeed         = 95f;
    public const float SparkTurnRate      = 1.6f;
    public const float SparkLife          = 12f;
    public const float SparkCollisionR    = 8f;
    public const float SparkSpawnChanceL1 = 0.35f;   // chance per segment kill at level 1
    public const float SparkSpawnChanceLN = 0.85f;   // saturates around level 8+
    public const int   SparkScore         = 75;
    public const float TurretFireBaseInterval = 1.8f;
    public const float TurretRadius      = 28f;
    public const float ShipCollisionRadius = 9f;
    public const float TurretFireSafeDist  = 90f; // turret won't fire when player is this close (mercy)

    // --- State ---
    public GameMode Mode = GameMode.Title;
    public int Level = 1;
    public int Score;
    public int HighScore;
    public int LivesLeft = 3;
    public string PlacardText = "";
    public float PlacardTimer;

    public Ship Ship = new();
    public Turret Turret = new();
    public Ring[] Rings = Array.Empty<Ring>();
    public List<Bullet>     Bullets   = new();
    public List<Spark>      Sparks    = new();
    public List<Particle>   Particles = new();
    public List<ScorePopup> Popups    = new();

    public float TitleIdleTimer;

    // Input flags driven by MainPage.
    public bool RotateLeft, RotateRight, Thrust, Firing;

    static readonly Random _rng = new();

    static readonly HighScoreStore HighScoreStore = new("Heiau");

    public GameWorld()
    {
        HighScore = HighScoreStore.Load();
        BuildLevel(1);
    }

    /// <summary>No-op: world coords are fixed and the renderer letterboxes.</summary>
    public void Resize(float w, float h) { /* fixed world coords */ }

    void BuildLevel(int level)
    {
        Level = level;
        Rings = RingGeometry.BuildRings(level);
        Turret = new Turret { Position = Center, Alive = true, FireCooldown = 1.5f };
        ResetShipToSpawn();
    }

    void ResetShipToSpawn()
    {
        // Spawn at outer edge, away from rings, facing toward center.
        Ship = new Ship
        {
            Position = new Vec2(WorldW * 0.5f, WorldH * 0.5f + 320f),
            Velocity = Vec2.Zero,
            AngleRadians = -MathF.PI / 2f, // pointing up (toward center)
            Invuln = 2.0f,
            SpawnAnim = 0f,
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
        Bullets.Clear();
        Sparks.Clear();
        Particles.Clear();
        Popups.Clear();
        ShowPlacard($"LEVEL {Level}", 1.6f);
    }

    /// <summary>Starts the self-playing attract demo (autopilot, near-infinite lives).</summary>
    public void StartAttract()
    {
        StartGame();
        Mode = GameMode.Attract;
        LivesLeft = 9999;
    }

    /// <summary>Returns to the title screen and clears the playfield.</summary>
    public void ReturnToTitle()
    {
        Mode = GameMode.Title;
        TitleIdleTimer = 0f;
        Bullets.Clear();
        Sparks.Clear();
        Particles.Clear();
        Popups.Clear();
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
        UpdateShip(dt);
        UpdateBullets(dt);
        UpdateRings(dt);
        UpdateTurret(dt);
        UpdateSparks(dt);
        CheckShipRingCollision();
        UpdateParticles(dt);
        UpdatePopups(dt);
        CheckTurretKill();
    }

    void SpawnSparkFromSegment(Ring r, int seg, Vec2 spawnPos)
    {
        // Initial outward velocity from world center — gives a brief "ejected
        // from the ring" feel before the homing kicks in.
        float dx = spawnPos.X - Center.X;
        float dy = spawnPos.Y - Center.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) { dx = 1f; dy = 0f; len = 1f; }
        Sparks.Add(new Spark
        {
            Position = spawnPos,
            Velocity = new Vec2(dx / len * SparkSpeed * 0.6f, dy / len * SparkSpeed * 0.6f),
            Life     = SparkLife,
            Hue      = (float)_rng.NextDouble() * 360f,
        });
    }

    void UpdateSparks(float dt)
    {
        for (int i = Sparks.Count - 1; i >= 0; i--)
        {
            var s = Sparks[i];
            s.Life -= dt;
            s.Hue = (s.Hue + dt * 180f) % 360f;
            if (s.Life <= 0) { Sparks.RemoveAt(i); continue; }

            if (Ship.Alive)
            {
                float spd = s.Velocity.Length;
                if (spd < 0.001f) spd = SparkSpeed;
                float currentAng = MathF.Atan2(s.Velocity.Y, s.Velocity.X);
                float dx = Ship.Position.X - s.Position.X;
                float dy = Ship.Position.Y - s.Position.Y;
                float desired = MathF.Atan2(dy, dx);
                float diff = RingGeometry.WrapAngle(desired - currentAng);
                float maxTurn = SparkTurnRate * dt;
                float turn = MathF.Max(-maxTurn, MathF.Min(maxTurn, diff));
                float newAng = currentAng + turn;
                // Accelerate gently up to SparkSpeed so initial outward drift
                // smoothly transitions to homing pursuit.
                float newSpeed = MathF.Min(SparkSpeed, spd + 40f * dt);
                s.Velocity = new Vec2(MathF.Cos(newAng) * newSpeed, MathF.Sin(newAng) * newSpeed);
            }

            s.Position += s.Velocity * dt;

            // Screen wrap
            if (s.Position.X < 0) s.Position.X += WorldW;
            if (s.Position.X >= WorldW) s.Position.X -= WorldW;
            if (s.Position.Y < 0) s.Position.Y += WorldH;
            if (s.Position.Y >= WorldH) s.Position.Y -= WorldH;

            // Player bullet kills the spark.
            bool consumed = false;
            for (int b = Bullets.Count - 1; b >= 0; b--)
            {
                var bullet = Bullets[b];
                if (!bullet.FromPlayer) continue;
                float bx = bullet.Position.X - s.Position.X;
                float by = bullet.Position.Y - s.Position.Y;
                float rr = SparkCollisionR + 3f;
                if (bx * bx + by * by <= rr * rr)
                {
                    Score += SparkScore;
                    if (Score > HighScore) HighScore = Score;
                    Popups.Add(new ScorePopup
                    {
                        Pos = s.Position, Value = SparkScore,
                        Life = 0.8f, MaxLife = 0.8f,
                        Color = 0xFF_FFEE66,
                    });
                    EmitExplosion(s.Position, 14, 0xFF_FFEE66);
                    AudioEngine.PlayShipExplosion();
                    Bullets.RemoveAt(b);
                    Sparks.RemoveAt(i);
                    consumed = true;
                    break;
                }
            }
            if (consumed) continue;

            // Hit the player on contact.
            if (Ship.Alive && Ship.Invuln <= 0)
            {
                float dx = s.Position.X - Ship.Position.X;
                float dy = s.Position.Y - Ship.Position.Y;
                float rr = SparkCollisionR + ShipCollisionRadius;
                if (dx * dx + dy * dy <= rr * rr)
                {
                    OnShipHit();
                    Sparks.RemoveAt(i);
                }
            }
        }
    }

    // Flying into an alive ring segment kills the ship — faithful to original
    // Star Castle. The ship has to stay outside the rings; only bullets can
    // pass through. Respawn position is well outside the outer ring, so post-
    // death respawn won't immediately re-collide.
    void CheckShipRingCollision()
    {
        if (!Ship.Alive || Ship.Invuln > 0) return;
        foreach (var r in Rings)
        {
            int seg = RingGeometry.HitSegment(r, Center, Ship.Position, ShipCollisionRadius);
            if (seg >= 0)
            {
                OnShipHit();
                return;
            }
        }
    }

    void UpdateShip(float dt)
    {
        if (!Ship.Alive) return;
        if (Ship.Invuln > 0) Ship.Invuln -= dt;
        if (Ship.SpawnAnim < 1) Ship.SpawnAnim = MathF.Min(1f, Ship.SpawnAnim + dt * 3f);
        if (Ship.ShootCooldown > 0) Ship.ShootCooldown -= dt;

        if (RotateLeft)  Ship.AngleRadians -= ShipRotateSpeed * dt;
        if (RotateRight) Ship.AngleRadians += ShipRotateSpeed * dt;

        Ship.Thrusting = Thrust;
        if (Thrust)
        {
            Ship.Velocity.X += MathF.Cos(Ship.AngleRadians) * ShipThrustAccel * dt;
            Ship.Velocity.Y += MathF.Sin(Ship.AngleRadians) * ShipThrustAccel * dt;
            EmitThrustFlame(dt);
        }
        AudioState(Thrust);

        // Cap to max speed
        float spd = Ship.Velocity.Length;
        if (spd > ShipMaxSpeed) Ship.Velocity = Ship.Velocity * (ShipMaxSpeed / spd);
        // Light drag
        float dampFactor = MathF.Pow(1f - ShipDrag, dt);
        Ship.Velocity = Ship.Velocity * dampFactor;

        // Integrate
        Ship.Position += Ship.Velocity * dt;

        // Screen wrap
        if (Ship.Position.X < 0) Ship.Position.X += WorldW;
        if (Ship.Position.X >= WorldW) Ship.Position.X -= WorldW;
        if (Ship.Position.Y < 0) Ship.Position.Y += WorldH;
        if (Ship.Position.Y >= WorldH) Ship.Position.Y -= WorldH;

        // Fire
        if (Firing) TryFirePlayerBullet();
    }

    bool _prevThrustOn;
    void AudioState(bool thrusting)
    {
        if (thrusting && !_prevThrustOn) AudioEngine.StartThrust();
        if (!thrusting && _prevThrustOn) AudioEngine.StopThrust();
        _prevThrustOn = thrusting;
    }

    void EmitThrustFlame(float dt)
    {
        _flameTimer -= dt;
        if (_flameTimer > 0) return;
        _flameTimer = 0.012f;
        float nx = MathF.Cos(Ship.AngleRadians);
        float ny = MathF.Sin(Ship.AngleRadians);
        var emitPos = new Vec2(Ship.Position.X - nx * 8f, Ship.Position.Y - ny * 8f);
        for (int i = 0; i < 2; i++)
        {
            float spread = ((float)_rng.NextDouble() - 0.5f) * 0.6f;
            float dx = -nx + spread * (-ny);
            float dy = -ny + spread *  nx;
            float spd = 200f + (float)_rng.NextDouble() * 80f;
            uint color = (i & 1) == 0 ? 0xFF_FFCC33 : 0xFF_FF8833;
            Particles.Add(new Particle
            {
                Pos     = emitPos,
                Vel     = new Vec2(Ship.Velocity.X * 0.4f + dx * spd, Ship.Velocity.Y * 0.4f + dy * spd),
                Life    = 0.28f,
                MaxLife = 0.28f,
                Color   = color,
                Size    = 2.4f,
            });
        }
    }
    float _flameTimer;

    int CountPlayerBullets()
    {
        int n = 0;
        foreach (var b in Bullets) if (b.FromPlayer) n++;
        return n;
    }

    void TryFirePlayerBullet()
    {
        if (Ship.ShootCooldown > 0) return;
        if (CountPlayerBullets() >= PlayerBulletCap) return;
        float nx = MathF.Cos(Ship.AngleRadians);
        float ny = MathF.Sin(Ship.AngleRadians);
        Bullets.Add(new Bullet
        {
            Position   = new Vec2(Ship.Position.X + nx * 12f, Ship.Position.Y + ny * 12f),
            Velocity   = new Vec2(nx * PlayerBulletSpeed + Ship.Velocity.X * 0.3f,
                                   ny * PlayerBulletSpeed + Ship.Velocity.Y * 0.3f),
            FromPlayer = true,
            Life       = PlayerBulletLife,
        });
        Ship.ShootCooldown = 0.18f;
        AudioEngine.PlayShoot();
    }

    void UpdateBullets(float dt)
    {
        for (int i = Bullets.Count - 1; i >= 0; i--)
        {
            var b = Bullets[i];

            // Turret bullets gently steer toward the player — matches the
            // original Star Castle "Sparx" that homed in on the ship.
            if (!b.FromPlayer && Ship.Alive)
            {
                float spd = b.Velocity.Length;
                if (spd > 0.001f)
                {
                    float currentAng = MathF.Atan2(b.Velocity.Y, b.Velocity.X);
                    float dx = Ship.Position.X - b.Position.X;
                    float dy = Ship.Position.Y - b.Position.Y;
                    float desired = MathF.Atan2(dy, dx);
                    float diff = RingGeometry.WrapAngle(desired - currentAng);
                    float maxTurn = TurretBulletTurnRate * dt;
                    float turn = MathF.Max(-maxTurn, MathF.Min(maxTurn, diff));
                    float newAng = currentAng + turn;
                    b.Velocity = new Vec2(MathF.Cos(newAng) * spd, MathF.Sin(newAng) * spd);
                }
            }

            Vec2 prevPos = b.Position;
            b.Position += b.Velocity * dt;
            b.Life -= dt;
            if (b.Life <= 0) { Bullets.RemoveAt(i); continue; }

            // Screen wrap (applied to current position only — swept check below
            // assumes prev→curr is a straight line, which fails across a wrap.
            // Player bullets that wrap-and-collide in the same frame are rare;
            // the worst case is one missed hit on the wrap frame).
            if (b.Position.X < 0) b.Position.X += WorldW;
            if (b.Position.X >= WorldW) b.Position.X -= WorldW;
            if (b.Position.Y < 0) b.Position.Y += WorldH;
            if (b.Position.Y >= WorldH) b.Position.Y -= WorldH;

            // Player bullet: swept collision along prev→curr so high-speed bullets
            // don't tunnel through the thin ring band. 4 substeps gives ~2.3 px
            // per sample at PlayerBulletSpeed/60fps — well inside the band.
            if (b.FromPlayer)
            {
                bool consumed = false;
                const int Steps = 4;
                for (int step = 1; step <= Steps && !consumed; step++)
                {
                    float t = (float)step / Steps;
                    var samp = new Vec2(prevPos.X + (b.Position.X - prevPos.X) * t,
                                        prevPos.Y + (b.Position.Y - prevPos.Y) * t);
                    foreach (var r in OrderedRingsByRadiusDesc())
                    {
                        int seg = RingGeometry.HitSegment(r, Center, samp);
                        if (seg >= 0)
                        {
                            DestroySegment(r, seg);
                            consumed = true;
                            break;
                        }
                    }
                    if (!consumed && Turret.Alive)
                    {
                        float dx = samp.X - Turret.Position.X;
                        float dy = samp.Y - Turret.Position.Y;
                        if (dx * dx + dy * dy <= TurretRadius * TurretRadius)
                        {
                            OnTurretHit();
                            consumed = true;
                        }
                    }
                }
                if (consumed) { Bullets.RemoveAt(i); continue; }
            }
            else
            {
                // Enemy bullet: rings absorb their own turret's shots too
                // (faithful to original — turret fire can break its own rings).
                foreach (var r in OrderedRingsByRadiusDesc())
                {
                    int seg = RingGeometry.HitSegment(r, Center, b.Position);
                    if (seg >= 0)
                    {
                        DestroySegment(r, seg, scoreForPlayer: false);
                        Bullets.RemoveAt(i);
                        goto nextBullet;
                    }
                }

                // Hit the player?
                if (Ship.Alive && Ship.Invuln <= 0)
                {
                    float dx = b.Position.X - Ship.Position.X;
                    float dy = b.Position.Y - Ship.Position.Y;
                    if (dx * dx + dy * dy <= ShipCollisionRadius * ShipCollisionRadius)
                    {
                        OnShipHit();
                        Bullets.RemoveAt(i);
                        continue;
                    }
                }
                nextBullet:;
            }
        }
    }

    Ring[] OrderedRingsByRadiusDesc()
    {
        if (_orderedCache == null || _orderedCache.Length != Rings.Length)
        {
            _orderedCache = new Ring[Rings.Length];
            Array.Copy(Rings, _orderedCache, Rings.Length);
            Array.Sort(_orderedCache, (a, b) => b.Radius.CompareTo(a.Radius));
        }
        return _orderedCache;
    }
    Ring[]? _orderedCache;

    void DestroySegment(Ring r, int seg, bool scoreForPlayer = true)
    {
        if (!r.IsAlive(seg)) return;
        r.Health[seg]--;
        r.HitFlash[seg] = 1f;
        AudioEngine.PlayRingHit();

        if (r.Health[seg] > 0)
        {
            // Partial hit — small spark, no score (so it doesn't farm-reward
            // chipping at infinite rings).
            EmitExplosion(SegmentMidPoint(r, seg), 4, 0xFF_99CCFF);
            return;
        }

        // Fully destroyed
        r.AliveCount--;
        var midPt = SegmentMidPoint(r, seg);
        if (scoreForPlayer)
        {
            int v = 10;
            Score += v;
            if (Score > HighScore) HighScore = Score;
            Popups.Add(new ScorePopup
            {
                Pos = midPt,
                Value = v,
                Life = 0.7f,
                MaxLife = 0.7f,
                Color = 0xFF_55FF77,
            });

            // Spawn a Spark with a level-scaled probability. Saturates near
            // level 8 so the upper-level cap stays at ~85% per segment kill.
            float t = MathF.Min(1f, (Level - 1) / 7f);
            float chance = SparkSpawnChanceL1 + (SparkSpawnChanceLN - SparkSpawnChanceL1) * t;
            if (_rng.NextDouble() < chance)
            {
                SpawnSparkFromSegment(r, seg, midPt);
            }
        }
        EmitExplosion(midPt, 8, 0xFF_99CCFF);
    }

    Vec2 SegmentMidPoint(Ring r, int seg)
    {
        float segWidth = MathF.Tau / r.Segments;
        float ang = r.Rotation + segWidth * seg;
        return new Vec2(Center.X + MathF.Cos(ang) * r.Radius,
                        Center.Y + MathF.Sin(ang) * r.Radius);
    }

    void UpdateRings(float dt)
    {
        for (int i = 0; i < Rings.Length; i++)
        {
            var r = Rings[i];
            r.Rotation = (r.Rotation + r.AngularSpeed * dt) % MathF.Tau;
            // Decay per-segment hit flashes (used by the renderer for a brief
            // brightness pop when a segment takes damage but isn't destroyed).
            for (int s = 0; s < r.HitFlash.Length; s++)
            {
                if (r.HitFlash[s] > 0)
                    r.HitFlash[s] = MathF.Max(0f, r.HitFlash[s] - dt * 4f);
            }
        }
    }

    void UpdateTurret(float dt)
    {
        if (!Turret.Alive) return;

        // Barrel tracks the player with a small lag (the original wiggles a bit too).
        float dx = Ship.Position.X - Turret.Position.X;
        float dy = Ship.Position.Y - Turret.Position.Y;
        float desired = MathF.Atan2(dy, dx);
        float diff = RingGeometry.WrapAngle(desired - Turret.BarrelAngle);
        Turret.BarrelAngle += diff * MathF.Min(1f, dt * 5f);
        Turret.Spin += dt * 0.4f;

        // Fire when cooldown elapses, the player isn't right on top of us, AND
        // there's a clear line of sight (no alive ring segment blocks the
        // bullet's path). The turret has to wait until the player has chipped
        // an alignment hole — much closer to the original game's feel.
        Turret.FireCooldown -= dt;
        if (Turret.FireCooldown <= 0)
        {
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > TurretFireSafeDist && HasClearShotToPlayer())
            {
                FireTurretBullet();
                Turret.FireCooldown = MathF.Max(0.5f, TurretFireBaseInterval - Level * 0.15f);
            }
            else
            {
                // Re-check soon — rings rotate fast enough that openings come and go.
                Turret.FireCooldown = 0.15f;
            }
        }
    }

    // True if the straight line from the turret to the player crosses no alive
    // ring segments. The turret sits at the world center, so each ring's
    // intersection with the ray is at the same angle (atan2 dy/dx) — we just
    // check whether that angle hits an alive segment in any ring.
    bool HasClearShotToPlayer()
    {
        float dx = Ship.Position.X - Turret.Position.X;
        float dy = Ship.Position.Y - Turret.Position.Y;
        float playerDist = MathF.Sqrt(dx * dx + dy * dy);
        if (playerDist < 1f) return true;
        float angleToPlayer = MathF.Atan2(dy, dx);
        foreach (var r in Rings)
        {
            // The ring only obstructs if the player is OUTSIDE its radius —
            // otherwise the bullet hasn't crossed that ring on its way out.
            if (r.Radius >= playerDist) continue;
            int seg = SegmentAtAngle(r, angleToPlayer);
            if (seg >= 0 && r.IsAlive(seg)) return false;
        }
        return true;
    }

    static int SegmentAtAngle(Ring ring, float worldAngle)
    {
        float angle = worldAngle - ring.Rotation;
        float segWidth = MathF.Tau / ring.Segments;
        float normAngle = (angle % MathF.Tau + MathF.Tau) % MathF.Tau;
        int k = (int)MathF.Round(normAngle / segWidth) % ring.Segments;
        float segCenter = k * segWidth;
        float delta = RingGeometry.WrapAngle(normAngle - segCenter);
        if (MathF.Abs(delta) > RingGeometry.SegmentHalfArc * 0.92f) return -1;
        return k;
    }

    void FireTurretBullet()
    {
        float nx = MathF.Cos(Turret.BarrelAngle);
        float ny = MathF.Sin(Turret.BarrelAngle);
        Bullets.Add(new Bullet
        {
            Position   = new Vec2(Turret.Position.X + nx * 22f, Turret.Position.Y + ny * 22f),
            Velocity   = new Vec2(nx * TurretBulletSpeed, ny * TurretBulletSpeed),
            FromPlayer = false,
            Life       = TurretBulletLife,
        });
        AudioEngine.PlayTurretFire();
    }

    void CheckTurretKill()
    {
        if (!Turret.Alive) return;
        // Turret is only killable when all three rings have any open path to it —
        // i.e. when the player bullet successfully reached the center. We already
        // checked turret collision in UpdateBullets — this method exists as a hook
        // for game-end conditions and ring-completion detection.

        // Did the player wipe out all rings? Score bonus.
        bool allRingsGone = true;
        foreach (var r in Rings) if (r.AliveCount > 0) { allRingsGone = false; break; }
        if (allRingsGone && !_ringsBonusAwarded)
        {
            _ringsBonusAwarded = true;
            Score += 500;
            ShowPlacard("RINGS CLEARED  +500", 1.4f);
        }
    }
    bool _ringsBonusAwarded;

    void OnTurretHit()
    {
        Turret.Alive = false;
        Score += 1000;
        if (Score > HighScore) HighScore = Score;
        EmitExplosion(Turret.Position, 60, 0xFF_FFCC33);
        AudioEngine.PlayTurretKill();
        AudioEngine.StopThrust();

        // Brief celebratory pause then advance.
        _levelCleanupTimer = 1.8f;
        _ringsBonusAwarded = false;
    }
    float _levelCleanupTimer;

    void OnShipHit()
    {
        if (Mode == GameMode.Attract) { Ship.Invuln = 1.0f; return; }
        Ship.Alive = false;
        EmitExplosion(Ship.Position, 40, 0xFF_33F8FF);
        AudioEngine.StopThrust();
        AudioEngine.PlayShipExplosion();
        LivesLeft--;
        if (LivesLeft <= 0)
        {
            Mode = GameMode.GameOver;
            HighScoreStore.Save(HighScore);
        }
        else
        {
            // Quick respawn after a moment.
            _respawnTimer = 1.4f;
        }
    }
    float _respawnTimer;

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
        // Handle respawn / level advance timing on the same tick as particles
        // so the explosion settles before the next state.
        if (!Ship.Alive && Mode != GameMode.GameOver)
        {
            _respawnTimer -= dt;
            if (_respawnTimer <= 0)
            {
                ResetShipToSpawn();
                Ship.Alive = true;
            }
        }
        if (!Turret.Alive)
        {
            _levelCleanupTimer -= dt;
            if (_levelCleanupTimer <= 0)
            {
                AdvanceLevel();
            }
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

    void AdvanceLevel()
    {
        BuildLevel(Level + 1);
        _orderedCache = null;
        Bullets.Clear();
        Sparks.Clear();
        // Mode (Playing or Attract) is preserved naturally — we never changed it
        // during the post-turret-kill pause.
        ShowPlacard($"LEVEL {Level}", 1.4f);
    }

    void EmitExplosion(Vec2 origin, int count, uint color)
    {
        for (int i = 0; i < count; i++)
        {
            double ang = _rng.NextDouble() * Math.PI * 2.0;
            float spd = 80f + (float)_rng.NextDouble() * 280f;
            Particles.Add(new Particle
            {
                Pos     = origin,
                Vel     = new Vec2((float)Math.Cos(ang) * spd, (float)Math.Sin(ang) * spd),
                Life    = 0.9f,
                MaxLife = 0.9f,
                Color   = color,
                Size    = 2.2f + (float)_rng.NextDouble() * 2.0f,
            });
        }
    }

    // --- Attract AI: simple — orbit, fire constantly, drift toward gaps ---
    float _attractInputTimer;
    void UpdateAttractAI(float dt)
    {
        _attractInputTimer -= dt;
        Firing = true; // hold fire
        if (_attractInputTimer > 0) return;
        _attractInputTimer = 0.18f + (float)_rng.NextDouble() * 0.12f;

        // Heuristic: point roughly at the turret, occasionally thrust to stay in orbit.
        float dx = Turret.Position.X - Ship.Position.X;
        float dy = Turret.Position.Y - Ship.Position.Y;
        float desiredAngle = MathF.Atan2(dy, dx);
        float diff = RingGeometry.WrapAngle(desiredAngle - Ship.AngleRadians);
        RotateLeft  = diff < -0.05f;
        RotateRight = diff >  0.05f;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        Thrust = dist > 260f && _rng.NextDouble() < 0.45;
    }
}
