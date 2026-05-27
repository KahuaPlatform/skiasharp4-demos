using System.IO;

namespace Lua.Game;

// Tiny persistence layer for the high score. Writes a single int to a file in the
// user's local-application-data directory on desktop platforms; no-ops on wasm
// (where filesystem access requires a different mechanism).
public static class HighScoreStore
{
    static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lua", "highscore.txt");

    public static int Load()
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

    public static void Save(int score)
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
