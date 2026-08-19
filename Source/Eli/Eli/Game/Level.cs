using System;
using System.Collections.Generic;

namespace Eli.Game;

// The parsed result of one level: the static terrain (Field) plus the dynamic
// features the game instantiates as entities. Same terrain-vs-feature split Koa
// established, with one re-decision: BOULDERS are features, not tiles, because a
// tile cannot hold a sub-cell Y position while it falls.
public sealed class LoadedLevel
{
    public Field Field = null!;
    public Vec2  DiggerSpawn;
    public readonly List<(int col, int row, EnemyKind kind)> Enemies  = new();
    public readonly List<(int col, int row)>                 Boulders = new();
}

// Authored ASCII dirt fields + the AsciiMap-based loader. Legend:
//   ' ' sky        ':' dirt          '.' pre-carved tunnel   '#' bedrock
//   'O' boulder    'U' Uhane spawn   'N' Nohu spawn          '@' digger spawn
//
// Feature glyphs write their underlying terrain AND register the feature: the
// spawn glyphs sit on Tunnel, while a boulder sits in Dirt (it is embedded in the
// earth, and its support test reads the cell beneath it).
public static class Level
{
    // How many authored fields ship. Past this they re-serve in cycle with a
    // difficulty ramp. A PROCEDURAL dirt field would be meaningless here (an undug
    // field is just a rectangle), which is why Eli cycles where Koa falls through
    // to a BuildProcedural stub.
    public static int AuthoredCount => Maps.Length;

    // Extra enemies seeded past the authored maps, and the speed multiplier that
    // rides with them. Both cap so late levels stay playable.
    public const int   MaxExtraEnemies   = 4;
    public const float SpeedRampPerLevel = 0.06f;
    public const float MaxSpeedScale     = 1.50f;

    public static float SpeedScaleFor(int level) =>
        level <= AuthoredCount
            ? 1f
            : MathF.Min(MaxSpeedScale, 1f + SpeedRampPerLevel * (level - AuthoredCount));

    // Build the field for a 1-based `level`. Authored maps 1..AuthoredCount play in
    // order; past that they cycle, with up to MaxExtraEnemies extra monsters seeded
    // into free tunnel cells.
    public static LoadedLevel Build(int level, Random rng)
    {
        int idx = level <= Maps.Length ? level - 1 : (level - 1) % Maps.Length;
        var loaded = Parse(Maps[idx]);

        int extra = level <= Maps.Length ? 0 : Math.Min(MaxExtraEnemies, level - Maps.Length);
        for (int i = 0; i < extra; i++)
            if (TryFindFreeTunnel(loaded, rng, out int c, out int r))
                loaded.Enemies.Add((c, r, i % 2 == 0 ? EnemyKind.Uhane : EnemyKind.Nohu));

        return loaded;
    }

    // Parse one ASCII field. AsciiMap.Parse validates rectangularity up front and
    // throws on a ragged map, so a mis-typed row fails at load rather than
    // rendering wrong.
    static LoadedLevel Parse(string[] rows)
    {
        var result = new LoadedLevel();
        Field? field = null;

        var (cols, _) = AsciiMap.Parse(rows, (c, r, ch) =>
        {
            field ??= new Field(rows[0].Length, rows.Length);

            switch (ch)
            {
                case ' ': field[c, r] = Tile.Sky;    break;
                case '#': field[c, r] = Tile.Rock;   break;
                case '.': field[c, r] = Tile.Tunnel; break;

                case 'O':
                    // Embedded in the earth: the cell stays Dirt so the boulder
                    // reads as suspended until the player digs out from under it.
                    field[c, r] = Tile.Dirt;
                    result.Boulders.Add((c, r));
                    break;

                case 'U':
                    field[c, r] = Tile.Tunnel;
                    result.Enemies.Add((c, r, EnemyKind.Uhane));
                    break;

                case 'N':
                    field[c, r] = Tile.Tunnel;
                    result.Enemies.Add((c, r, EnemyKind.Nohu));
                    break;

                case '@':
                    field[c, r] = Tile.Tunnel;
                    result.DiggerSpawn = new Vec2(c * Field.CellSize + Field.CellSize * 0.5f,
                                                  r * Field.CellSize + Field.CellSize * 0.5f);
                    break;

                default: // ':' and anything unrecognised => packed dirt
                    field[c, r] = Tile.Dirt;
                    break;
            }
        });

        result.Field = field!;
        // Fallback spawn at the surface centre if a map forgot its '@'.
        if (result.DiggerSpawn.X == 0 && result.DiggerSpawn.Y == 0)
            result.DiggerSpawn = new Vec2(cols * Field.CellSize * 0.5f, Field.CellSize * 2.5f);
        return result;
    }

