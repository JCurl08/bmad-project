using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The radioactive mushroom people of the Chemistry face, built with the 1.5 NpcFactory after the reveal.
    /// Their parts come from the run seed (NpcFactory.ChooseParts with IndexBase + i), and each talks with a
    /// mushroom greeting, a Curie line, a role line and sparse first-run hints that are true for the run
    /// (RunFacts). The first mushroom also explains the isotope's stages (true for every run: the mechanics).
    /// </summary>
    public static class ChemistryPopulation
    {
        public const int MushroomCount = 3;

        /// <summary>NPC index base for Chemistry mushrooms (Town 1000, Biology finches 2000).</summary>
        public const int IndexBase = 3000;

        public static NpcSpec MushroomSpec(int seed, int i) => NpcFactory.ChooseParts(seed, Race.Mushroom, IndexBase + i);

        /// <summary>The lines a Chemistry mushroom says: greeting, Curie line, role line, hints (and the stage line for mushroom 0).</summary>
        public static List<string> LinesFor(NpcSpec spec, RunFacts facts, HintDensity density, bool sayStages)
        {
            List<string> lines = Dialogue.For(spec, facts, density);
            lines.Insert(1, Dialogue.CurieLine(spec.Salt));
            if (sayStages) lines.Add(Dialogue.IsotopeStageLine);
            return lines;
        }
    }
}
