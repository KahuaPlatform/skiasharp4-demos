using System;
using System.Collections.Generic;

namespace Pohaku.Game;

public enum GameMode { Demo, Playing, GameOver }

public class GameWorld
{
    public float Width = 1280;
    public float Height = 720;

    public Ship Ship = new();
    public List<Asteroid> Asteroids = new();
    public List<Bullet> Bullets = new();
    public List<Particle> Particles = new();
    public Saucer? Saucer;

    public GameMode Mode = GameMode.Demo;
    public bool VibrantMode;
    public int Score;
    public int HighScore;
    public int Level = 1;

    private float _saucerSpawnTimer = 18f;
    private float _gameOverTimer;
    private float _attractTextTimer;
    private float _demoAiTimer;
    private float _respawnTimer;

    private readonly Random _rng = new();

    public bool ShowAttractText => (_attractTextTimer % 1.2f) < 0.7f;

    public GameWorld()
    {
        StartDemo();
    }

    public void Resize(float w, float h)
    {
        Width = MathF.Max(320, w);
        Height = MathF.Max(240, h);
    }

    public void StartDemo()
    {
        Mode = GameMode.Demo;
        Score = 0;
        Level = 1;
        Asteroids.Clear();
        Bullets.Clear();
        Particles.Clear();
        Saucer = null;
        SpawnAsteroidWave(5);
        Ship = new Ship
        {
            Position = new Vec2(Width / 2, Height / 2),
            Velocity = Vec2.Zero,
            InvincibleTime = 9999f,
            Lives = 3,
        };
    }

    public void StartGame()
    {
        Mode = GameMode.Playing;
        Score = 0;
        Level = 1;
        Asteroids.Clear();
        Bullets.Clear();
        Particles.Clear();
        Saucer = null;
        Ship = new Ship
        {
            Position = new Vec2(Width / 2, Height / 2),
            Velocity = Vec2.Zero,
            InvincibleTime = 2.5f,
            Lives = 3,
        };
        SpawnAsteroidWave(4);
    }

