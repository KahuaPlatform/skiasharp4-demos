using System;
using System.Collections.Generic;
using Arcade.Common.Chassis;

namespace Kiai.Game;

// --- Shared enums ---------------------------------------------------------

// The standard documented 4-state machine (02-Demo-Anatomy): Title screen,
// active Playing, GameOver placard, and a self-playing Attract autopilot.
public enum GameMode { Title, Playing, GameOver, Attract }

// A Lander's abduction lifecycle. Descending from the ceiling -> Hunting a
// standing humanoid -> Lifting one toward the ceiling -> Cruising (a Lander with
// no humanoid left to grab just patrols). Reaching the ceiling while Lifting
// consumes the humanoid and mutates the Lander into a Mutant (see GameWorld).
public enum LanderState { Descending, Hunting, Lifting, Cruising }

// A Humanoid's lifecycle. Standing on the terrain is the default; a Lander
// Seizes one and carries it up; shooting the captor drops it to Falling; the
// ship can catch a Falling humanoid (Caught, rides the ship) and deposit it back
// on the ground (Standing). A high fall splats it (Dead).
public enum HumanoidState { Standing, Seized, Falling, Caught, Dead }

// --- Entity base ----------------------------------------------------------

// Base for everything that lives in the toroidal world. Unlike Pohaku's
// wrap-both-axes Entity, Kia'i wraps **X only**: the world is a horizontal loop
// (a planet you can circle) but is just one screen tall, so Y is bounded by the
// terrain below and a ceiling above (the owning systems clamp Y; the base only
// wraps X). Position.X is always kept in [0, WorldWidth) by WrapX.
public abstract class Entity
{
    public Vec2 Position;
    public Vec2 Velocity;
    public float Rotation;
    public float Radius;
    public bool Alive = true;

    // worldW is the torus circumference (WorldWidth); worldH is unused for
    // wrapping (Y is clamped elsewhere) but kept in the signature to mirror the
    // template's Update shape.
    public virtual void Update(float dt, float worldW, float worldH)
    {
        Position += Velocity * dt;
        WrapX(worldW);
    }

    // Fold Position.X back into [0, worldW). Defender's world is a seamless loop,
    // so an entity that flies off the right edge reappears on the left and vice
    // versa — the canonical positive-modulo wrap the camera also uses.
    protected void WrapX(float worldW)
    {
        Position.X = Camera2D.Wrap(Position.X, worldW);
    }
}

// --- Ship -----------------------------------------------------------------

// The player's patrol craft. Directional thrust + inertia + drag (no
// rotate-to-aim — this is Defender, you fly the way you push). FacingSign flips
// when you thrust the opposite way and governs which way bullets fly and which
// way the look-ahead camera leads. Y is clamped to [ceiling, terrain - clearance]
// by the world. Carries an optional rescued Humanoid that rides along until
// landed.
public class Ship : Entity
{
    public bool ThrustLeft, ThrustRight, ThrustUp, ThrustDown;
    public float InvincibleTime;
    public int Lives = 3;
    public int SmartBombs = 3;
    public float ShootCooldown;

    // +1 faces right, -1 faces left. Bullets fly this way; the camera look-ahead
    // leads in this direction.
    public float FacingSign = 1f;

    // The humanoid currently riding the ship after a mid-air catch (deposited on
    // landing). Null when not carrying.
    public Humanoid? Carrying;

    // True while any thrust key is held — drives the engine flame render and the
    // looping thrust voice.
    public bool ThrustingAny => ThrustLeft || ThrustRight || ThrustUp || ThrustDown;

    public Ship()
    {
        Radius = 12f;
    }

    // Tuning. Horizontal accel is stronger than vertical (you patrol sideways);
    // drag bleeds speed when no thrust is applied on that axis.
    const float AccelX = 620f;
    const float AccelY = 460f;
    const float Drag = 0.18f;        // per-second velocity retention exponent base
    const float MaxSpeedX = 520f;
    const float MaxSpeedY = 360f;

    public override void Update(float dt, float worldW, float worldH)
    {
        float ax = 0f, ay = 0f;
        if (ThrustLeft)  { ax -= AccelX; FacingSign = -1f; }
        if (ThrustRight) { ax += AccelX; FacingSign = +1f; }
        if (ThrustUp)    ay -= AccelY;
        if (ThrustDown)  ay += AccelY;

        Velocity.X += ax * dt;
        Velocity.Y += ay * dt;

        // Drag only on axes with no active thrust, so released keys coast to a stop.
        if (ax == 0f) Velocity.X *= MathF.Pow(Drag, dt);
        if (ay == 0f) Velocity.Y *= MathF.Pow(Drag, dt);

        Velocity.X = Math.Clamp(Velocity.X, -MaxSpeedX, MaxSpeedX);
        Velocity.Y = Math.Clamp(Velocity.Y, -MaxSpeedY, MaxSpeedY);

        if (ShootCooldown > 0) ShootCooldown -= dt;
        if (InvincibleTime > 0) InvincibleTime -= dt;

        base.Update(dt, worldW, worldH);   // integrate + wrap X (Y clamped by world)
    }
}

