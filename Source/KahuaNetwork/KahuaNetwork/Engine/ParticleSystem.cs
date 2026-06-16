using System;
using System.Collections.Generic;
using System.Numerics;
using SkiaSharp;

namespace KahuaNetwork.Engine;

/// <summary>
/// Owns the particle pool: emission, per-frame integration (velocity, drag,
/// gravity, choreographed targets), and the additive glow+core rendering pass.
/// </summary>
internal sealed class ParticleSystem
{
    private readonly List<Particle> _particles = new(8192);
    private readonly Random _rng = new(7);

    public int Count => _particles.Count;
    public int Capacity { get; set; } = 6000;

    public void Emit(Particle p)
    {
        if (_particles.Count >= Capacity) return;
        _particles.Add(p);
    }

    public void EmitBurst(Vector3 center, SKColor color, int count, float speed, float size, float life)
    {
        for (int i = 0; i < count; i++)
        {
            // Random direction in unit sphere
            double theta = _rng.NextDouble() * Math.PI * 2;
            double phi = Math.Acos(2 * _rng.NextDouble() - 1);
            float s = speed * (0.5f + (float)_rng.NextDouble());
            var dir = new Vector3(
                (float)(Math.Sin(phi) * Math.Cos(theta)),
                (float)(Math.Cos(phi)),
                (float)(Math.Sin(phi) * Math.Sin(theta)));
            Emit(new Particle
            {
                Position = center,
                Velocity = dir * s,
                Color = color,
                Life = life * (0.5f + (float)_rng.NextDouble()),
                MaxLife = life,
                Size = size * (0.6f + (float)_rng.NextDouble() * 0.8f),
                Kind = ParticleKind.Burst,
                Drag = 0.985f,
                Gravity = new Vector3(0, -8f, 0),
            });
        }
    }

    public void EmitAmbient(Vector3 worldCenter, float radius, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_particles.Count >= Capacity) return;
            float x = (float)(_rng.NextDouble() - 0.5) * 2f * radius;
            float z = (float)(_rng.NextDouble() - 0.5) * 2f * radius;
            float y = (float)(_rng.NextDouble()) * 600f;
            Emit(new Particle
            {
                Position = worldCenter + new Vector3(x, y, z),
                Velocity = new Vector3(
                    (float)(_rng.NextDouble() - 0.5) * 4f,
                    4f + (float)_rng.NextDouble() * 6f,
                    (float)(_rng.NextDouble() - 0.5) * 4f),
                Color = Theme.Cyan.WithAlpha(180),
                Life = 4f + (float)_rng.NextDouble() * 3f,
                MaxLife = 7f,
                Size = 1.5f + (float)_rng.NextDouble() * 2.5f,
                Kind = ParticleKind.Spark,
                Drag = 0.998f,
                Gravity = Vector3.Zero,
            });
        }
    }

    public void EmitTelemetry(Building b, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_particles.Count >= Capacity) return;
            float ox = (float)(_rng.NextDouble() - 0.5) * b.Width * 0.8f;
            float oz = (float)(_rng.NextDouble() - 0.5) * b.Depth * 0.8f;
            Emit(new Particle
            {
                Position = b.GroundCenter + new Vector3(ox, b.Height * (0.9f + b.ExpandProgress * 0.6f), oz),
                Velocity = new Vector3(
                    (float)(_rng.NextDouble() - 0.5) * 3f,
                    20f + (float)_rng.NextDouble() * 25f,
                    (float)(_rng.NextDouble() - 0.5) * 3f),
                Color = b.BaseColor.WithAlpha(220),
                Life = 1.8f + (float)_rng.NextDouble() * 1.2f,
                MaxLife = 3f,
                Size = 1.2f + (float)_rng.NextDouble() * 1.6f,
                Kind = ParticleKind.Telemetry,
                Drag = 0.995f,
                Gravity = new Vector3(0, -4f, 0),
            });
        }
    }

    public void EmitRiskStorm(Building b, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_particles.Count >= Capacity) return;
            double angle = _rng.NextDouble() * Math.PI * 2;
            float r = b.Width * 0.7f + (float)_rng.NextDouble() * 30f;
            float y = (float)_rng.NextDouble() * b.Height;
            float tangX = -(float)Math.Sin(angle);
            float tangZ = (float)Math.Cos(angle);
            float speed = 25f + (float)_rng.NextDouble() * 25f;
            Emit(new Particle
            {
                Position = b.GroundCenter + new Vector3((float)Math.Cos(angle) * r, y, (float)Math.Sin(angle) * r),
                Velocity = new Vector3(tangX * speed, (float)(_rng.NextDouble() - 0.3f) * 12f, tangZ * speed),
                Color = Theme.RiskColor(b.Risk).WithAlpha(220),
                Life = 1.4f + (float)_rng.NextDouble() * 0.8f,
                MaxLife = 2.2f,
                Size = 1.4f + (float)_rng.NextDouble() * 2.0f,
                Kind = ParticleKind.RiskStorm,
                Drag = 0.96f,
                Gravity = Vector3.Zero,
            });
        }
    }

    public void Update(float dt)
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            p.Velocity += p.Gravity * dt;
            p.Velocity *= MathF.Pow(p.Drag, dt * 60f);
            p.Position += p.Velocity * dt;
            p.Life -= dt;
            _particles[i] = p;
        }
        // Compact
        int write = 0;
        for (int read = 0; read < _particles.Count; read++)
        {
            if (_particles[read].Life > 0)
            {
                if (write != read) _particles[write] = _particles[read];
                write++;
            }
        }
        if (write < _particles.Count)
            _particles.RemoveRange(write, _particles.Count - write);
    }

    public void Render(SKCanvas canvas, Camera3D camera)
    {
        // Single pass: glow base then bright core via additive (PlusLighter)
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.Plus,
        };
        using var corePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.Plus,
        };

        foreach (var p in _particles)
        {
            if (!camera.Project(p.Position, out var s, out var depth)) continue;

            float lifeFrac = p.LifeFrac;
            float alpha = MathF.Min(1f, lifeFrac * 2.5f) * (lifeFrac > 0.6f ? 1f : lifeFrac / 0.6f);
            float depthScale = 1f - MathF.Min(0.7f, depth);
            float screenSize = p.Size * (1.5f - depth * 1.2f) * 4f;
            if (screenSize < 0.5f) continue;

            byte ga = (byte)(alpha * 80);
            byte ca = (byte)(alpha * 255);

            glowPaint.Color = p.Color.WithAlpha(ga);
            canvas.DrawCircle(s.X, s.Y, screenSize * 3f, glowPaint);

            corePaint.Color = p.Color.WithAlpha(ca);
            canvas.DrawCircle(s.X, s.Y, screenSize * 0.9f, corePaint);
        }
    }

    public void Clear() => _particles.Clear();

    public IReadOnlyList<Particle> Snapshot() => _particles;

    public void Replace(int index, Particle p)
    {
        if ((uint)index >= (uint)_particles.Count) return;
        _particles[index] = p;
    }
}
