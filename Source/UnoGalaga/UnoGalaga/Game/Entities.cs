namespace UnoGalaga.Game;

// Minimal entity skeletons for a vertically-scrolling shooter.

public class Player
{
    public Vec2 Position;
    public Vec2 Velocity;
    public float Radius = 14f;
    public bool Alive = true;
    public int Lives = 3;
    public float ShootCooldown;
    public float InvincibleTime;
}

public enum EnemyState
{
    Entering,     // sweeping in from off-screen along an entry path
    InFormation,  // parked at SlotPos with formation breathing applied
    Diving,       // launched on a dive path toward the player
    Rejoining,    // post-dive: looping back from above-screen into the formation slot
    Flyby,        // mystery mothership traversing the top of the screen
}

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
}

public class Bullet
{
    public Vec2 Position;
    public Vec2 Velocity;
    public float Radius = 3f;
    public bool Alive = true;
    public bool FromPlayer;
    public float Lifetime = 2.5f;
}

public class Particle
{
    public Vec2 Position;
    public Vec2 Velocity;
    public float Lifetime;
    public float MaxLife;
    public uint Color;  // packed AARRGGBB; matches SKColor(uint)
}
