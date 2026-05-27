using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;
using Uno3dViewer.Rendering;

namespace Uno3dViewer;

public sealed partial class MainPage : Page
{
    private enum DragButton { None, Left, Middle, Right }

    private uint? _activePointer;
    private DragButton _dragButton;
    private Point _lastPos;

    private readonly HashSet<VirtualKey> _heldKeys = new();
    private DateTime _lastFrameTime;
    private bool _renderLoopActive;
    private bool _demoMode;
    private const float DemoSecondsPerRotation = 30f;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Viewer.Camera.Changed += OnCameraChanged;
        Viewer.LoadFailed += OnLoadFailed;
    }

    private void OnLoadFailed(Exception ex)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await new ContentDialog
            {
                Title = "Load failed",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot,
            }.ShowAsync();
        });
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Viewer.Focus(FocusState.Programmatic);
        UpdateModeButtons();
        OnCameraChanged();
    }

    private void OnCameraChanged()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(OnCameraChanged);
            return;
        }
        ZoomText.Text = $"{(int)Math.Round(Viewer.Camera.ZoomPercent)}%";
    }

    private void UpdateModeButtons()
    {
        var orbit = Viewer.Camera.Mode == CameraMode.Orbit;
        OrbitBtn.Background = orbit
            ? (Brush)Resources["ToolbarActive"]
            : (Brush)Resources["ToolbarTransparent"];
        WalkBtn.Background = !orbit
            ? (Brush)Resources["ToolbarActive"]
            : (Brush)Resources["ToolbarTransparent"];
        DemoBtn.Background = _demoMode
            ? (Brush)Resources["ToolbarActive"]
            : (Brush)Resources["ToolbarTransparent"];
    }

    private void Viewer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Viewer.Focus(FocusState.Pointer);
    }

    private void Viewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Viewer.CapturePointer(e.Pointer);
        Viewer.Focus(FocusState.Pointer);

        var pp = e.GetCurrentPoint(Viewer);
        _lastPos = pp.Position;
        _activePointer = e.Pointer.PointerId;

        var props = pp.Properties;
        if (props.IsLeftButtonPressed) _dragButton = DragButton.Left;
        else if (props.IsMiddleButtonPressed) _dragButton = DragButton.Middle;
        else if (props.IsRightButtonPressed) _dragButton = DragButton.Right;
        else _dragButton = DragButton.None;

        e.Handled = true;
    }

    private void Viewer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_activePointer != e.Pointer.PointerId || _dragButton == DragButton.None) return;

        var pp = e.GetCurrentPoint(Viewer);
        var pos = pp.Position;
        var dx = (float)(pos.X - _lastPos.X);
        var dy = (float)(pos.Y - _lastPos.Y);
        _lastPos = pos;

        var cam = Viewer.Camera;
        if (cam.Mode == CameraMode.Orbit)
        {
            switch (_dragButton)
            {
                case DragButton.Left: cam.Orbit(dx, dy); break;
                case DragButton.Middle:
                case DragButton.Right: cam.Pan(dx, dy); break;
            }
        }
        else
        {
            cam.MouseLook(dx, dy);
        }
        e.Handled = true;
    }

    private void Viewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        Viewer.ReleasePointerCapture(e.Pointer);
        EndDrag();
        e.Handled = true;
    }

    private void Viewer_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndDrag();

    private void EndDrag()
    {
        _activePointer = null;
        _dragButton = DragButton.None;
    }

    private void Viewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var pp = e.GetCurrentPoint(Viewer);
        Viewer.Camera.Dolly(pp.Properties.MouseWheelDelta / 120f);
        e.Handled = true;
    }

    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        _heldKeys.Add(e.Key);
        if (Viewer.Camera.Mode == CameraMode.Walk)
            EnsureRenderLoop();
    }

    private void Page_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        _heldKeys.Remove(e.Key);
    }

    private void EnsureRenderLoop()
    {
        if (_renderLoopActive) return;
        _renderLoopActive = true;
        _lastFrameTime = DateTime.UtcNow;
        CompositionTarget.Rendering += OnRenderTick;
    }

    private void StopRenderLoop()
    {
        if (!_renderLoopActive) return;
        _renderLoopActive = false;
        CompositionTarget.Rendering -= OnRenderTick;
    }

    private void OnRenderTick(object? sender, object e)
    {
        var now = DateTime.UtcNow;
        var dt = (float)(now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;
        dt = Math.Min(dt, 0.1f);

        bool walkActive = Viewer.Camera.Mode == CameraMode.Walk && _heldKeys.Count > 0;
        bool demoActive = _demoMode && Viewer.Camera.Mode == CameraMode.Orbit;

        if (demoActive)
        {
            var rotPerSec = MathF.PI * 2f / DemoSecondsPerRotation;
            Viewer.Camera.Spin(rotPerSec * dt);
        }

        if (walkActive)
        {
            float speed = 2f * Viewer.Camera.SceneScale;
            float fwd = 0, rgt = 0, up = 0;
            if (_heldKeys.Contains(VirtualKey.W)) fwd += 1;
            if (_heldKeys.Contains(VirtualKey.S)) fwd -= 1;
            if (_heldKeys.Contains(VirtualKey.D)) rgt += 1;
            if (_heldKeys.Contains(VirtualKey.A)) rgt -= 1;
            if (_heldKeys.Contains(VirtualKey.E)) up += 1;
            if (_heldKeys.Contains(VirtualKey.Q)) up -= 1;
            if (fwd != 0 || rgt != 0 || up != 0)
                Viewer.Camera.WalkMove(fwd * speed * dt, rgt * speed * dt, up * speed * dt);
        }

        if (!walkActive && !demoActive) StopRenderLoop();
    }

    private void Orbit_Click(object sender, RoutedEventArgs e)
    {
        var cam = Viewer.Camera;
        if (cam.Mode == CameraMode.Walk)
        {
            var rot = Matrix4x4.CreateFromYawPitchRoll(cam.Yaw, cam.Pitch, 0);
            var fwd = Vector3.TransformNormal(-Vector3.UnitZ, rot);
            cam.Target = cam.Position + fwd * 5f;
            var d = cam.Position - cam.Target;
            cam.Distance = MathF.Max(0.1f, d.Length());
            cam.Elevation = MathF.Asin(Math.Clamp(d.Y / cam.Distance, -1f, 1f));
            cam.Azimuth = MathF.Atan2(d.X, d.Z);
        }
        cam.Mode = CameraMode.Orbit;
        UpdateModeButtons();
    }

    private void Walk_Click(object sender, RoutedEventArgs e)
    {
        var cam = Viewer.Camera;
        if (cam.Mode == CameraMode.Orbit)
        {
            cam.Position = cam.EyePosition;
            var dir = Vector3.Normalize(cam.Target - cam.Position);
            cam.Yaw = MathF.Atan2(-dir.X, -dir.Z);
            cam.Pitch = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f));
        }
        cam.Mode = CameraMode.Walk;
        UpdateModeButtons();
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        foreach (var ext in new[] { ".gltf", ".glb", ".fbx", ".obj", ".ply", ".stl", ".dae", ".3ds" })
            picker.FileTypeFilter.Add(ext);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;
        Viewer.LoadModel(file.Path);
    }

    private void Demo_Click(object sender, RoutedEventArgs e)
    {
        _demoMode = !_demoMode;
        if (_demoMode && Viewer.Camera.Mode == CameraMode.Walk)
            Orbit_Click(this, e);
        UpdateModeButtons();
        if (_demoMode) EnsureRenderLoop();
    }

    private void ViewTop_Click(object sender, RoutedEventArgs e)   => Viewer.SetStandardView(StandardView.Top);
    private void ViewFront_Click(object sender, RoutedEventArgs e) => Viewer.SetStandardView(StandardView.Front);
    private void ViewRight_Click(object sender, RoutedEventArgs e) => Viewer.SetStandardView(StandardView.Right);
    private void ViewIso_Click(object sender, RoutedEventArgs e)   => Viewer.SetStandardView(StandardView.Iso);
    private void Fit_Click(object sender, RoutedEventArgs e)       => Viewer.FitToView();
    private void Reset_Click(object sender, RoutedEventArgs e)     => Viewer.SetStandardView(StandardView.Iso);
}
