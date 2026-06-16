using System;
using System.Collections.Generic;

namespace Pohaku.Game;

/// <summary>
/// Base for every Asteroids entity. Carries position/velocity/rotation/radius and
/// the toroidal screen-wrap shared by all of them. Subclasses extend
/// <see cref="Update"/> for their own per-frame behavior.
/// </summary>
public abstract class Entity
{
    /// <summary>World-space center position.</summary>
    public Vec2 Position;
    /// <summary>Velocity in world units/second.</summary>
    public Vec2 Velocity;
    /// <summary>Facing angle in radians.</summary>
    public float Rotation;
    /// <summary>Collision radius.</summary>
    public float Radius;
    /// <summary>Cleared to false when destroyed; dead entities are reaped each frame.</summary>
    public bool Alive = true;

    /// <summary>Integrates position from velocity and applies screen wrap. Override to add behavior.</summary>
    public virtual void Update(float dt, float worldW, float worldH)
    {
        Position += Velocity * dt;
        Wrap(worldW, worldH);
    }

    // Toroidal wrap: an entity leaving one edge reappears on the opposite edge.
    // The +Radius padding lets a shape fully exit before it pops back in.
    protected void Wrap(float w, float h)
    {
        if (Position.X < -Radius) Position.X += w + Radius * 2;
        if (Position.X > w + Radius) Position.X -= w + Radius * 2;
        if (Position.Y < -Radius) Position.Y += h + Radius * 2;
        if (Position.Y > h + Radius) Position.Y -= h + Radius * 2;
    }
}

/// <summary>The player ship: rotate, thrust with inertia, fire bullets, with screen wrap.</summary>
public class Ship : Entity
{
    public bool ThrustOn;
    public bool TurningLeft;
    public bool TurningRight;
    public bool Shielded;
    public float InvincibleTime;   // post-respawn / hyperspace grace, in seconds
    public int Lives = 3;
    public List<Vec2> ThrustFlame = new();
    public float ShootCooldown;

    public Ship()
    {
        Radius = 12f;
        Rotation = -MathF.PI / 2f;  // start pointing up
    }

    /// <summary>Applies turning, thrust/drag (with a speed cap), and ticks down the timers.</summary>
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
            // Accelerate along the nose; clamp to MaxSpeed.
            var dir = Vec2.FromAngle(Rotation);
            Velocity += dir * Accel * dt;
            if (Velocity.Length > MaxSpeed)
                Velocity = Velocity.Normalized() * MaxSpeed;
        }
        else
        {
            // Exponential coast-down so the ship glides to a stop, frame-rate independent.
            Velocity = Velocity * MathF.Pow(Drag, dt);
        }

        if (ShootCooldown > 0) ShootCooldown -= dt;
        if (InvincibleTime > 0) InvincibleTime -= dt;

        base.Update(dt, w, h);
    }
}

/// <summary>
/// A drifting asteroid. <see cref="Shape"/> holds 12 randomized radii so each rock
/// has a lumpy silhouette; <see cref="Size"/> (3/2/1) governs radius and how it
/// splits when shot.
/// </summary>
public class Asteroid : Entity
{
    public int Size; // 3 = large, 2 = medium, 1 = small
    public float[] Shape; // radii at 12 fixed angles around the perimeter
    public float SpinSpeed;

    public Asteroid(int size, Random rng)
    {
        Size = size;
        Radius = size switch { 3 => 42f, 2 => 22f, _ => 12f };
        const int N = 12;
        Shape = new float[N];
        // Perturb each vertex radius to 70%–120% of the base radius for a lumpy rock.
        for (int i = 0; i < N; i++)
        {
            Shape[i] = Radius * (0.7f + (float)rng.NextDouble() * 0.5f);
        }
        SpinSpeed = ((float)rng.NextDouble() - 0.5f) * 1.4f; // random tumble, either way
    }

    /// <summary>Tumbles the rock and drifts it with screen wrap.</summary>
    public override void Update(float dt, float w, float h)
    {
        Rotation += SpinSpeed * dt;
        base.Update(dt, w, h);
    }
}

/// <summary>A short-lived shot. <see cref="FromShip"/> distinguishes player vs saucer fire.</summary>
public class Bullet : Entity
{
    public float Lifetime;
    public bool FromShip;

    public Bullet()
    {
        Radius = 2f;
        Lifetime = 0.9f;  // limited range, like the arcade
    }

    /// <summary>Ages the bullet (dies at end of life) and drifts it with screen wrap.</summary>
    public override void Update(float dt, float w, float h)
    {
        Lifetime -= dt;
        if (Lifetime <= 0) Alive = false;
        base.Update(dt, w, h);
    }
}

/// <summary>The enemy UFO. Large vs small changes size, score, and aim accuracy.</summary>
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

/// <summary>A short-lived explosion/thrust spark that fades and decelerates.</summary>
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

    /// <summary>Ages the spark and decelerates it; no screen wrap (sparks are transient).</summary>
    public override void Update(float dt, float w, float h)
    {
        Lifetime -= dt;
        if (Lifetime <= 0) Alive = false;
        Position += Velocity * dt;
        Velocity = Velocity * MathF.Pow(0.5f, dt);  // frame-rate-independent drag
    }
}
