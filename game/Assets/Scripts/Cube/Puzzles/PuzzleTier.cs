namespace Game.Cube
{
    /// <summary>
    /// A puzzle screen's difficulty tier (4.10, puzzle-difficulty-research.md section 0): from its face's die number (4.8,
    /// CAP-29), faces 2-3 early, 4-5 mid, 6 late. Town (1) has no puzzles; anything below 2 counts as early.
    /// </summary>
    public enum PuzzleTier { Early = 0, Mid = 1, Late = 2 }

    public static class PuzzleTiers
    {
        public static readonly PuzzleTier[] All = { PuzzleTier.Early, PuzzleTier.Mid, PuzzleTier.Late };

        /// <summary>The tier of a face numbered dieNumber: 2-3 early, 4-5 mid, 6 late.</summary>
        public static PuzzleTier OfDieNumber(int dieNumber) =>
            dieNumber >= 6 ? PuzzleTier.Late : dieNumber >= 4 ? PuzzleTier.Mid : PuzzleTier.Early;
    }
}
