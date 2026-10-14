using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The temples' words (4.14): each puzzle's rule (said on walking in), the sealed door's reply, the solve moment's
    /// popups, the map badge and the overlay label. Voice-neutral (no character says them), short lines (at most
    /// MaxLineLength), under the text rule (InWorldText: no science words, region names keep their "*").
    /// </summary>
    public static class TempleText
    {
        public const int MaxLineLength = 60;

        /// <summary>Said walking into a sealed temple's door.</summary>
        public const string SealedPopup = "Sealed. Its leader holds the key:\nbring every fragment of this face.";

        /// <summary>The map's header badge while inside a temple.</summary>
        public const string MapBadge = "Inside the temple";

        /// <summary>The debug overlay's label.</summary>
        public const string OverlayLabel = "In the temple of";

        /// <summary>Town's tiles (4.19): a struck tile that is not beside the gap.</summary>
        public const string TileStuckPopup = "Only a tile beside the gap can slide.";

        /// <summary>The finches (4.19): a wrong note; the round's song plays again.</summary>
        public const string WrongNotePopup = "Wrong note. They sing it again.";

        /// <summary>The finches (4.19): a round sung back right; the next song is one note longer.</summary>
        public const string NextSongPopup = "Well sung! Now one note more.";

        /// <summary>Said once the puzzle is solved, as the fragments rise.</summary>
        public const string MendingPopup = "The fragments rise and mend...";

        /// <summary>The rule of a temple's puzzle (shown on walking in, once per attempt).</summary>
        public static string Rule(TemplePuzzleKind kind)
        {
            switch (kind)
            {
                case TemplePuzzleKind.Islands:
                    return "Light every plank to win the gem.\nA plank glows when you cross it.\nIf you cross a glowing plank, then they all go dark.";
                case TemplePuzzleKind.Ice:
                    return "Slide onto the gold pad to win the gem.\nOn ice you slide until something stops you.\nA pit drops you back at the door.";
                case TemplePuzzleKind.Wind:
                    return "Reach the gold ring to win the gem.\nStep into a gust and it carries you.\nYou cannot steer while riding.";
                case TemplePuzzleKind.Booths:
                    return "Get into both booths to win the gem.\nEach door needs three boulders by it.\nThe temple lends you a grip:\n[Interact] grabs a boulder.\nStep on a switch, then run in before it shuts.";
                case TemplePuzzleKind.Lights:
                    return "Light every crystal to win the gem.\nA strike flips a crystal and the ones beside it:\nlit ones go dark, dark ones light.\nAny swing will do here.";
                case TemplePuzzleKind.Song:
                    return "Watch the finches sing, then strike them\nin the same order to win the gem.\nEach song is one note longer. A wrong note,\nand they sing it again; so does the grey stone.\nAny swing will do here.";
                default:
                    return "Mend the town crest to win the gem.\nStrike a tile beside the gap to slide it in.\nThe small crest by the wall shows the picture.\nAny swing will do here.";
            }
        }

        /// <summary>The gem's popup: "+1 gem: wider gaps" (its boss benefit's label).</summary>
        public static string GemPopup(Theme theme) => "+1 gem: " + BossBenefits.Label(BossBenefits.Of(theme));

        /// <summary>A plaque in a solved temple.</summary>
        public const string SolvedPlaque = "Solved. The gem is yours.";

        /// <summary>Every fixed text here, for the scans.</summary>
        public static IEnumerable<string> All()
        {
            yield return SealedPopup;
            yield return MapBadge;
            yield return TileStuckPopup;
            yield return WrongNotePopup;
            yield return NextSongPopup;
            yield return MendingPopup;
            yield return SolvedPlaque;
            for (int k = 0; k <= (int)TemplePuzzleKind.Sliding; k++) yield return Rule((TemplePuzzleKind)k);
            foreach (Theme theme in new[] { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere })
                yield return GemPopup(theme);
        }
    }
}
