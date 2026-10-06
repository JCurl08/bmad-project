using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The radioactive mushroom people of the Chemistry face, built with the 1.5 NpcFactory after the reveal.
    /// Their parts come from the run seed (NpcFactory.ChooseParts with IndexBase + i), and each talks with a
    /// Curie line, then its first hint, true for the run (RunFacts): two lines. The first mushroom explains the
    /// isotope's stages (true for every run: the mechanics) in place of the Curie line.
    /// </summary>
    public static class ChemistryPopulation
    {
        public const int MushroomCount = 3;

        /// <summary>NPC index base for Chemistry mushrooms (Town 1000, Biology finches 2000).</summary>
        public const int IndexBase = 3000;

        public static NpcSpec MushroomSpec(int seed, int i) => NpcFactory.ChooseParts(seed, Race.Mushroom, IndexBase + i);

        /// <summary>The lines a Chemistry mushroom says: the Curie line (the stage line for mushroom 0), then its first hint.</summary>
        public static List<string> LinesFor(NpcSpec spec, RunFacts facts, HintDensity density, bool sayStages)
        {
            string flavour = sayStages ? Dialogue.IsotopeStageLine : Dialogue.CurieLine(spec.Salt);
            return Dialogue.Conversation(flavour, HintGenerator.Generate(facts, spec.HintKind, spec.Salt, density));
        }
    }
}
