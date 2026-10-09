using System;
using System.Collections.Generic;
using Game.Tracer;

namespace Game.Cube
{
    /// <summary>The help a repaired face gives in the core fight (4.8, CAP-29): one each, fixed per face theme (BossBenefits.Of).</summary>
    [Flags]
    public enum BossBenefit
    {
        None = 0,

        /// <summary>Fewer particles to un-order (Chemistry: the Ember Vale* fizzes some away).</summary>
        FewerParticles = 1,

        /// <summary>Wider always-open gaps at the membrane's ends (Math: the Bridgelands* widen the way across).</summary>
        WiderGap = 2,

        /// <summary>Particles drift slightly towards the player (Biology: drawn like finches to seed).</summary>
        Attraction = 4,

        /// <summary>The Demon restores order more slowly (Physics: time drags in the Weightlands*' way).</summary>
        SlowerRestore = 8,

        /// <summary>One more heart for the fight (Town: the Mayor's townsfolk have the player's back).</summary>
        ExtraHeart = 16,

        /// <summary>The Demon's order pulses fly slower and come less often (Earth: the Wildlands*' headwind).</summary>
        SlowerShots = 32,
    }

    /// <summary>
    /// What each repaired face gives the fight, and what that does to the arena (ArenaTuning). The mapping is fixed per face
    /// theme (assumption, the plan's call): Town +1 heart, Biology attraction, Chemistry fewer particles, Physics slower
    /// restore, Math wider gaps, Earth slower and rarer shots. With no benefits the tuning is the 1.11 arena's, value for
    /// value, so the skill check is unchanged.
    /// </summary>
    public static class BossBenefits
    {
        /// <summary>Every benefit, in theme order (Town, Biology, Chemistry, Physics, Math, Earth).</summary>
        public static readonly IReadOnlyList<BossBenefit> All = new[]
        {
            BossBenefit.ExtraHeart, BossBenefit.Attraction, BossBenefit.FewerParticles, BossBenefit.SlowerRestore,
            BossBenefit.WiderGap, BossBenefit.SlowerShots,
        };

        /// <summary>The benefit a repaired face of a theme gives.</summary>
        public static BossBenefit Of(Theme theme)
        {
            switch (theme)
            {
                case Theme.Town: return BossBenefit.ExtraHeart;
                case Theme.Biology: return BossBenefit.Attraction;
                case Theme.Chemistry: return BossBenefit.FewerParticles;
                case Theme.Physics: return BossBenefit.SlowerRestore;
                case Theme.Math: return BossBenefit.WiderGap;
                default: return BossBenefit.SlowerShots;
            }
        }

        /// <summary>The benefits of a run's repaired faces.</summary>
        public static BossBenefit From(IEnumerable<FaceId> repaired, CubeModel model)
        {
            BossBenefit result = BossBenefit.None;
            if (repaired == null || model == null) return result;
            foreach (FaceId face in repaired) result |= Of(model.ThemeOf(face));
            return result;
        }

        /// <summary>A benefit's short label (HUDs and the map), in plain words (the text rule: never "particle").</summary>
        public static string Label(BossBenefit benefit)
        {
            switch (benefit)
            {
                case BossBenefit.FewerParticles: return "fewer to mix";
                case BossBenefit.WiderGap: return "wider gaps";
                case BossBenefit.Attraction: return "drawn to you";
                case BossBenefit.SlowerRestore: return "slower tidying";
                case BossBenefit.ExtraHeart: return "+1 heart";
                case BossBenefit.SlowerShots: return "slower, rarer shots";
                default: return "";
            }
        }

        /// <summary>The core HUD's help line ("Help from your gems: +1 heart, wider gaps"), or null with none.</summary>
        public static string HudLine(BossBenefit benefits)
        {
            if (benefits == BossBenefit.None) return null;
            var labels = new List<string>();
            foreach (BossBenefit b in All)
                if ((benefits & b) != 0) labels.Add(Label(b));
            return "Help from your gems: " + string.Join(", ", labels);
        }

        /// <summary>Every fixed text here, for the text scans.</summary>
        public static IEnumerable<string> AllText()
        {
            foreach (BossBenefit b in All) yield return Label(b);
            BossBenefit every = BossBenefit.None;
            foreach (BossBenefit b in All) every |= b;
            yield return HudLine(every);
        }
    }

