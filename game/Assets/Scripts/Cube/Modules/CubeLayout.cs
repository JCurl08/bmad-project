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

        public FaceLayout(FaceId face, Theme theme, int faceSize, int[] modules, Vector2Int coreCell, int coreSlot)
        {
            Face = face;
            Theme = theme;
            FaceSize = faceSize;
            this.modules = modules;
            CoreCell = coreCell;
            CoreSlot = coreSlot;
        }

        /// <summary>Pool index of the module on a cell.</summary>
        public int ModuleAt(Vector2Int cell) => modules[cell.y * FaceSize + cell.x];
    }

    /// <summary>
    /// Pure, deterministic science layout: given a CubeModel and the module library's shape, assigns one
    /// pool module to every screen of every built science face and chooses exactly one core-entrance slot
    /// per face. Uses its own SeededRng stream derived from the run seed, so it does not disturb the theme
    /// placement and is the same whenever the reveal happens. Town (fixed modules) and sealed faces get
    /// no entry.
    /// </summary>
    public sealed class CubeLayout
    {
        /// <summary>PCG32 stream for the layout (the theme placement uses the default stream).</summary>
        public const ulong RngStream = 2;

        private readonly Dictionary<FaceId, FaceLayout> faces = new Dictionary<FaceId, FaceLayout>();

        public int Seed { get; }

        public IReadOnlyDictionary<FaceId, FaceLayout> Faces => faces;

        private CubeLayout(int seed)
        {
            Seed = seed;
        }

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
                if (poolSize <= 0)
                    throw new InvalidOperationException($"Module library has no modules for {theme}");
                var coreCapable = new List<int>();
                for (int i = 0; i < poolSize; i++)
                    if (catalog.CoreSlotCount(theme, i) > 0) coreCapable.Add(i);
                if (coreCapable.Count == 0)
                    throw new InvalidOperationException($"Module library has no {theme} module with a core-entrance slot");

                // Core screen first, then its module and slot, then every other screen from the full pool.
                int coreIndex = rng.NextInt(cells);
                int coreModule = coreCapable[rng.NextInt(coreCapable.Count)];
                int coreSlot = rng.NextInt(catalog.CoreSlotCount(theme, coreModule));
                var modules = new int[cells];
                for (int c = 0; c < cells; c++)
                    modules[c] = c == coreIndex ? coreModule : rng.NextInt(poolSize);

                var coreCell = new Vector2Int(coreIndex % n, coreIndex / n);
                layout.faces[face] = new FaceLayout(face, theme, n, modules, coreCell, coreSlot);
            }
            return layout;
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
