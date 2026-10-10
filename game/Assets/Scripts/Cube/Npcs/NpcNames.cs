using System.Collections.Generic;
using System.Linq;

namespace Game.Cube
{
    /// <summary>
    /// The voices NPCs speak in (CAP-32, voices-and-names.md): one per race, and two for the dinosaurs (the ground
    /// dinosaurs are rugged and outdoorsy, the avian ones breezier). Dialogue and HintGenerator pick their wording by it.
    /// </summary>
    public enum Voice { Town = 0, Finch = 1, Mushroom = 2, Alien = 3, Shape = 4, Ground = 5, Sky = 6 }

    public static class Voices
    {
        public static readonly Voice[] All = { Voice.Town, Voice.Finch, Voice.Mushroom, Voice.Alien, Voice.Shape, Voice.Ground, Voice.Sky };

        /// <summary>The voice of a race (a dinosaur's by its variant: avian dinosaurs are Sky, the rest Ground).</summary>
        public static Voice Of(Race race, DinoVariant variant = DinoVariant.None)
        {
            switch (race)
            {
                case Race.Finch: return Voice.Finch;
                case Race.Mushroom: return Voice.Mushroom;
                case Race.Alien: return Voice.Alien;
                case Race.Shape: return Voice.Shape;
                case Race.Dinosaur: return variant == DinoVariant.Avian ? Voice.Sky : Voice.Ground;
                default: return Voice.Town;
            }
        }

        public static Voice Of(NpcSpec spec) => spec == null ? Voice.Town : Of(spec.Race, spec.Variant);
    }

    /// <summary>
    /// Every character's name (CAP-32, lore-and-tone.md): chosen names, so no "*". Generic NPCs take a name from their
    /// race's seeded pool; leaders, patrons, the greeter, the apple-bonked sage and the Demon have fixed names. Names are
    /// a little punny and hint at their culture, never at a real scientist (InWorldText's banned list still applies).
    /// The pool draw has its own RNG stream (RngStreams.NpcNames), so naming never shifts the parts, spots or wander.
    /// Within one population, consecutive slots get distinct names (a seeded permutation of the pool).
    /// </summary>
    public static class NpcNames
    {
        /// <summary>PCG32 stream for the name pools (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.NpcNames;

        // ---------------------------------------------------------------- fixed names

        /// <summary>The Town greeter (Townsfolk 0), who welcomes the player.</summary>
        public const string Greeter = "Hattie Hullo";

        /// <summary>The boss. "Demon" is its title; the name is its own.</summary>
        public const string Demon = "Primsworth";

        /// <summary>The boss's title (capitalised, a chosen name: no star).</summary>
        public const string DemonTitle = "Demon";

        /// <summary>The boss's name with its title, as the text says it.</summary>
        public const string DemonFull = Demon + " the " + DemonTitle;

        /// <summary>The greeter's label in the dialogue box ("Hattie Hullo the greeter").</summary>
        public const string GreeterKind = "greeter";

        /// <summary>The Mayor's tour guide (4.20), who waits at the tour's talking stop.</summary>
        public const string Guide = "Gus Guidewell";

        /// <summary>The guide's label in the dialogue box ("Gus Guidewell the tour guide").</summary>
        public const string GuideKind = "tour guide";

        /// <summary>The apple cameo on the Weightlands* (Newton-inspired, never named so).</summary>
        public const string AppleSage = "Sage Conkwell";

        /// <summary>An old mushroom of the Ember Vale*, half a skeleton already.</summary>
        public const string OldMushroom = "Grandpa Brittlecap";

        /// <summary>The finches' patron (Darwin-inspired, never named so).</summary>
        public const string FinchPatron = "Grandsire Beakwell";

        /// <summary>The mushroom people's patron (Curie-inspired, never named so).</summary>
        public const string MushroomPatron = "Mother Glimmercap";

        /// <summary>The alien people's patron (Einstein-inspired, never named so).</summary>
        public const string AlienPatron = "Old Whenever";

        /// <summary>The shape people's patron (Euler-inspired, never named so).</summary>
        public const string ShapePatron = "Madam Spanwell";

        /// <summary>The ground dinosaurs' patron (Anning-inspired, never named so).</summary>
        public const string GroundPatron = "Granny Trowel";

        /// <summary>The avian dinosaurs' patron (Richter- and weather-inspired, never named so).</summary>
        public const string SkyPatron = "Auntie Cloudeye";

        /// <summary>Each race's leader (one per race, for 4.5's quests).</summary>
        public static string Leader(Race race)
        {
            switch (race)
            {
                case Race.Finch: return "Queen Wrennifer";
                case Race.Mushroom: return "Duchess Flarella";
                case Race.Alien: return "Grand Muser Yondrix";
                case Race.Shape: return "Chancellor Quadrilla";
                case Race.Dinosaur: return "Chief Rockjaw";
                default: return "Mayor Muddleton";
            }
        }

        /// <summary>Every fixed name (leaders, patrons, the greeter, the cameos and the Demon).</summary>
        public static IEnumerable<string> Fixed()
        {
            foreach (Race race in RaceExtensions.All) yield return Leader(race);
            yield return FinchPatron;
            yield return MushroomPatron;
            yield return AlienPatron;
            yield return ShapePatron;
            yield return GroundPatron;
            yield return SkyPatron;
            yield return OldMushroom;
            yield return AppleSage;
            yield return Greeter;
            yield return Guide;
            yield return Demon;
            yield return DemonTitle;
            yield return DemonFull;
        }

        // ---------------------------------------------------------------- pools

