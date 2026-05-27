using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pohaku.Game;
using Windows.System;

namespace Pohaku;

public sealed partial class MainPage : Page
{
    private readonly GameWorld _world = new();
    private readonly Stopwatch _clock = new();
    private TimeSpan _lastTick;
    private bool _rendering;

    private bool _left, _right, _up, _fire;
    private bool _firePressedThisFrame;
    private bool _hyperPressedThisFrame;

    public MainPage()
    {
        this.InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        PointerPressed += OnPointerPressed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus(FocusState.Programmatic);

        GameCanvas.World = _world;

        _clock.Start();
        _lastTick = _clock.Elapsed;

        CompositionTarget.Rendering += OnRendering;
        _rendering = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_rendering)
        {
            CompositionTarget.Rendering -= OnRendering;
            _rendering = false;
        }
    }

    private void OnRendering(object? sender, object e)
    {
        var now = _clock.Elapsed;
        float dt = (float)(now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (dt > 1.0f / 30.0f) dt = 1.0f / 30.0f;
        if (dt <= 0) dt = 1.0f / 60.0f;

        if (_world.Mode == GameMode.Playing)
        {
            _world.Ship.TurningLeft = _left;
            _world.Ship.TurningRight = _right;
            _world.Ship.ThrustOn = _up;
            if (_firePressedThisFrame)
            {
                _world.FireBullet();
            }
            else if (_fire && _world.Ship.ShootCooldown <= 0)
            {
                _world.FireBullet();
            }
            if (_hyperPressedThisFrame)
            {
                _world.HyperSpace();
            }
        }
        _firePressedThisFrame = false;
        _hyperPressedThisFrame = false;

        _world.Update(dt);
        GameCanvas.Invalidate();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A: _left = true; break;
            case VirtualKey.Right:
            case VirtualKey.D: _right = true; break;
            case VirtualKey.Up:
            case VirtualKey.W: _up = true; break;
            case VirtualKey.Space:
                if (_world.Mode == GameMode.Demo) _world.StartGame();
                else _firePressedThisFrame = true;
                _fire = true;
                break;
            case VirtualKey.H:
                _hyperPressedThisFrame = true;
                break;
            case VirtualKey.V:
                _world.VibrantMode = !_world.VibrantMode;
                break;
            case VirtualKey.Enter:
                if (_world.Mode == GameMode.Demo || _world.Mode == GameMode.GameOver) _world.StartGame();
                break;
        }
        e.Handled = true;
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A: _left = false; break;
            case VirtualKey.Right:
            case VirtualKey.D: _right = false; break;
            case VirtualKey.Up:
            case VirtualKey.W: _up = false; break;
            case VirtualKey.Space: _fire = false; break;
        }
        e.Handled = true;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Focus(FocusState.Pointer);
        if (_world.Mode == GameMode.Demo)
        {
            _world.StartGame();
        }
        e.Handled = true;
    }
}
