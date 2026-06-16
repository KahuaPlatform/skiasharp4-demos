using System;
using System.Collections.Generic;

namespace Koa.Game;

// The parsed result of one level: the static terrain (TileMap) plus the dynamic
// features (hero spawn, generators, pickups) the game instantiates as entities.
// The terrain-vs-feature split is the key authoring decision (see DESIGN): walls
// / doors / exit bake into cheap-to-test tiles; everything destroyable or
// collectable becomes an entity.
public sealed class LoadedLevel
{
    public TileMap Map = null!;
    public Vec2    HeroSpawn;
    public readonly List<(int col, int row, EnemyKind kind)> Generators = new();
    public readonly List<(int col, int row, PickupKind kind)> Pickups   = new();
}

// Authored ASCII dungeons + the AsciiMap-based loader. Legend:
//   '#' wall     '.' floor    'D' door     'X' exit     'G' generator
//   'K' key      'F' food     'P' potion   '$' treasure '@' hero spawn
//   ' ' void (solid filler outside the dungeon)
public static class Level
{
    // How many authored maps ship; beyond this, BuildProcedural takes over.
    public static int AuthoredCount => Maps.Length;

    // Build the level for a 1-based `level` index. Authored maps cycle/extend
    // into the procedural stub past the authored count.
    public static LoadedLevel Build(int level, Random rng)
    {
        if (level >= 1 && level <= Maps.Length)
            return Parse(Maps[level - 1]);
        return BuildProcedural(level, rng);
    }

    // Parse one ASCII map into a LoadedLevel. Static glyphs write into the
    // TileMap; feature glyphs both register the feature AND write the underlying
    // terrain (a key sits on floor; a generator marks its own solid tile).
    static LoadedLevel Parse(string[] rows)
    {
        var result = new LoadedLevel();
        TileMap? map = null;

        var (cols, rowCount) = AsciiMap.Parse(rows, (c, r, ch) =>
        {
            // Lazily size the map from the first glyph (AsciiMap has already
            // validated rectangularity, so cols is stable).
            map ??= new TileMap(rows[0].Length, rows.Length);

            switch (ch)
            {
                case '#': map[c, r] = Tile.Wall; break;
                case ' ': map[c, r] = Tile.Void; break;
                case 'D': map[c, r] = Tile.Door; break;
                case 'X': map[c, r] = Tile.Exit; break;

                case 'G':
                    map[c, r] = Tile.Generator;
                    // Spawn kind cycles by position so a map mixes monster types.
                    result.Generators.Add((c, r, (EnemyKind)((c + r) % 3)));
                    break;

                case 'K': map[c, r] = Tile.Floor; result.Pickups.Add((c, r, PickupKind.Key));      break;
                case 'F': map[c, r] = Tile.Floor; result.Pickups.Add((c, r, PickupKind.Food));     break;
                case 'P': map[c, r] = Tile.Floor; result.Pickups.Add((c, r, PickupKind.Potion));   break;
                case '$': map[c, r] = Tile.Floor; result.Pickups.Add((c, r, PickupKind.Treasure)); break;

                case '@':
                    map[c, r] = Tile.Floor;
                    result.HeroSpawn = new Vec2(c * TileMap.CellSize + TileMap.CellSize * 0.5f,
                                                r * TileMap.CellSize + TileMap.CellSize * 0.5f);
                    break;

                default: // '.' and anything unrecognised => floor
                    map[c, r] = Tile.Floor;
                    break;
            }
        });

        result.Map = map!;
        // Fallback hero spawn at the centre if the map forgot a '@'.
        if (result.HeroSpawn.X == 0 && result.HeroSpawn.Y == 0)
            result.HeroSpawn = new Vec2(cols * TileMap.CellSize * 0.5f, rowCount * TileMap.CellSize * 0.5f);
        return result;
    }

