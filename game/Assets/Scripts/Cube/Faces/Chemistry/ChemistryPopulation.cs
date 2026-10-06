using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The glowing mushroom people of the Chemistry face, built with the 1.5 NpcFactory after the reveal. Their parts
    /// come from the run seed (NpcFactory.ChooseParts with IndexBase + i), and each talks with a flavour line, then its
    /// first hint, true for the run (RunFacts): two lines. Mushroom 0 names this visit's stone (the rolled stone and what
    /// it does, so it is true too), mushroom 1 tells the lore of the stones changing between visits, and the rest say a
    /// Glowmother* line.
    /// </summary>
    public static class ChemistryPopulation
    {
        /// <summary>Mushroom people on a face of CubeSettings.TunedScreensPerFace screens; MushroomCount scales it per screen.</summary>
        public const int TunedMushroomCount = 3;

        /// <summary>Mushroom people on a face of faceSize x faceSize screens (about the same number per screen).</summary>
        public static int MushroomCount(int faceSize) => CubeSettings.PerScreen(TunedMushroomCount, faceSize);

        /// <summary>NPC index base for Chemistry mushrooms (Town 1000, Biology finches 2000).</summary>
        public const int IndexBase = 3000;

        /// <summary>The mushroom that names this visit's stone.</summary>
        public const int StoneTeller = 0;

        /// <summary>The mushroom that tells the lore of the changing stones.</summary>
        public const int LoreTeller = 1;

        public static NpcSpec MushroomSpec(int seed, int i) => NpcFactory.ChooseParts(seed, Race.Mushroom, IndexBase + i);

        /// <summary>The flavour line of mushroom i in a run with this stone.</summary>
        public static string FlavourFor(NpcSpec spec, IsotopeStage stage, int i)
        {
            if (i == StoneTeller) return Dialogue.StoneLine(stage);
            if (i == LoreTeller) return Dialogue.StoneLoreLine;
            return Dialogue.CurieLine(spec.Salt);
        }

        /// <summary>The lines mushroom i says: its flavour line (FlavourFor), then its first hint.</summary>
        public static List<string> LinesFor(NpcSpec spec, RunFacts facts, HintDensity density, IsotopeStage stage, int i) =>
            Dialogue.Conversation(FlavourFor(spec, stage, i), HintGenerator.Generate(facts, spec.HintKind, spec.Salt, density));
    }
}
