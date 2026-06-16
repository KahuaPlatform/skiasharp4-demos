using System;
using System.Numerics;

namespace KahuaNetwork.Engine;

/// <summary>
/// Drives the camera cinematically: a slow idle orbit around the network, and a
/// smooth focus-on-organization move when a tower is selected (and back when
/// deselected).
/// </summary>
internal sealed class CameraDirector
{
    public Camera3D Camera { get; }

    public Vector3 TargetPosition { get; set; }
    public float TargetYaw { get; set; }
    public float TargetPitch { get; set; }
    public float Smoothing { get; set; } = 3.0f;
    public Vector3 LookAt { get; set; } = new(0, 80, 0);

    private double _idleTime;
    private bool _focused;

    public CameraDirector(Camera3D camera)
    {
        Camera = camera;
        TargetPosition = camera.Position;
        TargetYaw = camera.Yaw;
        TargetPitch = camera.Pitch;
    }

    public void Update(float dt)
    {
        if (!_focused)
        {
            _idleTime += dt;
            // Faster, more dynamic orbit around the city center
            float t = (float)(_idleTime * 0.22);
            float radius = 680f + MathF.Sin(t * 0.31f) * 90f;
            float height = 300f + MathF.Sin(t * 0.7f) * 110f;
            TargetPosition = new Vector3(
                MathF.Cos(t) * radius,
                height,
                MathF.Sin(t) * radius);
            // Always look toward the city center
            AimAt(TargetPosition, LookAt);
        }

        float alpha = MathF.Min(1f, dt * Smoothing);
        Camera.Position = Vector3.Lerp(Camera.Position, TargetPosition, alpha);
        Camera.Yaw += AngleDelta(Camera.Yaw, TargetYaw) * alpha;
        Camera.Pitch += (TargetPitch - Camera.Pitch) * alpha;
    }

    public void FocusOn(Building b)
    {
        _focused = true;
        var offset = new Vector3(b.Width * 4f, b.Height * 1.4f + 100f, b.Depth * 4f);
        TargetPosition = b.GroundCenter + offset;
        AimAt(TargetPosition, b.GroundCenter + new Vector3(0, b.Height * 0.6f, 0));
    }

    private void AimAt(Vector3 from, Vector3 to)
    {
        var dir = to - from;
        TargetYaw = MathF.Atan2(-dir.X, -dir.Z);
        var horiz = MathF.Sqrt(dir.X * dir.X + dir.Z * dir.Z);
        TargetPitch = MathF.Atan2(dir.Y, horiz);
    }

    public void ResumeIdle()
    {
        _focused = false;
    }

    private static float AngleDelta(float from, float to)
    {
        float d = (to - from) % (MathF.PI * 2);
        if (d > MathF.PI) d -= MathF.PI * 2;
        if (d < -MathF.PI) d += MathF.PI * 2;
        return d;
    }
}
