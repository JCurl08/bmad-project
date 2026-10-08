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
            IReadOnlyList<Vector2Int> islandCells = null)
        {
            Face = face;
            Theme = theme;
            FaceSize = faceSize;
            this.modules = modules;
            CoreCell = coreCell;
            CoreSlot = coreSlot;
            IslandCells = islandCells ?? Array.Empty<Vector2Int>();
        }

        /// <summary>The town cell (the face's centre, 4.5), or null on a face too small for a town.</summary>
        public Vector2Int? TownCell => TownSquare.HasTown(FaceSize) ? TownSquare.Cell(FaceSize) : (Vector2Int?)null;

        /// <summary>The cells that hold the theme's island module (Math's island screens), row by row; empty for other themes.</summary>
        public IReadOnlyList<Vector2Int> IslandCells { get; }

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
    /// draws, so the rest of the layout draws as before.
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
                if (island >= 0 && island != poolSize - 1)
                    throw new InvalidOperationException($"{theme}'s island module must be the last of its pool");
                // The island and town modules end the pool and are never drawn at random.
                int regular = poolSize - (island >= 0 ? 1 : 0) - (town >= 0 ? 1 : 0);
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
                List<Vector2Int> islandCells = island >= 0 ? IslandCellsFor(model.Seed, face, n, coreCell) : new List<Vector2Int>();
                foreach (Vector2Int cell in islandCells) modules[cell.y * n + cell.x] = island;
                if (town >= 0 && TownSquare.HasTown(n))
                {
                    Vector2Int townCell = TownSquare.Cell(n);
                    modules[townCell.y * n + townCell.x] = town;
                }
                layout.faces[face] = new FaceLayout(face, theme, n, modules, coreCell, coreSlot, islandCells);
            }
            return layout;
        }

        /// <summary>
        /// The island screens of a face: IslandScreensPerFace cells in a seeded order (their own stream), skipping the core
        /// cell, the town (4.5: after the shuffle, so other cells keep their order) and any cell beside one already taken; row by row.
        /// </summary>
        public static List<Vector2Int> IslandCellsFor(int seed, FaceId face, int n, Vector2Int coreCell)
        {
            var rng = new SeededRng(((ulong)(uint)seed << 8) | (uint)face, IslandRngStream);
            var cells = new List<Vector2Int>();
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (new Vector2Int(x, y) != coreCell) cells.Add(new Vector2Int(x, y));
            rng.Shuffle(cells);
            var chosen = new List<Vector2Int>();
            int wanted = IslandScreensPerFace(n);
            foreach (Vector2Int c in cells)
            {
                if (chosen.Count >= wanted) break;
                if (TownSquare.IsTownCell(c, n)) continue;
                bool beside = false;
                foreach (Vector2Int o in chosen)
                    if (Mathf.Abs(o.x - c.x) + Mathf.Abs(o.y - c.y) == 1) beside = true;
                if (!beside) chosen.Add(c);
            }
            chosen.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
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