        private static readonly Dictionary<Voice, string[]> Pools = new Dictionary<Voice, string[]>
        {
            // A friendly, mismatched lot: cosy village names.
            [Voice.Town] = new[]
            {
                "Bessie", "Wilbur", "Mabel", "Ned", "Gertie", "Barnaby", "Clem", "Rosie", "Alfie", "Mungo", "Peggy", "Ozzie",
                "Winnie", "Bert", "Dottie", "Fennel", "Mopsy", "Rufus", "Tilly", "Percy", "Nell", "Hobb", "Lulu", "Jasper",
            },
            // Courtly bird names: hedgerows, songs and seeds.
            [Voice.Finch] = new[]
            {
                "Pipwick", "Wrenna", "Featherby", "Nestor", "Quillon", "Fenwick", "Linnet", "Chirpwell", "Tansy", "Sparrowby",
                "Robina", "Twiggs", "Peregrine", "Thistle", "Warbleton", "Hedgely", "Larkspur", "Dunnock", "Seedwell", "Plumley",
            },
            // Fizzy, flaring mushroom names.
            [Voice.Mushroom] = new[]
            {
                "Fizz", "Sizzle", "Morel", "Truffle", "Portia", "Enoki", "Popkin", "Flarebee", "Inky", "Chanty",
                "Puffkin", "Bubbla", "Cappy", "Sporrow", "Shimmer", "Gillian", "Russ", "Fumi", "Blewitt", "Crackle",
            },
            // Musing, drifting names.
            [Voice.Alien] = new[]
            {
                "Ponderel", "Musewell", "Yonder", "Lingerby", "Driftly", "Whenna", "Thinkle", "Wistrom", "Quizzle", "Tarrix",
                "Dawdlix", "Glimwhen", "Eona", "Hmmble", "Quandra", "Wonderly", "Murmix", "Ruminel", "Vastra", "Someday",
            },
            // Neat names with a corner or two in them.
            [Voice.Shape] = new[]
            {
                "Angela", "Trixie", "Rhona", "Hexter", "Octavia", "Isolde", "Primrose", "Dot", "Otto", "Quincy",
                "Evan", "Cubert", "Arc", "Lina", "Rex Tangle", "Tess", "Polly", "Ova", "Cornelia", "Penny",
            },
            // Rugged names off the trail.
            [Voice.Ground] = new[]
            {
                "Flint", "Shale", "Cragg", "Rumble", "Granite Gert", "Clay", "Dusty", "Ridge", "Cliff", "Mossback", "Tusk",
                "Slate", "Talus", "Ruckus", "Burl", "Boulder Bea",
            },
            // Breezy names off the sky.
            [Voice.Sky] = new[]
            {
                "Gale", "Breezy", "Skye", "Cirra", "Gusty", "Nimbo", "Drizzle", "Squall", "Flurry", "Mistral", "Sunny",
                "Cumulina", "Stormy", "Windy Wendy", "Puffcloud", "Zephyrine",
            },
        };

        /// <summary>A voice's generic name pool.</summary>
        public static IReadOnlyList<string> Pool(Voice voice) => Pools[voice];

        /// <summary>Every name, pooled and fixed (for the text checks and InWorldText's chosen-name rule).</summary>
        public static IEnumerable<string> All()
        {
            foreach (Voice voice in Voices.All)
                foreach (string name in Pools[voice]) yield return name;
            foreach (string name in Fixed()) yield return name;
        }

        private static List<string[]> phrases;

        /// <summary>
        /// Every name as its words ("Queen", "Wrennifer"), longest first: InWorldText reads a capitalised word as chosen
        /// only where a whole name phrase stands (followed by an optional numeral, see For).
        /// </summary>
        public static IReadOnlyList<string[]> Phrases()
        {
            if (phrases == null)
                phrases = All().Distinct().Select(n => n.Split(' ')).OrderByDescending(w => w.Length).ToList();
            return phrases;
        }

        /// <summary>The numerals a repeated name takes ("Pipwick II"): II to XX.</summary>
        public static readonly string[] Numerals =
        {
            "", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII",
            "XVIII", "XIX", "XX",
        };

        /// <summary>True for a numeral a name may carry (II to XX).</summary>
        public static bool IsNumeral(string word) => System.Array.IndexOf(Numerals, word) > 0;

        /// <summary>
        /// The offset a population's names start at in its pool: 7 per population (Town 1000, Biology 2000, ...), so one
        /// face's people and that race's Town visitor rarely share a name.
        /// </summary>
        public static int OffsetOf(int index) => index / 1000 * 7;

        /// <summary>The ordinal of an NPC index within its population (Town 1000 + i gives i).</summary>
        public static int OrdinalOf(int index) => index % 1000;

        /// <summary>
        /// The generic name of the ordinal-th person of a population (offset: OffsetOf) of a race (a dinosaur's by its
        /// variant) in a run: the pool is shuffled by (seed, voice), and the person takes entry (offset + ordinal) mod the
        /// pool's size. Once a population outgrows the pool, later people carry a numeral ("Pipwick II", "Pipwick III"), so
        /// within one population two people never share a name: distinct ordinals give distinct entries or numerals.
        /// </summary>
        public static string For(int seed, Race race, DinoVariant variant, int offset, int ordinal)
        {
            Voice voice = Voices.Of(race, variant);
            string[] pool = Pools[voice];
            var order = new int[pool.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            new SeededRng(NpcFactory.NpcSeed(seed, race, (int)voice), RngStream).Shuffle(order);
            ordinal = System.Math.Max(0, ordinal);
            int k = (offset + ordinal) % pool.Length;
            int round = ordinal / pool.Length;
            string name = pool[order[k]];
            if (round == 0) return name;
            return name + " " + (round < Numerals.Length ? Numerals[round] : (round + 1).ToString());
        }
    }
}
