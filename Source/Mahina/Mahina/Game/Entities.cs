using System;

namespace Mahina.Game;

/// <summary>Top-level game state for Mahina's Lunar-Lander gameplay.</summary>
public enum GameMode { Title, Playing, Landed, Crashed, GameOver, Attract }

/// <summary>
/// The player's lunar module. Position/velocity are world coords (1280×720);
/// <see cref="AngleRadians"/> is rotation from straight-up (0 = nose up, +CW);
/// <see cref="FuelKg"/> is propellant that thrust depletes linearly.
/// </summary>
public sealed class Lander
{
    public Vec2  Position;
    public Vec2  Velocity;
    public float AngleRadians;     // 0 = straight up, +CW
    public float AngularVelocity;
    public float FuelKg;
    public bool  Thrusting;
    public bool  Alive = true;
}

/// <summary>
/// A flat terrain segment you can land on, with a score multiplier (5 = narrow
/// "expert", 3 = medium, 2 = wide "safe"). Drawn brighter than terrain and labeled.
/// </summary>
public sealed class LandingPad
{
    public float X0;       // left edge
    public float X1;       // right edge
    public float Y;        // pad surface elevation (world Y; smaller = higher)
    public int   Multiplier;
}

/// <summary>
/// The lunar surface: a polyline along the world bottom with <see cref="Pads"/>
/// embedded as flat segments; everything else is jagged hills/valleys.
/// </summary>
public sealed class Terrain
{
    public Vec2[] Points = Array.Empty<Vec2>();
    public LandingPad[] Pads = Array.Empty<LandingPad>();
}

/// <summary>A short-lived thrust-flame / crash-explosion particle (purely visual).</summary>
public sealed class Particle
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Life;
    public float MaxLife;
    public uint  Color;
    public float Size = 2.0f;
}

/// <summary>A floating "+score" number shown briefly after a landing.</summary>
public sealed class ScorePopup
{
    public Vec2  Pos;
    public int   Value;
    public float Life;
    public float MaxLife;
    public uint  Color;
}
