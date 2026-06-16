using System.Numerics;
using SkiaSharp;

namespace KahuaNetwork.Engine;

/// <summary>
/// One additive-blended particle. A value type so the system can hold large pools
/// in flat arrays with no GC churn. <see cref="Target"/> drives choreographed flows
/// (e.g. the topology-graph reassembly).
/// </summary>
internal struct Particle
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Target; // for choreographed flows
    public SKColor Color;
    public float Life;        // remaining
    public float MaxLife;
    public float Size;
    public ParticleKind Kind;
    public float Drag;
    public Vector3 Gravity;

    /// <summary>Remaining life as a 0..1 fraction (drives fade/size).</summary>
    public readonly float LifeFrac => Life / MaxLife;
}

/// <summary>The behavioral flavors of particle the emitter produces.</summary>
internal enum ParticleKind
{
    Spark,        // ambient drift
    Telemetry,    // rising from buildings
    RiskStorm,    // chaotic swirl around risky buildings
    Mitigation,   // gentle calming particles when AI suggests fix
    Burst,        // explosion fragments
    Node,         // topology graph node
}
