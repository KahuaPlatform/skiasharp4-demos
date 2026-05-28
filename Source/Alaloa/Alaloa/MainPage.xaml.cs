using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Alaloa.Game;
using Windows.System;

namespace Alaloa;

public sealed partial class MainPage : Page
{
    readonly GameWorld _world = new();
    readonly Stopwatch _clock = new();
    TimeSpan _lastTick;
    bool _rendering;

    // Edge-triggered turn flags: cleared after one frame so a held key doesn't
    // fire turn-events every tick.
    bool _turnUp, _turnDown, _turnLeft, _turnRight;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        KeyDown += OnKeyDown;
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
            _world.TurnUp    = _turnUp;
            _world.TurnDown  = _turnDown;
            _world.TurnLeft  = _turnLeft;
            _world.TurnRight = _turnRight;
        }
        else
        {
            _world.TurnUp = _world.TurnDown = _world.TurnLeft = _world.TurnRight = false;
        }
        // Reset edge-triggered flags after consumption — only the next key press
        // produces another turn request.
        _turnUp = _turnDown = _turnLeft = _turnRight = false;

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
            case VirtualKey.A: _turnLeft = true; AudioEngine.PlayTurn(); break;
            case VirtualKey.Right:
            case VirtualKey.D: _turnRight = true; AudioEngine.PlayTurn(); break;
            case VirtualKey.Up:
            case VirtualKey.W: _turnUp = true; AudioEngine.PlayTurn(); break;
            case VirtualKey.Down:
            case VirtualKey.S: _turnDown = true; AudioEngine.PlayTurn(); break;
            case VirtualKey.Space:
            case VirtualKey.Enter:
                if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver)
                    _world.StartGame();
                break;
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
