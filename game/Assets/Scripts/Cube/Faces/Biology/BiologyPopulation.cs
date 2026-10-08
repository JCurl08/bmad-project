using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The finch people of the Biology face, built with the 1.5 NpcFactory after the reveal. Their parts come
    /// from the run seed (NpcFactory.ChooseParts with IndexBase + i), and each talks with a finch greeting, a
    /// Darwin line, then its first hint, true for the run (RunFacts): two lines. The first finch says which beak the
    /// season favours (the rolled beak, so it is true too) in place of the Darwin line.
    /// </summary>
    public static class BiologyPopulation
    {
        /// <summary>Finches on a face of CubeSettings.TunedScreensPerFace screens; FinchCount scales it per screen.</summary>
        public const int TunedFinchCount = 3;

        /// <summary>Finches on a face of faceSize x faceSize screens (about the same number per screen).</summary>
        public static int FinchCount(int faceSize) => CubeSettings.PerScreen(TunedFinchCount, faceSize);

        /// <summary>NPC index base for Biology finches (Town uses 1000, F3 debug batches 0, 1, ...).</summary>
        public const int IndexBase = 2000;

        public static NpcSpec FinchSpec(int seed, int i) => NpcFactory.ChooseParts(seed, Race.Finch, IndexBase + i);

        /// <summary>The lines a Biology finch says: the Darwin line (the beak line for finch 0), then its first hint.</summary>
        public static List<string> LinesFor(NpcSpec spec, RunFacts facts, HintDensity density, BeakKind beak, bool sayBeak)
        {
            string flavour = sayBeak ? Dialogue.BeakLine(beak == BeakKind.Thin) : Dialogue.DarwinLine(spec.Salt);
            return Dialogue.Conversation(flavour, HintGenerator.For(spec, facts, density));
        }
    }
}
