using System;
using System.IO;

namespace Arcade.Common;

// Tiny file-backed high-score persistence shared by every neon demo.
// Writes a single int to %LocalAppData%/<AppName>/highscore.txt on desktop;
// no-ops on wasm (filesystem access requires a different mechanism there).
public sealed class HighScoreStore
{
    readonly string _appName;
    public HighScoreStore(string appName) { _appName = appName; }

    string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        _appName, "highscore.txt");

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