    private void SpawnAsteroidWave(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var a = new Asteroid(3, _rng);
            // Place away from ship
            float x, y;
            do
            {
                x = (float)_rng.NextDouble() * Width;
                y = (float)_rng.NextDouble() * Height;
            } while (Distance(new Vec2(x, y), Ship.Position) < 180f);
            a.Position = new Vec2(x, y);
            float angle = (float)_rng.NextDouble() * MathF.Tau;
            a.Velocity = Vec2.FromAngle(angle, 30f + (float)_rng.NextDouble() * 50f);
            Asteroids.Add(a);
        }
    }

    public void FireBullet()
    {
        if (Ship.ShootCooldown > 0) return;
        if (Bullets.Count >= 5) return;
        var dir = Vec2.FromAngle(Ship.Rotation);
        var b = new Bullet
        {
            Position = Ship.Position + dir * (Ship.Radius + 2),
            Velocity = Ship.Velocity + dir * 520f,
            FromShip = true,
        };
        Bullets.Add(b);
        Ship.ShootCooldown = 0.18f;
        AudioEngine.PlayShoot();
    }

    public void HyperSpace()
    {
        AudioEngine.PlayHyperspace();
        Ship.Position = new Vec2((float)_rng.NextDouble() * Width, (float)_rng.NextDouble() * Height);
        Ship.Velocity = Vec2.Zero;
        // 1 in 8 chance of bad hyperspace
        if (_rng.Next(8) == 0)
        {
            KillShip();
        }
    }

    public void Update(float dt)
    {
        _attractTextTimer += dt;

        if (Mode == GameMode.Demo)
        {
            UpdateDemoAI(dt);
        }

        Ship.Update(dt, Width, Height);

        foreach (var a in Asteroids) a.Update(dt, Width, Height);
        foreach (var b in Bullets) b.Update(dt, Width, Height);
        foreach (var p in Particles) p.Update(dt, Width, Height);

        if (Saucer != null)
        {
            UpdateSaucer(dt);
        }
        else
        {
            _saucerSpawnTimer -= dt;
            if (_saucerSpawnTimer <= 0 && Mode != GameMode.GameOver)
            {
                SpawnSaucer();
                _saucerSpawnTimer = 22f + (float)_rng.NextDouble() * 16f;
            }
        }

        if (Ship.ThrustOn && _rng.NextDouble() < 0.6f)
        {
            var back = -Vec2.FromAngle(Ship.Rotation);
            var jitter = ((float)_rng.NextDouble() - 0.5f) * 0.6f;
            var dir = Vec2.FromAngle(Ship.Rotation + MathF.PI + jitter);
            Particles.Add(new Particle(
                Ship.Position + back * Ship.Radius,
                Ship.Velocity + dir * (60f + (float)_rng.NextDouble() * 60f),
                0.35f));
        }

        HandleCollisions();

        Bullets.RemoveAll(b => !b.Alive);
        Asteroids.RemoveAll(a => !a.Alive);
        Particles.RemoveAll(p => !p.Alive);

        if (Asteroids.Count == 0 && (Saucer == null))
        {
            Level++;
            SpawnAsteroidWave(Math.Min(4 + Level, 11));
            if (Mode == GameMode.Playing) Ship.InvincibleTime = MathF.Max(Ship.InvincibleTime, 1.5f);
        }

        if (Mode == GameMode.GameOver)
        {
            _gameOverTimer -= dt;
            if (_gameOverTimer <= 0)
            {
                if (Score > HighScore) HighScore = Score;
                StartDemo();
            }
        }

        if (_respawnTimer > 0)
        {
            _respawnTimer -= dt;
            if (_respawnTimer <= 0 && Ship.Lives > 0)
            {
                Ship.Position = new Vec2(Width / 2, Height / 2);
                Ship.Velocity = Vec2.Zero;
                Ship.Rotation = -MathF.PI / 2f;
                Ship.InvincibleTime = 2.5f;
                Ship.Alive = true;
            }
        }

        UpdateAudioState();
    }

    // Drives Start/Stop of the looping AudioEngine voices in response to game state.
    private bool _prevThrustOn;
    private bool _prevSaucerActive;

    private void UpdateAudioState()
    {
        bool thrustOn = Ship.Alive && Ship.ThrustOn && Mode != GameMode.GameOver;
        if (thrustOn != _prevThrustOn)
        {
            if (thrustOn) AudioEngine.StartThrust();
            else          AudioEngine.StopThrust();
            _prevThrustOn = thrustOn;
        }

        bool saucerActive = Saucer != null;
        if (saucerActive != _prevSaucerActive)
        {
            if (saucerActive) AudioEngine.StartSaucer(Saucer!.Large);
            else              AudioEngine.StopSaucer();
            _prevSaucerActive = saucerActive;
        }
    }

    private void UpdateDemoAI(float dt)
    {
        _demoAiTimer -= dt;

        // Find nearest asteroid
        Asteroid? nearest = null;
        float bestD = float.MaxValue;
        foreach (var a in Asteroids)
        {
            float d = Distance(a.Position, Ship.Position);
            if (d < bestD) { bestD = d; nearest = a; }
        }

        Ship.TurningLeft = false;
        Ship.TurningRight = false;
        Ship.ThrustOn = false;

        if (nearest != null)
        {
            var to = nearest.Position - Ship.Position;
            float targetAngle = MathF.Atan2(to.Y, to.X);
            float diff = NormalizeAngle(targetAngle - Ship.Rotation);
            if (diff > 0.06f) Ship.TurningRight = true;
            else if (diff < -0.06f) Ship.TurningLeft = true;

            if (MathF.Abs(diff) < 0.18f && _demoAiTimer <= 0)
            {
                FireBullet();
                _demoAiTimer = 0.25f + (float)_rng.NextDouble() * 0.3f;
            }

            // Thrust occasionally to drift
            if (bestD > 220f && _rng.NextDouble() < 0.02f) Ship.ThrustOn = true;

            // Avoid if too close
            if (bestD < 80f)
            {
                Ship.ThrustOn = true;
            }
        }
    }

    private static float NormalizeAngle(float a)
    {
        while (a > MathF.PI) a -= MathF.Tau;
        while (a < -MathF.PI) a += MathF.Tau;
        return a;
    }

    private void SpawnSaucer()
    {
        bool large = _rng.Next(2) == 0 || Score < 10000;
        var s = new Saucer(large);
        bool fromLeft = _rng.Next(2) == 0;
        s.Position = new Vec2(fromLeft ? -30 : Width + 30, (float)_rng.NextDouble() * Height);
        s.Velocity = new Vec2(fromLeft ? 110f : -110f, 0);
        s.DirectionChangeTimer = 1.5f;
        s.ShootTimer = 1.4f;
        Saucer = s;
    }

    private void UpdateSaucer(float dt)
    {
        var s = Saucer!;
        s.Position += s.Velocity * dt;

        s.DirectionChangeTimer -= dt;
        if (s.DirectionChangeTimer <= 0)
        {
            s.DirectionChangeTimer = 1.2f + (float)_rng.NextDouble() * 1.2f;
            float vy = ((float)_rng.NextDouble() - 0.5f) * 140f;
            s.Velocity = new Vec2(MathF.Sign(s.Velocity.X) * 110f, vy);
        }

        s.ShootTimer -= dt;
        if (s.ShootTimer <= 0 && s.Position.X > 0 && s.Position.X < Width)
        {
            s.ShootTimer = 1.2f + (float)_rng.NextDouble() * 0.8f;
            float angle;
            if (s.Large)
            {
                angle = (float)_rng.NextDouble() * MathF.Tau;
            }
            else
            {
                // aim near ship with some inaccuracy
                var to = Ship.Position - s.Position;
                angle = MathF.Atan2(to.Y, to.X) + ((float)_rng.NextDouble() - 0.5f) * 0.4f;
            }
            var dir = Vec2.FromAngle(angle);
            Bullets.Add(new Bullet
            {
                Position = s.Position + dir * (s.Radius + 2),
                Velocity = dir * 380f,
                FromShip = false,
                Lifetime = 1.4f,
            });
        }

        if (s.Position.X < -50 || s.Position.X > Width + 50)
        {
            Saucer = null;
        }
    }

    private void HandleCollisions()
    {
        // bullet vs asteroid
        foreach (var b in Bullets)
        {
            if (!b.Alive) continue;
            foreach (var a in Asteroids)
            {
                if (!a.Alive) continue;
                if (Distance(a.Position, b.Position) < a.Radius)
                {
                    b.Alive = false;
                    SplitAsteroid(a);
                    if (b.FromShip) AddScore(a.Size switch { 3 => 20, 2 => 50, _ => 100 });
                    break;
                }
            }
        }

        // bullet vs saucer
        if (Saucer != null)
        {
            foreach (var b in Bullets)
            {
                if (!b.Alive || !b.FromShip) continue;
                if (Distance(Saucer.Position, b.Position) < Saucer.Radius)
                {
                    b.Alive = false;
                    Explode(Saucer.Position, 22);
                    if (b.FromShip) AddScore(Saucer.Large ? 200 : 1000);
                    Saucer = null;
                    break;
                }
            }
        }

        // ship vs asteroid
        if (Ship.Alive && Ship.InvincibleTime <= 0 && Mode == GameMode.Playing)
        {
            foreach (var a in Asteroids)
            {
                if (Distance(Ship.Position, a.Position) < Ship.Radius + a.Radius * 0.7f)
                {
                    KillShip();
                    SplitAsteroid(a);
                    break;
                }
            }
            if (Saucer != null && Distance(Saucer.Position, Ship.Position) < Saucer.Radius + Ship.Radius)
            {
                KillShip();
                Explode(Saucer.Position, 22);
                Saucer = null;
            }
            // saucer bullets
            foreach (var b in Bullets)
            {
                if (!b.Alive || b.FromShip) continue;
                if (Distance(Ship.Position, b.Position) < Ship.Radius)
                {
                    b.Alive = false;
                    KillShip();
                    break;
                }
            }
        }
    }

    private void AddScore(int s)
    {
        int prev = Score;
        Score += s;
        if (Score / 10000 > prev / 10000 && Mode == GameMode.Playing)
        {
            Ship.Lives++;
        }
    }

    private void KillShip()
    {
        Explode(Ship.Position, 24);
        if (Mode == GameMode.Playing)
        {
            Ship.Lives--;
            Ship.Alive = false;
            if (Ship.Lives <= 0)
            {
                if (Score > HighScore) HighScore = Score;
                Mode = GameMode.GameOver;
                _gameOverTimer = 4.5f;
            }
            else
            {
                _respawnTimer = 1.5f;
                Ship.Position = new Vec2(-1000, -1000); // off-screen until respawn
            }
        }
    }

    private void SplitAsteroid(Asteroid a)
    {
        a.Alive = false;
        Explode(a.Position, 12);
        if (a.Size > 1)
        {
            for (int i = 0; i < 2; i++)
            {
                var na = new Asteroid(a.Size - 1, _rng)
                {
                    Position = a.Position,
                    Velocity = a.Velocity + Vec2.FromAngle(
                        (float)_rng.NextDouble() * MathF.Tau,
                        50f + (float)_rng.NextDouble() * 60f),
                };
                Asteroids.Add(na);
            }
        }
    }

    private void Explode(Vec2 pos, int n)
    {
        for (int i = 0; i < n; i++)
        {
            var ang = (float)_rng.NextDouble() * MathF.Tau;
            var sp = 60f + (float)_rng.NextDouble() * 140f;
            Particles.Add(new Particle(pos, Vec2.FromAngle(ang, sp), 0.5f + (float)_rng.NextDouble() * 0.4f));
        }
        AudioEngine.PlayExplosion();
    }

    private static float Distance(Vec2 a, Vec2 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
