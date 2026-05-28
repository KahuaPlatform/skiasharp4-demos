using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Hahai.Game;
using Windows.System;

namespace Hahai;

public sealed partial class MainPage : Page
{
    readonly GameWorld _world = new();
    readonly Stopwatch _clock = new();
    TimeSpan _lastTick;
    bool _rendering;
    // Filter OS key-repeat so a held direction key doesn't re-pump
    // RequestedThisFrame at 30 Hz — that pump beats against the 60 Hz render
    // tick and causes visible frame jitter on Windows.
    Direction _lastRequestedDir = Direction.None;
    bool _spaceHeld;

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
            case VirtualKey.A:  RequestDirection(Direction.Left);  e.Handled = true; break;
            case VirtualKey.Right:
            case VirtualKey.D:  RequestDirection(Direction.Right); e.Handled = true; break;
            case VirtualKey.Up:
            case VirtualKey.W:  RequestDirection(Direction.Up);    e.Handled = true; break;
            case VirtualKey.Down:
            case VirtualKey.S:  RequestDirection(Direction.Down);  e.Handled = true; break;
            case VirtualKey.Space:
            case VirtualKey.Enter:
                if (_spaceHeld) { e.Handled = true; break; }
                _spaceHeld = true;
                if (_world.Mode == GameMode.Title || _world.Mode == GameMode.GameOver)
                    _world.StartGame();
                e.Handled = true;
                break;
        }
    }

    void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        // Releasing the held direction clears the dedupe so the next press of
        // the same key gets re-queued.
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.A:     if (_lastRequestedDir == Direction.Left)  _lastRequestedDir = Direction.None; break;
            case VirtualKey.Right:
            case VirtualKey.D:     if (_lastRequestedDir == Direction.Right) _lastRequestedDir = Direction.None; break;
            case VirtualKey.Up:
            case VirtualKey.W:     if (_lastRequestedDir == Direction.Up)    _lastRequestedDir = Direction.None; break;
            case VirtualKey.Down:
            case VirtualKey.S:     if (_lastRequestedDir == Direction.Down)  _lastRequestedDir = Direction.None; break;
            case VirtualKey.Space:
            case VirtualKey.Enter: _spaceHeld = false; break;
        }
    }

    void RequestDirection(Direction d)
    {
        // Ignore key-repeat for the same direction — Pac already moves in d on
        // its own; reasserting Requested every 33 ms only beats the render tick.
        if (_lastRequestedDir == d) return;
        _lastRequestedDir = d;
        _world.Requested = d;
        _world.RequestedThisFrame = true;
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
