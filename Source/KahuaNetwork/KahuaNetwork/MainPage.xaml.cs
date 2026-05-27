using System;
using KahuaNetwork.Engine;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Windows.Foundation;
using Windows.System;

namespace KahuaNetwork;

public sealed partial class MainPage : Page
{
    private readonly SceneRenderer _scene;
    private readonly Hud _hud;
    private DateTime _lastFrame = DateTime.UtcNow;

    public MainPage()
    {
        this.InitializeComponent();
        _scene = new SceneRenderer();
        _hud = new Hud(_scene);
        Canvas.Painter = PaintScene;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering += OnRender;
        Canvas.Focus(FocusState.Programmatic);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRender;
    }

    private void OnRender(object? sender, object e)
    {
        var now = DateTime.UtcNow;
        float dt = (float)(now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        if (dt > 0.1f) dt = 0.1f;

        _scene.Update(dt);
        _hud.Update(dt);
        Canvas.Invalidate();
    }

    private void PaintScene(SKCanvas canvas, Size area)
    {
        _scene.Resize((float)area.Width, (float)area.Height);
        _scene.Render(canvas);
        _hud.Render(canvas);
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(Canvas).Position;
        var s = ToSkPoint(pt);
        foreach (var btn in _hud.Buttons)
            btn.Hovered = btn.Bounds.Contains(s);
        if (_hud.HitTestButton(s) == null)
            _scene.SetHover(s);
        else
            _scene.SetHover(null);
    }

    private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _scene.SetHover(null);
        foreach (var btn in _hud.Buttons) btn.Hovered = false;
    }

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Canvas.Focus(FocusState.Pointer);
        var pt = e.GetCurrentPoint(Canvas).Position;
        var s = ToSkPoint(pt);
        var hit = _hud.HitTestButton(s);
        if (hit != null)
        {
            HandleButton(hit.Action);
            return;
        }
        var building = _scene.PickBuilding(s);
        _scene.Select(building);
    }

    private void Canvas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.G: _scene.TriggerGlobalView(); break;
            case VirtualKey.Escape: _scene.Select(null); break;
            case VirtualKey.R: HandleButton(HudButtonAction.Regenerate); break;
            case VirtualKey.Space: _scene.ShowGrid = !_scene.ShowGrid; break;
            case VirtualKey.M: HandleButton(HudButtonAction.Mitigate); break;
        }
    }

    private void HandleButton(HudButtonAction action)
    {
        switch (action)
        {
            case HudButtonAction.GlobalView:
                _scene.TriggerGlobalView();
                break;
            case HudButtonAction.Regenerate:
                _scene.City.Buildings.Clear();
                _scene.City.DataStreams.Clear();
                var rebuilt = Engine.City.Generate(new Random().Next());
                foreach (var b in rebuilt.Buildings) _scene.City.Buildings.Add(b);
                foreach (var s in rebuilt.DataStreams) _scene.City.DataStreams.Add(s);
                _scene.Select(null);
                break;
            case HudButtonAction.ToggleGrid:
                _scene.ShowGrid = !_scene.ShowGrid;
                break;
            case HudButtonAction.Mitigate:
                var b2 = _scene.SelectedBuilding;
                if (b2 != null)
                {
                    b2.Risk = Math.Max(0, b2.Risk - 0.35);
                    _scene.Particles.EmitBurst(
                        b2.GroundCenter + new System.Numerics.Vector3(0, b2.Height * 0.7f, 0),
                        Theme.Lime, 120, 80f, 2.5f, 2.4f);
                    b2.PendingApprovals = Math.Max(0, b2.PendingApprovals - (3 + new Random().Next(5)));
                    _hud.Insights.Push(new Insight(
                        $"AI auto-routed {b2.Name}'s backlog — pending approvals cleared by 40%.",
                        DateTime.UtcNow, InsightKind.Win, b2));
                }
                break;
        }
    }

    private static SKPoint ToSkPoint(Point p) => new((float)p.X, (float)p.Y);
}
