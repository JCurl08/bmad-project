using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Game.Cube
{
    /// <summary>
    /// The one place the subtle-science text rule lives (lore-and-tone.md). Characters never know they stand for a science:
    /// in-world text never names a real scientist and never uses a face theme's science words. Faces go by in-world region
    /// names, and every invented region or item name that the naming brainstorm has not chosen yet carries a trailing "*"
    /// (Town and the Partition are chosen). Characters (4.4) have chosen names, unstarred (NpcNames): every word of a
    /// character's name reads as chosen here. The text sources (Dialogue, HintGenerator, the HUDs, the item
    /// names, the between-runs screen) take their names from here, and the shared scan test checks them all with Problems.
    /// Code identifiers and comments may keep the science names; only what the player reads follows the rule.
    /// </summary>
    public static class InWorldText
    {
        // ---------------------------------------------------------------- names

        /// <summary>The hub face: a chosen name, so no star.</summary>
        public const string Town = "Town";

        /// <summary>The Biology face's region (placeholder).</summary>
        public const string Greenwood = "Greenwood*";

        /// <summary>The Chemistry face's region (placeholder).</summary>
        public const string EmberVale = "Ember Vale*";

        /// <summary>The Physics face's region (placeholder).</summary>
        public const string Weightlands = "Weightlands*";

        /// <summary>The Math face's region (placeholder).</summary>
        public const string Bridgelands = "Bridgelands*";

        /// <summary>The Earth and Atmosphere face's region (placeholder).</summary>
        public const string Wildlands = "Wildlands*";

        /// <summary>
        /// The boss's title: a chosen character name since 4.4, so no star (never "Maxwell"). Its own name is
        /// NpcNames.Demon; patrons, leaders and every other character are named in NpcNames.
        /// </summary>
        public const string Demon = NpcNames.DemonTitle;

        /// <summary>The boss as the text names it: its name and title ("Primsworth the Demon").</summary>
        public const string DemonFull = NpcNames.DemonFull;

        /// <summary>The meta currency (placeholder).</summary>
        public const string Currency = "Jumbles*";

        /// <summary>Proper place and event names that have been chosen (they carry no star). Characters: NpcNames.</summary>
        public static readonly IReadOnlyList<string> ChosenNames = new[] { "Town", "Partition" };

        /// <summary>
        /// A face's region name as it reads mid-sentence: Town; "the Greenwood*", "the Ember Vale*", "the Weightlands*";
        /// "Bridgelands*" and "Wildlands*" without an article.
        /// </summary>
        public static string Region(Theme theme)
        {
            switch (theme)
            {
                case Theme.Town: return Town;
                case Theme.Biology: return "the " + Greenwood;
                case Theme.Chemistry: return "the " + EmberVale;
                case Theme.Physics: return "the " + Weightlands;
                case Theme.Math: return Bridgelands;
                default: return Wildlands;
            }
        }

        /// <summary>Every region name, mid-sentence form (Region of every theme).</summary>
        public static IEnumerable<string> AllRegions()
        {
            foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
                yield return Region(theme);
        }

        /// <summary>Every placeholder name, starred (the regions and the currency; characters are named in NpcNames, unstarred).</summary>
        public static IEnumerable<string> Placeholders()
        {
            yield return Greenwood;
            yield return EmberVale;
            yield return Weightlands;
            yield return Bridgelands;
            yield return Wildlands;
            yield return Currency;
        }

        // ---------------------------------------------------------------- the rule

        /// <summary>Real scientists the faces are inspired by (and a few more): never named in game.</summary>
        public static readonly IReadOnlyList<string> BannedNames = new[]
        {
            "darwin", "curie", "einstein", "euler", "newton", "anning", "richter", "maxwell", "isaac", "galileo", "mendel",
            "lavoisier", "pythagoras", "bohr", "marie", "königsberg", "konigsberg", "koenigsberg",
        };

        /// <summary>
        /// Explicit science vocabulary of the face themes (and the core's), as word starts: "biolog" catches biology and
        /// biologist, "relativ" catches relativity and relatively. A trailing \b makes a whole-word entry.
        /// </summary>
        public static readonly IReadOnlyList<string> BannedWords = new[]
        {
            // The sciences themselves.
            "science", "scientist", "scientific", "biolog", "chemi", "physic", "math", "geolog", "meteorolog", "atmospher",
            @"earth\b",
            // Biology.
            "evolution", "evolv", "adapt", "niche", "natural selection", "species", "fittest", @"gene\b", "genetic", @"dna\b",
            "mutation", "organism",
            // Chemistry.
            "isotope", "radioactiv", "radiation", "radium", "polonium", "uranium", "decay", "half-life", "half life", "element",
            "atom", "nuclear", "neutron", "proton", "electron", "molecul", @"lab\b", "laborator",
            // Physics.
            "relativ", "gravit", "quantum", @"mass\b", "mc squared", "space-time", "spacetime", "photon", "lensing", "velocity",
            "momentum", "thermodynamic", "particle", "diffus", // ("entropy" is allowed since 1.24: the user's call)
            // Math (and since 1.25 the old bridge puzzle's wording: the island screens never echo it).
            "graph", "vertex", "vertices", @"edge", "theorem", "geometr", "polygon", "algebra", "calculus", "equation",
            "exactly once", "seven bridges",
            // Earth and atmosphere.
            "weather", "seismic", "tectonic", "climate", "paleontolog", "palaeontolog",
        };

        /// <summary>The word the game never says (lore-and-tone.md): the Demon's order is shown, never named.</summary>
        public const string ForbiddenWord = "sort";

        /// <summary>
        /// Capitalised words that are not names (sentence-style UI labels, a short list of shouted interjections, the
        /// pronoun I), so the placeholder rule
        /// skips them. Everything else capitalised mid-sentence is read as a proper name.
        /// </summary>
        public static readonly HashSet<string> NotNames = new HashSet<string>
        {
            "I", "I'm", "I'll", "I've", "I'd", "RAWR", "SKRAW", "VICTORY", "MAXED", "Lv", "Victories", "Buy", "Interact", "Esc",
            "Enter", "Tab", "M",
            // Shouted interjections (4.4): the only capitals-only words allowed mid-sentence. Other emphasis uses *stars*.
            "CLANG", "BOOM",
        };

        private static readonly List<Regex> BannedPatterns = Build();

        private static List<Regex> Build()
        {
            var list = new List<Regex>();
            foreach (string name in BannedNames) list.Add(new Regex(@"\b" + name, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            foreach (string word in BannedWords) list.Add(new Regex(@"\b" + word, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            return list;
        }

        /// <summary>The banned names and words a text uses (empty when it keeps the rule).</summary>
        public static List<string> BannedIn(string text)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(text)) return found;
            foreach (Regex pattern in BannedPatterns)
            {
                Match m = pattern.Match(text);
                if (m.Success) found.Add(m.Value);
            }
            if (text.ToLowerInvariant().Contains(ForbiddenWord)) found.Add(ForbiddenWord);
            return found;
        }

        /// <summary>Characters that end a sentence or a label part (a capital after one starts a new sentence).</summary>
        private const string SentenceEnds = ".!?:;↔|(";

        /// <summary>
        /// Proper names in a text that lack their placeholder star: a capitalised word that does not start a sentence is a
        /// name (unless it is a chosen place name or in NotNames); a name runs on over capitalised words ("Ember Vale*") and
        /// its last word carries the star. Character names (4.4, NpcNames) are chosen, but only as whole names: a word is
        /// skipped only where the full name phrase stands ("Queen Wrennifer", "Primsworth the Demon"), with the numeral a
        /// repeated name may carry ("Pipwick II"); a lone "Queen" or "Old" is still a name that needs its star. Placeholders
        /// that appear lower-case (the greenwood*) are checked by MissingKnownStars.
        /// </summary>
        public static List<string> UnstarredNames(string text)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(text)) return found;
            string[] words = text.Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            string[] cores = new string[words.Length];
            for (int i = 0; i < words.Length; i++) cores[i] = Core(words[i]);
            bool[] named = CharacterNameWords(cores);
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].TrimStart('.', '"', '\'', '(', '+', '*', '[', '-');
                if (word.Length == 0 || !char.IsUpper(word[0])) continue;
                string prev = i > 0 ? words[i - 1].TrimEnd('\'', '"', '*', ')', ']') : "";
                bool sentenceStart = i == 0 || prev.Length == 0 || SentenceEnds.IndexOf(prev[prev.Length - 1]) >= 0 ||
                                     words[i].StartsWith("(");
                string core = cores[i];
                if (sentenceStart || named[i] || NotNames.Contains(core) || IsChosen(core)) continue;
                int last = i;
                while (!words[last].Contains("*") && last + 1 < words.Length && words[last + 1].Length > 0 &&
                       char.IsUpper(words[last + 1][0]) && !named[last + 1])
                    last++;
                if (!words[last].Contains("*")) found.Add(core);
                i = last;
            }
            found.AddRange(MissingKnownStars(text));
            return found;
        }

        /// <summary>A word's letters (apostrophes and hyphens kept), without leading punctuation or a trailing "'s".</summary>
        private static string Core(string word)
        {
            word = word.TrimStart('.', '"', '\'', '(', '+', '*', '[', '-');
            int end = 0;
            while (end < word.Length && (char.IsLetter(word[end]) || word[end] == '\'' || word[end] == '-')) end++;
            string core = word.Substring(0, end);
            if (core.EndsWith("'s")) core = core.Substring(0, core.Length - 2);
            return core.TrimEnd('\'');
        }

        /// <summary>Which words belong to a whole character name (NpcNames.Phrases), with a numeral right after it.</summary>
        private static bool[] CharacterNameWords(string[] cores)
        {
            var named = new bool[cores.Length];
            for (int i = 0; i < cores.Length; i++)
            {
                foreach (string[] phrase in NpcNames.Phrases())
                {
                    if (i + phrase.Length > cores.Length) continue;
                    bool match = true;
                    for (int k = 0; k < phrase.Length && match; k++) match = cores[i + k] == phrase[k];
                    if (!match) continue;
                    for (int k = 0; k < phrase.Length; k++) named[i + k] = true;
                    int after = i + phrase.Length;
                    if (after < cores.Length && NpcNames.IsNumeral(cores[after])) named[after] = true;
                    break;
                }
            }
            return named;
        }

        private static bool IsChosen(string core)
        {
            foreach (string name in ChosenNames)
                if (core == name) return true;
            return false;
        }

        /// <summary>Known placeholder names (and every star-carrying item name passed in) that appear without their star.</summary>
        public static List<string> MissingKnownStars(string text, IEnumerable<string> extraNames = null)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(text)) return found;
            // Known placeholders are matched in any case ("the demon" is still the Demon*); other names (items, upgrades)
            // only as written, so a common noun ("a beak") is not mistaken for the name ("Beak*").
            var names = new List<(string Name, System.StringComparison Comparison)>();
            foreach (string name in Placeholders()) names.Add((name, System.StringComparison.OrdinalIgnoreCase));
            if (extraNames != null)
                foreach (string name in extraNames) names.Add((name, System.StringComparison.Ordinal));
            foreach ((string name, System.StringComparison comparison) in names)
            {
                if (!name.EndsWith("*")) continue;
                string bare = name.TrimEnd('*');
                int at = text.IndexOf(bare, comparison);
                while (at >= 0)
                {
                    int after = at + bare.Length;
                    bool wordEnd = after >= text.Length || !char.IsLetter(text[after]);
                    bool wordStart = at == 0 || !char.IsLetter(text[at - 1]);
                    if (wordStart && wordEnd && (after >= text.Length || text[after] != '*')) found.Add(bare);
                    at = text.IndexOf(bare, after, comparison);
                }
            }
            return found;
        }

        /// <summary>Everything wrong with one player-visible string under the rule (empty when it is fine).</summary>
        public static List<string> Problems(string text, IEnumerable<string> starredNames = null)
        {
            var problems = new List<string>();
            foreach (string banned in BannedIn(text)) problems.Add($"uses \"{banned}\"");
            foreach (string name in UnstarredNames(text)) problems.Add($"names \"{name}\" without its star");
            if (starredNames != null)
                foreach (string name in MissingKnownStars(text, starredNames))
                    if (!problems.Contains($"names \"{name}\" without its star")) problems.Add($"names \"{name}\" without its star");
            return problems;
        }
    }
}
