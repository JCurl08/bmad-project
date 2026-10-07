namespace Game.Cube
{
    /// <summary>
    /// The seeded roll that picks which variant of a theme's item a run uses (Biology: thin or thick beak).
    /// Pure and on its own SeededRng stream, keyed by seed and theme, so the same seed always rolls the same
    /// variant and the roll never disturbs any other seeded system.
    /// </summary>
    public static class ItemVariants
    {
        /// <summary>PCG32 stream for variant rolls (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.ItemVariants;

        /// <summary>The rolled variant index in [0, count) for a seed and theme; 0 when count is 1 or less.</summary>
        public static int Roll(int seed, Theme theme, int count)
        {
            if (count <= 1) return 0;
            var rng = new SeededRng(((ulong)(uint)seed << 32) | (uint)theme, RngStream);
            return rng.NextInt(count);
        }

        /// <summary>
        /// The kind of each variant of a theme's item in a catalog, in variant-index order, read from the items themselves
        /// (kindOf, by id, so the catalog's order never matters). Null without a catalog, without variants of the theme,
        /// or when one variant is not of the kind (kindOf gives null).
        /// </summary>
        public static System.Collections.Generic.List<TKind> KindsOf<TKind>(ItemCatalog catalog, Theme theme,
            System.Func<ItemDefinition, TKind?> kindOf) where TKind : struct
        {
            if (catalog == null) return null;
            System.Collections.Generic.List<ItemDefinition> variants = catalog.VariantsOf(theme);
            if (variants.Count == 0) return null;
            var kinds = new System.Collections.Generic.List<TKind>(variants.Count);
            foreach (ItemDefinition item in variants)
            {
                TKind? kind = kindOf(item);
                if (kind == null) return null;
                kinds.Add(kind.Value);
            }
            return kinds;
        }
    }
}
