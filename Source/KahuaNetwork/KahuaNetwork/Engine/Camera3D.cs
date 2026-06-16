using System;
using System.Numerics;
using SkiaSharp;

namespace KahuaNetwork.Engine;

/// <summary>
/// A simple perspective camera (position + yaw/pitch + FOV) that projects world
/// points to screen space for the city renderer.
/// </summary>
internal sealed class Camera3D
{
    public Vector3 Position { get; set; } = new(0, 220, 520);
    public float Yaw { get; set; } = 0f;
    public float Pitch { get; set; } = -0.42f;
    public float FieldOfView { get; set; } = 1.05f; // ~60 deg
    public float Near { get; set; } = 1f;
    public float Far { get; set; } = 3000f;

    public float ViewportWidth { get; set; } = 1280;
    public float ViewportHeight { get; set; } = 720;

    public Matrix4x4 ViewMatrix
    {
        get
        {
            var rot = Matrix4x4.CreateFromYawPitchRoll(Yaw, Pitch, 0);
            var forward = Vector3.Transform(new Vector3(0, 0, -1), rot);
            var up = Vector3.Transform(new Vector3(0, 1, 0), rot);
            return Matrix4x4.CreateLookAt(Position, Position + forward, up);
        }
    }

    public Matrix4x4 ProjectionMatrix =>
        Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, ViewportWidth / ViewportHeight, Near, Far);

    /// <summary>
    /// Projects a world point to <paramref name="screen"/> pixels and its camera-space
    /// <paramref name="depth"/>. Returns false if the point is behind the camera
    /// (caller should cull it).
    /// </summary>
    public bool Project(Vector3 world, out SKPoint screen, out float depth)
    {
        var v = Vector4.Transform(new Vector4(world, 1f), ViewMatrix);
        var c = Vector4.Transform(v, ProjectionMatrix);
        if (c.W <= 0.0001f)
        {
            screen = default;
            depth = float.PositiveInfinity;
            return false;
        }
        var nx = c.X / c.W;
        var ny = c.Y / c.W;
        screen = new SKPoint(
            (nx * 0.5f + 0.5f) * ViewportWidth,
            (1f - (ny * 0.5f + 0.5f)) * ViewportHeight);
        depth = c.Z / c.W;
        return depth > 0 && depth < 1;
    }

    public Vector3 Forward
    {
        get
        {
            var rot = Matrix4x4.CreateFromYawPitchRoll(Yaw, Pitch, 0);
            return Vector3.Transform(new Vector3(0, 0, -1), rot);
        }
    }
}
