using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Paku.Game;
using Windows.System;

namespace Paku;

/// <summary>
/// Hosts the playfield and drives Paku's game loop. Subscribes to
/// <c>CompositionTarget.Rendering</c> (vsync-aligned) to step the world each
/// frame, and translates raw key/pointer events into the world's input flags.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly GameWorld _world = new();
    private readonly Stopwatch _clock = new();
    private TimeSpan _lastTick;
    private bool _rendering;

    // Track held state for each input independently so releasing one key
    // doesn't kill thrust while another direction key is still held.
    private bool _up, _down, _left, _right, _space;
    private bool _pointerDown;

    public MainPage()
    {
        this.InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
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

    // One compositor frame: compute dt (clamped to [1/60, 1/30] so a stall can't
    // destabilize physics), push input into the world, step it, request a repaint.
    private void OnRendering(object? sender, object e)
    {
        var now = _clock.Elapsed;
        float dt = (float)(now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (dt > 1.0f / 30.0f) dt = 1.0f / 30.0f; // cap spikes (debugger pause, GC)
        if (dt <= 0) dt = 1.0f / 60.0f;            // first tick / clock quirk

        // Sync input state to world each frame
        _world.InputUp = _up;
        _world.InputDown = _down;
        _world.InputLeft = _left;
        _world.InputRight = _right;

        // Thrust is active when any directional key, space, or pointer is held
        _world.Thrusting = _up || _down || _left || _right || _space || _pointerDown;

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
            case VirtualKey.Down:
            case VirtualKey.S: _down = true; break;
            case VirtualKey.Space:
                _space = true;
                if (_world.Mode == GameMode.Attract || _world.Mode == GameMode.GameOver)
                    _world.StartGame();
                break;
            case VirtualKey.Enter:
                if (_world.Mode == GameMode.Attract || _world.Mode == GameMode.GameOver)
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
            case VirtualKey.A: _left = false; break;
            case VirtualKey.Right:
            case VirtualKey.D: _right = false; break;
            case VirtualKey.Up:
            case VirtualKey.W: _up = false; break;
            case VirtualKey.Down:
            case VirtualKey.S: _down = false; break;
            case VirtualKey.Space: _space = false; break;
        }
        e.Handled = true;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Focus(FocusState.Pointer);
        _pointerDown = true;

        var pt = e.GetCurrentPoint(this);
        _world.PointerX = (float)pt.Position.X;
        _world.PointerY = (float)pt.Position.Y;
        _world.PointerValid = true;

        if (_world.Mode == GameMode.Attract || _world.Mode == GameMode.GameOver)
            _world.StartGame();

        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _pointerDown = false;
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        _world.PointerX = (float)pt.Position.X;
        _world.PointerY = (float)pt.Position.Y;
        _world.PointerValid = true;
        e.Handled = true;
    }
}
