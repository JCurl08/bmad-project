using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The shape people of the Math face (round faces, triangular bodies, rectangular legs), built with the NpcFactory after
    /// the reveal. Their parts come from the run seed (NpcFactory.ChooseParts with IndexBase + i), and each talks with a
    /// flavour line, then its first hint, true for the run (RunFacts): two lines. Shape 0 explains the bridge doors and
    /// shape 1 the gap-crosser* (both true for every run: the mechanics); the rest say a Bridgekeeper* line.
    /// </summary>
    public static class MathPopulation
    {
        /// <summary>Shape people on a face of CubeSettings.TunedScreensPerFace screens; ShapeCount scales it per screen.</summary>
        public const int TunedShapeCount = 3;

        public static int ShapeCount(int faceSize) => CubeSettings.PerScreen(TunedShapeCount, faceSize);

        /// <summary>NPC index base for Math shape people (Town 1000, finches 2000, mushrooms 3000, aliens 4000).</summary>
        public const int IndexBase = 5000;

        /// <summary>The shape person who explains the bridge doors.</summary>
        public const int BridgeTeller = 0;

        /// <summary>The shape person who explains the gap-crosser*.</summary>
        public const int GapTeller = 1;

        public static NpcSpec ShapeSpec(int seed, int i) => NpcFactory.ChooseParts(seed, Race.Shape, IndexBase + i);

        public static string FlavourFor(NpcSpec spec, int i)
        {
            if (i == BridgeTeller) return Dialogue.BridgeLine;
            if (i == GapTeller) return Dialogue.GapCrosserLine;
            return Dialogue.BridgekeeperLine(spec.Salt);
        }

        public static List<string> LinesFor(NpcSpec spec, RunFacts facts, HintDensity density, int i) =>
            Dialogue.Conversation(FlavourFor(spec, i), HintGenerator.Generate(facts, spec.HintKind, spec.Salt, density));
    }
}
