using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// What people say about the quests 4.18 adds, in every voice (voices-and-names.md; the voice, length and text scans read
    /// them through Dialogue.SpokenLines and AllFixedText):
    /// <list type="bullet">
    /// <item>a face's errand (Errands): the target's reply when the word arrives ({name}: the giver) and the giver's thanks;</item>
    /// <item>the Mayor's tour of Town (4.20, TownTourText: it replaced 4.18's lost-and-found trail): his question and answers,
    /// each stop's line and the tour guide's.</item>
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

        // ------------------------------------------------------------------------------------------ the Mayor

        /// <summary>
        /// The Mayor's lines about Town's fragment (4.20; 4.18's lost-and-found trail and its townsfolk's lines are retired): the
        /// tour's question and answers (TownTourText), in the Town voice.
        /// </summary>
        public static IEnumerable<string> MayorLines()
        {
            yield return TownTourText.Offer;
            yield return TownTourText.Start(SampleWhere);
            yield return TownTourText.Declined;
            yield return TownTourText.Reminder(SampleWhere);
            yield return TownTourText.SkipAsk;
            yield return TownTourText.Skipped;
            yield return TownTourText.KeepGoing(SampleWhere);
        }

        // ------------------------------------------------------------------------------------------ where

        /// <summary>
        /// Where a Town person stands, worded the same in every voice: "in the town square" (the start screen) or "on Town's
        /// north-east screen" (HintGenerator.ScreenPhrase).
        /// </summary>
        public static string Where(ScreenAddress screen, int faceSize) =>
            TownSquare.IsTown(screen, faceSize) || screen.Cell == CubeModel.StartCell(faceSize)
                ? "in the town square"
                : $"on Town's {HintGenerator.ScreenPhrase(screen, faceSize)} screen";

        // ------------------------------------------------------------------------------------------ scans

        /// <summary>The longest {where} a Town line can say (the scans fill templates with it).</summary>
        public const string SampleWhere = "on Town's middle north screen";

        /// <summary>Every line a voice can say about these quests, filled with a sample name from its pool and the longest where.</summary>
        public static IEnumerable<string> SpokenLines(Voice voice)
        {
            string name = Dialogue.SampleName(voice);
            yield return DeliveredLine(voice, name);
            yield return ThanksLine(voice);
            if (voice == Voice.Town)
                foreach (string line in TownTourText.AllLines()) yield return line;
        }

        /// <summary>Every template and filled line (the tone and text scans).</summary>
        public static IEnumerable<string> AllFixedText()
        {
            foreach (var table in new[] { Delivered, Thanks })
                foreach (string line in table.Values) yield return line;
            yield return TownTourText.StartTemplate;
            yield return TownTourText.ReminderTemplate;
            yield return TownTourText.KeepGoingTemplate;
            foreach (Voice voice in Voices.All)
                foreach (string line in SpokenLines(voice)) yield return line;
        }
    }
}