    // Procedural endless-mode stub: a walled rectangular room with a ring of
    // pillars, scaled by level, a handful of generators and some food. Deliberately
    // simple — enough to keep the loop going past the authored maps; a real maze
    // generator can replace this later without touching GameWorld.
    static LoadedLevel BuildProcedural(int level, Random rng)
    {
        int cols = 28 + Math.Min(level, 12);
        int rows = 20 + Math.Min(level, 8);
        var map = new TileMap(cols, rows);

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                bool border = c == 0 || r == 0 || c == cols - 1 || r == rows - 1;
                map[c, r] = border ? Tile.Wall : Tile.Floor;
            }

        // Scattered pillar blocks for the swarm to flow around.
        int pillars = 8 + level * 2;
        for (int i = 0; i < pillars; i++)
        {
            int c = 2 + rng.Next(cols - 4);
            int r = 2 + rng.Next(rows - 4);
            map[c, r] = Tile.Wall;
        }

        var result = new LoadedLevel { Map = map };
        result.HeroSpawn = map.CellCenter(cols / 2, rows / 2);

        // Generators in the corners; count scales with level.
        int genCount = Math.Min(2 + level, 8);
        for (int i = 0; i < genCount; i++)
        {
            int c = 2 + rng.Next(cols - 4);
            int r = 2 + rng.Next(rows - 4);
            if (map[c, r] != Tile.Floor) continue;
            map[c, r] = Tile.Generator;
            result.Generators.Add((c, r, (EnemyKind)(i % 3)));
        }

