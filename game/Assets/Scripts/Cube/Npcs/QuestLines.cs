using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// What people say about the quests 4.18 adds, in every voice (voices-and-names.md; the voice, length and text scans read
    /// them through Dialogue.SpokenLines and AllFixedText):
    /// <list type="bullet">
    /// <item>a face's errand (Errands): the target's reply when the word arrives ({name}: the giver) and the giver's thanks;</item>
    /// <item>Town's lost-and-found trail (TownTrail): a trail person points to the next ({name}, {where}), the favour-asker asks
    /// a favour of the helper ({name}, {where}), the helper answers ({name}: the asker), the asker thanks the player, and the
    /// last person hands the fragment over;</item>
    /// <item>the Mayor: the fragment went missing, who had it last, and a reminder of who to ask next.</item>
    /// </list>
    /// The {where} phrase (Where) is worded the same in every voice, so it is always true.
    /// </summary>
    public static class QuestLines
    {
        // ------------------------------------------------------------------------------------------ errands

        /// <summary>What the errand's target says when the word arrives ({name}: the giver).</summary>
        public static readonly IReadOnlyDictionary<Voice, string> Delivered = new Dictionary<Voice, string>
        {
            [Voice.Town] = "A hello from {name}? Well, hello right back! Tell them I said so. Actually, I'll pop round myself.",
            [Voice.Finch] = "Greetings from {name}? How courteous! Prithee, tell them I return them twofold.",
            [Voice.Mushroom] = "{name} said that? Oh, I'm so *happy* I could fizz! Tell them I said hi back!",
            [Voice.Alien] = "Word from {name}? How curious. I had wondered when it would arrive. Thank you.",
            [Voice.Shape] = "{name} sent word. If it is correct, then I am glad. It is correct. Thank you.",
            [Voice.Ground] = "Word from {name}, all the way out here? Good to hear. Tell 'em I'll see 'em at camp.",
            [Voice.Sky] = "A message from {name}, on the breeze? Lovely. I'll send one back on the wind.",
        };

        /// <summary>What the errand's giver says once the word has arrived.</summary>
        public static readonly IReadOnlyDictionary<Voice, string> Thanks = new Dictionary<Voice, string>
        {
            [Voice.Town] = "You said hello for me! You're one of us now. Gossip's on the house, forever.",
            [Voice.Finch] = "Thou hast delivered my greetings! Verily, thou art a most courteous traveller.",
            [Voice.Mushroom] = "You did it! Oh, I could cry! I *am* crying! Happy tears! Thank you!",
            [Voice.Alien] = "My word arrived. Or did it always arrive, and we merely caught up with it, I wonder? Thank you.",
            [Voice.Shape] = "The word was delivered. Therefore, the task is complete. Thank you.",
            [Voice.Ground] = "Word got through. Good scouting. You'd do fine out on the trail.",
            [Voice.Sky] = "You carried it like a breeze carries a feather. Thanks, sky-friend.",
        };

        public static string DeliveredLine(Voice voice, string giver) => Delivered[voice].Replace("{name}", giver);

        public static string ThanksLine(Voice voice) => Thanks[voice];

        // ------------------------------------------------------------------------------------------ Town's trail

        /// <summary>A trail person points to the next ({name}, {where}).</summary>
        public static readonly IReadOnlyDictionary<Voice, string> Pointer = new Dictionary<Voice, string>
        {
            [Voice.Town] = "The mayor's fragment? I had it, but I passed it to {name}, {where}. Lovely chat, that was.",
            [Voice.Finch] = "The mayor's fragment? Verily, I passed it to {name}, {where}. Hasten thee there.",
            [Voice.Mushroom] = "The fragment? Oh, I gave it to {name}, {where}! I'm *so* sorry! Go, go!",
            [Voice.Alien] = "The fragment passed through my hands, as all things pass. It went to {name}, {where}, I suppose.",
            [Voice.Shape] = "The fragment was here. Then I gave it to {name}, {where}. Therefore, it is there now.",
            [Voice.Ground] = "That fragment? Traded it to {name} round the campfire. Find 'em {where}.",
            [Voice.Sky] = "The fragment drifted my way, then I passed it to {name}, {where}. Like a feather on the wind.",
        };

        /// <summary>The favour-asker asks a favour of the helper ({name}, {where}) before telling.</summary>
        public static readonly IReadOnlyDictionary<Voice, string> FavourAsk = new Dictionary<Voice, string>
        {
            [Voice.Town] = "I know where it went! But first, a favour: tell {name}, {where}, I'm sorry about the hedge. Then come back and I'll spill.",
            [Voice.Finch] = "I know whither it went. But first, prithee, carry my apology to {name}, {where}. Return, and I shall tell thee.",
            [Voice.Mushroom] = "I know where it is! But first, tell {name}, {where}, I'm sorry I shouted! Ugh, I *always* shout! Then come back!",
            [Voice.Alien] = "I know where it went. But first, ask {name}, {where}, whether yesterday is finished. Then come back to me.",
            [Voice.Shape] = "I know where it went. First, given fairness, a favour: ask {name}, {where}, about my lost angle. Then return.",
            [Voice.Ground] = "Know where it went. But first, tell {name}, {where}, I'm sorry I borrowed their boots. Then come back to camp.",
            [Voice.Sky] = "I know where it drifted. But first, tell {name}, {where}, I'm sorry about the rain on their washing. Then come back.",
        };

        /// <summary>The helper answers the favour ({name}: the asker).</summary>
        public static readonly IReadOnlyDictionary<Voice, string> HelperReply = new Dictionary<Voice, string>
        {
            [Voice.Town] = "{name} says sorry? Oh, bless them. Tell them all's forgiven, and the hedge grew back anyway.",
            [Voice.Finch] = "{name} sendeth an apology? Then tell them all is forgiven, verily.",
            [Voice.Mushroom] = "{name} said sorry? Oh! I'm *so* happy! Tell them I forgive them! I'm crying!",
            [Voice.Alien] = "Is yesterday finished? Tell {name}: perhaps, but it keeps coming back.",
            [Voice.Shape] = "The angle? Given that I found it, tell {name} it is a right one. Thus, all is well.",
            [Voice.Ground] = "Sorry about my boots, eh? Tell {name} no hard feelings. Mud washes off.",
            [Voice.Sky] = "An apology for the rain? Tell {name} it's fine. My washing likes a good cloud.",
        };

        /// <summary>The favour-asker thanks the player, then (the next line) points on or hands the fragment over.</summary>
        public static readonly IReadOnlyDictionary<Voice, string> FavourThanks = new Dictionary<Voice, string>
        {
            [Voice.Town] = "All forgiven? What a relief! Right then, as promised...",
            [Voice.Finch] = "Thou hast mended my quarrel! Most kind. As I promised, then...",
            [Voice.Mushroom] = "Forgiven? Oh, I'm *so* relieved I could pop! As promised...",
            [Voice.Alien] = "So yesterday is never finished. I suspected. Perhaps that is a comfort. As promised...",
            [Voice.Shape] = "Good. The angle is right. Therefore, as agreed...",
            [Voice.Ground] = "No hard feelings? Good. Rock-solid of you. As promised...",
            [Voice.Sky] = "All clear between us, like a blue sky. Thanks. As promised...",
        };

        /// <summary>The last trail person hands the mayor's fragment over.</summary>
        public static readonly IReadOnlyDictionary<Voice, string> Give = new Dictionary<Voice, string>
        {
            [Voice.Town] = "Oh, the mayor's fragment! I was keeping it safe. Here you go: take it back to " + Mayor + " with my love!",
            [Voice.Finch] = "Verily, here is the mayor's fragment. I did guard it well. Bear it back to " + Mayor + ", prithee.",
            [Voice.Mushroom] = "Oh, the fragment! Here! I was *so* worried I'd lose it! Take it back to " + Mayor + ", quick!",
            [Voice.Alien] = "Here is the fragment. Was I keeping it, or was it keeping me, I wonder? Take it to " + Mayor + ".",
            [Voice.Shape] = "Here is the fragment. If you take it to " + Mayor + ", then Town's temple opens. Therefore, go.",
            [Voice.Ground] = "Here's the fragment. Kept it safe as a rock. Haul it back to " + Mayor + ".",
            [Voice.Sky] = "Here's the fragment, safe and dry from the rain. Float it back to " + Mayor + ".",
        };

        private const string Mayor = "Mayor Muddleton";

        public static string PointerLine(Voice voice, string next, string where) => Fill(Pointer[voice], next, where);

        public static string FavourAskLine(Voice voice, string helper, string where) => Fill(FavourAsk[voice], helper, where);

        public static string HelperReplyLine(Voice voice, string asker) => Fill(HelperReply[voice], asker, null);

        public static string FavourThanksLine(Voice voice) => FavourThanks[voice];

        public static string GiveLine(Voice voice) => Give[voice];

        // ------------------------------------------------------------------------------------------ the Mayor

        /// <summary>The Mayor's quest in a run (4.18): the fragment went missing, and who had it last ({name}, {where}).</summary>
        public const string MayorLost =
            "Every face is coming apart, friend, and Town's one fragment has gone missing! {name} had it last, {where}. Find it, bring it back, and we'll mend Town!";

        /// <summary>The Mayor while the trail is on: who to ask next ({name}, {where}).</summary>
        public const string MayorReminder =
            "Still hunting Town's fragment? Ask {name}, {where}. Then bring it back here and we'll mend Town!";

        public static string MayorLostLine(string name, string where) => Fill(MayorLost, name, where);

        public static string MayorReminderLine(string name, string where) => Fill(MayorReminder, name, where);

        // ------------------------------------------------------------------------------------------ where

        /// <summary>
        /// Where a Town person stands, worded the same in every voice: "in the town square" (the start screen) or "on Town's
        /// north-east screen" (HintGenerator.ScreenPhrase).
        /// </summary>
        public static string Where(ScreenAddress screen, int faceSize) =>
            TownSquare.IsTown(screen, faceSize) || screen.Cell == CubeModel.StartCell(faceSize)
                ? "in the town square"
                : $"on Town's {HintGenerator.ScreenPhrase(screen, faceSize)} screen";

        private static string Fill(string template, string name, string where) =>
            template.Replace("{name}", name ?? "someone").Replace("{where}", where ?? "in the town square");

        // ------------------------------------------------------------------------------------------ scans

        /// <summary>The longest {where} the trail can say (the scans fill templates with it).</summary>
        public const string SampleWhere = "on Town's middle north screen";

        /// <summary>Every line a voice can say about these quests, filled with a sample name from its pool and the longest where.</summary>
        public static IEnumerable<string> SpokenLines(Voice voice)
        {
            string name = Dialogue.SampleName(voice);
            yield return DeliveredLine(voice, name);
            yield return ThanksLine(voice);
            yield return PointerLine(voice, name, SampleWhere);
            yield return FavourAskLine(voice, name, SampleWhere);
            yield return HelperReplyLine(voice, name);
            yield return FavourThanksLine(voice);
            yield return GiveLine(voice);
            if (voice == Voice.Town)
                foreach (string line in MayorLines()) yield return line;
        }

        /// <summary>The Mayor's quest lines, filled with a sample name and the longest where.</summary>
        public static IEnumerable<string> MayorLines()
        {
            string name = Dialogue.SampleName(Voice.Town);
            yield return MayorLostLine(name, SampleWhere);
            yield return MayorReminderLine(name, SampleWhere);
        }

        /// <summary>Every template and filled line (the tone and text scans).</summary>
        public static IEnumerable<string> AllFixedText()
        {
            foreach (var table in new[] { Delivered, Thanks, Pointer, FavourAsk, HelperReply, FavourThanks, Give })
                foreach (string line in table.Values) yield return line;
            yield return MayorLost;
            yield return MayorReminder;
            foreach (Voice voice in Voices.All)
                foreach (string line in SpokenLines(voice)) yield return line;
        }
    }
}
