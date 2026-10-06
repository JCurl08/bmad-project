namespace Game.Cube
{
    /// <summary>
    /// Every named PCG32 stream (SeededRng's stream argument) in one place. Each seeded system draws from its own
    /// stream so that adding draws to one never shifts another's results. Changing a value changes what a seed
    /// produces, so never renumber or reuse one. A new system takes the
    /// next free value (17) and adds it here; RngStreamsTests fails on a duplicate. The cube model's theme placement
    /// uses SeededRng's default stream. The owners' RngStream constants are aliases of these.
    /// </summary>
    public static class RngStreams
    {
        /// <summary>Science face layout (CubeLayout).</summary>
        public const ulong CubeLayout = 2;

        /// <summary>Item gates and pickups (ItemPlacement).</summary>
        public const ulong ItemPlacement = 3;

        /// <summary>NPC part choice (NpcFactory).</summary>
        public const ulong NpcParts = 4;

        /// <summary>NPC wander targets (NpcMover).</summary>
        public const ulong NpcWander = 5;

        /// <summary>F7 debug enemy weakness picks (CubeDebug).</summary>
        public const ulong DebugEnemySpawn = 6;

        /// <summary>Town population count, parts and spots (TownPlan).</summary>
        public const ulong TownPlan = 7;

        /// <summary>Item variant rolls (ItemVariants).</summary>
        public const ulong ItemVariants = 8;

        /// <summary>The Biology face plan (BiologyPlan).</summary>
        public const ulong BiologyPlan = 9;

        /// <summary>Biology trial spots, population screens and spots (BiologyFace).</summary>
        public const ulong BiologyPopulation = 10;

        /// <summary>The Chemistry face plan (ChemistryPlan).</summary>
        public const ulong ChemistryPlan = 11;

        /// <summary>Chemistry trial spots, population screens and spots (ChemistryFace).</summary>
        public const ulong ChemistryPopulation = 12;

        /// <summary>The Physics face plan (PhysicsPlan).</summary>
        public const ulong PhysicsPlan = 13;

        /// <summary>Physics population screens and spots (PhysicsFace).</summary>
        public const ulong PhysicsPopulation = 14;

        /// <summary>Core arena particle drift (CoreArena).</summary>
        public const ulong CoreArenaDrift = 15;

        /// <summary>Hidden meta-currency placement (HiddenCurrencyPlacement).</summary>
        public const ulong HiddenCurrency = 16;
    }
}
