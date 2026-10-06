using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The alien people of the Physics face, built with the 1.5 NpcFactory after the reveal. Their parts come from
    /// the run seed (NpcFactory.ChooseParts with IndexBase + i), and each talks with an alien greeting, an Einstein
    /// line, a role line and sparse first-run hints that are true for the run (RunFacts). The first alien also
    /// explains the timed doors and the Mass Mitt (true for every run: the mechanics). Sir Isaac Newton visits as a
    /// cameo, forever being bonked by apples.
    /// </summary>
    public static class PhysicsPopulation
    {
        public const int AlienCount = 3;

        /// <summary>NPC index base for Physics aliens (Town 1000, Biology finches 2000, Chemistry mushrooms 3000).</summary>
        public const int IndexBase = 4000;

        /// <summary>NPC index of the Newton cameo (a townsperson's parts).</summary>
        public const int NewtonIndex = IndexBase + 99;

        public const string NewtonName = "Sir Isaac Newton";

        public static NpcSpec AlienSpec(int seed, int i) => NpcFactory.ChooseParts(seed, Race.Alien, IndexBase + i);

        public static NpcSpec NewtonSpec(int seed) => NpcFactory.ChooseParts(seed, Race.Townsfolk, NewtonIndex);

        /// <summary>The lines a Physics alien says: greeting, Einstein line, role line, hints (and the mitt line for alien 0).</summary>
        public static List<string> LinesFor(NpcSpec spec, RunFacts facts, HintDensity density, bool sayMitt)
        {
            List<string> lines = Dialogue.For(spec, facts, density);
            lines.Insert(1, Dialogue.EinsteinLine(spec.Salt));
            if (sayMitt) lines.Add(Dialogue.MassMittLine);
            return lines;
        }

        /// <summary>Newton's lines: every apple complaint, then the mitt line (true: the mechanics).</summary>
        public static List<string> NewtonLines()
        {
            var lines = new List<string>(Dialogue.NewtonLines) { Dialogue.MassMittLine };
            return lines;
        }
    }
}
