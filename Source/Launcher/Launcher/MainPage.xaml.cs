using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Launcher.Game;

namespace Launcher;

public sealed partial class MainPage : Page
{
    readonly LauncherWorld _world = new();
    readonly Stopwatch _clock = new();
    TimeSpan _lastTick;
    bool _rendering;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PointerMoved += OnPointerMoved;
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

    // Pointer position is in canvas/visual coordinates. The renderer transforms
    // world coords into canvas coords via the Viewbox scale + letterbox offset,
    // so we need to unproject the pointer back into world space before hit-
    // testing against `_world.CardRects` (which are stored in world coords).
    void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(GameCanvas).Position;
        var (wx, wy) = CanvasToWorld((float)p.X, (float)p.Y);
        _world.PointerX = wx;
        _world.PointerY = wy;
    }

    void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Focus(FocusState.Pointer);
        var p = e.GetCurrentPoint(GameCanvas).Position;
        var (wx, wy) = CanvasToWorld((float)p.X, (float)p.Y);
        _world.PointerX = wx;
        _world.PointerY = wy;
        for (int i = 0; i < _world.CardRects.Length; i++)
        {
            if (_world.CardRects[i].Contains(wx, wy))
            {
                NavigateToGame(GameCatalog.Games[i]);
                break;
            }
        }
        e.Handled = true;
    }

    (float wx, float wy) CanvasToWorld(float px, float py)
    {
        float cw = (float)GameCanvas.ActualWidth;
        float ch = (float)GameCanvas.ActualHeight;
        if (cw <= 0 || ch <= 0) return (px, py);
        float scale = MathF.Min(cw / _world.Width, ch / _world.Height);
        if (scale <= 0) return (px, py);
        float ox = (cw - _world.Width  * scale) / 2f;
        float oy = (ch - _world.Height * scale) / 2f;
        return ((px - ox) / scale, (py - oy) / scale);
    }

    static void NavigateToGame(GameCatalog.Entry entry)
    {
#if __WASM__
        // Wasm: send the browser to the deployed game's path. In a local dev
        // session this 404s unless the games have been published to
        // /games/<name>/ — by design, since the launcher targets a static
        // multi-app deploy (see Docs/Launcher/README).
        try { Uno.Foundation.WebAssemblyRuntime.InvokeJS($"window.location.href = '{entry.WasmPath}';"); }
        catch { /* fail silent */ }
#else
        // Desktop preview: prefer a pre-built exe so clicking a card launches
        // sub-second instead of re-running MSBuild. Run `Builds\Build-All.ps1`
        // once and every game has an exe at
        // Source\<Folder>\<Folder>\bin\<cfg>\net10.0-desktop\<Folder>.exe.
        // Falls back to `dotnet run` for fresh clones where nothing's built yet.
        try
        {
            string? repoRoot = FindRepoRoot();
            if (repoRoot is null) return;
            string gameDir = System.IO.Path.Combine(repoRoot, "Source", entry.FolderName, entry.FolderName);
            foreach (var cfg in new[] { "Release", "Debug" })
            {
                string exe = System.IO.Path.Combine(gameDir, "bin", cfg, "net10.0-desktop", $"{entry.FolderName}.exe");
                if (System.IO.File.Exists(exe))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = true,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(exe),
                    });
                    return;
                }
            }
            string project = System.IO.Path.Combine(gameDir, $"{entry.FolderName}.csproj");
            if (!System.IO.File.Exists(project)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{project}\" -c Debug -f net10.0-desktop",
                UseShellExecute = true,
                WorkingDirectory = repoRoot,
            });
        }
        catch { /* fail silent */ }
#endif
    }

    // Walk up from the running app's directory until we find one that has a
    // "Source" subfolder — that's the repo root. Returns null if we walk off
    // the top of the tree without finding it (e.g. the launcher was installed
    // somewhere outside the dev repo).
    static string? FindRepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "Source")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
