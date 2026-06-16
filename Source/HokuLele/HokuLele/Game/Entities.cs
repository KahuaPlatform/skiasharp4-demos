namespace HokuLele.Game;

// Model types for the Galaga-style vertical shooter. Positions/velocities are in
// world coords (720×1280 portrait). The renderer reads these; GameWorld mutates
// them each frame.

/// <summary>The player fighter (and, after a rescue, its dual-fighter wingman).</summary>
public class Player
{
    public Vec2 Position;
    public Vec2 Velocity;
    public float Radius = 14f;
    public bool Alive = true;
    public int Lives = 3;
    public float ShootCooldown;
    public float InvincibleTime;

    // Dual-fighter: after rescuing a captured ship, the player flies side-by-side with
    // a wingman that fires in parallel. First hit removes the wingman; the second hit
    // is a normal death.
    public bool HasWingman;
    public float WingmanOffsetX = 22f;  // wingman is drawn this many pixels to the right
}

/// <summary>The choreography phase a single enemy is currently performing.</summary>
public enum EnemyState
{
    Entering,           // sweeping in from off-screen along an entry path
    InFormation,        // parked at SlotPos with formation breathing applied
    Diving,             // launched on a dive path toward the player
    Rejoining,          // post-dive: looping back from above-screen into the formation slot
    Flyby,              // mystery mothership traversing the top of the screen
    BeamSeek,           // moving to a hover position above the player to deploy beam
    BeamActive,         // hovering and deploying tractor beam — captures player on contact
    ReturnWithCapture,  // post-beam: flying back to formation with the captive trailing
}

/// <summary>
/// An enemy ship. Its <see cref="State"/> selects which path/behavior runs each
/// frame; the remaining fields are the parameters that choreography needs
/// (formation slot, path progress, dive curl, beam hover position, etc.).
/// </summary>
public class Enemy
{
    public Vec2 Position;
    public Vec2 Velocity;
    public float Rotation;
    public float Radius = 14f;
    public bool Alive = true;
    public int Kind;        // 0 = drone, 1 = wing, 2 = captain, 3 = boss, 4 = mothership, 5 = snowflake
    public float Phase;     // per-enemy phase offset for formation breathing

    // Wave choreography
    public EnemyState State = EnemyState.Entering;
    public Vec2 SlotPos;            // assigned formation slot
    public float PathT;             // 0..1 progress along current path
    public float PathDuration;      // seconds to traverse current path
    public bool EntryFromLeft;      // which side of the screen the entry stream came from
    public float DiveCurlSign;      // +1 curls right, -1 curls left
    public Vec2 DiveTarget;         // captured player position at dive launch
    public float NextFireTime;      // wall-clock time (WaveTime) of next dive-shot

    // Challenge-stage flythrough: enemy enters via ChallengePath and exits off-screen
    // without settling into formation. PatternIdx selects the sub-variant within the
    // current challenge stage's main pattern (set on GameWorld.ChallengeStagePattern).
    public bool IsChallengeFlythrough;
    public int PatternIdx;

    // Which of the four entry-flight choreographies this enemy used. Set at spawn time.
    public int FlightIdx;

    // Mystery flyby: enemy traverses the top of the screen on a straight line.
    public bool FlybyFromLeft;

    // Tractor beam (boss only): the boss flies to BeamHoverPos, deploys a downward beam,
    // and may capture the player. HasCaptive is set true if the capture succeeded; the
    // captive ship trails the boss back to formation. BeamFromX / BeamFromY snapshot the
    // boss position at BeamSeek start so the seek path can lerp cleanly.
    public Vec2 BeamHoverPos;
    public Vec2 BeamFromPos;
    public bool HasCaptive;
}

/// <summary>A projectile. <see cref="FromPlayer"/> distinguishes player vs enemy shots.</summary>
public class Bullet
{
    public Vec2 Position;
    public Vec2 Velocity;
    public float Radius = 3f;
    public bool Alive = true;
    public bool FromPlayer;
    public float Lifetime = 2.5f;
}

/// <summary>A short-lived explosion spark. Purely visual.</summary>
public class Particle
{
    public Vec2 Position;
    public Vec2 Velocity;
    public float Lifetime;
    public float MaxLife;
    public uint Color;  // packed AARRGGBB; matches SKColor(uint)
}

/// <summary>A floating "+score" number shown briefly at a kill site.</summary>
public class ScorePopup
{
    public Vec2 Position;
    public int Value;
    public float Lifetime;
    public float MaxLife;
    public uint Color;
}
