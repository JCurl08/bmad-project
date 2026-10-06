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
    }
}