    /// <summary>
    /// The core arena's tuning for a fight (4.8): the parameters each benefit changes. Base is the arena as 1.11 tuned it;
    /// For(benefits) changes only the parameters of the benefits given, each in the player's favour.
    /// </summary>
    public readonly struct ArenaTuning
    {
        /// <summary>FewerParticles: this many fewer particles a side (8 to 6, so 16 to 12 in all).</summary>
        public const int FewerParticlesBy = 2;

        /// <summary>WiderGap: each membrane gap's height (1.5 to 2.25 of the first screens' units, scaled: x1.5).</summary>
        public const float WideGapHeight = 2.25f * ScreenMath.ContentScale;

        /// <summary>Attraction: how fast a drifting particle is drawn towards the player (0 without it).</summary>
        public const float AttractSpeed = 0.3f * ScreenMath.ContentScale;

        /// <summary>Attraction: particles within this distance of the player feel it.</summary>
        public const float AttractRange = 9f * ScreenMath.ContentScale;

        /// <summary>SlowerRestore: the Demon's pause between fetches is this many times as long.</summary>
        public const float SlowRestoreScale = 2f;

        /// <summary>SlowerRestore: the speed the Demon steers a fetched particle at (4.5 to 3.375 a second, scaled: x0.75).</summary>
        public const float SlowNudgeSpeed = MaxwellDemon.NudgeSpeed * 0.75f;

        /// <summary>ExtraHeart: hearts added for the fight.</summary>
        public const int ExtraHearts = 1;

        /// <summary>SlowerShots: seconds between order pulses (4 to 6).</summary>
        public const float SlowPulseInterval = 6f;

        /// <summary>SlowerShots: an order pulse's speed (6.5 to 4.5 a second, scaled).</summary>
        public const float SlowPulseSpeed = 4.5f * ScreenMath.ContentScale;

        public readonly int ParticlesPerSide;
        public readonly float GapHeight;
        public readonly float Attract;
        public readonly float RestoreScale;
        public readonly float NudgeSpeed;
        public readonly int Hearts;
        public readonly float PulseInterval;
        public readonly float PulseSpeed;

        public ArenaTuning(int particlesPerSide, float gapHeight, float attract, float restoreScale, float nudgeSpeed, int hearts,
            float pulseInterval, float pulseSpeed)
        {
            ParticlesPerSide = particlesPerSide;
            GapHeight = gapHeight;
            Attract = attract;
            RestoreScale = restoreScale;
            NudgeSpeed = nudgeSpeed;
            Hearts = hearts;
            PulseInterval = pulseInterval;
            PulseSpeed = pulseSpeed;
        }

        /// <summary>The arena with no benefits, for a side of particlesPerSide (the arena's setting, 8 by default).</summary>
        public static ArenaTuning Base(int particlesPerSide = CoreArena.DefaultParticlesPerSide) => new ArenaTuning(particlesPerSide,
            CoreArena.GapHeight, 0f, 1f, MaxwellDemon.NudgeSpeed, 0, MaxwellDemon.PulseInterval, MaxwellDemon.PulseSpeed);

        /// <summary>The tuning with a set of benefits applied (None gives Base exactly).</summary>
        public static ArenaTuning For(BossBenefit benefits, int particlesPerSide = CoreArena.DefaultParticlesPerSide)
        {
            ArenaTuning b = Base(particlesPerSide);
            bool Has(BossBenefit x) => (benefits & x) != 0;
            return new ArenaTuning(
                Has(BossBenefit.FewerParticles) ? Math.Max(1, b.ParticlesPerSide - FewerParticlesBy) : b.ParticlesPerSide,
                Has(BossBenefit.WiderGap) ? WideGapHeight : b.GapHeight,
                Has(BossBenefit.Attraction) ? AttractSpeed : b.Attract,
                Has(BossBenefit.SlowerRestore) ? SlowRestoreScale : b.RestoreScale,
                Has(BossBenefit.SlowerRestore) ? SlowNudgeSpeed : b.NudgeSpeed,
                Has(BossBenefit.ExtraHeart) ? ExtraHearts : b.Hearts,
                Has(BossBenefit.SlowerShots) ? SlowPulseInterval : b.PulseInterval,
                Has(BossBenefit.SlowerShots) ? SlowPulseSpeed : b.PulseSpeed);
        }

        public override string ToString() =>
            $"particles/side {ParticlesPerSide}, gap {GapHeight:0.###}, attract {Attract:0.###}, restore x{RestoreScale:0.##}, " +
            $"nudge {NudgeSpeed:0.###}, +{Hearts} hearts, pulses every {PulseInterval:0.##} s at {PulseSpeed:0.###}";
    }
}
