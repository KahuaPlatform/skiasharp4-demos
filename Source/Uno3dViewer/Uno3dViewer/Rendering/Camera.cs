using System;
using System.Numerics;

namespace Uno3dViewer.Rendering;

public enum CameraMode { Orbit, Walk }

public enum StandardView { Top, Bottom, Front, Back, Left, Right, Iso }

public sealed class Camera
{
    public event Action? Changed;

    private CameraMode _mode = CameraMode.Orbit;
    public CameraMode Mode
    {
        get => _mode;
        set { if (_mode == value) return; _mode = value; Changed?.Invoke(); }
    }

    public Vector3 Target { get; set; } = Vector3.Zero;
    public float Distance { get; set; } = 5f;
    public float Azimuth { get; set; } = MathF.PI * 0.25f;
    public float Elevation { get; set; } = MathF.PI / 6f;

    public Vector3 Position { get; set; } = new(0f, 1.6f, 5f);
    public float Yaw { get; set; }
    public float Pitch { get; set; }

    public float FieldOfView { get; set; } = MathF.PI / 4f;
    public float NearPlane { get; set; } = 0.05f;
    public float FarPlane { get; set; } = 1000f;

    public float SceneScale { get; private set; } = 1f;

    public float ZoomPercent => 100f * 5f / MathF.Max(0.01f, Distance);

    public Vector3 EyePosition => Mode == CameraMode.Orbit ? OrbitEye() : Position;

    private Vector3 OrbitEye()
    {
        var cosE = MathF.Cos(Elevation);
        return Target + new Vector3(
            Distance * cosE * MathF.Sin(Azimuth),
            Distance * MathF.Sin(Elevation),
            Distance * cosE * MathF.Cos(Azimuth));
    }

    public Matrix4x4 GetViewMatrix()
    {
        if (Mode == CameraMode.Orbit)
            return Matrix4x4.CreateLookAt(OrbitEye(), Target, Vector3.UnitY);

        var rot = Matrix4x4.CreateFromYawPitchRoll(Yaw, Pitch, 0);
        var fwd = Vector3.TransformNormal(-Vector3.UnitZ, rot);
        return Matrix4x4.CreateLookAt(Position, Position + fwd, Vector3.UnitY);
    }

    public Matrix4x4 GetProjectionMatrix(float aspect) =>
        Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, MathF.Max(0.01f, aspect), NearPlane, FarPlane);

    public void Orbit(float dx, float dy)
    {
        Azimuth -= dx * 0.005f;
        Elevation = Math.Clamp(Elevation + dy * 0.005f, -MathF.PI * 0.499f, MathF.PI * 0.499f);
        Changed?.Invoke();
    }

    public void Spin(float deltaAzimuthRadians)
    {
        Azimuth -= deltaAzimuthRadians;
        Changed?.Invoke();
    }

    public void Pan(float dx, float dy)
    {
        var eye = OrbitEye();
        var fwd = Vector3.Normalize(Target - eye);
        var right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
        var up = Vector3.Cross(right, fwd);
        var s = Distance * 0.0015f;
        Target += -right * dx * s + up * dy * s;
        Changed?.Invoke();
    }

    public void Dolly(float delta)
    {
        if (Mode == CameraMode.Orbit)
        {
            Distance = Math.Max(0.05f, Distance * MathF.Pow(0.9f, delta));
        }
        else
        {
            var rot = Matrix4x4.CreateFromYawPitchRoll(Yaw, Pitch, 0);
            var fwd = Vector3.TransformNormal(-Vector3.UnitZ, rot);
            Position += fwd * delta * SceneScale * 0.3f;
        }
        Changed?.Invoke();
    }

    public void MouseLook(float dx, float dy)
    {
        Yaw -= dx * 0.004f;
        Pitch = Math.Clamp(Pitch - dy * 0.004f, -MathF.PI * 0.499f, MathF.PI * 0.499f);
        Changed?.Invoke();
    }

    public void WalkMove(float forward, float right, float up)
    {
        var rot = Matrix4x4.CreateFromYawPitchRoll(Yaw, 0, 0);
        var fwd = Vector3.TransformNormal(-Vector3.UnitZ, rot);
        var rgt = Vector3.TransformNormal(Vector3.UnitX, rot);
        Position += fwd * forward + rgt * right + Vector3.UnitY * up;
        Changed?.Invoke();
    }

    public void FitToBounds(Vector3 min, Vector3 max)
    {
        var center = (min + max) * 0.5f;
        var radius = MathF.Max(0.01f, (max - min).Length() * 0.5f);
        Target = center;
        Distance = radius / MathF.Sin(FieldOfView * 0.5f) * 1.25f;
        SceneScale = radius;
        Mode = CameraMode.Orbit;
        Azimuth = MathF.PI * 0.25f;
        Elevation = MathF.PI / 6f;

        Position = center + Vector3.UnitZ * Distance + Vector3.UnitY * radius * 0.4f;
        Yaw = 0; Pitch = 0;

        Changed?.Invoke();
    }

    public void SetStandardView(StandardView v, Vector3 min, Vector3 max)
    {
        FitToBounds(min, max);
        switch (v)
        {
            case StandardView.Top:    Azimuth = 0;                 Elevation =  MathF.PI * 0.499f; break;
            case StandardView.Bottom: Azimuth = 0;                 Elevation = -MathF.PI * 0.499f; break;
            case StandardView.Front:  Azimuth = 0;                 Elevation = 0; break;
            case StandardView.Back:   Azimuth = MathF.PI;          Elevation = 0; break;
            case StandardView.Right:  Azimuth = MathF.PI * 0.5f;   Elevation = 0; break;
            case StandardView.Left:   Azimuth = -MathF.PI * 0.5f;  Elevation = 0; break;
            case StandardView.Iso:    Azimuth = MathF.PI * 0.25f;  Elevation = MathF.PI / 6f; break;
        }
        Changed?.Invoke();
    }
}
