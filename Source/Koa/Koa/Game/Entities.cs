using System;

namespace Koa.Game;

// Koa's 4-state mode machine — the documented arcade standard
// (Title -> Playing -> GameOver -> Attract). Attract is a flow-field auto-hero
// demo loop; it reuses the same sim, just driven by AI instead of the keyboard.
public enum GameMode { Title, Playing, GameOver, Attract }

// --- Hero classes (the co-op seam) ---------------------------------------
//
// v1 ships single-player as the Warrior, but the class/stats split is kept so a
// later co-op build can drop in extra heroes (Valkyrie/Wizard/Elf) without
// reworking the sim — every hero just reads its ClassStats.
public enum HeroClass { Warrior, Valkyrie, Wizard, Elf }

// Per-class tuning. Speed is px/sec; ShotSpeed px/sec; Cooldown seconds between
// shots; Armor scales incoming contact damage (lower = tankier); MaxHealth is
// the starting/refill cap of the health-clock.
public readonly struct ClassStats
{
    public readonly float Speed;
    public readonly float ShotSpeed;
    public readonly float Cooldown;
    public readonly float Armor;
    public readonly float MaxHealth;
    public readonly int   ShotDamage;

    public ClassStats(float speed, float shotSpeed, float cooldown, float armor, float maxHealth, int shotDamage)
    {
        Speed = speed; ShotSpeed = shotSpeed; Cooldown = cooldown;
        Armor = armor; MaxHealth = maxHealth; ShotDamage = shotDamage;
    }

    // The canonical class table. Warrior: tanky, hits hard, slow shots. Valkyrie:
    // balanced. Wizard: fast/strong shots, fragile. Elf: fast feet, rapid fire.
    public static ClassStats For(HeroClass c) => c switch
    {
        HeroClass.Warrior  => new(170f, 560f, 0.13f, 0.7f, 2000f, 34),
        HeroClass.Valkyrie => new(185f, 600f, 0.12f, 0.8f, 1800f, 28),
        HeroClass.Wizard   => new(175f, 680f, 0.15f, 1.3f, 1400f, 46),
        HeroClass.Elf      => new(210f, 640f, 0.08f, 1.0f, 1500f, 22),
        _                  => new(170f, 560f, 0.13f, 0.7f, 2000f, 34),
    };
}

// Non-wrapping entity base (contrast Pohaku's toroidal Entity). Koa's world is
// bounded, so positions are plain world-space pixels with no wrap fold.
public abstract class Entity
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Radius;
    public bool  Alive = true;
}

public sealed class Hero : Entity
{
    public HeroClass  Class = HeroClass.Warrior;
    public ClassStats Stats = ClassStats.For(HeroClass.Warrior);

    // Health doubles as the survival timer (the "warrior needs food" clock):
    // it drains continuously and on contact, and food tops it back up to
    // Stats.MaxHealth. Reaching 0 is death.
    public float Health;

    // Inventory + aim.
    public int   Keys;
    public int   Potions;
    public Vec2  AimDir = new(1f, 0f);  // last non-zero move dir; bullets fire along it
    public Vec2  MoveDir;               // this-frame normalised movement intent
    public float ShootCooldown;         // seconds until the next shot is allowed

    public void SetClass(HeroClass c)
    {
        Class = c;
        Stats = ClassStats.For(c);
    }
}

public enum EnemyKind { Grunt, Ghost, Demon }

public sealed class Enemy : Entity
{
    public EnemyKind Kind;
    public float     Health;
    public float     Speed;        // px/sec, copied from the kind table at spawn
    public float     HitCooldown;  // seconds before this enemy can damage the hero again
    public float     Wobble;       // per-enemy phase so the swarm doesn't pulse in lockstep
    public Vec2      StepDir;      // steering dir for the frame (flow + jitter + separation)
}

// A monster generator (spawner). Emits enemies on a jittered cadence while the
// global live cap allows; destroying it flips its tile to Floor and stops the
// stream — the core objective.
public sealed class Generator : Entity
{
    // Arcade-faithful: generators have a level 1-3 whose hit points equal the
    // level. Each shot knocks the level (and HP) down by one, and the kind it
    // emits weakens with it (3=Demon, 2=Ghost, 1=Grunt); at level 0 it's destroyed.
    public int       Level;
    public float     SpawnTimer;   // seconds until the next emit
    public EnemyKind Spawns;       // which kind this generator currently produces
    public int       Col, Row;     // its tile (flipped to Floor on death)
}

public sealed class Projectile : Entity
{
    public bool  FromHero;
    public float Lifetime;   // seconds before it fizzles
    public int   Damage;
}

public enum PickupKind { Key, Food, Potion, Treasure }

public sealed class Pickup : Entity
{
    public PickupKind Kind;
}

public sealed class Particle : Entity
{
    public float Life;
    public float MaxLife;
    public uint  Color;
    public float Size = 2.0f;
}