        // A bit of food to fight the drain, plus an exit far from spawn.
        for (int i = 0; i < 3 + level; i++)
        {
            int c = 2 + rng.Next(cols - 4);
            int r = 2 + rng.Next(rows - 4);
            if (map[c, r] == Tile.Floor) result.Pickups.Add((c, r, PickupKind.Food));
        }
        map[cols - 2, rows - 2] = Tile.Exit;
        return result;
    }

    // ---- Authored maps -----------------------------------------------------
    // Faithful transcriptions of the first eight authentic arcade GAUNTLET levels
    // (vgmaps.com / RBR Arcade — https://www.vgmaps.com/Atlas/Arcade/index.htm).
    // The reference maps show only the WALL STRUCTURE and EXIT labels; generators,
    // items, the hero spawn (@) and the single advancing exit (X) are placed here
    // by Gauntlet convention and for good play. Each map is rectangular (AsciiMap
    // enforces it) and several screens across, so the bounded follow-camera
    // scrolls. Generator count ramps up across the eight levels; Level 8 "Go Back"
    // keeps its authentic chaotic trap layout. Glyph legend is in the header above.

    // Level 1 "One" — 45x32. A large open arena split by an S/Z divider: a wall
    // runs down from the top edge, a gap, then a jog left and a wall dropping to the
    // bottom edge — cutting the arena into two halves joined around the jog. Gentle
    // intro: only two generators (one per half), some food/treasure, one potion.
    static readonly string[] Level1 =
    {
        "#############################################",
        "#@....................#....................X#",
        "#.....................#.....................#",
        "#.....................#.....................#",
        "#.......F.............#.........G...........#",
        "#.....................#.....................#",
        "#.....................#.....................#",
        "#..........G..........#.........$...........#",
        "#.....................#.....................#",
        "#.....................#.....................#",
        "#.....................#.....................#",
        "#........$............#.....................#",
        "#.....................#.........F...........#",
        "#.....................#.....................#",
        "#.....................#.....................#",
        "#.....................#.....................#",
        "#...........................................#",
        "#..........###########......................#",
        "#..........#................................#",
        "#..........#................................#",
        "#..........#................................#",
        "#..........#..............F.................#",
        "#..........#................................#",
        "#..........#..................P.............#",
        "#..........#................................#",
        "#..........#................................#",
        "#..........#................................#",
        "#..........#................................#",
        "#..........#................................#",
        "#..........#................................#",
        "#..........#................................#",
        "#############################################",
    };

    // Level 2 "Two" — 45x32. The serpentine comb: four full-height walls divide the
    // arena into vertical corridors, each wall gapped at an alternating end so the
    // only route weaves up-and-down through the comb. Spawn bottom-left, exit
    // top-right. Generators tucked in the corridor dead-ends; food along the route.
    static readonly string[] Level2 =
    {
        "#############################################",
        "#.................#.................#......X#",
        "#.................#...G........G....#...G...#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#...F....#........#...F....#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#...F....#........#...F....#........#...F...#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#........#........#........#........#.......#",
        "#...G....#...G.............#................#",
        "#@.......#.................#................#",
        "#############################################",
    };

    // Level 3 "Three" — 44x32. A wide-open arena littered with free-standing pillar
    // blocks (the scattered wall chunks on the reference). Heavy generator presence
    // for an open shootout; exit on the south-east edge behind a small wall shelf.
    static readonly string[] Level3 =
    {
        "############################################",
        "#@.........................................#",
        "#..........##...............##.............#",
        "#..........##.....G.........##.............#",
        "#.............................F............#",
        "#....##....................................#",
        "#....##........$......................G....#",
        "#..........................................#",
        "#.##................F............G.........#",
        "#.##.......................................#",
        "#.....F....................................#",
        "#........G......##.........................#",
        "#....G..........##......$..................#",
        "#.......##.................................#",
        "#.......##...........##....................#",
        "#..##................##...G................#",
        "#..##...........................F..........#",
        "#.......G.....................##...........#",
        "#...............F.............##...........#",
        "#..........##....................G.........#",
        "#......##..##.....................$........#",
        "#......##..G...............................#",
        "#.....F.........##.........................#",
        "#...............##......................F..#",
        "#..................##.......G..............#",
        "#..................##......................#",
        "#..............G..............##...........#",
        "#........$............F.......##...........#",
        "#...................................######.#",
        "#.P...............F...........G............#",
        "#.........................................X#",
        "############################################",
    };

    // Level 4 "Four" — 45x33. The square-spiral maze: concentric nested wall rings
    // joined by gaps into one path that spirals inward to a central room holding the
    // advancing exit. Spawn just inside the outer ring (bottom-left, by the arcade
    // EXIT label). Generators line the spiral arms so the swarm pours along the path.
    static readonly string[] Level4 =
    {
        "#############################################",
        "#.......G...................................#",
        "#.#########################################.#",
        "#.#.......F.........G.....................#.#",
        "#.#.#####################################.#.#",
        "#.#.#...................................#.#.#",
        "#.#.#.#################################.#.#.#",
        "#.#.#.#...............................#.#.#.#",
        "#.#.#.#.##############.##############.#.#.#.#",
        "#.#.#.#.#...........................#.#.#.#.#",
        "#.#.#.#.#.#########################.#.#.#.#.#",
        "#.#.#.#.#.#.......................#.#.#.#.#.#",
        "#.#.#.#.#.#.#####################.#.#.#.#.#.#",
        "#.#.#.#.#.#.#...................#.#.#.#.#.#.#",
        "#.#.#.#.#.#.#.#################.#.#.#.#.#.#.#",
        "#.#.#.#.#.#.#.#...............#.#.#.#.#.#.#.#",
        "#.#.#...#.#.#.........X.......#.#...#.#.#...#",
        "#.#.#.#.#.#.#.#.....F.........#.#.#.#.#.#.#.#",
        "#.#.#.#.#.#.#.#################.#.#.#.#.#.#.#",
        "#.#.#.#.#.#.#...................#.#.#.#.#.#.#",
        "#.#.#.#.#.#.##########.##########.#.#.#.#.#.#",
        "#.#.#.#.#.#.......................#.#.#.#.#.#",
        "#.#.#.#.#.#########################.#.#.#.#.#",
        "#.#.#.#.#...........................#.#.#.#.#",
        "#.#.#.#.#############################.#.#.#.#",
        "#.#.#.#...............................#.#.#.#",
        "#.#.#.#################################.#.#.#",
        "#.#.#...................................#.#.#",
        "#.#.##################.##################.#.#",
        "#.#.......................................#.#",
        "#.#########################################.#",
        "#@..........F.................G............P#",
        "#############################################",
    };

    // Level 5 "Five" — 45x33. A second spiral, opening into a small central room with
    // the EXIT dead-centre (as on the reference). Spawn top-left. More generators
    // than Level 4, with treasure and a potion along the way in.
    static readonly string[] Level5 =
    {
        "#############################################",
        "#@..................G............F..........#",
        "#.#########################################.#",
        "#.#.....G................$................#.#",
        "#.#.#####################################.#.#",
        "#.#.#.............F.....................#.#.#",
        "#.#.#.#################################.#.#.#",
        "#.#.#.#...............................#.#.#.#",
        "#.#.#.#.##############.##############.#.#.#.#",
        "#.#.#.#.#...........................#.#.#.#.#",
        "#.#.#.#.#.#########################.#.#.#.#.#",
        "#.#.#.#.#.#.......................#.#.#.#.#.#",
        "#.#.#.#.#.#.#####################.#.#.#.#.#.#",
        "#.#.#.#.#.#.#...................#.#.#.#.#.#.#",
        "#.#.#.#.#.#.#.#################.#.#.#.#.#.#.#",
        "#.#.#.#.#.#.#.#..........G....#.#.#.#.#.#.#.#",
        "#.#.#...#.#.#.........X.......#.#...#.#.#...#",
        "#.#.#.#.#.#.#.#...............#.#.#.#.#.#.#.#",
        "#.#.#.#.#.#.#.#################.#.#.#.#.#.#.#",
        "#.#.#.#.#.#.#...................#.#.#.#.#.#.#",
        "#.#.#.#.#.#.##########.##########.#.#.#.#.#.#",
        "#.#.#.#.#.#.......................#.#.#.#.#.#",
        "#.#.#.#.#.#########################.#.#.#.#.#",
        "#.#.#.#.#...........................#.#.#.#.#",
        "#.#.#.#.#############################.#.#.#.#",
        "#.#.#.#...............................#.#.#.#",
        "#.#.#.#################################.#.#.#",
        "#.#.#.........................G.........#.#.#",
        "#.#.##################.##################.#.#",
        "#.#.......G...................F...........#.#",
        "#.#########################################.#",
        "#P..........................................#",
        "#############################################",
    };

    // Level 6 "Six" — 44x32. An open level strewn with T-shaped, L-shaped and straight
    // wall fragments plus free-standing block pillars — a loose tactical maze rather
    // than a tight corridor crawl. Exit bottom-left. Many generators behind the
    // fragments.
    static readonly string[] Level6 =
    {
        "############################################",
        "#..........................................#",
        "#..#######.G........................#....@.#",
        "#........#..........................#G.....#",
        "#........#..........................#......#",
        "#........#.....########.............#...$..#",
        "#........#.......G..........#######........#",
        "#......G............#......................#",
        "#...................#...........F..........#",
        "#........#..........#......................#",
        "#.#####..#..........#.................G....#",
        "#........#....G.....#......................#",
        "#........#...########...................P..#",
        "#......#.#.................................#",
        "#....F.#...................................#",
        "#.#....#...........G..........######.......#",
        "#.#....#$.........#........................#",
        "#.#....#..........#........G...............#",
        "#.#...............#........................#",
        "#..G#######.......#...........#............#",
        "#.................#...........#.........F..#",
        "#.................#....G......#............#",
        "#.........G....########.......#............#",
        "#.............................#..#.........#",
        "#................................#.........#",
        "#.....######.....G...............#G........#",
        "#..........#.....................#.........#",
        "#..........#............#######..#.........#",
        "#..........#...............................#",
        "#....G.....#..........G............G.......#",
        "#X.........................................#",
        "############################################",
    };

    // Level 7 "Seven" — 46x32. The boustrophedon snake: long horizontal walls reach
    // alternately from the left and right edges, each gapped at its far end so the
    // only path is one long corridor switchbacking down the level. Spawn bottom-left,
    // exit top-right. Generators in the switchback bends; food along the corridor.
    static readonly string[] Level7 =
    {
        "##############################################",
        "#...........................................X#",
        "############################################.#",
        "#............................................#",
        "#.############################################",
        "#...........................................G#",
        "############################################.#",
        "#............................................#",
        "#.############################################",
        "#G...........................................#",
        "############################################.#",
        "#............................................#",
        "#.############################################",
        "#...................F........................#",
        "############################################.#",
        "#...........................................G#",
        "#.############################################",
        "#............................................#",
        "############################################.#",
        "#G...........................................#",
        "#.############################################",
        "#.............................F..............#",
        "############################################.#",
        "#............................................#",
        "#.############################################",
        "#...........................................G#",
        "############################################.#",
        "#......F.....................................#",
        "#.############################################",
        "#............................................#",
        "#@...........................................#",
        "##############################################",
    };

    // Level 8 "Go Back" — 44x32. The historic trap/joke level: a dense, chaotic
    // brick-wall mess of short staggered wall segments with the EXIT jammed in the
    // top-left corner — easy to die in, hard to read. Heaviest generator load of the
    // eight. Spawn bottom-right; the exit (the "go back" gag) is the far corner.
    static readonly string[] Level8 =
    {
        "############################################",
        "#X.##..#.#.......#...#...#..##..##..##.##..#",
        "#..G.#...#G..#...#...#..G....#.G......G##..#",
        "#..........................................#",
        "#...##.##..#...#...#...#...#...#...#...#.###",
        "##.G......G#...##G.##...G....#.G......G....#",
        "#....#.##..#.#..##.#.#.##..#.#..##.###..##.#",
        "#....#..##.##..##..#.#.........#...#.#.....#",
        "#..#...#..G##...##..##..G#...#.G.....#...###",
        "#..........................................#",
        "#...##.##..##....#.....##......#...#.#..##.#",
        "#..##...##...#.....#...##...##...#.....#...#",
        "#....#..##.###.#.#...#..##.##...##.....#####",
        "#..#.#..##..##.........##..#.#...#...#..##.#",
        "#...##...#.....#...#...#............##.....#",
        "#..........................................#",
        "#......##..##..##..#.#...#..##..##.##..#...#",
        "#....#.........##...##...#...#...#...#...###",
        "#..###.###.#...##..###.###.##..#.#..##.###.#",
        "#...##.##..##....#..##.##..##..#....##.##..#",
        "##...#.....#...#...##..##........#...#...###",
        "#..........................................#",
        "#....#.....##..##..#.#..##..##..##..##.##..#",
        "#......#...#...##...##..##..##..##..##..##.#",
        "#...##.###..##.##..###.##..#.#.##..#.#..##.#",
        "##..##..##..##.##..#.#...#...#...#.........#",
        "#......##..##......##............#.....#...#",
        "#..........................................#",
        "#..#...#.#..##.##........#..##..##..##.....#",
        "#..F....##...#...#...#.....#...##..##..##..#",
        "#..###.##..###.###...#.###...#.....###....@#",
        "############################################",
    };

    // Eight authored maps in arcade order: One, Two, Three, Four, Five, Six,
    // Seven, Go Back. GameWorld advances through these on reaching each exit
    // (LoadLevel(Level+1)); past the eighth, Level.Build falls through to the
    // procedural endless stub so the loop keeps going.
    static readonly string[][] Maps =
        { Level1, Level2, Level3, Level4, Level5, Level6, Level7, Level8 };
}