// --- Bullet ---------------------------------------------------------------

// A fast forward shot. FromShip distinguishes the player's bullets from enemy
// fire (Landers/Mutants shoot back). Lifetime-limited so off-screen shots expire.
public class Bullet : Entity
{
    public float Lifetime;
    public bool FromShip;

    public Bullet()
    {
        Radius = 3f;
        Lifetime = 1.1f;
    }

    public override void Update(float dt, float worldW, float worldH)
    {
        Lifetime -= dt;
        if (Lifetime <= 0) Alive = false;
        base.Update(dt, worldW, worldH);
    }
}

// --- Humanoid -------------------------------------------------------------

// A colonist on the surface. The rescue loop revolves around these: Landers
// abduct them, you shoot the captor to drop them, then dive to catch them before
// they hit the ground. See HumanoidState for the lifecycle. GameWorld owns all
// transitions; the entity just carries the state + a captor link + its home
// ground Y (where it stands / is redeposited).
public class Humanoid : Entity
{
    public HumanoidState State = HumanoidState.Standing;
    public Lander? Captor;     // set while Seized; the Lander carrying this one
    public float GroundY;      // the terrain Y this humanoid stands on (for redeposit)

    public Humanoid()
    {
        Radius = 7f;
    }

    // Humanoids are positioned by the world's state logic each frame (riding a
    // captor, riding the ship, or standing). Only the Falling state moves under
    // its own velocity, so that's all the base integrate handles here.
    public override void Update(float dt, float worldW, float worldH)
    {
        if (State == HumanoidState.Falling)
        {
            Position += Velocity * dt;
            WrapX(worldW);
        }
    }
}

// --- Lander ---------------------------------------------------------------

// The core enemy: descends, hunts the nearest standing humanoid (toroidally),
// seizes it and climbs. If it reaches the ceiling with its captive, the humanoid
// is consumed and the Lander mutates into a Mutant. Shot while Lifting, it drops
// the humanoid to Falling. Periodically fires aimed shots at the ship.
public class Lander : Entity
{
    public LanderState State = LanderState.Descending;
    public Humanoid? Target;     // the humanoid being hunted/carried
    public float ShootTimer;
    public float RetargetTimer;

    public Lander()
    {
        Radius = 13f;
    }
}

// --- Mutant ---------------------------------------------------------------

// What a Lander becomes after a successful abduction: a fast, erratic homing
// swarmer that chases the ship aggressively. No abduction behaviour — pure
// threat. Movement uses the generic homing+wobble in GameWorld.
public class Mutant : Entity
{
    public float WobblePhase;
    public float ShootTimer;

    public Mutant()
    {
        Radius = 12f;
    }
}

// --- Baiter ---------------------------------------------------------------

// The "hurry up" enemy: spawns when the player lingers too long in a wave
// (wave-linger timer). Very fast sinusoidal cruiser that harasses the ship until
// the wave clears.
public class Baiter : Entity
{
    public float WavePhase;

    public Baiter()
    {
        Radius = 12f;
    }
}

// --- Bomber ---------------------------------------------------------------

// Drifts along laying mine Particles in its wake — area-denial that punishes
// careless flying. Moves on a slow sine path; the mines it lays are Particles
// flagged IsMine.
public class Bomber : Entity
{
    public float WavePhase;
    public float MineTimer;

    public Bomber()
    {
        Radius = 14f;
    }
}

// --- Pod / Swarmer --------------------------------------------------------

// A slow floating Pod that bursts into several fast Swarmers when shot (the
// classic split pattern). Swarmers are small, quick, and home loosely on the
// ship.
public class Pod : Entity
{
    public float WavePhase;

    public Pod()
    {
        Radius = 15f;
    }
}

public class Swarmer : Entity
{
    public float WobblePhase;

    public Swarmer()
    {
        Radius = 8f;
    }
}

// --- Particle -------------------------------------------------------------

// Short-lived spark for explosions, thrust trail, and Bomber mines. When IsMine
// is true the particle is a stationary hazard with a longer life that damages
// the ship on contact (handled in GameWorld collisions); otherwise it is purely
// cosmetic and drag-decays like Pohaku's.
public class Particle : Entity
{
    public float Lifetime;
    public float MaxLife;
    public bool IsMine;
    public uint Color;     // packed ARGB; 0 => renderer picks a default

    public Particle(Vec2 pos, Vec2 vel, float life, bool isMine = false, uint color = 0u)
    {
        Position = pos;
        Velocity = vel;
        Lifetime = life;
        MaxLife = life;
        Radius = isMine ? 4f : 1.4f;
        IsMine = isMine;
        Color = color;
    }

    public override void Update(float dt, float worldW, float worldH)
    {
        Lifetime -= dt;
        if (Lifetime <= 0) Alive = false;
        if (!IsMine)
        {
            Position += Velocity * dt;
            Velocity = Velocity * MathF.Pow(0.5f, dt);   // drag toward rest
        }
        // Mines are stationary; still wrap X in case one was laid near the seam.
        WrapX(worldW);
    }
}
