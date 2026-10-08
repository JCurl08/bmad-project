using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What CubeLayout needs to know about the module library: pool sizes and core slots.</summary>
    public interface IModuleCatalog
    {
        /// <summary>Number of modules in a science theme's pool.</summary>
        int PoolSize(Theme theme);

        /// <summary>Number of core-entrance slots in a pool module.</summary>
        int CoreSlotCount(Theme theme, int moduleIndex);

        /// <summary>
        /// The pool index of a theme's island module (1.25: Math's island screens), always the last of its pool, or -1 when
        /// the theme has none. The layout never draws it at random; it gives it to IslandScreensPerFace chosen screens.
        /// </summary>
        int IslandModuleIndex(Theme theme) => -1;

        /// <summary>
        /// The pool index of a theme's town module (4.5: every face's centre is its town), at the end of its pool before any
        /// island module, or -1 when the theme has none. The layout never draws it at random; it gives it to the town cell.
        /// </summary>
        int TownModuleIndex(Theme theme) => -1;

        /// <summary>
        /// The pool index of a theme's wind module (4.11: Earth's wind-stream screens), just before its island module, or -1
        /// when the theme has none. The layout never draws it at random; it gives it to WindScreensPerFace chosen screens.
        /// </summary>
        int WindModuleIndex(Theme theme) => -1;
    }

    /// <summary>The module per screen and the chosen core-entrance slot of one built science face.</summary>
    public sealed class FaceLayout
    {
        private readonly int[] modules;

        public FaceId Face { get; }
        public Theme Theme { get; }
        public int FaceSize { get; }

        /// <summary>The screen whose module holds the active core entrance.</summary>
        public Vector2Int CoreCell { get; }

        /// <summary>Index of the active core-entrance slot within the module at CoreCell.</summary>
        public int CoreSlot { get; }

        public FaceLayout(FaceId face, Theme theme, int faceSize, int[] modules, Vector2Int coreCell, int coreSlot,
            IReadOnlyList<Vector2Int> islandCells = null, IReadOnlyList<Vector2Int> windCells = null)
        {
            Face = face;
            Theme = theme;
            FaceSize = faceSize;
            this.modules = modules;
            CoreCell = coreCell;
            CoreSlot = coreSlot;
            IslandCells = islandCells ?? Array.Empty<Vector2Int>();
            WindCells = windCells ?? Array.Empty<Vector2Int>();
        }

        /// <summary>The town cell (the face's centre, 4.5), or null on a face too small for a town.</summary>
        public Vector2Int? TownCell => TownSquare.HasTown(FaceSize) ? TownSquare.Cell(FaceSize) : (Vector2Int?)null;

        /// <summary>The cells that hold the theme's island module (Math's island screens), row by row; empty for other themes.</summary>
        public IReadOnlyList<Vector2Int> IslandCells { get; }

        /// <summary>The cells that hold the theme's wind module (4.11: Earth's wind-stream screens), row by row; empty for other themes.</summary>
        public IReadOnlyList<Vector2Int> WindCells { get; }

        /// <summary>True for a whole-screen puzzle's cell (an island, ice or wind screen): no slots, pickups or roadblocks but its goal.</summary>
        public bool IsPuzzleCell(Vector2Int cell)
        {
            foreach (Vector2Int c in IslandCells)
                if (c == cell) return true;
            foreach (Vector2Int c in WindCells)
                if (c == cell) return true;
            return false;
        }

        /// <summary>Pool index of the module on a cell.</summary>
        public int ModuleAt(Vector2Int cell) => modules[cell.y * FaceSize + cell.x];
    }

    /// <summary>
    /// Pure, deterministic science layout: given a CubeModel and the module library's shape, assigns one
    /// pool module to every screen of every built science face and chooses exactly one core-entrance slot
    /// per face. Uses its own SeededRng stream derived from the run seed, so it does not disturb the theme
    /// placement and is the same whenever the reveal happens. Town (fixed modules) and sealed faces get
    /// no entry. A theme with an island module (Math, 1.25) gets it on IslandScreensPerFace of its screens, never the core
    /// screen and never two side by side, chosen on their own stream (RngStreams.IslandScreens) after the face's other
    /// draws, so the rest of the layout draws as before. A theme with a wind module too (Earth, 4.11) gets it on
    /// WindScreensPerFace more screens, from the same shuffle after the island ones, beside none of them (nor the core or the town).
    /// Towns (4.5): on a face with a town (TownSquare.HasTown) the centre cell gets the theme's town module (when the catalog
    /// has one), and neither the core screen nor an island screen is ever the town: the core cell is drawn again while it
    /// lands on the town, and the island shuffle skips it. The centre still draws its module as before (then replaced), so
    /// the other cells keep their draws.
    /// </summary>
    public sealed class CubeLayout
    {
        /// <summary>PCG32 stream for the layout (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.CubeLayout;

        private readonly Dictionary<FaceId, FaceLayout> faces = new Dictionary<FaceId, FaceLayout>();

        public int Seed { get; }

        public IReadOnlyDictionary<FaceId, FaceLayout> Faces => faces;

        private CubeLayout(int seed)
        {
            Seed = seed;
        }

        /// <summary>PCG32 stream for choosing the island screens (see RngStreams).</summary>
        public const ulong IslandRngStream = RngStreams.IslandScreens;

        /// <summary>Island screens on a face with an island module: one per CubeSettings.TunedScreensPerFace screens (2 at 3x3).</summary>
        public static int IslandScreensPerFace(int faceSize) =>
            Math.Min(CubeSettings.PerScreen(1, faceSize), Math.Max(0, faceSize * faceSize - 1));

        /// <summary>Wind screens on a face with a wind module (4.11): one at 3x3 (none on the town-less test faces).</summary>
        public static int WindScreensPerFace(int faceSize) =>
            TownSquare.HasTown(faceSize) ? Math.Max(1, IslandScreensPerFace(faceSize) / 2) : 0;

        /// <summary>True for faces the layout covers: built, unsealed science faces (not Town).</summary>
        public static bool IsLaidOut(CubeModel model, FaceId face) =>
            face != CubeModel.StartFace && !model.IsSealed(face);

        public static CubeLayout Generate(CubeModel model, IModuleCatalog catalog)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var layout = new CubeLayout(model.Seed);
            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);
            int n = model.FaceSize;
            int cells = n * n;

            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (!IsLaidOut(model, face)) continue;
                Theme theme = model.ThemeOf(face);

                int poolSize = catalog.PoolSize(theme);
                int island = catalog.IslandModuleIndex(theme);
                int town = catalog.TownModuleIndex(theme);
                int wind = catalog.WindModuleIndex(theme);
                if (island >= 0 && island != poolSize - 1)
                    throw new InvalidOperationException($"{theme}'s island module must be the last of its pool");
                // The town, wind and island modules end the pool and are never drawn at random.
                int regular = poolSize - (island >= 0 ? 1 : 0) - (town >= 0 ? 1 : 0) - (wind >= 0 ? 1 : 0);
                if (wind >= 0 && wind < regular)
                    throw new InvalidOperationException($"{theme}'s wind module must end its pool (before any island module)");
                if (town >= 0 && town < regular)
                    throw new InvalidOperationException($"{theme}'s town module must end its pool (before any island module)");
                if (regular <= 0)
                    throw new InvalidOperationException($"Module library has no modules for {theme}");
                var coreCapable = new List<int>();
                for (int i = 0; i < regular; i++)
                    if (catalog.CoreSlotCount(theme, i) > 0) coreCapable.Add(i);
                if (coreCapable.Count == 0)
                    throw new InvalidOperationException($"Module library has no {theme} module with a core-entrance slot");

                // Core screen first (never the town), then its module and slot, then every other screen from the full pool.
                int coreIndex = TownSquare.PickCellIndex(rng, n);
                int coreModule = coreCapable[rng.NextInt(coreCapable.Count)];
                int coreSlot = rng.NextInt(catalog.CoreSlotCount(theme, coreModule));
                var modules = new int[cells];
                for (int c = 0; c < cells; c++)
                    modules[c] = c == coreIndex ? coreModule : rng.NextInt(regular);

                var coreCell = new Vector2Int(coreIndex % n, coreIndex / n);
                List<Vector2Int> islandCells = PuzzleCellsFor(model.Seed, face, n, coreCell, island >= 0 ? IslandScreensPerFace(n) : 0,
                    wind >= 0 ? WindScreensPerFace(n) : 0, out List<Vector2Int> windCells);
                foreach (Vector2Int cell in islandCells) modules[cell.y * n + cell.x] = island;
                foreach (Vector2Int cell in windCells) modules[cell.y * n + cell.x] = wind;
                if (town >= 0 && TownSquare.HasTown(n))
                {
                    Vector2Int townCell = TownSquare.Cell(n);
                    modules[townCell.y * n + townCell.x] = town;
                }
                layout.faces[face] = new FaceLayout(face, theme, n, modules, coreCell, coreSlot, islandCells, windCells);
            }
            return layout;
        }

        /// <summary>
        /// The island screens of a face: IslandScreensPerFace cells in a seeded order (their own stream), skipping the core
        /// cell, the town (4.5: after the shuffle, so other cells keep their order) and any cell beside one already taken; row by row.
        /// </summary>
        public static List<Vector2Int> IslandCellsFor(int seed, FaceId face, int n, Vector2Int coreCell) =>
            PuzzleCellsFor(seed, face, n, coreCell, IslandScreensPerFace(n), 0, out _);

        /// <summary>
        /// The island (or ice) screens and then the wind screens of a face (4.11): islands as IslandCellsFor; then, going through
        /// the same shuffled order again, windCount more cells that are not the core, the town, an island cell or beside any
        /// cell already chosen. Each list row by row.
        /// </summary>
        public static List<Vector2Int> PuzzleCellsFor(int seed, FaceId face, int n, Vector2Int coreCell, int islandCount, int windCount,
            out List<Vector2Int> windCells)
        {
            var rng = new SeededRng(((ulong)(uint)seed << 8) | (uint)face, IslandRngStream);
            var cells = new List<Vector2Int>();
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (new Vector2Int(x, y) != coreCell) cells.Add(new Vector2Int(x, y));
            rng.Shuffle(cells);
            var chosen = new List<Vector2Int>();
            bool Fits(Vector2Int c, List<Vector2Int> taken, List<Vector2Int> also)
            {
                if (TownSquare.IsTownCell(c, n) || taken.Contains(c) || (also != null && also.Contains(c))) return false;
                foreach (Vector2Int o in taken)
                    if (Mathf.Abs(o.x - c.x) + Mathf.Abs(o.y - c.y) == 1) return false;
                if (also != null)
                    foreach (Vector2Int o in also)
                        if (Mathf.Abs(o.x - c.x) + Mathf.Abs(o.y - c.y) == 1) return false;
                return true;
            }
            foreach (Vector2Int c in cells)
            {
                if (chosen.Count >= islandCount) break;
                if (Fits(c, chosen, null)) chosen.Add(c);
            }
            windCells = new List<Vector2Int>();
            foreach (Vector2Int c in cells)
            {
                if (windCells.Count >= windCount) break;
                if (Fits(c, windCells, chosen)) windCells.Add(c);
            }
            Comparison<Vector2Int> rowByRow = (a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x);
            chosen.Sort(rowByRow);
            windCells.Sort(rowByRow);
            return chosen;
        }

        public bool TryGetFace(FaceId face, out FaceLayout faceLayout) => faces.TryGetValue(face, out faceLayout);

        /// <summary>Compact text of the whole layout, for comparisons and logs.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                if (!faces.TryGetValue((FaceId)f, out FaceLayout fl)) continue;
                sb.Append(fl.Face).Append('=').Append(fl.Theme).Append('[');
                for (int y = 0; y < fl.FaceSize; y++)
                    for (int x = 0; x < fl.FaceSize; x++)
                        sb.Append(fl.ModuleAt(new Vector2Int(x, y))).Append(' ');
                sb.Append("core ").Append(fl.CoreCell.x).Append(',').Append(fl.CoreCell.y)
                    .Append('#').Append(fl.CoreSlot).Append("] ");
            }
            return sb.ToString();
        }
    }
}
