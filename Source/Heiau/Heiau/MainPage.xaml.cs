using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Heiau.Game;
using Windows.System;

namespace Heiau;

public sealed partial class MainPage : Page
{
    readonly GameWorld _world = new();
    readonly Stopwatch _clock = new();
    TimeSpan _lastTick;
    bool _rendering;

    bool _left, _right, _thrust, _fire;

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
            _world.RotateLeft  = _left;
            _world.RotateRight = _right;
            _world.Thrust      = _thrust;
            _world.Firing      = _fire;
        }
        else
        {
            _world.RotateLeft = _world.RotateRight = _world.Thrust = _world.Firing = false;
        }

        _world.Update(dt);
        GameCanvas.Invalidate();
        BackgroundCanvas.Invalidate();
    }

    void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_world.Mode == GameMode.Attract)
        {
            _world.ReturnToTitle();
            e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A: _left = true; break;
            case VirtualKey.Right:
            case VirtualKey.D: _right = true; break;
            case VirtualKey.Up:
            case VirtualKey.W: _thrust = true; break;
            case VirtualKey.Space:
                if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver)
                    _world.StartGame();
                else
                    _fire = true;
                break;
            case VirtualKey.Enter:
                if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver)
                    _world.StartGame();
                break;
        }
        e.Handled = true;
    }

    void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A: _left = false; break;
            case VirtualKey.Right:
            case VirtualKey.D: _right = false; break;
            case VirtualKey.Up:
            case VirtualKey.W: _thrust = false; break;
            case VirtualKey.Space: _fire = false; break;
        }
        e.Handled = true;
    }

    void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Focus(FocusState.Pointer);
        if (_world.Mode == GameMode.Attract)
        {
            _world.ReturnToTitle();
        }
        else if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver)
        {
            _world.StartGame();
        }
        e.Handled = true;
    }
}
