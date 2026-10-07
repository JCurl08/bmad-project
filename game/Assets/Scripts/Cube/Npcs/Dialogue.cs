using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// Campy NPC lines (lore-and-tone.md: silly, never explicit, never the s-word). A conversation is at most two lines
    /// (MaxLines): one flavour line, then the head's first hint (true for the run). For a generic NPC the flavour is its
    /// role line, flavoured by the race's nouns; the face populations use their patron's line (or a mechanic line)
    /// instead. Subtle science (InWorldText): no real names and no science words; patrons and regions go by their
    /// in-world placeholder names. The table names (DarwinLines, CurieLines...) are code only and never shown.
    /// Tables are small (3 to 4 lines each); the NPC's salt picks the variant, so the same NPC always says the same thing. Greetings stay in the tables for the tone checks and later use.
    /// </summary>
    public static class Dialogue
    {
        /// <summary>The word the game never says (lore-and-tone.md). Tests check every line against it.</summary>
        public const string ForbiddenWord = InWorldText.ForbiddenWord;

        /// <summary>The most lines a conversation has: one flavour line, then the hint.</summary>
        public const int MaxLines = 2;

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
                    "the " + InWorldText.OldFledgling + " keeps measuring my beak. Every. Single. Morning.",
                    "the dinosaurs say they're our cousins. Distant cousins. *Very* distant.",
                },
                Greetings = new[]
                {
                    "Tweet! Oh, sorry, that was rude. Hello!",
                    "Cheep cheep! Fluffed up and ready to chat!",
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
                    "old " + InWorldText.Marcel + " is half a skeleton already, and he's *thrilled* about the fingers.",
                    "the " + InWorldText.Glowmother + " says I glow with potential. Mostly I just glow.",
                    "the skeleton-key folk can open any door. Nobody invites them anywhere, though.",
                },
                Greetings = new[]
                {
                    "Hello, I'm a fun-gi! ...Every time. I say it every time.",
                    "Greetings! Please excuse the glow. It's a phase. The stones go through phases too.",
                    "Oh! A visitor! Mind the spores, they're friendly.",
                },
            },
            [Race.Alien] = new RaceFlavour
            {
                Wares = "floaty socks",
                Metal = "clockwork",
                Errand = "slow down time near the big door so I can finish my nap",
                Rumours = new[]
                {
                    "the " + InWorldText.Timekeeper + "'s hair is so big that small things orbit it.",
                    "time runs slower near the boulders. I stand there to stay young.",
                    "around here nobody is ever late. Time is very forgiving.",
                },
                Greetings = new[]
                {
                    "Greetings, squishy visitor! Hello, eventually.",
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
                    "the " + InWorldText.Bridgekeeper + " counts the planks in her sleep. She gets the same number every time.",
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
                    "the " + InWorldText.OldDigger + " found my great-great-great-grandma's tooth. We're very proud.",
                    "the avian lot keep saying the sky is 'their department'.",
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
                    "the " + InWorldText.AppleSage + " got bonked by another apple this morning. Third one today.",
                    "these ruins have bits of every face in them. Mixed up. Just how we like it.",
                    "the " + InWorldText.Demon + " wants everyone neat and apart. Gives me the shivers.",
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
                "RAWR! ...That means hello. *stomp!* Sorry, that was a small quake.",
                "Hello, tiny friend! Mind your toes, mine are enormous.",
                "Greetings from ground level! Everything here shakes. It's fine.",
            },
            [DinoVariant.Avian] = new[]
            {
                "SKRAW! Hello from up here! Well, slightly up here.",
                "Hello! Is it going to rain? I can feel it in my feathers.",
                "Greetings! Ask me about the clouds. Please. Nobody ever asks.",
            },
        };

        /// <summary>Lines the finches of the Greenwood* say about their patron, the Old Fledgling* (one per finch, picked by salt).</summary>
        public static readonly string[] DarwinLines =
        {
            "Find your perch and keep it! The " + InWorldText.OldFledgling + " says that every morning. Mostly to me.",
            "The " + InWorldText.OldFledgling + " measured my beak again. It grew a whole hair! Three pages of notes about it.",
            "Only the toughest beaks crack the toughest seeds! Mine is not tough. Mine is chatty. That counts, right?",
            "Different beaks for different seeds! Some of us crack, some of us sip. All of us gossip.",
            "The " + InWorldText.OldFledgling + " says every finch has a favourite spot. Mine is this exact one. Please don't stand in it.",
        };

        /// <summary>The season's beak, as finches tell it: [0] thin, [1] thick. True for the run that says it.</summary>
        public static readonly string[] BeakLines =
        {
            "This season it's all thin beaks! Long and delicate: peck far-off buttons, tickle flowers till the vines grow.",
            "This season it's all thick beaks! Big and blunt: rocks, pots, crack 'em like seeds.",
        };

        /// <summary>
        /// Lines the mushroom people of the Chemistry face say about their patron, the Glowmother* (one per mushroom,
        /// picked by salt). Subtle science: no real names and no science words, only glowing, stones and changing.
        /// </summary>
        public static readonly string[] CurieLines =
        {
            "Rule one from the " + InWorldText.Glowmother + ": glow responsibly! Rule two: also glow responsibly.",
            "The " + InWorldText.Glowmother + " says everything changes, given time. I'm changing too. Very, very slowly.",
            "When I'm old I'll be all skeleton, with skeleton-key fingers! Every lock on the face, mine. Can't wait.",
            "Full heart, steady glow! The " + InWorldText.Glowmother + " taught us that. Then she wrote it down in a notebook that glows.",
            "Spores? Stones? I prefer mush. The " + InWorldText.Glowmother + " says mush isn't a proper stone. Yet.",
        };

        /// <summary>
        /// The lore line about the stones (a mushroom says it every run): they change between visits, in no set order
        /// (each run rolls its stone), never while you hold one.
        /// </summary>
        public const string StoneLoreLine =
            "The stones here never stay the same from one visit to the next. Leave, come back, and yours might glow, " +
            "might go wild, might turn heavy. Nobody can guess which. That's half the fun!";

        /// <summary>
        /// This visit's stone, as the mushrooms tell it, by stage: [0] Glowstone*, [1] Wildstone*, [2] Leadstone*. True for
        /// the run that says it (it names the rolled stone and what it does).
        /// </summary>
        public static readonly string[] StoneLines =
        {
            "This visit the stones glow! Find the " + ChemistryIsotope.GlowName + " and swing it at the dark doorways to light them up.",
            "This visit the stones are wild! Find the " + ChemistryIsotope.UnstableName + " and swing it at the cracked walls. Boom!",
            "This visit the stones are heavy! Find the " + ChemistryIsotope.LeadName + " and swing it at the pressure plates to weigh them down.",
        };

        /// <summary>The line naming a run's stone (see StoneLines).</summary>
        public static string StoneLine(IsotopeStage stage) => StoneLines[stage == IsotopeStage.None ? 0 : (int)stage];

        /// <summary>Lines the alien people of the Weightlands* say about their patron, the Timekeeper* (one per alien, picked by salt).</summary>
        public static readonly string[] EinsteinLines =
        {
            "Time is a stretchy thing, darling! The " + InWorldText.Timekeeper + " says so, and is never, ever late.",
            "The " + InWorldText.Timekeeper + " says heavy things make time dawdle. Time mostly tells me to nap.",
            "The " + InWorldText.Timekeeper + " wrote one tiny rule on the wall. Nobody can read it. We think it says 'cookies'.",
            "Stand next to a big boulder and time slows down. I've been twenty for six hundred years.",
            "The " + InWorldText.Timekeeper + "'s hair? So big it bends the light. And the comb. Mostly the comb.",
        };

        /// <summary>How the timed doors work, as the aliens tell it (true for every run: the mechanics).</summary>
        public const string MassMittLine =
            "Timed doors open from a switch, then close again. Grab boulders with the " + MassMittItem.Name + " (press Interact or swing it) " +
            "and drag them near the door: the heavier the pile, the slower time goes, so it stays open longer. Then step on the switch and run!";

        /// <summary>What the apple-bonked sage* says on the Weightlands* (the apple cameo; Newton-inspired, never named).</summary>
        public static readonly string[] NewtonLines =
        {
            "Ow! Another apple. That makes four hundred and twelve. I am beginning to suspect a pattern.",
            "Every apple falls toward my head. Every single one. I shall call it... a grudge.",
            "The " + InWorldText.Timekeeper + " says the apple is just taking its time. My forehead disagrees.",
        };

        /// <summary>
        /// Lines the shape people of the Math face say about their patron, the Bridgekeeper* (one per shape person, picked
        /// by salt). Subtle science: no real names and no science words, only planks, islands and walking.
        /// </summary>
        public static readonly string[] BridgekeeperLines =
        {
            "The " + InWorldText.Bridgekeeper + " says every plank deserves exactly one visit. Two is clingy. Zero is rude.",
            "I tried to cross the same plank twice once. The planks noticed. They always notice.",
            "The " + InWorldText.Bridgekeeper + " walks every plank on the face each morning, once each, and is home in time for tea.",
            "Start at the flag, finish wherever the planks run out. That's the " + InWorldText.Bridgekeeper + "'s whole rule. It's a good rule.",
            "Round head, pointy body, sturdy legs! The " + InWorldText.Bridgekeeper + " says we're built for walking planks.",
        };

        /// <summary>How the bridge doors work, as the shape people tell it (true for every run: the mechanics).</summary>
        public const string BridgeLine =
            "See the islands and planks by the locked doors? Start on the flag and cross every plank exactly once. " +
            "Cross one twice and they all reset!";

        /// <summary>How the gap-crosser* works, as the shape people tell it (true for every run: the mechanics).</summary>
        public const string GapCrosserLine =
            "A gap in front of a door? Swing the " + GapCrosserItem.Name + " at it and it lays a crossing right over. One gap at a time, mind.";

        /// <summary>
        /// Lines the terrestrial dinosaurs of the Earth face say about their patron, the Old Digger* (one per dinosaur, picked
        /// by salt). Subtle science: no real names and no science words, only digging, bones, rocks and rumbles.
        /// </summary>
        public static readonly string[] OldDiggerLines =
        {
            "The " + InWorldText.OldDigger + " says every rock has a story. Most of them are about being a rock. Riveting stuff.",
            "When the ground rumbles, the " + InWorldText.OldDigger + " grabs a brush. Crevices open up and bones say hello.",
            "I found a bone in a crevice once! Turned out it was mine. Long story. I want it back.",
            "The " + InWorldText.OldDigger + " digs slowly, gently, for hours. I stomp. We get there in the end, both of us.",
            "Layers and layers and layers! The " + InWorldText.OldDigger + " reads the cliffs like a book. A very slow, very heavy book.",
        };

        /// <summary>
        /// Lines the avian dinosaurs of the Earth face say about their patron, the Sky-Watcher* (one per dinosaur, picked by
        /// salt). Subtle science: no real names and no science words, only clouds, wind, rain and feathers.
        /// </summary>
        public static readonly string[] SkyWatcherLines =
        {
            "The " + InWorldText.SkyWatcher + " reads the clouds every morning. Today they said 'puffy'. Very insightful.",
            "Feathers up means rain coming! The " + InWorldText.SkyWatcher + " taught me that. Feathers down means lunch.",
            "The " + InWorldText.SkyWatcher + " says the wind is just the sky going somewhere in a hurry. Same, sky. Same.",
            "I flapped once and a breeze came. The " + InWorldText.SkyWatcher + " says that was a coincidence. I say it was me.",
            "Snow, rain, wind, calm! The " + InWorldText.SkyWatcher + " has a feather for each. She runs out of feathers by noon.",
        };

        /// <summary>
        /// This visit's Stormcaller*, as the dinosaurs tell it, by phenomenon: [0] quake, [1] rain, [2] snow, [3] tornado. True
        /// for the run that says it (it names the rolled wand and what it does).
        /// </summary>
        public static readonly string[] WandLines =
        {
            "This visit the " + StormcallerItem.Name + " calls up a quake! Find it and swing it at the rock pillars to knock them flat.",
            "This visit the " + StormcallerItem.Name + " calls up rain! Find it and swing it at the walls of fire to douse them.",
            "This visit the " + StormcallerItem.Name + " calls up snow! Find it and swing it at the water to freeze a crossing.",
            "This visit the " + StormcallerItem.Name + " calls up a tornado! Find it and swing it at the debris piles to blow them away.",
        };

        /// <summary>The wand line when the run's phenomenon is unknown: it names none, so it is never false.</summary>
        public const string NeutralWandLine =
            "Somewhere out here lies the " + StormcallerItem.Name + ". Find it, swing it, and see what the sky has in mind!";

        /// <summary>The line naming a run's Stormcaller* (see WandLines); NeutralWandLine when the phenomenon is unknown.</summary>
        public static string WandLine(Phenomenon kind) =>
            kind >= Phenomenon.Quake && (int)kind < WandLines.Length ? WandLines[(int)kind] : NeutralWandLine;

        /// <summary>How the hazards of the Earth face work, as the dinosaurs tell it (true for every run: the mechanics).</summary>
        public const string HazardLine =
            "Out in the " + InWorldText.Wildlands + " the wind shoves you, snow sends you sliding and freezing rain slows you down. " +
            "Go round the crevices. The middle of the face is calm, though!";

        public static string OldDiggerLine(uint salt) => OldDiggerLines[(int)(salt / 3u % (uint)OldDiggerLines.Length)];

        public static string SkyWatcherLine(uint salt) => SkyWatcherLines[(int)(salt / 3u % (uint)SkyWatcherLines.Length)];

        public static string BridgekeeperLine(uint salt) => BridgekeeperLines[(int)(salt / 3u % (uint)BridgekeeperLines.Length)];

        public static string EinsteinLine(uint salt) => EinsteinLines[(int)(salt / 3u % (uint)EinsteinLines.Length)];

        public static string CurieLine(uint salt) => CurieLines[(int)(salt / 3u % (uint)CurieLines.Length)];

        public static string DarwinLine(uint salt) => DarwinLines[(int)(salt / 3u % (uint)DarwinLines.Length)];

        public static string BeakLine(bool thin) => BeakLines[thin ? 0 : 1];

        /// <summary>The whole conversation for an NPC in a run: its role line, then its first hint.</summary>
        public static List<string> For(NpcSpec spec, RunFacts facts, HintDensity density) =>
            Lines(spec, HintGenerator.Generate(facts, spec.HintKind, spec.Salt, density));

        /// <summary>The conversation from given hints: the role line, then the first hint.</summary>
        public static List<string> Lines(NpcSpec spec, IReadOnlyList<Hint> hints) => Conversation(RoleLine(spec), hints);

        /// <summary>A conversation: the flavour line, then the first hint (if any). Never more than MaxLines.</summary>
        public static List<string> Conversation(string flavour, IReadOnlyList<Hint> hints)
        {
            var lines = new List<string>(MaxLines) { flavour };
            if (hints != null && hints.Count > 0) lines.Add(hints[0].Text);
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
            yield return StoneLoreLine;
            foreach (string line in StoneLines) yield return line;
            foreach (string line in EinsteinLines) yield return line;
            yield return MassMittLine;
            foreach (string line in NewtonLines) yield return line;
            foreach (string line in BridgekeeperLines) yield return line;
            yield return BridgeLine;
            yield return GapCrosserLine;
            foreach (string line in OldDiggerLines) yield return line;
            foreach (string line in SkyWatcherLines) yield return line;
            foreach (string line in WandLines) yield return line;
            yield return NeutralWandLine;
            yield return HazardLine;
        }
    }
}
