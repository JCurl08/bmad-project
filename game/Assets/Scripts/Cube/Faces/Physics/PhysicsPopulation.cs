using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The alien people of the Physics face, built with the 1.5 NpcFactory after the reveal. Their parts come from
    /// the run seed (NpcFactory.ChooseParts with IndexBase + i), and each talks with an Einstein line, then its first
    /// hint, true for the run (RunFacts): two lines. The first alien explains the timed doors and the Mass Mitt (true
    /// for every run: the mechanics) in place of the Einstein line. Sir Isaac Newton visits as a cameo, forever being
    /// bonked by apples.
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

        /// <summary>The lines a Physics alien says: the Einstein line (the mitt line for alien 0), then its first hint.</summary>
        public static List<string> LinesFor(NpcSpec spec, RunFacts facts, HintDensity density, bool sayMitt)
        {
            string flavour = sayMitt ? Dialogue.MassMittLine : Dialogue.EinsteinLine(spec.Salt);
            return Dialogue.Conversation(flavour, HintGenerator.Generate(facts, spec.HintKind, spec.Salt, density));
        }

        /// <summary>Newton's lines: an apple complaint, then the mitt line (true: the mechanics).</summary>
        public static List<string> NewtonLines() => new List<string> { Dialogue.NewtonLines[0], Dialogue.MassMittLine };
    }
}
