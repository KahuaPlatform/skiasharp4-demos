using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Eli.Game;
using Windows.System;

namespace Eli;

public sealed partial class MainPage : Page
{
    readonly GameWorld _world = new();
    readonly Stopwatch _clock = new();
    TimeSpan _lastTick;
    bool _rendering;

    // Latched movement flags. Eli is 4-DIRECTIONAL (contrast Koa's 8-way): the
    // most-recently-pressed axis wins, so holding Right+Down yields one cardinal
    // rather than a diagonal. Cardinal-only motion is what keeps carved tunnels
    // exactly one cell wide, because the corridor-centering assist then always has
    // a dominant axis to ease against.
    bool _up, _down, _left, _right;
    bool _horizontalLast;   // which axis was pressed most recently
    bool _fire;
    bool _startHeld;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        PointerPressed += OnPointerPressed;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus(FocusState.Programmatic);
        GameCanvas.World = _world;
        _clock.Start();
        _lastTick = _clock.Elapsed;
        CompositionTarget.Rendering += OnRendering;
        _rendering = true;
    }

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_rendering)
        {
            CompositionTarget.Rendering -= OnRendering;
            _rendering = false;
        }
    }

    void OnRendering(object? sender, object e)
    {
        var now = _clock.Elapsed;
        float dt = (float)(now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (dt > 1.0f / 30.0f) dt = 1.0f / 30.0f;
        if (dt <= 0)           dt = 1.0f / 60.0f;

        if (_world.Mode == GameMode.Playing)
        {
            var (mx, my) = ComposeCardinal();
            _world.SetMoveIntent(mx, my);
            _world.FireHeld = _fire;
        }

        _world.Update(dt);
        GameCanvas.Invalidate();
        BackgroundCanvas.Invalidate();
    }

    // Collapse the latched flags to a single cardinal. When both axes are held,
    // the axis pressed most recently wins — that keeps turning into a side passage
    // responsive without ever producing a diagonal.
    (float mx, float my) ComposeCardinal()
    {
        float hx = (_right ? 1f : 0f) - (_left ? 1f : 0f);
        float vy = (_down  ? 1f : 0f) - (_up   ? 1f : 0f);

        if (hx != 0f && vy != 0f)
            return _horizontalLast ? (hx, 0f) : (0f, vy);

        return (hx, vy);
    }

    void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A: _left  = true; _horizontalLast = true;  break;
            case VirtualKey.Right:
            case VirtualKey.D: _right = true; _horizontalLast = true;  break;
            case VirtualKey.Up:
            case VirtualKey.W: _up    = true; _horizontalLast = false; break;
            case VirtualKey.Down:
            case VirtualKey.S: _down  = true; _horizontalLast = false; break;

            case VirtualKey.Space:
                // Held: fires the harpoon, then works the pump while attached.
                _fire = true;
                if (!_startHeld)
                {
                    if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver) _world.StartGame();
                    else if (_world.Mode == GameMode.Attract) _world.ReturnToTitle();
                }
                _startHeld = true;
                break;

            case VirtualKey.Enter:
                if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver) _world.StartGame();
                else if (_world.Mode == GameMode.Attract) _world.ReturnToTitle();
                break;
        }
        e.Handled = true;
    }

    void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A: _left  = false; break;
            case VirtualKey.Right:
            case VirtualKey.D: _right = false; break;
            case VirtualKey.Up:
            case VirtualKey.W: _up    = false; break;
            case VirtualKey.Down:
            case VirtualKey.S: _down  = false; break;
            case VirtualKey.Space: _fire = false; _startHeld = false; break;
        }
        e.Handled = true;
    }

    void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Focus(FocusState.Pointer);
        if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver)
            _world.StartGame();
        else if (_world.Mode == GameMode.Attract)
            _world.ReturnToTitle();
        e.Handled = true;
    }
}
