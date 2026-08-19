using System;

namespace Eli.Game;

// Eli's 4-state mode machine — the documented arcade standard
// (Title -> Playing -> GameOver -> Attract). Attract is an autopilot demo loop
// that digs, hunts and pumps; it drives the same sim as the keyboard does.
public enum GameMode { Title, Playing, GameOver, Attract }

// Movement is 4-directional (contrast Koa's 8-way). Cardinal-only motion is what
// keeps carved corridors exactly one cell wide, because the corridor-centering
// assist then always has a dominant axis to ease against.
public enum Facing { Right, Left, Up, Down }

public static class Facings
{
    public static Vec2 ToVec(Facing f) => f switch
    {
        Facing.Right => new Vec2( 1f,  0f),
        Facing.Left  => new Vec2(-1f,  0f),
        Facing.Up    => new Vec2( 0f, -1f),
        _            => new Vec2( 0f,  1f),
    };

    // Degrees for SKCanvas.RotateDegrees (0 = +X), used by the digger sprite.
    public static float ToDegrees(Facing f) => f switch
    {
        Facing.Right => 0f,
        Facing.Left  => 180f,
        Facing.Up    => -90f,
        _            => 90f,
    };
}

// Non-wrapping entity base, verbatim in shape from Koa: Eli's field is bounded,
// so positions are plain world-space pixels with no wrap fold.
public abstract class Entity
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Radius;
    public bool  Alive = true;
}

public sealed class Digger : Entity
{
    public Facing Facing = Facing.Right;

    // Set each frame from the latched input (or the attract bot). Zero when idle.
    public Vec2 MoveDir;

    // True while the digger's leading edge is inside undug earth — halves speed
    // and drives the dig-scrape voice.
    public bool Digging;

    // Death/respawn: while > 0 the digger is off the field and the sim skips it.
    public float RespawnTimer;
}

public enum EnemyKind
{
    // Uhane ("spirit") — the Pooka analogue. Fast tunnel patroller that can
    // FLATTEN AND PHASE THROUGH DIRT on a straight line toward the digger when
    // the tunnel network can't reach them. The two-mode AI lives on this kind.
    Uhane,
    // Nohu ("stonefish") — the Fygar analogue. Slower, tougher (one extra pump),
    // worth double. Never leaves the tunnels.
    Nohu,
}

// Which of the two AI modes an enemy is currently running. Flow-field routing
// does not apply in Phasing mode — that is the whole point of it.
public enum EnemyMode { Tunnel, Phasing }

public sealed class Enemy : Entity
{
    public EnemyKind Kind;
    public EnemyMode Mode = EnemyMode.Tunnel;
    public float     Speed;         // px/sec from the kind table at spawn, scaled by level
    public Vec2      SpawnPos;      // reset target when the digger dies
    public float     Wobble;        // per-enemy phase so the pair don't pulse in lockstep

    // --- Harpoon / inflation state ---
    // Inflation is in PUMPS, fractional because it decays continuously between
    // presses. Reaching the kind's PumpsToBurst bursts the enemy.
    public float Inflation;
    public bool  Pinned;            // harpooned: AI and movement are suspended
    public float BurstTimer;        // > 0 while the pop animation plays out

    // --- Phasing state ---
    public float GhostCheckTimer;   // seconds until the next "should I phase?" test
    public float PhaseElapsed;      // seconds spent in the current phasing run

    public int PumpsToBurst => Kind == EnemyKind.Nohu ? GameWorld.NohuPumpsToBurst
                                                      : GameWorld.UhanePumpsToBurst;
}

// A boulder suspended in the dirt. An ENTITY, not a tile, because a tile cannot
// hold a sub-cell Y position while it falls. It reads the field for support and
// writes to it (carving) as it drops.
public enum BoulderState { Settled, Wobbling, Falling, Shattering }

public sealed class Boulder : Entity
{
    public BoulderState State = BoulderState.Settled;
    public int   Col, Row;        // seed cell (also the silhouette seed)
    public float StateTimer;      // wobble countdown / shatter countdown
    public int   Crushed;         // enemies killed by THIS fall (chains the bonus)
}

public sealed class Particle : Entity
{
    public float Life;
    public float MaxLife;
    public uint  Color;
    public float Size = 2.0f;
}

// The harpoon: a stateful extending segment, NOT a fire-and-forget projectile.
// It grows from the digger along its facing until it hits dirt or an enemy, then
// holds; pumping an attached enemy inflates it over several presses until it
// bursts. Lives as a single struct on GameWorld rather than in a list, because
// there is only ever one.
public enum HarpoonState { Idle, Extending, Attached, Retracting }

public struct Harpoon
{
    public HarpoonState State;
    public Vec2         Origin;   // muzzle position, latched at fire time
    public Vec2         Dir;      // cardinal unit vector, latched at fire time
    public float        Length;   // current extension in world px
    public Enemy?       Victim;   // the attached enemy, if State == Attached
    public float        PumpTimer;// rate-limits pumps to GameWorld.PumpInterval

    public Vec2 Tip => Origin + Dir * Length;

    public void Reset()
    {
        State = HarpoonState.Idle;
        Length = 0f;
        Victim = null;
        PumpTimer = 0f;
    }
}