    // Find a pre-carved tunnel cell that isn't already an enemy spawn, for the extra
    // monsters on cycled levels. Bounded attempts so a dense map can't spin.
    static bool TryFindFreeTunnel(LoadedLevel lv, Random rng, out int col, out int row)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int c = rng.Next(lv.Field.Cols);
            int r = rng.Next(Field.SkyRows, lv.Field.Rows);
            if (lv.Field[c, r] != Tile.Tunnel) continue;

            bool taken = false;
            foreach (var (ec, er, _) in lv.Enemies)
                if (ec == c && er == r) { taken = true; break; }
            if (taken) continue;

            // Keep them off the digger's doorstep.
            if ((lv.Field.CellCenter(c, r) - lv.DiggerSpawn).Length < Field.CellSize * 6f) continue;

            col = c; row = r;
            return true;
        }
        col = row = 0;
        return false;
    }

    // ---- Authored fields ---------------------------------------------------
    // Four 44x30 fields (1408 x 960 world px at CellSize 32), each larger than the
    // viewport on BOTH axes so the bounded follow-camera scrolls in X and Y. Rows
    // 0-1 are open sky; rows 2-29 divide into four 7-row strata whose hue and score
    // multiplier both come from Field.StratumAt. Every boulder is authored resting
    // on dirt or bedrock, so nothing falls on frame 1.

    // Level 1 "Kahua" (foundation) — the gentle intro. One starter shaft down from
    // the surface, a shallow gallery per stratum, two vertical links joining them.
    // Three Uhane spread down the strata, one Nohu, three boulders.
    static readonly string[] Level1 =
    {
        "#                                          #",
        "#                                          #",
        "#:::::@::::::::::::::::::::::::::::::::::::#",
        "#:::::.::::::::::::::::::::::::::::::::::::#",
        "#:::::.:::::::::::O::::::::::::::::::::::::#",
        "#:::::.:::::.U..:::::::::::::::::::::::::::#",
        "#:::::...........::::::::::::::::::::::::::#",
        "#:::::::::::::::.::::::::::::::::::::::::::#",
        "#:::::::::::::::.::::::::::::::::::::::::::#",
        "#:::::::::::::::.::::::::::::::::::::::::::#",
        "#:::::::::::::::.::::::::::::::::::::::::::#",
        "#:::::::::::::::.:::::::::::::::::O::::::::#",
        "#:::::::.:::::::...........U....:::::::::::#",
        "#:::::::.:::::::::::::::::::::.::::::::::::#",
        "#:::::::.:::::::::::::::::::::.::::::::::::#",
        "#:::::::.:::::::::::::::::::::.::::::::::::#",
        "#:::::::.:::::::::::::::::::::.::::::::::::#",
        "#:::::::.:::::::::::::::::::::.::::::::::::#",
        "#:::::::.:::::::::::O:::::::::.::::::::::::#",
        "#:::::::..N...::::::::::::::::.::::::::::::#",
        "#:::::::::::::::::::::::::::::.::::::::::::#",
        "#:::::::::::::::::::::::::::::.::::::::::::#",
        "#:::::::::::::::::::::::::::::.::::::::::::#",
        "#:::::::::::::::::::::::::::::.::::::::::::#",
        "#:::::::::::::::::::::::::::::.::::::::::::#",
        "#:::::::::::::::::::::::::::::....U...:::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "############################################",
    };

    // Level 2 "Lua" (pit) — two deep vertical shafts and nothing else: there are no
    // horizontal galleries, so every lateral route is one you dig yourself. The
    // isolated pockets mean an Uhane will usually have to phase to reach you.
    static readonly string[] Level2 =
    {
        "#                                          #",
        "#                                          #",
        "#::::::::::.:::::::::@::::::::::.::::::::::#",
        "#::::::::::.:::::::::.::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.:::O::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#:::::::::.U.:::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::O:::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.:::::::::::::::::::.U.:::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.:::::O::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#:::::::::.N.:::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.:::::O::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::U:::::::::::::::::::.N.:::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::.::::::::::::::::::::.::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "############################################",
    };

    // Level 3 "Punawai" (spring) — a dense pre-carved warren through strata 0-1 over
    // completely untouched deep earth. Early enemies swarm through the warren; every
    // boulder is buried deep, so the crush bonuses have to be dug for.
    static readonly string[] Level3 =
    {
        "#                                          #",
        "#                                          #",
        "#:::::::@::::::::::::::::::::::::::::::::::#",
        "#::..........................N...........::#",
        "#::::.:::::.:::::.:::::.:::::.:::::.:::::::#",
        "#::::.:::::.:::::.:::::.:::::.:::::.:::::::#",
        "#::...U................U.................::#",
        "#::::.:::::.:::::.:::::.:::::.:::::.:::::::#",
        "#::::.:::::.:::::.:::::.:::::.:::::.:::::::#",
        "#::........N.......................U.....::#",
        "#::::.:::::.:::::.:::::.:::::.:::::.:::::::#",
        "#::::.:::::.:::::.:::::.:::::.:::::.:::::::#",
        "#::..............U.......................::#",
        "#::::.:::::.:::::.:::::.:::::.:::::.:::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#:::::::::::::::::::::::::::::::::::::O::::#",
        "#:::::::::::::O::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::O:::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::O:::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#:::::::O::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "############################################",
    };

    // Level 4 "Papaku" (bedrock) — three bedrock pillars split the field into four
    // quadrants connected ONLY across the surface band, with a boulder poised in the
    // dirt beside each quadrant's access shaft. Nohu are quadrant-locked (they cannot
    // phase), so clearing the field means visiting every quadrant.
    static readonly string[] Level4 =
    {
        "#                                          #",
        "#                                          #",
        "#:::::.::::::::::.:::@:::::.::::::::::.::::#",
        "#:::::.::::::::::.:::.:::::.::::::::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:O:::##:::.:O::##:::.:O::##::::.:O::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::.......:##:.......##:.......##:.......:#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::U:::::##:::U::::##:::U::::##::::U::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:O:.::::##:::.::::##::::.::::#",
        "#:::::.:::::##:::.::::##:::.::::##::::.::::#",
        "#:::::N:::::##:::.::::##:::N::::##::::N::::#",
        "#:::::::::::##::::::::##::::::::##:::::::::#",
        "#:::::::::::##::::::::##::::::::##:::::::::#",
        "#:::::::::::##::::::::##::::::::##:::::::::#",
        "#:::::::::::##::::::::##::::::::##:::::::::#",
        "#:::::::::::##::::::::##::::::::##:::::::::#",
        "#::::::::::::::::::::::::::::::::::::::::::#",
        "############################################",
    };

    // Four authored fields in play order. GameWorld advances on clearing every
    // enemy; Level.Build cycles them past the fourth with extra monsters.
    static readonly string[][] Maps = { Level1, Level2, Level3, Level4 };
}
