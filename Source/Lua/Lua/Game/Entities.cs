using System;
using System.Collections.Generic;

namespace Lua.Game;

// --- Game-mode and wave-state enums ---

/// <summary>Top-level game state. <c>Warp</c> is the between-levels dive down the well.</summary>
public enum GameMode { Title, Playing, GameOver, Attract, Warp }

/// <summary>
/// The player "blaster" / claw. Lives on the rim (depth 0); <see cref="Segment"/>
/// is the segment its claw straddles and <see cref="SegmentT"/> animates the
/// slide to an adjacent segment.
/// </summary>
public sealed class Player
{
    public int Segment;
    public float SegmentT;      // 0..1 sliding offset for animation
    public int   TargetSegment; // segment we're sliding toward (== Segment when idle)
    public float ShootCooldown;
    public bool  Alive = true;
    public float Invuln;        // post-respawn invulnerability seconds
    public float SpawnAnim;     // 0..1 fade-in after life lost
    public int   SuperZapperUsesLeft;  // 2 at level start, decrements per zapper
}

// --- Enemies ---
//
//  Flipper:  Most iconic foe. Walks up the well in one segment, then on reaching
//            the rim "flips" between adjacent segments to chase the player.
//            Killed in one shot.
//
//  Tanker:   Climbs slower, splits into two Flippers when hit OR on reaching the rim.
//
//  Spiker:   Stays at a single segment, climbs from far end leaving a spike trail.
//            The spike is a vertical line along the segment from MinDepth to 1.
//            Spikes persist for the level and can kill the player during warp.
//
//  Fuseball: Travels along the EDGES of segments (vertex-to-vertex), not the body.
//            Hard to hit unless directly aligned with its current vertex.
/// <summary>The four Tempest foes; see the comment above for each one's behavior.</summary>
public enum EnemyKind { Flipper, Tanker, Spiker, Fuseball }

/// <summary>Per-enemy behavior state.</summary>
public enum EnemyState
{
    Climbing,     // moving from far end toward rim
    OnRim,        // depth == 0, walking around looking for the player (Flipper)
    Flipping,     // animating between two adjacent segments (Flipper)
    Splitting,    // brief animation before Tanker splits
    Dead,         // marked for removal
}

/// <summary>An enemy climbing the well. <see cref="Kind"/> + <see cref="State"/> select its behavior.</summary>
public sealed class Enemy
{
    public EnemyKind Kind;
    public EnemyState State = EnemyState.Climbing;
    public int   Segment;             // current segment index
    public int   TargetSegment;       // for flipping/walking on rim
    public float SegmentT;            // 0..1 for flip-between-segments animation
    public float Depth = 1f;          // 1 = far end, 0 = rim
    public float ClimbSpeed = 0.10f;  // depth per second
    public float WalkSpeed  = 1.8f;   // segments per second on rim
    public float StateTimer;          // generic timer for state transitions
    public float SpawnDelay;          // counts down while State==Climbing if >0, pause
    public float Hue;                 // for fuseballs / pulsars (animated color)
    public bool  CarryingFlipper;     // tanker carrying spawned flippers
    public int   HitsToKill = 1;      // tankers might take 1 hit but spawn 2 flippers
}

/// <summary>A Spiker's trail: a vertical hazard line in one segment that can kill during warp.</summary>
public sealed class Spike
{
    public int   Segment;
    public float MinDepth = 1f;   // top of spike (closer to rim = smaller depth value)
    // Spike grows toward rim while its parent Spiker climbs. After Spiker dies, the
    // spike freezes. Cleared on level transition warp (player has to dodge them).
}

// --- Bullets ---
// Player bullets travel from rim (depth=0) to far end (depth=1+); enemy bullets
// travel the other direction. We use one type with a "FromPlayer" flag.
/// <summary>A shot in one segment travelling along depth; <see cref="FromPlayer"/> sets direction.</summary>
public sealed class Bullet
{
    public int   Segment;
    public float Depth;      // 0..1
    public float Speed;      // depth per second (signed for player vs enemy)
    public bool  FromPlayer;
    public float Life;       // safety guard so bullets always die eventually
}

/// <summary>A short-lived explosion / spawn-flash particle (purely visual).</summary>
public sealed class Particle
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Life;
    public float MaxLife;
    public uint  Color;      // packed AARRGGBB
    public float Size = 2.5f;
}

/// <summary>A floating "+score" number shown briefly after a kill.</summary>
public sealed class ScorePopup
{
    public Vec2  Pos;
    public int   Value;
    public float Life;
    public float MaxLife;
    public uint  Color;
}
