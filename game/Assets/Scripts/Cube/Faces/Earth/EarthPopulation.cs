using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The dinosaurs of the Earth face, terrestrial and avian in turn (even indices terrestrial, odd avian), built with the
    /// NpcFactory after the reveal. Each one's parts come from the run seed (NpcFactory.ChooseParts with the first index from
    /// IndexBase + i * VariantTries whose parts give the wanted variant), and each talks with a flavour line, then its first
    /// hint, true for the run (RunFacts): two lines. Dinosaur 0 names this visit's Stormcaller* (the rolled phenomenon and
    /// what it does, so it is true too), dinosaur 1 explains the hazards (the mechanics, true every run), and the rest say
    /// a line about their patron: Granny Trowel for the terrestrial ones, Auntie Cloudeye for the avian ones (NpcNames). Each
    /// kind is named from its own pool (NpcNames.For by variant) and speaks in its own voice (Ground or Sky).
    /// </summary>
    public static class EarthPopulation
    {
        /// <summary>Dinosaurs on a face of CubeSettings.TunedScreensPerFace screens; DinoCount scales it per screen.</summary>
        public const int TunedDinoCount = 3;

        public static int DinoCount(int faceSize) => CubeSettings.PerScreen(TunedDinoCount, faceSize);

        /// <summary>NPC index base for Earth dinosaurs (Town 1000, finches 2000, mushrooms 3000, aliens 4000, shapes 5000).</summary>
        public const int IndexBase = 6000;

        /// <summary>Part draws tried per dinosaur to get its variant (a draw that never gives it keeps the last one).</summary>
        public const int VariantTries = 16;

        /// <summary>The first name ordinal of a dinosaur whose draws never gave its kind: past every regular ordinal of a face up to
        /// 14x14 screens, so it carries a numeral ("Flint VII") and never repeats a name of that kind. Very rare.</summary>
        public const int FallbackOrdinal = 100;

        /// <summary>The dinosaur that names this visit's Stormcaller*.</summary>
        public const int WandTeller = 0;

        /// <summary>The dinosaur that explains the hazards.</summary>
        public const int HazardTeller = 1;

        /// <summary>The variant dinosaur i is: terrestrial for even i, avian for odd i.</summary>
        public static DinoVariant VariantOf(int i) => i % 2 == 0 ? DinoVariant.Terrestrial : DinoVariant.Avian;

        public static NpcSpec DinoSpec(int seed, int i)
        {
            DinoVariant want = VariantOf(i);
            NpcSpec spec = null;
            for (int k = 0; k < VariantTries; k++)
            {
                spec = NpcFactory.ChooseParts(seed, Race.Dinosaur, IndexBase + i * VariantTries + k);
                if (spec.Variant == want) break;
            }
            // Named within its own kind (ground and avian dinosaurs have their own pools): dinosaur i is its kind's
            // (i / 2)-th. One that fell back to the other kind takes an ordinal past every regular one of that kind
            // (FallbackOrdinal + i), so it never repeats a name there.
            int ordinal = spec.Variant == want ? i / 2 : FallbackOrdinal + i;
            return spec.Named(NpcNames.For(seed, Race.Dinosaur, spec.Variant, NpcNames.OffsetOf(IndexBase), ordinal));
        }

        /// <summary>The flavour line of dinosaur i in a run with this phenomenon.</summary>
        public static string FlavourFor(NpcSpec spec, Phenomenon rolled, int i)
        {
            if (i == WandTeller) return Dialogue.WandLine(rolled);
            if (i == HazardTeller) return Dialogue.HazardLine;
            return spec.Variant == DinoVariant.Avian ? Dialogue.SkyWatcherLine(spec.Salt) : Dialogue.OldDiggerLine(spec.Salt);
        }

        /// <summary>The lines dinosaur i says: its flavour line (FlavourFor), then its first hint.</summary>
        public static List<string> LinesFor(NpcSpec spec, RunFacts facts, HintDensity density, Phenomenon rolled, int i) =>
            Dialogue.Conversation(FlavourFor(spec, rolled, i), HintGenerator.For(spec, facts, density));
    }
}
