using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// Campy NPC lines (lore-and-tone.md: silly, never explicit, never the s-word). A conversation is a race
    /// greeting, a role line flavoured by the race's nouns, then the head's hints. Tables are small (3 to 4
    /// lines each); the NPC's salt picks the variant, so the same NPC always says the same thing.
    /// </summary>
    public static class Dialogue
    {
        /// <summary>The word the game never says (lore-and-tone.md). Tests check every line against it.</summary>
        public const string ForbiddenWord = "sort";

        /// <summary>Race nouns the role lines are filled with.</summary>
        public sealed class RaceFlavour
        {
            public string Wares;
            public string Metal;
            public string Errand;
            public string[] Rumours;
            public string[] Greetings;
        }

        /// <summary>Role lines. Tokens: {Wares}/{wares}, {metal}, {errand}, {rumour}.</summary>
        public static readonly IReadOnlyDictionary<NpcRole, string[]> RoleLines = new Dictionary<NpcRole, string[]>
        {
            [NpcRole.Merchant] = new[]
            {
                "{Wares} for sale! ...Fine, *hints* for sale. The shop isn't built yet.",
                "Step right up! Finest {wares} on the cube! Cash only. Also there is no till.",
                "Today's special: {wares}, lightly used. Tomorrow's special: the same {wares}.",
                "Buy one, get one free! Of what? Mostly {wares}. And a free tip, see below.",
            },
            [NpcRole.QuestGiver] = new[]
            {
                "Ah, a hero! Perfect. I need someone to {errand}. Reward: my eternal gratitude, plus gossip.",
                "Quest for you! Please {errand}. No rush. Well, some rush. Moderate rush.",
                "They told me to wait here until an adventurer showed up. It's been ages. Please {errand}.",
            },
            [NpcRole.Smith] = new[]
            {
                "I hammer {metal} all day. Clang. Clang. Do you want to hear the clang? Clang.",
                "Best {metal} work this side of the core! Bring me something to fix. Anything. I'm bored.",
                "Careful, the {metal} is still hot. Everything is still hot. I may have overdone it.",
            },
            [NpcRole.Gossip] = new[]
            {
                "Between you and me... {rumour}",
                "Ooh, have you heard? {rumour}",
                "Don't tell anyone I told you, but {rumour}",
            },
        };

        public static readonly IReadOnlyDictionary<Race, RaceFlavour> Flavours = new Dictionary<Race, RaceFlavour>
        {
            [Race.Finch] = new RaceFlavour
            {
                Wares = "seeds",
                Metal = "beak-polish",
                Errand = "find out why my cousin's beak is suddenly so *thick*",
                Rumours = new[]
                {
                    "the thin-beaks think they're better than everybody. Tweet-tweet, la-di-da.",
                    "Professor Darwin keeps measuring my beak. Every. Single. Morning.",
                    "the dinosaurs say they're our cousins. Distant cousins. *Very* distant.",
                },
                Greetings = new[]
                {
                    "Tweet! Oh, sorry, that was rude. Hello!",
                    "Cheep cheep! Adapted and ready to chat!",
                    "Peep! You're not a cat, are you? Good. Hello!",
                },
            },
            [Race.Mushroom] = new RaceFlavour
            {
                Wares = "spores",
                Metal = "lead",
                Errand = "fetch me a nice damp log. For reasons. Fungal reasons",
                Rumours = new[]
                {
                    "old Marcel is half a skeleton already, and he's *thrilled* about the fingers.",
                    "Madame Curie says I glow with potential. Mostly I just glow.",
                    "the skeleton-key folk can open any door. Nobody invites them anywhere, though.",
                },
                Greetings = new[]
                {
                    "Hello, I'm a fun-gi! ...Every time. I say it every time.",
                    "Greetings! Please excuse the glow. It's a phase. A half-life phase.",
                    "Oh! A visitor! Mind the spores, they're friendly.",
                },
            },
            [Race.Alien] = new RaceFlavour
            {
                Wares = "anti-gravity socks",
                Metal = "space-time",
                Errand = "slow down time near the big door so I can finish my nap",
                Rumours = new[]
                {
                    "Herr Einstein's hair is a gravitational anomaly. Things orbit it.",
                    "time runs slower near the boulders. I stand there to stay young.",
                    "relatively speaking, nobody here is late. Ever.",
                },
                Greetings = new[]
                {
                    "Greetings, carbon unit! Relatively speaking, hello.",
                    "Bleep. Bloop. Ha! Just kidding. We don't say that.",
                    "Hello from the future! Well, from three seconds ago. Time is funny here.",
                },
            },
            [Race.Shape] = new RaceFlavour
            {
                Wares = "slightly used angles",
                Metal = "right angles",
                Errand = "cross every bridge exactly once and tell me how it felt",
                Rumours = new[]
                {
                    "the triangles are unstable. Emotionally. Structurally they're fine.",
                    "the sphere keeps insisting it belongs. 'I have *infinite* faces!' Sure, buddy.",
                    "Mister Euler counts the bridges in his sleep. He gets the same number every time.",
                },
                Greetings = new[]
                {
                    "Hello! I'm well-rounded. Well, some of me.",
                    "Greetings! Exactly one greeting, as is proper.",
                    "Oh, hi! Don't mind my corners, they're pointy but polite.",
                },
            },
            [Race.Dinosaur] = new RaceFlavour
            {
                Wares = "fossils (gently used)",
                Metal = "volcano rock",
                Errand = "count the raindrops on my head. All of them. I'll wait",
                Rumours = new[]
                {
                    "Mary Anning found my great-great-great-grandma's tooth. We're very proud.",
                    "the avian lot keep saying the weather is 'their department'.",
                    "the eye of the hurricane is the calmest spot around. Great for naps.",
                },
                Greetings = new[] { "RAWR! ...That means hello." },
            },
            [Race.Townsfolk] = new RaceFlavour
            {
                Wares = "odds and ends from every face",
                Metal = "mystery metal from the ruins",
                Errand = "say hello to everyone in town. Everyone! It's what we do here",
                Rumours = new[]
                {
                    "Newton got bonked by another apple this morning. Third one today.",
                    "these ruins have bits of every face in them. Mixed up. Just how we like it.",
                    "a big stern demon wants everyone neat and apart. Gives me the shivers.",
                },
                Greetings = new[]
                {
                    "Welcome to town! Everyone's welcome. Even you! Especially you!",
                    "Howdy, neighbour! Lovely mess today, isn't it?",
                    "Oh, hello! Mind the rubble. It's historic rubble.",
                },
            },
        };

        /// <summary>Dinosaur greetings by variant (the shared table holds only the roar).</summary>
        public static readonly IReadOnlyDictionary<DinoVariant, string[]> DinoGreetings = new Dictionary<DinoVariant, string[]>
        {
            [DinoVariant.Terrestrial] = new[]
            {
                "RAWR! ...That means hello. *stomp* Sorry, that was a small quake.",
                "Hello, tiny friend! Mind your toes, mine are enormous.",
                "Greetings from ground level! Everything here shakes. It's fine.",
            },
            [DinoVariant.Avian] = new[]
            {
                "SKRAW! Hello from up here! Well, slightly up here.",
                "Hello! Is it going to rain? I can feel it in my feathers.",
                "Greetings! Ask me about the weather. Please. Nobody ever asks.",
            },
        };

        /// <summary>Darwin-flavoured lines the finches of the Biology face say (one per finch, picked by salt).</summary>
        public static readonly string[] DarwinLines =
        {
            "Adapt or get out of the niche! Professor Darwin says that every morning. Mostly to me.",
            "Professor Darwin measured my beak again. It grew a whole hair! He wrote three pages about it.",
            "Survival of the fittest! I'm not the fittest. I'm the chattiest. That counts, right?",
            "Different beaks for different seeds! Some of us crack, some of us sip. All of us gossip.",
            "Darwin says every finch has its niche. Mine is this exact spot. Please don't stand in it.",
        };

        /// <summary>The season's beak, as finches tell it: [0] thin, [1] thick. True for the run that says it.</summary>
        public static readonly string[] BeakLines =
        {
            "This season it's all thin beaks! Long and delicate: peck far-off buttons, tickle flowers till the vines grow.",
            "This season it's all thick beaks! Big and blunt: rocks, pots, crack 'em like seeds.",
        };

        /// <summary>Curie-flavoured lines the mushroom people of the Chemistry face say (one per mushroom, picked by salt).</summary>
        public static readonly string[] CurieLines =
        {
            "Glow responsibly! Madame Curie's first rule. Her second rule: also glow responsibly.",
            "Madame Curie says decay is just change on a schedule. I'm on page three of my schedule.",
            "When I'm old I'll be all skeleton, with skeleton-key fingers! Every lock on the face, mine. Can't wait.",
            "Half-life, full heart! Madame Curie taught us that. Then she wrote it down in a glowing notebook.",
            "Radium? Polonium? I prefer Mush-ium. Madame Curie says that isn't an element. Yet.",
        };

        /// <summary>How the isotope works, as the mushrooms tell it (true for every run: the mechanics).</summary>
        public const string IsotopeStageLine =
            "Fresh isotopes glow and light dark rooms. Then they go unstable: swing one at a cracked wall, boom! " +
            "Then they settle into lead. Drop lead on a plate and it stays put. The dispenser always has another.";

        public static string CurieLine(uint salt) => CurieLines[(int)(salt / 3u % (uint)CurieLines.Length)];

        public static string DarwinLine(uint salt) => DarwinLines[(int)(salt / 3u % (uint)DarwinLines.Length)];

        public static string BeakLine(bool thin) => BeakLines[thin ? 0 : 1];

        /// <summary>The whole conversation for an NPC in a run: greeting, role line, then hints.</summary>
        public static List<string> For(NpcSpec spec, RunFacts facts, HintDensity density) =>
            Lines(spec, HintGenerator.Generate(facts, spec.HintKind, spec.Salt, density));

        public static List<string> Lines(NpcSpec spec, IReadOnlyList<Hint> hints)
        {
            var lines = new List<string> { Greeting(spec), RoleLine(spec) };
            if (hints != null)
                foreach (Hint hint in hints) lines.Add(hint.Text);
            return lines;
        }

        public static string Greeting(NpcSpec spec)
        {
            string[] table = spec.Race == Race.Dinosaur && DinoGreetings.TryGetValue(spec.Variant, out string[] dino)
                ? dino
                : Flavours[spec.Race].Greetings;
            return table[(int)(spec.Salt % (uint)table.Length)];
        }

        public static string RoleLine(NpcSpec spec)
        {
            string[] table = RoleLines[spec.Role];
            string template = table[(int)(spec.Salt / 5u % (uint)table.Length)];
            return Fill(template, Flavours[spec.Race], spec.Salt);
        }

        private static string Fill(string template, RaceFlavour flavour, uint salt)
        {
            string wares = flavour.Wares;
            string capital = wares.Length > 0 ? char.ToUpperInvariant(wares[0]) + wares.Substring(1) : wares;
            string rumour = flavour.Rumours[(int)(salt / 13u % (uint)flavour.Rumours.Length)];
            return template
                .Replace("{Wares}", capital)
                .Replace("{wares}", wares)
                .Replace("{metal}", flavour.Metal)
                .Replace("{errand}", flavour.Errand)
                .Replace("{rumour}", rumour);
        }

        /// <summary>Every fixed line and filler, for the tone check.</summary>
        public static IEnumerable<string> AllFixedText()
        {
            foreach (string[] table in RoleLines.Values)
                foreach (string line in table) yield return line;
            foreach (RaceFlavour f in Flavours.Values)
            {
                yield return f.Wares;
                yield return f.Metal;
                yield return f.Errand;
                foreach (string line in f.Rumours) yield return line;
                foreach (string line in f.Greetings) yield return line;
            }
            foreach (string[] table in DinoGreetings.Values)
                foreach (string line in table) yield return line;
            foreach (string line in DarwinLines) yield return line;
            foreach (string line in BeakLines) yield return line;
            foreach (string line in CurieLines) yield return line;
            yield return IsotopeStageLine;
        }
    }
}
