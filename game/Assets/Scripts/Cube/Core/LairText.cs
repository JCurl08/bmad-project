using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// What the lair's prisoners say (4.13), in their own voices (voices-and-names.md): a captive line (CaptiveLines, two a
    /// voice: they took a wrong portal, and the way out is the way in), then one true hint. A hint is the voice's hint opener
    /// (HintGenerator.VoiceTable, its full-density openers, picked by the speaker's salt) and a plain fact worded the same in
    /// every voice, so it stays true: how Primsworth the Demon's fight works (FightFacts, subtle and in-world: motes, its
    /// wall, its door, its tiring), what mending a face gives in the fight (BenefitFact, BossBenefits' mapping), or which
    /// region holds the one real way down (PortalFact). Subtle science (InWorldText): no science words, regions keep their
    /// star, the boss is "Primsworth the Demon".
    /// </summary>
    public static class LairText
    {
        /// <summary>A prisoner's label after their race ("Pipwick the finch captive").</summary>
        public const string CaptiveKind = "captive";

        /// <summary>The header badge on the map while the player is down in a lair room.</summary>
        public const string MapBadge = "In the Demon's lair";

        /// <summary>The F1 overlay's name for a lair room.</summary>
        public const string OverlayLabel = "In the lair below";

        /// <summary>Each voice's captive lines (a prisoner says the one its index picks).</summary>
        public static readonly IReadOnlyDictionary<Voice, string[]> CaptiveLines = new Dictionary<Voice, string[]>
        {
            [Voice.Town] = new[]
            {
                "Oh, a visitor! Mind the bars, dear, they pinch. That portal was one of the Demon's tricks, and it dropped us all down here.",
                "Welcome to the Demon's cellar, love! We took the wrong portal and got shut in. You can still walk out the way you came.",
            },
            [Voice.Finch] = new[]
            {
                "Alas, good traveller! That portal was a cruel trick, and it hath dropped us into this cage.",
                "Hark! Thou too wert fooled by a false portal? Fear not: thy way out lieth where thou camest in.",
            },
            [Voice.Mushroom] = new[]
            {
                "Sob! We took the wrong portal and now we're stuck in a cage! I'm furious! And sad! Mostly sad!",
                "Ooh, a visitor! I'm so happy! Also trapped! That portal was a trick, ugh!",
            },
            [Voice.Alien] = new[]
            {
                "A cage beneath the world. Perhaps we are not trapped, I wonder, but merely... very still?",
                "I stepped through a portal and arrived in a cage. Consider: was it the portal that lied, or my hopes?",
            },
            [Voice.Shape] = new[]
            {
                "If you came through that portal, then you were tricked, as we were. The way out is behind you.",
                "This is a cell. Given the bars, I cannot leave. Therefore, I will talk.",
            },
            [Voice.Ground] = new[]
            {
                "Followed the wrong trail through that portal and ended up in this rock pit. Bars are solid. Tried 'em.",
                "Took a bad trail through that portal. Now I'm camping in a cage. Your way out's right behind you.",
            },
            [Voice.Sky] = new[]
            {
                "No sky down here, not a breath of wind. That portal was a trick, friend.",
                "Clipped my feathers on these bars. Took the wrong portal, and the breeze never reaches this deep.",
            },
        };

        /// <summary>
        /// How the fight works, true of every fight (MaxwellDemon, MixingPhase, CoreArena): plain clauses after a hint opener,
        /// never naming the science.
        /// </summary>
        public static readonly string[] FightFacts =
        {
            // MixingPhase: the meter fills as the halves mix.
            "Primsworth the Demon keeps the warm motes on one side of its wall and the cool ones on the other, and only mixing them up beats it",
            // CoreArena: the gaps at the membrane's ends are always open.
            "the gaps at both ends of the Demon's wall stay open whatever it does, so knock the motes through there",
            // MaxwellDemon: fresh it out-works the player, then it tires (RestoreFatigue).
            "Primsworth the Demon tidies fast at first but tires as the fight drags on, so keep on mixing and it falls behind",
            // MaxwellDemon: a telegraph (a glow and a line) before every shot.
            "the Demon glows and draws a line before it shoots, so step aside when you see it",
            // MaxwellDemon: a knock breaks its hold on a mote it is fetching.
            "a knock on a mote the Demon is carrying home makes it lose its grip",
            // CoreArena: the exit door in the west wall, while the fight is not won.
            "there is a door out in the west wall of the Demon's den, if you need to catch your breath",
        };

        /// <summary>A Benefit hint's fact: what the gem of a theme's temple (4.14: solving it mends the face) does in the fight (BossBenefits.Of, in plain words).</summary>
        public static string BenefitFact(Theme theme)
        {
            string region = InWorldText.Region(theme);
            switch (BossBenefits.Of(theme))
            {
                case BossBenefit.ExtraHeart: return $"the gem from the temple of {region} gives you one more heart against Primsworth the Demon";
                case BossBenefit.Attraction: return $"the gem from the temple of {region} makes the Demon's bouncing motes drift toward you";
                case BossBenefit.FewerParticles: return $"the gem from the temple of {region} leaves fewer bouncing motes for you to mix";
                case BossBenefit.SlowerRestore: return $"the gem from the temple of {region} makes the Demon tidy things back more slowly";
                case BossBenefit.WiderGap: return $"the gem from the temple of {region} widens the gaps at both ends of the Demon's wall";
                default: return $"the gem from the temple of {region} makes the Demon's shots fly slower and come less often";
            }
        }

        /// <summary>A RealPortal hint's fact: the region whose portal truly leads down to the Demon.</summary>
        public static string PortalFact(Theme realTheme) => $"the one true way down to the Demon is the portal in {InWorldText.Region(realTheme)}";

        /// <summary>A hint sentence: the voice's full-density opener (picked by salt), the fact and the opener's end.</summary>
        public static string Hint(Voice voice, uint salt, string fact)
        {
            HintGenerator.HintVoice wording = HintGenerator.VoiceTable[voice];
            string opener = wording.Full[(int)(salt / 7u % (uint)wording.Full.Length)];
            return $"{opener} {fact}{wording.EndFor(opener)}";
        }

        /// <summary>A prisoner's captive line (the one their index picks).</summary>
        public static string CaptiveLine(Voice voice, int index)
        {
            string[] table = CaptiveLines[voice];
            return table[(index & int.MaxValue) % table.Length];
        }

        /// <summary>What a prisoner says: their captive line, then their hint (two lines, Dialogue.MaxLines).</summary>
        public static List<string> Conversation(NpcSpec spec, PrisonerPlan prisoner, CubeModel model, FaceId realFace)
        {
            string fact;
            switch (prisoner.Hint)
            {
                case PrisonerHint.Fight: fact = FightFacts[prisoner.Topic % FightFacts.Length]; break;
                case PrisonerHint.Benefit: fact = BenefitFact(prisoner.BenefitTheme); break;
                default: fact = PortalFact(model.ThemeOf(realFace)); break;
            }
            return new List<string>(Dialogue.MaxLines) { CaptiveLine(spec.Voice, prisoner.Index), Hint(spec.Voice, spec.Salt, fact) };
        }

        /// <summary>Every line a voice can say down in the lair (every captive line, every hint with every opener), for the scans.</summary>
        public static IEnumerable<string> SpokenLines(Voice voice)
        {
            foreach (string line in CaptiveLines[voice]) yield return line;
            var facts = new List<string>(FightFacts);
            foreach (Theme theme in LairPlan.BenefitThemes)
            {
                facts.Add(BenefitFact(theme));
                facts.Add(PortalFact(theme));
            }
            int openers = HintGenerator.VoiceTable[voice].Full.Length;
            foreach (string fact in facts)
                for (int o = 0; o < openers; o++)
                    yield return Hint(voice, (uint)(o * 7), fact);
        }

        /// <summary>Every fixed piece of lair text (the labels and the bare facts), for the tone check.</summary>
        public static IEnumerable<string> AllFixedText()
        {
            yield return CaptiveKind;
            yield return MapBadge;
            yield return OverlayLabel;
            foreach (string[] lines in CaptiveLines.Values)
                foreach (string line in lines) yield return line;
            foreach (string fact in FightFacts) yield return fact;
            foreach (Theme theme in LairPlan.BenefitThemes)
            {
                yield return BenefitFact(theme);
                yield return PortalFact(theme);
            }
        }
    }
}
