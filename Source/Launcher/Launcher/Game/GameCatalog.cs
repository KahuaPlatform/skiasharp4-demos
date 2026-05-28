using SkiaSharp;

namespace Launcher.Game;

// Static metadata for every demo in the catalog. The launcher renders one card
// per entry and, on click, navigates to its WasmPath (resolved relative to the
// site root in the production deploy — see Docs/Launcher/README for deploy
// layout). Order = the order in which cards appear on the launcher grid.
public static class GameCatalog
{
    public sealed record Entry(
        string Name,           // Display name as it appears on the card title
        string Gloss,          // Hawaiian-meaning subtitle ("stone", "moon", ...)
        string OriginalGame,   // What classic arcade this homages
        string Description,    // One-line tagline
        SKColor Color,         // Card accent + glow color
        string WasmPath,       // URL the Play button navigates to in wasm
        string FolderName);    // Source/<FolderName>/.../<FolderName>.csproj — used by the
                               // desktop preview to shell out to `dotnet run` on the project.

    public static readonly Entry[] Games =
    {
        new("POHAKU",   "stone",                    "Asteroids",     "Vector shooter — drift, thrust, fire, hyperspace",                new SKColor(0xFF, 0x33, 0xCC), "/games/pohaku/",   "Pohaku"),
        new("HOKULELE", "shooting stars",           "Galaga",        "Vertical formation shmup with tractor-beam captures",             new SKColor(0xFF, 0xEE, 0x44), "/games/hokulele/", "HokuLele"),
        new("LUA",      "pit / well",               "Tempest",       "Walk the rim of a 3D well, shoot flippers + tankers + spikers",   new SKColor(0x33, 0xF8, 0xFF), "/games/lua/",      "Lua"),
        new("MAHINA",   "moon",                     "Lunar Lander",  "Land the Apollo LM on multiplier pads under gravity + fuel",      new SKColor(0xCC, 0xEE, 0xFF), "/games/mahina/",   "Mahina"),
        new("HEIAU",    "sacred stone temple",      "Star Castle",   "Break three energy walls to reach the pohaku turret",             new SKColor(0xFF, 0xCC, 0x33), "/games/heiau/",    "Heiau"),
        new("KANAPI",   "centipede",                "Centipede",     "Snipe the centipede before it reaches you through the mushrooms",new SKColor(0x66, 0xFF, 0xAA), "/games/kanapi/",   "Kanapi"),
        new("ALALOA",   "long path / trail",        "Tron Cycles",   "4-cycle duel — last trail standing wins the round",               new SKColor(0x33, 0xF8, 0xFF), "/games/alaloa/",   "Alaloa"),
        new("HAHAI",    "to chase",                 "Pac-Man",       "Eat pellets, dodge four ghosts, flip them edible on a power dot", new SKColor(0xFF, 0xAA, 0x55), "/games/hahai/",    "Hahai"),
    };
}
