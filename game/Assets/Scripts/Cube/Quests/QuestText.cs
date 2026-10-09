using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The quest log's own words (4.18): titles, steps and progress for every kind of quest, plus the log's labels. Plain and
    /// short, under the text rule (InWorldText: region names keep their "*", characters go by their whole chosen name).
    /// </summary>
    public static class QuestText
    {
        public const string LogTitle = "Quest log";
        public const string CloseHint = "[Q] / [Esc] close";
        public const string MapHint = "[Q] quest log";
        public const string Empty = "No quests yet. Talk to people: each town's leader has one, and somebody always needs a hand.";
        public const string DoneLabel = "Done";
        public const string NewQuestPopup = "New quest";
        public const string QuestDonePopup = "Quest done";

        // ---------- leaders (science faces)

        public static string LeaderTitle(Theme theme) => $"Mend {InWorldText.Region(theme)}";

        public static string LeaderStep(Theme theme, string leader, int count, int found) => found >= count
            ? $"You hold all {count} fragments of {InWorldText.Region(theme)}. Take them to {leader}."
            : $"Find the {count} fragments of {InWorldText.Region(theme)} and bring them all to {leader}.";

        public static string FragmentProgress(int found, int count) => $"{found} of {count} fragments found";

        public static string LeaderDone(Theme theme, string leader) => $"{InWorldText.Region(theme)} is mended. {leader} thanks you.";

        // ---------- Town's lost fragment (TownTrail)

        public const string TownTitle = "Town's lost fragment";

        public static string TownAsk(string name, string where) => $"Town's fragment went missing. Ask {name} {where}.";

        public static string TownFavour(string asker, string helper, string where) => $"{asker} wants a word passed to {helper} {where} first.";

        public static string TownFavourBack(string asker, string where) => $"Take the answer back to {asker} {where}.";

        public const string TownReturn = "You have Town's fragment. Take it back to Mayor Muddleton in the town square.";

        public const string TownDone = "Town is mended. Mayor Muddleton is delighted.";

        // ---------- errands (Errands)

        public static string ErrandTitle(string giver) => $"An errand for {giver}";

        /// <summary>Where an errand's target lives: Town's square, or a science face's town.</summary>
        public static string TownOf(Theme theme) => theme == Theme.Town ? "Town's square" : $"the town of {InWorldText.Region(theme)}";

        public static string ErrandStep(string giver, string target, Theme theme) => $"Carry {giver}'s word to {target} in {TownOf(theme)}.";

        public static string ErrandDone(string target) => $"Delivered to {target}.";

        // ---------- popups

        public static string Added(string title) => $"{NewQuestPopup}: {title}  [Q]";

        public static string Completed(string title) => $"{QuestDonePopup}: {title}";

        /// <summary>Every label and a sample of every title, step and popup, for the text scans.</summary>
        public static IEnumerable<string> AllTexts()
        {
            yield return LogTitle;
            yield return CloseHint;
            yield return MapHint;
            yield return Empty;
            yield return DoneLabel;
            const string leader = "Queen Wrennifer", person = "Pipwick", other = "Wrenna";
            foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
            {
                yield return LeaderTitle(theme);
                yield return LeaderStep(theme, leader, 4, 2);
                yield return LeaderStep(theme, leader, 4, 4);
                yield return LeaderDone(theme, leader);
                yield return ErrandStep(person, other, theme);
                yield return Added(LeaderTitle(theme));
                yield return Completed(LeaderTitle(theme));
            }
            yield return FragmentProgress(2, 5);
            yield return TownTitle;
            yield return TownAsk(person, QuestLines.SampleWhere);
            yield return TownFavour(person, other, QuestLines.SampleWhere);
            yield return TownFavourBack(person, "in the town square");
            yield return TownReturn;
            yield return TownDone;
            yield return ErrandTitle(person);
            yield return ErrandDone(other);
            yield return Added(TownTitle);
            yield return Completed(ErrandTitle(person));
        }
    }
}
