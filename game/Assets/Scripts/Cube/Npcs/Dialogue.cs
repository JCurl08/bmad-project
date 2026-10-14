using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// Campy NPC lines (lore-and-tone.md: silly, never explicit, never the s-word), each race in its own voice (4.4,
    /// voices-and-names.md): the shape people logical and terse, the mushroom folk emotionally reactive, the finches
    /// near-Shakespearean, the aliens philosophical, the ground dinosaurs rugged and outdoorsy (the avian ones breezier),
    /// the townsfolk a friendly mix. A conversation is at most two lines (MaxLines): one flavour line, then the head's
    /// first hint (true for the run, in the same voice: HintGenerator.For). For a generic NPC the flavour is its role
    /// line in its voice; a leader says its leader line; the face populations use their patron's line (or a mechanic
    /// line) instead. Subtle science (InWorldText): no real names and no science words; regions and items keep their
    /// "*" placeholders, characters have chosen names (NpcNames). The table names (DarwinLines, CurieLines...) are code
    /// only and never shown. The NPC's salt picks the variant, so the same NPC always says the same thing.
    /// </summary>
    public static class Dialogue
    {
        /// <summary>The word the game never says (lore-and-tone.md). Tests check every line against it.</summary>
        public const string ForbiddenWord = InWorldText.ForbiddenWord;

        /// <summary>The most lines a conversation has: one flavour line, then the hint.</summary>
        public const int MaxLines = 2;

        /// <summary>The longest a line may be: three lines of the dialogue box (760 px at 20 pt).</summary>
        public const int MaxLineLength = 200;

        /// <summary>A voice's nouns, role lines, rumours and greetings.</summary>
        public sealed class RaceFlavour
        {
            public string Wares;
            public string Metal;
            /// <summary>The face's one errand (4.18, Errands): carry a word to {name}, another resident of the town.</summary>
            public string Errand;

            /// <summary>
            /// What a quest-giver who does not give the face's errand says (4.18: one errand per face): they have none, but
            /// {name} (the face's errand-giver) here in town has one, then gossip ({rumour}). True whether or not the errand
            /// was given yet.
            /// </summary>
            public string SpareQuest;

            /// <summary>The same when no errand-giver is known (a debug spawn): they have no errand, then gossip.</summary>
            public string SpareQuestPlain;
            public string[] Rumours;
            public string[] Greetings;

            /// <summary>Role lines in this voice. Tokens: {Wares}/{wares}, {metal}, {errand} (with its {name}), {rumour}.</summary>
            public Dictionary<NpcRole, string[]> Roles;
        }

        // Patron and special names, as the lines say them.
        private const string Beakwell = NpcNames.FinchPatron;
        private const string Glimmercap = NpcNames.MushroomPatron;
        private const string Whenever = NpcNames.AlienPatron;
        private const string Spanwell = NpcNames.ShapePatron;
        private const string Trowel = NpcNames.GroundPatron;
        private const string Cloudeye = NpcNames.SkyPatron;

        public static readonly IReadOnlyDictionary<Voice, RaceFlavour> Flavours = new Dictionary<Voice, RaceFlavour>
        {
            // Townsfolk: a friendly, campy mix.
            [Voice.Town] = new RaceFlavour
            {
                Wares = "odds and ends from every face",
                Metal = "mystery metal from the ruins",
                Errand = "say hello to {name} for me. Everyone says hello here! It's what we do",
                SpareQuest = "No errand from me, but {name} here in town has one! Meanwhile, gossip: {rumour}",
                SpareQuestPlain = "No errands from me today, I'm afraid. Have some gossip instead: {rumour}",
                Roles = new Dictionary<NpcRole, string[]>
                {
                    [NpcRole.Merchant] = new[]
                    {
                        "{Wares} for sale! ...Fine, *hints* for sale. The shop isn't built yet.",
                        "Step right up! Finest {wares} on the cube! Cash only. Also there is no till.",
                        "Today's special: {wares}, lightly used. Tomorrow's special: the same {wares}.",
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
                },
                Rumours = new[]
                {
                    NpcNames.AppleSage + " got bonked by another apple this morning. Third one today.",
                    "these ruins have bits of every face in them. Mixed up. Just how we like it.",
                    InWorldText.DemonFull + " wants everyone neat and apart. Gives me the shivers.",
                },
                Greetings = new[]
                {
                    "Welcome to town! Everyone's welcome. Even you! Especially you!",
                    "Howdy, neighbour! Lovely mess today, isn't it?",
                    "Oh, hello! Mind the rubble. It's historic rubble.",
                },
            },
            // Finches: near-Shakespearean and proper, still easy to read.
            [Voice.Finch] = new RaceFlavour
            {
                Wares = "seeds most fine",
                Metal = "beak-polish",
                Errand = "carry my fond greetings to {name}, who doth perch here in our town",
                SpareQuest = "Alas, I have no errand, but {name} here in town hath one. Take gossip instead: {rumour}",
                SpareQuestPlain = "Alas, I have no errand for thee. Take gossip instead: {rumour}",
                Roles = new Dictionary<NpcRole, string[]>
                {
                    [NpcRole.Merchant] = new[]
                    {
                        "{Wares}, good traveller! Prithee, purchase some. Alas, the shop is not yet built.",
                        "Hark! The finest {wares} in all the land! Coin? Nay, I take compliments.",
                        "Good morrow! Today I offer {wares}. Tomorrow, verily, the very same {wares}.",
                    },
                    [NpcRole.QuestGiver] = new[]
                    {
                        "Ah, a hero! Prithee, {errand}. Thy reward: mine eternal thanks.",
                        "Good traveller, a quest! Pray {errand}. No haste. Well, some haste.",
                        "I have waited an age for one such as thee. Prithee, {errand}.",
                    },
                    [NpcRole.Smith] = new[]
                    {
                        "I polish {metal} from dawn till dusk. 'Tis a noble craft. Also, 'tis very shiny.",
                        "The finest {metal} work in all the land, forsooth! Bring me aught to mend.",
                        "Mind thy feathers, the {metal} is still warm. Verily, everything is warm.",
                    },
                    [NpcRole.Gossip] = new[]
                    {
                        "Betwixt thee and me... {rumour}",
                        "Hast thou heard? {rumour}",
                        "Tell no one, I pray thee, but {rumour}",
                    },
                },
                Rumours = new[]
                {
                    "the thin-beaks think themselves above us all. Tweet-tweet, la-di-da, forsooth.",
                    Beakwell + " doth measure my beak every morning. Every. Single. Morning.",
                    "the dinosaurs claim to be our cousins. Distant cousins, verily. *Very* distant.",
                },
                Greetings = new[]
                {
                    "Good morrow, traveller! Pray pardon my chirping.",
                    "Cheep! Well met, good stranger. Thou art most welcome.",
                    "Hark! Thou art no cat, I trust? Good. Well met!",
                },
            },
            // Mushroom folk: emotionally reactive; they flare up, fizz, calm down and bond fast.
            [Voice.Mushroom] = new RaceFlavour
            {
                Wares = "spores",
                Metal = "lead",
                Errand = "tell {name} I'm *sorry* about the spores! ...Or that I'm not! Ugh, just say hi for me",
                SpareQuest = "Ugh, I have *nothing* for you! But {name} here in town does! Fine, gossip: {rumour}",
                SpareQuestPlain = "Ugh, I have *nothing* for you today! Fine, gossip: {rumour}",
                Roles = new Dictionary<NpcRole, string[]>
                {
                    [NpcRole.Merchant] = new[]
                    {
                        "{Wares} for sale! Fresh! *Fresh!* ...Ugh, nobody ever buys. I'm fine. I'm *fine*.",
                        "Oh! A customer! Finest {wares} on the cube! I could cry! I am crying!",
                        "Buy my {wares}! Or don't! See if I care! ...Sob. I care so much.",
                    },
                    [NpcRole.QuestGiver] = new[]
                    {
                        "Ooh! You! Hero! Please, please {errand}!",
                        "A quest! A *quest!* Oh, I'm so excited I'm fizzing! Please {errand}!",
                        "I've waited *ages* for a hero! Sob! Please {errand}!",
                    },
                    [NpcRole.Smith] = new[]
                    {
                        "I hammer {metal} all day! CLANG! Ooh, that felt good! CLANG!",
                        "Argh! The {metal} is hot! Everything is hot! Why is everything so *hot*?",
                        "Bring me something to mend! Anything! I'm *so* bored I could pop!",
                    },
                    [NpcRole.Gossip] = new[]
                    {
                        "Oh my spores, have you heard? {rumour}",
                        "Gasp! You won't believe it! {rumour}",
                        "Don't tell a soul, but... {rumour}",
                    },
                },
                Rumours = new[]
                {
                    NpcNames.OldMushroom + " is half a skeleton already, and he's *thrilled* about the fingers!",
                    Glimmercap + " says I glow with potential! Mostly I just glow. Sob!",
                    "the skeleton-key folk can open any door! Nobody invites them anywhere, though. So sad!",
                },
                Greetings = new[]
                {
                    "Hello! I'm a fun-gi! ...Ugh, every time. I say it *every* time!",
                    "Greetings! Ooh, excuse the glow, it's a phase! The stones go through phases too!",
                    "Oh! A visitor! Mind the spores, they're friendly! Mostly!",
                },
            },
            // Aliens: philosophical and abstract; they muse on time, weight and motion.
            [Voice.Alien] = new RaceFlavour
            {
                Wares = "floaty socks",
                Metal = "clockwork",
                Errand = "ask {name} what the time is, then tell me. Is time even theirs to tell, I wonder?",
                SpareQuest = "I have no errand, perhaps, but {name} here in town has one. Instead: {rumour}",
                SpareQuestPlain = "An errand? Perhaps another day. Instead: {rumour}",
                Roles = new Dictionary<NpcRole, string[]>
                {
                    [NpcRole.Merchant] = new[]
                    {
                        "{Wares} for sale. But what is a sale, truly? A swap of... moments?",
                        "Consider these {wares}. Do they float, or does the ground merely fall away?",
                        "Today I sell {wares}. Tomorrow, perhaps, the {wares} sell me.",
                    },
                    [NpcRole.QuestGiver] = new[]
                    {
                        "Ah, a traveller. Or are we all travelling? Either way, please {errand}",
                        "I have waited here for you, or perhaps you for me. Please {errand}",
                        "A quest, if quests exist. Please {errand}",
                    },
                    [NpcRole.Smith] = new[]
                    {
                        "I tinker with {metal}. Tick, tock. Is the tock the tick's echo, I wonder?",
                        "Bring me something to mend. Or bring nothing, and we shall ponder the mending.",
                        "The {metal} turns, the hours turn. Who winds whom, I ask you?",
                    },
                    [NpcRole.Gossip] = new[]
                    {
                        "Have you considered... {rumour}",
                        "I mused upon it all night. {rumour}",
                        "A thought, only a thought: {rumour}",
                    },
                },
                Rumours = new[]
                {
                    Whenever + "'s hair is so big that small things circle it. Do they choose to, I wonder?",
                    "time runs slower near the boulders. I stand there to stay young. Is that wisdom, I wonder, or vanity?",
                    "nobody here is ever late. But is anyone ever early, I wonder?",
                },
                Greetings = new[]
                {
                    "Greetings, visitor. Hello now, or hello later? Perhaps both.",
                    "Hmm. You exist. How lovely. Do I, I wonder?",
                    "Hello from three seconds ago. Time is funny here. Ponder it.",
                },
            },
            // Shape people: logical, terse, a little cool, but helpful. They reason in if/then steps. Never "!".
            [Voice.Shape] = new RaceFlavour
            {
                Wares = "slightly used angles",
                Metal = "right angles",
                Errand = "tell {name} that the corner is square. Then we are both satisfied",
                SpareQuest = "I have no errand. {name} here in town has one, therefore ask there. Gossip: {rumour}",
                SpareQuestPlain = "I have no errand. Therefore, gossip: {rumour}",
                Roles = new Dictionary<NpcRole, string[]>
                {
                    [NpcRole.Merchant] = new[]
                    {
                        "{Wares} for sale. If you need one, then buy one. Simple.",
                        "Given my small stock, {wares} cost a lot. Therefore, hints cost nothing.",
                        "These are {wares}. If they are slightly used, then they are slightly cheaper.",
                    },
                    [NpcRole.QuestGiver] = new[]
                    {
                        "A task. If you are free, then {errand}.",
                        "Given that you are a hero, it follows that you will {errand}.",
                        "I require help. Therefore: {errand}.",
                    },
                    [NpcRole.Smith] = new[]
                    {
                        "I make {metal}. If it is not square, then it is not my work.",
                        "Bring me something crooked. Given time, I will make it straight.",
                        "The {metal} are cooling. Therefore, do not touch them. Thus ends my safety talk.",
                    },
                    [NpcRole.Gossip] = new[]
                    {
                        "Given the evidence: {rumour}",
                        "A rumour. If it is true, then {rumour}",
                        "Unconfirmed, therefore whispered: {rumour}",
                    },
                },
                Rumours = new[]
                {
                    "the triangles are unstable. Emotionally. Given their wide base, structurally they are fine.",
                    "the sphere insists it belongs. 'I have *infinite* faces.' If so, then it has infinite washing.",
                    Spanwell + " counts the planks in her sleep. She gets the same number every time. Therefore, they are fine.",
                },
                Greetings = new[]
                {
                    "Hello. If you need something, then ask.",
                    "Greetings. One greeting, given that one is enough.",
                    "Hi. My corners are pointy, therefore keep a polite distance.",
                },
            },
            // Ground dinosaurs: rugged and outdoorsy; practical, a bit gruff.
            [Voice.Ground] = new RaceFlavour
            {
                Wares = "trail rations and old bones",
                Metal = "volcano rock",
                Errand = "tell {name} the trail up the ridge is clear. They'll want to know",
                SpareQuest = "No errand from me, but {name} round the campfire here has one. Camp gossip: {rumour}",
                SpareQuestPlain = "No errands out on the trail today. Camp gossip instead: {rumour}",
                Roles = new Dictionary<NpcRole, string[]>
                {
                    [NpcRole.Merchant] = new[]
                    {
                        "{Wares} for sale. Good for the trail. Bad for the teeth.",
                        "Step up. {Wares}, fresh off the hill. Well, fresh-ish.",
                        "Got {wares}. Got mud on 'em, too. Mud's free.",
                    },
                    [NpcRole.QuestGiver] = new[]
                    {
                        "You look sturdy. Good. Go {errand}.",
                        "Need a hand out on the trail. Please {errand}.",
                        "Storm's coming, and I've got chores. Please {errand}.",
                    },
                    [NpcRole.Smith] = new[]
                    {
                        "I chip {metal} all day. Chip. Chip. Builds strong arms and short tempers.",
                        "Best {metal} work this side of the canyon. Bring me something to fix.",
                        "Careful, the {metal} is still hot. Like the ground round here. Mind your feet.",
                    },
                    [NpcRole.Gossip] = new[]
                    {
                        "Heard it round the campfire: {rumour}",
                        "Word on the trail: {rumour}",
                        "Keep it under your hat out there, but {rumour}",
                    },
                },
                Rumours = new[]
                {
                    Trowel + " dug my great-great-great-grandma's tooth out of a cliff. We're very proud.",
                    "the avian lot keep saying the sky is 'their department'. Ground's mine, then.",
                    "the eye of the hurricane is the calmest camp around. Great for naps.",
                },
                Greetings = new[]
                {
                    "RAWR. ...That means hello. Mind the rocks.",
                    "Hello, tiny friend. Watch your toes, the ground's rough.",
                    "Howdy from ground level. Everything here shakes. You get used to it.",
                },
            },
            // Avian dinosaurs: outdoorsy too, but breezier.
            [Voice.Sky] = new RaceFlavour
            {
                Wares = "feathers, lightly windblown",
                Metal = "storm-glass",
                Errand = "tell {name} there's rain on the wind. I can feel it in my feathers",
                SpareQuest = "Nothing on the wind from me, but {name} here in town has an errand. Gossip: {rumour}",
                SpareQuestPlain = "Nothing on the wind for you today. Here's a breeze of gossip: {rumour}",
                Roles = new Dictionary<NpcRole, string[]>
                {
                    [NpcRole.Merchant] = new[]
                    {
                        "{Wares}, for sale. Light as a breeze, and twice as fluffy.",
                        "Step right up, sky-friend. Finest {wares} under the clouds.",
                        "Got {wares}. Caught 'em on the wind this morning.",
                    },
                    [NpcRole.QuestGiver] = new[]
                    {
                        "Oh, a helper on the wind. Please {errand}.",
                        "Fancy a quest? Please {errand}.",
                        "The sky sent you, I knew it. Please {errand}.",
                    },
                    [NpcRole.Smith] = new[]
                    {
                        "I blow {metal} into little bubbles. Like clouds you can hold.",
                        "Best {metal} under the sun. Bring me something to fix, I'm breezing through my list.",
                        "Mind the {metal}, it's still warm from the sun.",
                    },
                    [NpcRole.Gossip] = new[]
                    {
                        "Word on the wind: {rumour}",
                        "A little breeze told me: {rumour}",
                        "Between you, me and the clouds... {rumour}",
                    },
                },
                Rumours = new[]
                {
                    Cloudeye + " reads the clouds every morning. Today they said 'puffy'. Very insightful.",
                    "the ground lot think the sky is just the lid. The *lid*. Honestly.",
                    "the eye of the hurricane is the calmest spot in the sky. Great for naps.",
                },
                Greetings = new[]
                {
                    "SKRAW. Hello from up here. Well, slightly up here, in the breeze.",
                    "Hello. Is it going to rain? I can feel it in my feathers.",
                    "Hi there. Ask me about the clouds. Please. Nobody ever asks.",
                },
            },
        };

        /// <summary>
        /// What each race's leader (NpcNames.Leader) says before its hint (4.5 gives leaders their quests). The dinosaur
        /// leader rules both kinds, so its line suits both voices.
        /// </summary>
        public static readonly IReadOnlyDictionary<Race, string> LeaderLines = new Dictionary<Race, string>
        {
            [Race.Townsfolk] = "Mayor Muddleton, at your service! Town's the last mixed-up place left, and I mean to keep it gloriously messy.",
            [Race.Finch] = "I am Queen Wrennifer, and the " + InWorldText.Greenwood + " is mine. Welcome, traveller. Pray be thou courteous to my finches.",
            [Race.Mushroom] = "I'm Duchess Flarella, and I'm *so* happy you're here! Also furious! Mostly happy! Welcome to the " + InWorldText.EmberVale + "!",
            [Race.Alien] = "I am Grand Muser Yondrix. Welcome to the " + InWorldText.Weightlands + "... or, I wonder, does it welcome you?",
            [Race.Shape] = "I am Chancellor Quadrilla. If you have business in " + InWorldText.Bridgelands + ", then state it. Briefly.",
            [Race.Dinosaur] = "Chief Rockjaw. I run this camp. Want to help the " + InWorldText.Wildlands + "? Then pack light and mind the cliffs.",
        };

        /// <summary>A leader's line (LeaderLines).</summary>
        public static string LeaderLine(Race race) => LeaderLines[race];

        /// <summary>
        /// Each race's leader's quest (4.5, filled in by 4.8), in their voice: their region broke into {count} fragments (its
        /// die number), the player holds {found}, and all of them mend it. Count words come from CountWord ("four",
        /// "none"). The Mayor's is MayorGiftLine (Town's one fragment, handed over at once). The dinosaur leader's suits both
        /// dinosaur voices.
        /// </summary>
        public static readonly IReadOnlyDictionary<Race, string> QuestTemplates = new Dictionary<Race, string>
        {
            [Race.Finch] = "Hark: the " + InWorldText.Greenwood + " doth fray, and {count} fragments are lost. Thou bearest {found} of them. Prithee, bring all {count} hither, that we may mend it together.",
            [Race.Mushroom] = "Ugh, the " + InWorldText.EmberVale + " broke into {count} fragments and I'm furious! You've found {found}. Bring me all {count} and we'll mend it? Oh, I'd love you forever!",
            [Race.Alien] = "The " + InWorldText.Weightlands + " lost {count} fragments, I think. You carry {found}... or do they carry you? Bring all {count}, and perhaps together we may mend it.",
            [Race.Shape] = "The " + InWorldText.Bridgelands + " is broken into {count} fragments. You hold {found}. If you bring all {count}, then we mend it. Therefore, search.",
            [Race.Dinosaur] = "The " + InWorldText.Wildlands + " cracked into {count} fragments, ridge to sky. You've hauled back {found}. Scout the trails, bring all {count} to camp, and we'll mend this ground.",
        };

        /// <summary>
        /// The Mayor's quest with no run to read (4.20; a debug spawn): Town keeps one fragment, and in town he offers a tour
        /// (TownTourText.Offer, with its question) or hands it straight over.
        /// </summary>
        public const string MayorGiftLine = "Every face is coming apart, friend, and Town keeps one fragment of its own. Talk to me in the town square: I'll show you round or hand it straight over, and then we'll mend Town!";

        /// <summary>
        /// The Mayor's fallback (4.8's gift) when the tour cannot run (4.20: no TownTourQuest bound, or a Town of another size):
        /// he hands Town's one fragment over; talking again hands it back and opens Town's temple.
        /// </summary>
        public const string MayorFoundLine = "Here's the job, friend: every face is coming apart. Town lost just one fragment, and look, I found it! Hold on to it, then hand it back to me and we'll mend Town!";

        /// <summary>
        /// What each leader says on taking all {count} fragments (4.8; since 4.14): the fragments are not mended yet, the leader
        /// unseals the face's temple instead; solving its puzzle mends them and gives the face's gem, which helps in the core
        /// (BossBenefits.Of its theme), in voice and in plain words.
        /// </summary>
        public static readonly IReadOnlyDictionary<Race, string> UnsealTemplates = new Dictionary<Race, string>
        {
            [Race.Townsfolk] = "That's the one! Now watch: I'm opening Town's old temple by the square. Solve the puzzle inside and the fragment mends. Its gem gives you an extra heart against the Demon!",
            [Race.Finch] = "Verily, all {count}! I have unsealed our temple. Solve the riddle within and the fragments shall mend. Its gem shall draw the Demon's bouncing motes toward thee.",
            [Race.Mushroom] = "All {count}! Oh, I'm so thrilled! I've unsealed our temple! Solve its puzzle and they'll mend! Its gem fizzes away some of the Demon's bouncing motes. Fewer to mix!",
            [Race.Alien] = "All {count}... I have opened our temple, I think. Solve what waits within, and perhaps they mend. Its gem makes time drag for the Demon: it will tidy things back far, far more slowly.",
            [Race.Shape] = "Given all {count} fragments, I unseal our temple. If you solve its puzzle, then they mend. Its gem widens the gaps at both ends of the Demon's wall. Therefore, go.",
            [Race.Dinosaur] = "All {count}! I've cracked open our temple, up the trail by the square. Solve it and the fragments mend. Its gem sends a headwind: the Demon's shots fly slower and rarer.",
        };

        /// <summary>What each leader says once their temple is open but its gem not yet won (4.14), in voice.</summary>
        public static readonly IReadOnlyDictionary<Race, string> TempleOpenLines = new Dictionary<Race, string>
        {
            [Race.Townsfolk] = "Town's temple is open, friend, right there by the square! Solve its puzzle and the fragment mends itself.",
            [Race.Finch] = "Our temple standeth open, good traveller. Hark: solve its riddle, and thou shalt hold its gem.",
            [Race.Mushroom] = "The temple's open! Go, go, go! Ooh, I can't watch! Solve it and the fragments mend!",
            [Race.Alien] = "The temple waits, open. Perhaps it has always been waiting for you. Solve it, and the fragments mend.",
            [Race.Shape] = "The temple is open. If you solve its puzzle, then the fragments mend. Hence, the gem.",
            [Race.Dinosaur] = "Temple's open, just up the trail. Solve what's inside and the fragments mend. Then the gem's yours.",
        };

        /// <summary>What each leader says once their face is mended and its gem won (4.8; 4.14), in voice.</summary>
        public static readonly IReadOnlyDictionary<Race, string> MendedLines = new Dictionary<Race, string>
        {
            [Race.Townsfolk] = "Town's mended and gloriously messy again, and that gem looks grand on you! Now the other faces: each leader is waiting on their own fragments.",
            [Race.Finch] = "The " + InWorldText.Greenwood + " doth flourish, and thou bearest its gem. Go now, and may the wind be kind to thy feathers.",
            [Race.Mushroom] = "It's mended and you've got the gem and I'm *so* happy! Go bonk that Demon for me! I'll be cheering! Loudly!",
            [Race.Alien] = "Whole again, and the gem is yours. I wonder whether it was ever truly broken? Go well, traveller.",
            [Race.Shape] = "The " + InWorldText.Bridgelands + " is whole, and the gem is yours. If the core waits, then go there.",
            [Race.Dinosaur] = "Ground's holding, the sky's clear and the gem's yours. Go give that Demon a storm of its own.",
        };

        /// <summary>A count in words for the quest lines: "none", "one" .. "six" (numerals past six).</summary>
        public static string CountWord(int n)
        {
            switch (n)
            {
                case 0: return "none";
                case 1: return "one";
                case 2: return "two";
                case 3: return "three";
                case 4: return "four";
                case 5: return "five";
                case 6: return "six";
                default: return n.ToString();
            }
        }

        /// <summary>The count a leader's quest names when no run is given (QuestLine(race), the scans): four, none found.</summary>
        public const int SampleCount = 4;

        /// <summary>A leader's quest line for their face's count of fragments, with found of them held (the Mayor's: MayorGiftLine).</summary>
        public static string QuestLine(Race race, int count, int found) => race == Race.Townsfolk
            ? MayorGiftLine
            : QuestTemplates[race].Replace("{count}", CountWord(count)).Replace("{found}", CountWord(found));

        /// <summary>A leader's quest hook with no run to read (SampleCount, none found; the Mayor's gift line).</summary>
        public static string QuestLine(Race race) => QuestLine(race, SampleCount, 0);

        /// <summary>A leader's line on taking all count fragments: the temple is unsealed (4.14).</summary>
        public static string UnsealLine(Race race, int count) => UnsealTemplates[race].Replace("{count}", CountWord(count));

        /// <summary>A leader's line while their temple is open and its gem not yet won (4.14).</summary>
        public static string TempleOpenLine(Race race) => TempleOpenLines[race];

        /// <summary>A leader's line once their face is mended (4.8).</summary>
        public static string MendedLine(Race race) => MendedLines[race];

        /// <summary>A leader's whole conversation (4.5) with no run to read: their greeting, then their quest hook (two lines, MaxLines).</summary>
        public static List<string> LeaderConversation(Race race) => new List<string>(MaxLines) { LeaderLine(race), QuestLine(race) };

        /// <summary>A leader's conversation while their face's fragments are still out (4.8): their greeting, then the quest naming the count.</summary>
        public static List<string> QuestConversation(Race race, int count, int found) =>
            new List<string>(MaxLines) { LeaderLine(race), QuestLine(race, count, found) };

        /// <summary>A leader's conversation as they take the fragments and unseal the temple (4.14).</summary>
        public static List<string> UnsealConversation(Race race, int count) => new List<string>(MaxLines) { UnsealLine(race, count) };

        /// <summary>A leader's conversation while their temple is open (4.14).</summary>
        public static List<string> TempleOpenConversation(Race race) => new List<string>(MaxLines) { TempleOpenLine(race) };

        /// <summary>A leader's conversation once the face is mended (4.8).</summary>
        public static List<string> MendedConversation(Race race) => new List<string>(MaxLines) { MendedLine(race) };

        /// <summary>Every quest, unseal, temple-open and mended line a race's leader can say (every count 2-6 and found 0..count; Town's own), for the scans.</summary>
        public static IEnumerable<string> LeaderQuestLines(Race race)
        {
            if (race == Race.Townsfolk)
            {
                yield return MayorGiftLine;
                yield return MayorFoundLine;
                foreach (string line in QuestLines.MayorLines()) yield return line;
                yield return UnsealLine(race, 1);
            }
            else
                for (int count = 2; count <= DieNumbers.Highest; count++)
                {
                    for (int found = 0; found <= count; found++) yield return QuestLine(race, count, found);
                    yield return UnsealLine(race, count);
                }
            yield return TempleOpenLine(race);
            yield return MendedLine(race);
        }

        /// <summary>
        /// What a shop's keeper says (4.6), in their voice: a welcome to a bare counter, then that the currency (InWorldText.Currency) is spent between
        /// trips (meta-currency spending stays on the between-runs screen; the counter is a hook for later stories).
        /// </summary>
        public static readonly IReadOnlyDictionary<Voice, string[]> KeeperLineTable = new Dictionary<Voice, string[]>
        {
            [Voice.Town] = new[]
            {
                "Welcome to the shop, dear! The shelves are all spoken for today, but the gossip's free and I've plenty of it.",
                InWorldText.Currency + " get spent between trips, back where you rest up. Come by after a long day and we'll talk prices!",
            },
            [Voice.Finch] = new[]
            {
                "Good morrow! Thou art welcome at my counter, though 'tis bare of wares this day.",
                "Prithee, keep thy " + InWorldText.Currency + " till thou art betwixt thy journeys; verily, 'tis then that spending is done.",
            },
            [Voice.Mushroom] = new[]
            {
                "Ooh, a customer! I'm *so* thrilled! Nothing's for sale today, though. Sob.",
                InWorldText.Currency + "? Ugh, you spend those between trips, not here. I just love standing at my counter!",
            },
            [Voice.Alien] = new[]
            {
                "A counter with nothing on it. Perhaps that is the purest shop of all, I wonder?",
                InWorldText.Currency + " are spent between journeys, I am told. Consider: are " + InWorldText.Currency + " unspent truly " + InWorldText.Currency + " at all?",
            },
            [Voice.Shape] = new[]
            {
                "This is a counter. If nothing is on it, then nothing is for sale.",
                InWorldText.Currency + " are spent between journeys. Therefore, keep them until then.",
            },
            [Voice.Ground] = new[]
            {
                "Counter's cut from ridge rock. Nothing on it yet; supplies come up the trail later.",
                "Spend your " + InWorldText.Currency + " back at camp, between trips. Out here we just swap stories by the fire.",
            },
            [Voice.Sky] = new[]
            {
                "Shelves are bare as a clear sky today. Stock blows in later.",
                "Spend your " + InWorldText.Currency + " between trips, when the wind's at your back.",
            },
        };

        /// <summary>A shop keeper's conversation (two lines, MaxLines) in a voice.</summary>
        public static List<string> KeeperLines(Voice voice) => new List<string>(KeeperLineTable[voice]);

        /// <summary>Lines the finches of the Greenwood* say about their patron, Grandsire Beakwell (one per finch, picked by salt).</summary>
        public static readonly string[] DarwinLines =
        {
            "\"Find thy perch and keep it!\" So saith " + Beakwell + " each morn. Mostly to me.",
            Beakwell + " measured my beak anew. It grew a whole hair! He wrote three pages on it, forsooth.",
            "Only the stoutest beaks crack the stoutest seeds, 'tis said. Mine is not stout. Mine is chatty. Doth that count?",
            "Different beaks for different seeds! Some crack, some sip, and all of us gossip, verily.",
            Beakwell + " saith every finch hath a favourite spot. Mine is this very one. Prithee, stand elsewhere.",
        };

        /// <summary>The season's beak, as finches tell it: [0] thin, [1] thick. True for the run that says it.</summary>
        public static readonly string[] BeakLines =
        {
            "Hark! This season 'tis all thin beaks: long and dainty, to peck far-off buttons and tickle flowers till the vines grow.",
            "Hark! This season 'tis all thick beaks: big and blunt, to crack rocks and pots like seeds.",
        };

        /// <summary>
        /// Lines the mushroom people of the Ember Vale* say about their patron, Mother Glimmercap (one per mushroom, picked
        /// by salt). Subtle science: no real names and no science words, only glowing, stones and changing.
        /// </summary>
        public static readonly string[] CurieLines =
        {
            "Rule one from " + Glimmercap + ": glow responsibly! Rule two: *also* glow responsibly! I love rules!",
            Glimmercap + " says everything changes, given time! I'm changing too! Very, very slowly. Ugh!",
            "When I'm old I'll be all skeleton, with skeleton-key fingers! Every lock on the face, mine! Eek, I can't *wait*!",
            "Full heart, steady glow! " + Glimmercap + " taught us that! Then she wrote it in a notebook that glows! Wow!",
            "Spores? Stones? I prefer mush! " + Glimmercap + " says mush isn't a proper stone. Yet! Sniff.",
        };

        /// <summary>
        /// The lore line about the stones (a mushroom says it every run): they change between visits, in no set order
        /// (each run rolls its stone), never while you hold one.
        /// </summary>
        public const string StoneLoreLine =
            "Oh, the stones here never stay the same between visits! Leave, come back, and yours might glow, go wild " +
            "or turn heavy! Nobody can guess which! Thrilling!";

        /// <summary>
        /// This visit's stone, as the mushrooms tell it, by stage: [0] Glowstone*, [1] Wildstone*, [2] Leadstone*. True for
        /// the run that says it (it names the rolled stone and what it does).
        /// </summary>
        public static readonly string[] StoneLines =
        {
            "Ooh! This visit the stones glow! Find the " + ChemistryIsotope.GlowName + " and swing it at the dark doorways to light them up!",
            "Eek! This visit the stones are wild! Find the " + ChemistryIsotope.UnstableName + " and swing it at the cracked walls! BOOM!",
            "Ugh! This visit the stones are heavy! Find the " + ChemistryIsotope.LeadName + " and swing it at the pressure plates to weigh them down!",
        };

        /// <summary>The line naming a run's stone (see StoneLines).</summary>
        public static string StoneLine(IsotopeStage stage) => StoneLines[stage == IsotopeStage.None ? 0 : (int)stage];

        /// <summary>Lines the alien people of the Weightlands* say about their patron, Old Whenever (one per alien, picked by salt).</summary>
        public static readonly string[] EinsteinLines =
        {
            "Time is a stretchy thing, says " + Whenever + ". Stretch it far enough, and does it snap... or sing, I wonder?",
            Whenever + " says heavy things make time dawdle. So I ponder: am I heavy, or merely sleepy?",
            Whenever + " wrote one tiny rule on the wall. Nobody can read it. Perhaps that is the rule?",
            "Stand by a big boulder and time slows down. I've been twenty for six hundred years. Or have I? Ponder that.",
            Whenever + "'s hair? So big it bends the light. And the comb. Where do combs go, I wonder?",
        };

        /// <summary>How the timed doors work, as the aliens tell it (true for every run: the mechanics).</summary>
        public const string MassMittLine =
            "Ponder the timed doors. Pile boulders by the door with the " + MassMittItem.Name + " (Interact or swing): more " +
            "weight, slower time, longer open. Then step on or hit the switch, and run. Why rush, I wonder?";

        /// <summary>What Sage Conkwell, the apple-bonked sage, says on the Weightlands* (the apple cameo; Newton-inspired, never named).</summary>
        public static readonly string[] NewtonLines =
        {
            "Ow! Another apple. That makes four hundred and twelve. I am beginning to suspect a pattern.",
            "Every apple falls toward my head. Every single one. I shall call it... a grudge.",
            Whenever + " says the apple is just taking its time. My forehead disagrees.",
        };

        /// <summary>
        /// Lines the shape people of Bridgelands* say about their patron, Madam Spanwell (one per shape person, picked by
        /// salt). Subtle science: no real names and no science words, only planks, islands, water and going round.
        /// </summary>
        public static readonly string[] BridgekeeperLines =
        {
            Spanwell + " says: if no plank reaches an island, then come at it from another side.",
            "Some planks go somewhere. Some go nowhere. " + Spanwell + " values both, given their different uses.",
            "If you cannot get there from here, then get there from elsewhere. That is " + Spanwell + "'s whole rule.",
            "I walked round three screens to reach an island I could see from the doorway. Therefore, it was worth it.",
            "Round head, pointy body, sturdy legs. " + Spanwell + " says we are built for planks. Hence the legs.",
        };

        /// <summary>
        /// How the island screens work, as the shape people tell it (true for every run: the mechanics, 4.12): every plank
        /// lights when crossed, a lit one crossed again puts them all out, all lit frees the chest, and the parity hint.
        /// </summary>
        public const string IslandLine =
            "On the islands, a plank lights when crossed. If you cross a lit one again, then every lamp goes out. " +
            "Light them all to free the chest. Count each island's planks: odd ones make good beginnings.";

        /// <summary>How the gap-crosser* works, as the shape people tell it (true for every run: the mechanics).</summary>
        public const string GapCrosserLine =
            "If a gap or a holed plank is in your way, then swing the " + GapCrosserItem.Name + " at it. It hops you over the gap and builds nothing. Therefore, one swing, one hop.";

        /// <summary>
        /// Lines the ground dinosaurs of the Wildlands* say about their patron, Granny Trowel (one per dinosaur, picked by
        /// salt). Subtle science: no real names and no science words, only digging, bones, rocks and rumbles.
        /// </summary>
        public static readonly string[] OldDiggerLines =
        {
            Trowel + " says every rock has a story. Most of them are about being a rock. Riveting stuff.",
            "When the ground rumbles, " + Trowel + " grabs her brush. Crevices open and bones say hello.",
            "Found a bone in a cliff crevice once. Turned out it was mine. Long story. I want it back.",
            Trowel + " digs slow and gentle, for hours. I stomp. We both get there in the end, out in the dirt.",
            "Layers on layers. " + Trowel + " reads the cliffs like a book. A slow, heavy, rocky book.",
        };

        /// <summary>
        /// Lines the avian dinosaurs of the Wildlands* say about their patron, Auntie Cloudeye (one per dinosaur, picked
        /// by salt). Subtle science: no real names and no science words, only clouds, wind, rain and feathers.
        /// </summary>
        public static readonly string[] SkyWatcherLines =
        {
            "Feathers up means rain coming. " + Cloudeye + " taught me that. Feathers down means lunch.",
            Cloudeye + " says the wind is just the sky going somewhere in a hurry. Same, sky. Same.",
            "I flapped once and a breeze came. " + Cloudeye + " says that was a coincidence. I say it was me.",
            "Snow, rain, wind, calm. " + Cloudeye + " has a feather for each. She runs out of feathers by noon.",
            Cloudeye + " says every cloud is a sheep that ran off to be famous. I believe her.",
        };

        /// <summary>
        /// This visit's Stormcaller*, as the dinosaurs tell it, by phenomenon: [0] quake, [1] rain, [2] snow, [3] tornado. True
        /// for the run that says it (it names the rolled wand and what it does).
        /// </summary>
        public static readonly string[] WandLines =
        {
            "This trip the " + StormcallerItem.Name + " calls up a quake. Find it and swing it at the rock pillars to knock 'em flat.",
            "This trip the " + StormcallerItem.Name + " calls up rain. Find it and swing it at the walls of fire to douse 'em.",
            "This trip the " + StormcallerItem.Name + " calls up snow. Find it and swing it at the water to freeze a crossing.",
            "This trip the " + StormcallerItem.Name + " calls up a tornado. Find it out on the trail and swing it at the debris piles to blow 'em away.",
        };

        /// <summary>The wand line when the run's phenomenon is unknown: it names none, so it is never false.</summary>
        public const string NeutralWandLine =
            "Somewhere out on the trail lies the " + StormcallerItem.Name + ". Find it, swing it, and see what the sky has in mind.";

        /// <summary>The line naming a run's Stormcaller* (see WandLines); NeutralWandLine when the phenomenon is unknown.</summary>
        public static string WandLine(Phenomenon kind) =>
            kind >= Phenomenon.Quake && (int)kind < WandLines.Length ? WandLines[(int)kind] : NeutralWandLine;

        /// <summary>How the hazards of the Wildlands* work, as the dinosaurs tell it (true for every run: the mechanics).</summary>
        public const string HazardLine =
            "Freezing rain slows you. On ice you slide till rock, rough snow or a hole stops you; fall in, start again. " +
            "Up high, follow the gusts. Mind the crevices. The middle's calm.";

        /// <summary>How a ride on the wind ends (4.11), as a ground dinosaur tells it: a grip rock stops it.</summary>
        public const string GripLine =
            "Riding the gusts? Aim for the grip rocks out in the sky. Land on one and you stop dead, boots planted, ready to pick your next gust.";

        public static string OldDiggerLine(uint salt) => OldDiggerLines[(int)(salt / 3u % (uint)OldDiggerLines.Length)];

        public static string SkyWatcherLine(uint salt) => SkyWatcherLines[(int)(salt / 3u % (uint)SkyWatcherLines.Length)];

        public static string BridgekeeperLine(uint salt) => BridgekeeperLines[(int)(salt / 3u % (uint)BridgekeeperLines.Length)];

        public static string EinsteinLine(uint salt) => EinsteinLines[(int)(salt / 3u % (uint)EinsteinLines.Length)];

        public static string CurieLine(uint salt) => CurieLines[(int)(salt / 3u % (uint)CurieLines.Length)];

        public static string DarwinLine(uint salt) => DarwinLines[(int)(salt / 3u % (uint)DarwinLines.Length)];

        public static string BeakLine(bool thin) => BeakLines[thin ? 0 : 1];

        /// <summary>
        /// The whole conversation for an NPC in a run: its flavour line, then its first hint (in its voice). A leader (4.5)
        /// gives its greeting and its region's quest hook instead (LeaderConversation).
        /// </summary>
        public static List<string> For(NpcSpec spec, RunFacts facts, HintDensity density, string errandGiver = null) =>
            Lines(spec, spec != null && spec.IsLeader ? null : HintGenerator.For(spec, facts, density), errandGiver);

        /// <summary>
        /// The conversation from given hints: the flavour line (the role line), then the first hint. A leader (4.5) always gives
        /// LeaderConversation (its greeting, then its quest hook), whatever the hints: the one source of a leader's lines.
        /// </summary>
        public static List<string> Lines(NpcSpec spec, IReadOnlyList<Hint> hints, string errandGiver = null) =>
            spec.IsLeader ? LeaderConversation(spec.Race) : Conversation(FlavourLine(spec, errandGiver), hints);

        /// <summary>A generic NPC's flavour line (its first line): a leader's greeting (LeaderConversation's first), else its role line.</summary>
        public static string FlavourLine(NpcSpec spec, string errandGiver = null) =>
            spec.IsLeader ? LeaderConversation(spec.Race)[0] : RoleLine(spec, errandGiver);

        /// <summary>A conversation: the flavour line, then the first hint (if any). Never more than MaxLines.</summary>
        public static List<string> Conversation(string flavour, IReadOnlyList<Hint> hints)
        {
            var lines = new List<string>(MaxLines) { flavour };
            if (hints != null && hints.Count > 0) lines.Add(hints[0].Text);
            return lines;
        }

        public static string Greeting(NpcSpec spec)
        {
            string[] table = Flavours[spec.Voice].Greetings;
            return table[(int)(spec.Salt % (uint)table.Length)];
        }

        /// <summary>
        /// A generic NPC's role line in its voice. A quest-giver says its voice's SpareQuest line (no errand from them, but
        /// errandGiver, the face's errand-giver, has one; then gossip), or SpareQuestPlain with no errand-giver known: each
        /// face has one errand (4.18, Errands), and only its errand-giver says it (ErrandLine).
        /// </summary>
        public static string RoleLine(NpcSpec spec, string errandGiver = null)
        {
            RaceFlavour flavour = Flavours[spec.Voice];
            if (spec.Role == NpcRole.QuestGiver)
                return errandGiver != null ? Fill(flavour.SpareQuest, flavour, spec.Salt, errandGiver) : Fill(flavour.SpareQuestPlain, flavour, spec.Salt);
            string[] table = flavour.Roles[spec.Role];
            string template = table[(int)(spec.Salt / 5u % (uint)table.Length)];
            return Fill(template, flavour, spec.Salt);
        }

        /// <summary>
        /// The errand a face's one errand-giver (4.18, Errands) gives in its voice: one of its voice's quest-giver lines, with
        /// the errand naming targetName (another resident of the town), picked by its salt.
        /// </summary>
        public static string ErrandLine(NpcSpec giver, string targetName)
        {
            RaceFlavour flavour = Flavours[giver.Voice];
            string[] table = flavour.Roles[NpcRole.QuestGiver];
            string template = table[(int)(giver.Salt / 5u % (uint)table.Length)];
            return Fill(template, flavour, giver.Salt, targetName);
        }

        /// <summary>The name the text scans fill an errand with (the first of the voice's pool, so the voice tests strip it).</summary>
        public static string SampleName(Voice voice) => NpcNames.Pool(voice)[0];

        private static string Fill(string template, RaceFlavour flavour, uint salt, string name = null)
        {
            string wares = flavour.Wares;
            string capital = wares.Length > 0 ? char.ToUpperInvariant(wares[0]) + wares.Substring(1) : wares;
            string rumour = flavour.Rumours[(int)(salt / 13u % (uint)flavour.Rumours.Length)];
            // A rumour after a full stop, a question or a shout starts a sentence of its own.
            int at = template.IndexOf("{rumour}", System.StringComparison.Ordinal);
            string before = at > 0 ? template.Substring(0, at).TrimEnd() : "";
            if (before.Length > 0 && ".?!".IndexOf(before[before.Length - 1]) >= 0 && rumour.Length > 0)
                rumour = char.ToUpperInvariant(rumour[0]) + rumour.Substring(1);
            return template
                .Replace("{Wares}", capital)
                .Replace("{wares}", wares)
                .Replace("{metal}", flavour.Metal)
                .Replace("{errand}", flavour.Errand)
                .Replace("{rumour}", rumour)
                .Replace("{name}", name ?? "someone in town");
        }

        /// <summary>
        /// Every line a voice can say as a whole line, filled in (each role template with every rumour), plus its greetings
        /// and, for a race's main voice, its leader line: what the voice tests check.
        /// </summary>
        public static IEnumerable<string> SpokenLines(Voice voice)
        {
            RaceFlavour f = Flavours[voice];
            foreach (string[] table in f.Roles.Values)
                foreach (string template in table)
                    for (int r = 0; r < f.Rumours.Length; r++)
                        yield return Fill(template, f, (uint)(r * 13), SampleName(voice));
            for (int r = 0; r < f.Rumours.Length; r++)
            {
                yield return Fill(f.SpareQuest, f, (uint)(r * 13), SampleName(voice));
                yield return Fill(f.SpareQuestPlain, f, (uint)(r * 13));
            }
            foreach (string line in f.Greetings) yield return line;
            foreach (string line in QuestLines.SpokenLines(voice)) yield return line; // 4.18: errands and Town's trail
            foreach (string line in KeeperLineTable[voice]) yield return line;
            foreach (string line in LairText.SpokenLines(voice)) yield return line; // 4.13: the lair's prisoners
            if (voice == Voice.Town)
                foreach (string line in BossTips.AllLines()) yield return line; // 4.25: Captain Shuffles's tips on the Demon
        }

        /// <summary>Every fixed line and filler, for the tone check.</summary>
        public static IEnumerable<string> AllFixedText()
        {
            foreach (RaceFlavour f in Flavours.Values)
            {
                foreach (string[] table in f.Roles.Values)
                    foreach (string line in table) yield return line;
                yield return f.Wares;
                yield return f.Metal;
                yield return f.Errand;
                yield return f.SpareQuest;
                yield return f.SpareQuestPlain;
                foreach (string line in f.Rumours) yield return line;
                foreach (string line in f.Greetings) yield return line;
            }
            foreach (Voice voice in Voices.All)
                foreach (string line in SpokenLines(voice)) yield return line;
            foreach (string line in LeaderLines.Values) yield return line;
            foreach (Race race in RaceExtensions.All)
                foreach (string line in LeaderQuestLines(race)) yield return line;
            foreach (string[] keeper in KeeperLineTable.Values)
                foreach (string line in keeper) yield return line;
            foreach (string line in DarwinLines) yield return line;
            foreach (string line in BeakLines) yield return line;
            foreach (string line in CurieLines) yield return line;
            yield return StoneLoreLine;
            foreach (string line in StoneLines) yield return line;
            foreach (string line in EinsteinLines) yield return line;
            yield return MassMittLine;
            foreach (string line in NewtonLines) yield return line;
            foreach (string line in BridgekeeperLines) yield return line;
            yield return IslandLine;
            yield return GapCrosserLine;
            foreach (string line in OldDiggerLines) yield return line;
            foreach (string line in SkyWatcherLines) yield return line;
            foreach (string line in WandLines) yield return line;
            yield return NeutralWandLine;
            yield return HazardLine;
            yield return GripLine;
            foreach (string line in LairText.AllFixedText()) yield return line;
            foreach (string line in QuestLines.AllFixedText()) yield return line;
        }
    }
}
