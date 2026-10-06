using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The finch people of the Biology face, built with the 1.5 NpcFactory after the reveal. Their parts come
    /// from the run seed (NpcFactory.ChooseParts with IndexBase + i), and each talks with a finch greeting, a
    /// Darwin line, a role line and sparse first-run hints that are true for the run (RunFacts). The first finch
    /// also says which beak the season favours (the rolled beak, so it is true too).
    /// </summary>
    public static class BiologyPopulation
    {
        public const int FinchCount = 3;

        /// <summary>NPC index base for Biology finches (Town uses 1000, F3 debug batches 0, 1, ...).</summary>
        public const int IndexBase = 2000;

        public static NpcSpec FinchSpec(int seed, int i) => NpcFactory.ChooseParts(seed, Race.Finch, IndexBase + i);

        /// <summary>The lines a Biology finch says: greeting, Darwin line, role line, hints (and the beak line for finch 0).</summary>
        public static List<string> LinesFor(NpcSpec spec, RunFacts facts, HintDensity density, BeakKind beak, bool sayBeak)
        {
            List<string> lines = Dialogue.For(spec, facts, density);
            lines.Insert(1, Dialogue.DarwinLine(spec.Salt));
            if (sayBeak) lines.Add(Dialogue.BeakLine(beak == BeakKind.Thin));
            return lines;
        }
    }
}
