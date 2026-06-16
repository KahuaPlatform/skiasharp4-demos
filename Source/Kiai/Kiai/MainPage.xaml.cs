using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Kiai.Game;
using Windows.System;

namespace Kiai;

// Host page + game loop. Mirrors the Pohaku/HokuLele loop structure: a clamped
// CompositionTarget.Rendering tick pushes latched input into the ship, fires the
// per-frame edge one-shots (fire/smart-bomb/hyperspace), advances the world, and
// invalidates both the playfield and the ambient background canvases.
//
// Input model (Kia'i is Defender-style directional thrust, NOT rotate-and-thrust):
//   Arrows / WASD — thrust left/right/up/down (left/right also flip the ship facing)
//   Space         — fire (and start a game from Title/GameOver)
//   B             — smart bomb (edge-triggered one-shot)
//   H             — hyperspace (edge-triggered one-shot)
//   Enter         — start a game from Title/GameOver
// Any key/click during Attract returns to the Title screen.
public sealed partial class MainPage : Page
{
    private readonly GameWorld _world = new();
    private readonly Stopwatch _clock = new();
    private TimeSpan _lastTick;
    private bool _rendering;

    // Latched directional-thrust + fire flags (held while the key is down).
    private bool _thrustLeft, _thrustRight, _thrustUp, _thrustDown, _fire;
    // Edge flags: set on key-down, consumed (and cleared) once per frame so a
    // single press triggers exactly one action.
    private bool _firePressedThisFrame;
    private bool _smartBombPressedThisFrame;
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
        if (dt > 1.0f / 30.0f) dt = 1.0f / 30.0f;   // clamp huge stalls
        if (dt <= 0) dt = 1.0f / 60.0f;

        if (_world.Mode == GameMode.Playing)
        {
            _world.Ship.ThrustLeft  = _thrustLeft;
            _world.Ship.ThrustRight = _thrustRight;
            _world.Ship.ThrustUp    = _thrustUp;
            _world.Ship.ThrustDown  = _thrustDown;

            if (_firePressedThisFrame || (_fire && _world.Ship.ShootCooldown <= 0))
                _world.FireBullet();
            if (_smartBombPressedThisFrame) _world.SmartBomb();
            if (_hyperPressedThisFrame)     _world.HyperSpace();
        }
        // Edge flags are single-frame: clear after every tick.
        _firePressedThisFrame = false;
        _smartBombPressedThisFrame = false;
        _hyperPressedThisFrame = false;

        _world.Update(dt);
        GameCanvas.Invalidate();
        BackgroundCanvas.Invalidate();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Attract loop: any key drops back to Title.
        if (_world.Mode == GameMode.Attract)
        {
            _world.ReturnToTitle();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A: _thrustLeft = true; break;
            case VirtualKey.Right:
            case VirtualKey.D: _thrustRight = true; break;
            case VirtualKey.Up:
            case VirtualKey.W: _thrustUp = true; break;
            case VirtualKey.Down:
            case VirtualKey.S: _thrustDown = true; break;
            case VirtualKey.Space:
                if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver)
                    _world.StartGame();
                else
                    _firePressedThisFrame = true;
                _fire = true;
                break;
            case VirtualKey.B:
                _smartBombPressedThisFrame = true;
                break;
            case VirtualKey.H:
                _hyperPressedThisFrame = true;
                break;
            case VirtualKey.Enter:
                if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver)
                    _world.StartGame();
                break;
        }
        e.Handled = true;
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A: _thrustLeft = false; break;
            case VirtualKey.Right:
            case VirtualKey.D: _thrustRight = false; break;
            case VirtualKey.Up:
            case VirtualKey.W: _thrustUp = false; break;
            case VirtualKey.Down:
            case VirtualKey.S: _thrustDown = false; break;
            case VirtualKey.Space: _fire = false; break;
        }
        e.Handled = true;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Focus(FocusState.Pointer);
        if (_world.Mode == GameMode.Attract)
            _world.ReturnToTitle();
        else if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver)
            _world.StartGame();
        e.Handled = true;
    }
}
