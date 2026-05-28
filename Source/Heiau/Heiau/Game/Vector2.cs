using System;

namespace Heiau.Game;

public struct Vec2
{
    public float X;
    public float Y;

    public Vec2(float x, float y) { X = x; Y = y; }

    public static Vec2 Zero => new(0, 0);

    public static Vec2 FromAngle(float radians, float magnitude = 1f) =>
        new((float)Math.Cos(radians) * magnitude, (float)Math.Sin(radians) * magnitude);

    public float Length => (float)Math.Sqrt(X * X + Y * Y);

    public Vec2 Normalized()
    {
        var l = Length;
        return l > 0 ? new Vec2(X / l, Y / l) : Zero;
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(float s, Vec2 a) => new(a.X * s, a.Y * s);
}
