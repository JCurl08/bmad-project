using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The Town welcome (lore-and-tone.md: campy, never explicit, never the s-word). There is no sign post
    /// yet: the town greeter (the first Townsperson, who stands on the start screen) says it as their flavour
    /// line, then their hint (two lines, like every conversation).
    /// </summary>
    public static class TownSign
    {
        /// <summary>The phrase the welcome must carry (story 7): what the town is.</summary>
        public const string LastMixedPlace = "the last place everyone still mixes";

        /// <summary>The welcome, in one line: what the town is, and the Partition that made it so.</summary>
        public static readonly IReadOnlyList<string> WelcomeLines = new[]
        {
            "Welcome to Town, " + LastMixedPlace + "! The Partition split every other race onto its own tidy face. We stayed put.",
        };

        /// <summary>
        /// The greeter's conversation from their usual one (flavour, hint): the welcome replaces the flavour line, and the
        /// hint (the last line, when there are two) follows.
        /// </summary>
        public static List<string> GreeterLines(IReadOnlyList<string> lines)
        {
            var result = new List<string> { WelcomeLines[0] };
            if (lines != null && lines.Count >= Dialogue.MaxLines) result.Add(lines[lines.Count - 1]);
            return result;
        }
    }
}
