using System;

namespace Arcade.Common;

/// <summary>
/// Lightweight 2D vector value type shared by every game in the repo as the
/// universal position/velocity type (entity <c>Pos</c>, <c>Vel</c>, etc.).
/// </summary>
/// <remarks>
/// Kept deliberately simple — no SIMD, no immutability — because the games all
/// accept the "value-type with public mutable fields" trade-off in their
/// gameplay code. Being a <see langword="struct"/> means positions live on the
/// stack or inline inside arrays, so the per-frame entity churn produces no GC
/// pressure.
/// </remarks>
public struct Vec2
{
    /// <summary>Horizontal component, in world units.</summary>
    public float X;
    /// <summary>Vertical component, in world units.</summary>
    public float Y;

    /// <summary>Constructs a vector from explicit X/Y components.</summary>
    public Vec2(float x, float y) { X = x; Y = y; }

    /// <summary>The origin <c>(0, 0)</c>.</summary>
    public static Vec2 Zero => new(0, 0);

    /// <summary>
    /// Builds a vector pointing along <paramref name="radians"/> with the given
    /// <paramref name="magnitude"/>. Angle convention is standard math: 0 points
    /// along +X, increasing angle rotates toward +Y.
    /// </summary>
    public static Vec2 FromAngle(float radians, float magnitude = 1f) =>
        new((float)Math.Cos(radians) * magnitude, (float)Math.Sin(radians) * magnitude);

    /// <summary>Euclidean length (magnitude) of the vector.</summary>
    public float Length => (float)Math.Sqrt(X * X + Y * Y);

    /// <summary>
    /// Returns a unit-length copy pointing in the same direction, or
    /// <see cref="Zero"/> for a zero-length vector (avoids divide-by-zero).
    /// </summary>
    public Vec2 Normalized()
    {
        var l = Length;
        return l > 0 ? new Vec2(X / l, Y / l) : Zero;
    }

    /// <summary>Component-wise addition.</summary>
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    /// <summary>Component-wise subtraction (<c>a - b</c> points from <c>b</c> to <c>a</c>).</summary>
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    /// <summary>Unary negation (reverses direction).</summary>
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    /// <summary>Scales the vector by a scalar.</summary>
    public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);
    /// <summary>Scales the vector by a scalar (scalar-first overload).</summary>
    public static Vec2 operator *(float s, Vec2 a) => new(a.X * s, a.Y * s);
}
