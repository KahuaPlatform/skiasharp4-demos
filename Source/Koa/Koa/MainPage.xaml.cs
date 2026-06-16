using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Koa.Game;
using Windows.System;

namespace Koa;

public sealed partial class MainPage : Page
{
    readonly GameWorld _world = new();
    readonly Stopwatch _clock = new();
    TimeSpan _lastTick;
    bool _rendering;

    // Latched movement flags — Koa is 8-directional, so we keep one bool per
    // cardinal and compose the move vector each frame (diagonals fall out of two
    // simultaneously-held keys). Fire is a held flag; potion/start are edge-triggered.
    bool _up, _down, _left, _right;
    bool _fire;
    bool _potionPressed;
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
            // Compose the 8-direction intent from the latched cardinals; the
            // world normalises it and keeps the last non-zero as the aim vector.
            float mx = (_right ? 1f : 0f) - (_left ? 1f : 0f);
            float my = (_down  ? 1f : 0f) - (_up   ? 1f : 0f);
            _world.SetMoveIntent(mx, my);
            _world.FireHeld = _fire;
            if (_potionPressed) _world.UsePotion();
        }
        _potionPressed = false;

        _world.Update(dt);
        GameCanvas.Invalidate();
        BackgroundCanvas.Invalidate();
    }

    void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A: _left = true; break;
            case VirtualKey.Right:
            case VirtualKey.D: _right = true; break;
            case VirtualKey.Up:
            case VirtualKey.W: _up = true; break;
            case VirtualKey.Down:
            case VirtualKey.S: _down = true; break;
            case VirtualKey.Space:
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
            case VirtualKey.Q:
            case VirtualKey.E:
                _potionPressed = true; // quaff a smite potion (screen-clear)
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
            case VirtualKey.W: _up = false; break;
            case VirtualKey.Down:
            case VirtualKey.S: _down = false; break;
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
