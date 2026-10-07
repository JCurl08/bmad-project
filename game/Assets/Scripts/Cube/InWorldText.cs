using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Game.Cube
{
    /// <summary>
    /// The one place the subtle-science text rule lives (lore-and-tone.md). Characters never know they stand for a science:
    /// in-world text never names a real scientist and never uses a face theme's science words. Faces go by in-world region
    /// names, patrons by in-world titles, and every invented name that the naming brainstorm has not chosen yet carries a
    /// trailing "*" (only Town and the Partition are chosen). The text sources (Dialogue, HintGenerator, the HUDs, the item
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

        /// <summary>The boss (placeholder; never "Maxwell").</summary>
        public const string Demon = "Demon*";

        /// <summary>The finches' patron (placeholder; Darwin-inspired, never named).</summary>
        public const string OldFledgling = "Old Fledgling*";

        /// <summary>The mushroom people's patron (placeholder; Curie-inspired, never named).</summary>
        public const string Glowmother = "Glowmother*";

        /// <summary>The alien people's patron (placeholder; Einstein-inspired, never named).</summary>
        public const string Timekeeper = "Timekeeper*";

        /// <summary>The shape people's patron (placeholder; Euler-inspired, never named).</summary>
        public const string Bridgekeeper = "Bridgekeeper*";

        /// <summary>The terrestrial dinosaurs' patron (placeholder; Anning-inspired, never named).</summary>
        public const string OldDigger = "Old Digger*";

        /// <summary>The avian dinosaurs' patron (placeholder; Richter- and weather-inspired, never named).</summary>
        public const string SkyWatcher = "Sky-Watcher*";

        /// <summary>An old mushroom of the Ember Vale*, half a skeleton already (placeholder).</summary>
        public const string Marcel = "Marcel*";

        /// <summary>The apple cameo on the Weightlands* (placeholder; Newton-inspired, never named).</summary>
        public const string AppleSage = "apple-bonked sage*";

        /// <summary>The meta currency (placeholder).</summary>
        public const string Currency = "Jumbles*";

        /// <summary>Proper names that have been chosen (they carry no star).</summary>
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

        /// <summary>Every placeholder name, starred (regions, patrons, the Demon*, the currency).</summary>
        public static IEnumerable<string> Placeholders()
        {
            yield return Greenwood;
            yield return EmberVale;
            yield return Weightlands;
            yield return Bridgelands;
            yield return Wildlands;
            yield return Demon;
            yield return OldFledgling;
            yield return Glowmother;
            yield return Timekeeper;
            yield return Bridgekeeper;
            yield return OldDigger;
            yield return SkyWatcher;
            yield return AppleSage;
            yield return Marcel;
            yield return Currency;
        }

        // ---------------------------------------------------------------- the rule

        /// <summary>Real scientists the faces are inspired by (and a few more): never named in game.</summary>
        public static readonly IReadOnlyList<string> BannedNames = new[]
        {
            "darwin", "curie", "einstein", "euler", "newton", "anning", "richter", "maxwell", "isaac", "galileo", "mendel",
            "lavoisier", "pythagoras", "bohr", "marie",
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
            "momentum", "thermodynamic", "entropy", "particle", "diffus",
            // Math.
            "graph", "vertex", "vertices", @"edge", "theorem", "geometr", "polygon", "algebra", "calculus", "equation",
            // Earth and atmosphere.
            "weather", "seismic", "tectonic", "climate", "paleontolog", "palaeontolog",
        };

        /// <summary>The word the game never says (lore-and-tone.md): the Demon's order is shown, never named.</summary>
        public const string ForbiddenWord = "sort";

        /// <summary>
        /// Capitalised words that are not names (sentence-style UI labels, shouts, the pronoun I), so the placeholder rule
        /// skips them. Everything else capitalised mid-sentence is read as a proper name.
        /// </summary>
        public static readonly HashSet<string> NotNames = new HashSet<string>
        {
            "I", "I'm", "I'll", "I've", "I'd", "RAWR", "SKRAW", "VICTORY", "MAXED", "Lv", "Victories", "Buy", "Interact", "Esc",
            "Enter", "Tab", "M",
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
        /// name (unless it is a chosen name or in NotNames); a name runs on over capitalised words ("Old Digger*") and its
        /// last word carries the star. Placeholders that appear lower-case (the apple-bonked sage*) are checked by
        /// MissingKnownStars.
        /// </summary>
        public static List<string> UnstarredNames(string text)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(text)) return found;
            string[] words = text.Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].TrimStart('.', '"', '\'', '(', '+', '*', '[', '-');
                if (word.Length == 0 || !char.IsUpper(word[0])) continue;
                string prev = i > 0 ? words[i - 1].TrimEnd('\'', '"', '*', ')', ']') : "";
                bool sentenceStart = i == 0 || prev.Length == 0 || SentenceEnds.IndexOf(prev[prev.Length - 1]) >= 0 ||
                                     words[i].StartsWith("(");
                int end = 0;
                while (end < word.Length && (char.IsLetter(word[end]) || word[end] == '\'' || word[end] == '-')) end++;
                string core = word.Substring(0, end);
                if (core.EndsWith("'s")) core = core.Substring(0, core.Length - 2);
                if (sentenceStart || NotNames.Contains(core) || IsChosen(core)) continue;
                int last = i;
                while (!words[last].Contains("*") && last + 1 < words.Length && words[last + 1].Length > 0 && char.IsUpper(words[last + 1][0]))
                    last++;
                if (!words[last].Contains("*")) found.Add(core);
                i = last;
            }
            found.AddRange(MissingKnownStars(text));
            return found;
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
