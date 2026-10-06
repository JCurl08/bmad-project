using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The Town welcome (lore-and-tone.md: campy, never explicit, never the s-word). There is no sign post
    /// yet: the town greeter (the first Townsperson, who stands on the start screen) says these lines right
    /// after their greeting.
    /// </summary>
    public static class TownSign
    {
        /// <summary>The phrase the welcome must carry (story 7): what the town is.</summary>
        public const string LastMixedPlace = "the last place everyone still mixes";

        public static readonly IReadOnlyList<string> WelcomeLines = new[]
        {
            "Welcome to Town, " + LastMixedPlace + "! Finches, fungi, aliens, shapes, dinosaurs... and us.",
            "Long ago the Partition split every race onto its own tidy little face. We never left. Call us stubborn. We call it neighbourly.",
            "Mind the ruins, they're a bit of every face glued together. Much like the guest list.",
        };

        /// <summary>The greeter's conversation: the usual greeting, the welcome, then the rest of their lines.</summary>
        public static List<string> GreeterLines(IReadOnlyList<string> lines)
        {
            var result = new List<string>();
            if (lines != null && lines.Count > 0) result.Add(lines[0]);
            result.AddRange(WelcomeLines);
            if (lines != null)
                for (int i = 1; i < lines.Count; i++) result.Add(lines[i]);
            return result;
        }
    }
}
