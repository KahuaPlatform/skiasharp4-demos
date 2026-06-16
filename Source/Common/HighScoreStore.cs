using System;
using System.IO;

namespace Arcade.Common;

/// <summary>
/// Tiny file-backed high-score persistence shared by every neon demo. Writes a
/// single integer to <c>%LocalAppData%/&lt;AppName&gt;/highscore.txt</c> on
/// desktop.
/// </summary>
/// <remarks>
/// Intentionally fail-silent and desktop-only: WASM has no filesystem access so
/// <see cref="Load"/> returns 0 and <see cref="Save"/> is a no-op there. High
/// scores are treated as a desktop courtesy, never a load-bearing feature, so
/// any I/O exception is swallowed rather than surfaced to the game.
/// </remarks>
public sealed class HighScoreStore
{
    readonly string _appName;

    /// <summary>
    /// Creates a store scoped to <paramref name="appName"/>, which becomes the
    /// per-demo subfolder under LocalApplicationData (keeps each demo's score
    /// independent).
    /// </summary>
    public HighScoreStore(string appName) { _appName = appName; }

    // Full path to this demo's score file under the OS per-user app-data root.
    string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        _appName, "highscore.txt");

    /// <summary>
    /// Reads the persisted high score. Returns 0 on browser, when the file is
    /// missing/unparseable, or on any I/O error.
    /// </summary>
    public int Load()
    {
        try
        {
            if (OperatingSystem.IsBrowser()) return 0;
            if (!File.Exists(SettingsPath)) return 0;
            return int.TryParse(File.ReadAllText(SettingsPath), out int v) ? v : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Persists <paramref name="score"/>, creating the per-demo directory if
    /// needed. No-op on browser; any I/O error is swallowed.
    /// </summary>
    public void Save(int score)
    {
        try
        {
            if (OperatingSystem.IsBrowser()) return;
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, score.ToString());
        }
        catch
        {
            // Fail silent — high-score persistence isn't load-bearing for the demo.
        }
    }
}
