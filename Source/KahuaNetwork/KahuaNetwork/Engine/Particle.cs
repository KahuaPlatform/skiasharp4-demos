using System.Numerics;
using SkiaSharp;

namespace KahuaNetwork.Engine;

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

    public readonly float LifeFrac => Life / MaxLife;
}

internal enum ParticleKind
{
    Spark,        // ambient drift
    Telemetry,    // rising from buildings
    RiskStorm,    // chaotic swirl around risky buildings
    Mitigation,   // gentle calming particles when AI suggests fix
    Burst,        // explosion fragments
    Node,         // topology graph node
}
