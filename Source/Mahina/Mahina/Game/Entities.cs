using System;

namespace Mahina.Game;

public enum GameMode { Title, Playing, Landed, Crashed, GameOver, Attract }

// Lunar Module — the player's ship.
//   Position / Velocity are in world coordinates (1280×720 landscape).
//   AngleRadians is rotation from "vertical / straight up". 0 = nose straight up.
//   FuelKg is the remaining propellant mass; thrust impulse depletes it linearly.
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

// Landing pads — flat segments of terrain at a specific X range with a score multiplier.
//   Multiplier 5 = narrow ("expert"), 3 = medium, 2 = wide ("safe").
//   Drawn brighter than surrounding terrain and labeled with the multiplier.
public sealed class LandingPad
{
    public float X0;       // left edge
    public float X1;       // right edge
    public float Y;        // pad surface elevation (world Y; smaller = higher)
    public int   Multiplier;
}

// Terrain — a polyline along the bottom of the world. Pads are flat segments
// embedded in this line; everything else is jagged hills/valleys.
public sealed class Terrain
{
    public Vec2[] Points = Array.Empty<Vec2>();
    public LandingPad[] Pads = Array.Empty<LandingPad>();
}

// Particles for thrust flame + crash explosion.
public sealed class Particle
{
    public Vec2  Pos;
    public Vec2  Vel;
    public float Life;
    public float MaxLife;
    public uint  Color;
    public float Size = 2.0f;
}

public sealed class ScorePopup
{
    public Vec2  Pos;
    public int   Value;
    public float Life;
    public float MaxLife;
    public uint  Color;
}
