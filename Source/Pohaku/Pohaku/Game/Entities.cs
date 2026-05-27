using System;
using System.Collections.Generic;

namespace Pohaku.Game;

public abstract class Entity
{
    public Vec2 Position;
    public Vec2 Velocity;
    public float Rotation;
    public float Radius;
    public bool Alive = true;

    public virtual void Update(float dt, float worldW, float worldH)
    {
        Position += Velocity * dt;
        Wrap(worldW, worldH);
    }

    protected void Wrap(float w, float h)
    {
        if (Position.X < -Radius) Position.X += w + Radius * 2;
        if (Position.X > w + Radius) Position.X -= w + Radius * 2;
        if (Position.Y < -Radius) Position.Y += h + Radius * 2;
        if (Position.Y > h + Radius) Position.Y -= h + Radius * 2;
    }
}

public class Ship : Entity
{
    public bool ThrustOn;
    public bool TurningLeft;
    public bool TurningRight;
    public bool Shielded;
    public float InvincibleTime;
    public int Lives = 3;
    public List<Vec2> ThrustFlame = new();
    public float ShootCooldown;

    public Ship()
    {
        Radius = 12f;
        Rotation = -MathF.PI / 2f;
    }

    public override void Update(float dt, float w, float h)
    {
        const float TurnRate = 3.6f;
        const float Accel = 240f;
        const float Drag = 0.55f;
        const float MaxSpeed = 420f;

        if (TurningLeft) Rotation -= TurnRate * dt;
        if (TurningRight) Rotation += TurnRate * dt;

        if (ThrustOn)
        {
            var dir = Vec2.FromAngle(Rotation);
            Velocity += dir * Accel * dt;
            if (Velocity.Length > MaxSpeed)
                Velocity = Velocity.Normalized() * MaxSpeed;
        }
        else
        {
            Velocity = Velocity * MathF.Pow(Drag, dt);
        }

        if (ShootCooldown > 0) ShootCooldown -= dt;
        if (InvincibleTime > 0) InvincibleTime -= dt;

        base.Update(dt, w, h);
    }
}

public class Asteroid : Entity
{
    public int Size; // 3 = large, 2 = medium, 1 = small
    public float[] Shape; // radii at fixed angles
    public float SpinSpeed;

    public Asteroid(int size, Random rng)
    {
        Size = size;
        Radius = size switch { 3 => 42f, 2 => 22f, _ => 12f };
        const int N = 12;
        Shape = new float[N];
        for (int i = 0; i < N; i++)
        {
            Shape[i] = Radius * (0.7f + (float)rng.NextDouble() * 0.5f);
        }
        SpinSpeed = ((float)rng.NextDouble() - 0.5f) * 1.4f;
    }

    public override void Update(float dt, float w, float h)
    {
        Rotation += SpinSpeed * dt;
        base.Update(dt, w, h);
    }
}

public class Bullet : Entity
{
    public float Lifetime;
    public bool FromShip;

    public Bullet()
    {
        Radius = 2f;
        Lifetime = 0.9f;
    }

    public override void Update(float dt, float w, float h)
    {
        Lifetime -= dt;
        if (Lifetime <= 0) Alive = false;
        base.Update(dt, w, h);
    }
}

public class Saucer : Entity
{
    public bool Large;
    public float ShootTimer;
    public float DirectionChangeTimer;

    public Saucer(bool large)
    {
        Large = large;
        Radius = large ? 22f : 14f;
    }
}

public class Particle : Entity
{
    public float Lifetime;
    public float MaxLife;

    public Particle(Vec2 pos, Vec2 vel, float life)
    {
        Position = pos;
        Velocity = vel;
        Lifetime = life;
        MaxLife = life;
        Radius = 1f;
    }

    public override void Update(float dt, float w, float h)
    {
        Lifetime -= dt;
        if (Lifetime <= 0) Alive = false;
        Position += Velocity * dt;
        Velocity = Velocity * MathF.Pow(0.5f, dt);
    }
}
