using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The player-visible words of the roadblock rewards (4.7): pickup popups, the HUD line and the between-runs lines. They
    /// are voice-neutral (no character says them) and keep the subtle-science rule (InWorldText); the text scans read All.
    /// </summary>
    public static class RewardText
    {
        public const string KeyPopup = "+1 key";
        public const string ChestKeyPopup = "+1 chest key";
        public const string HeartPopup = "+1 heart (this run)";
        public const string SpeedPopup = "Quicker feet (this run)";
        public const string PowerPopup = "Harder bonk (this run)";

        /// <summary>A fragment's popup (4.8): "Fragment 3 of 5 from the Greenwood*".</summary>
        public static string FragmentPopup(Theme theme, int ordinal, int count) => $"Fragment {ordinal} of {count} from {InWorldText.Region(theme)}";

        /// <summary>The popup when a face is repaired: "The Greenwood* is mended!" ("Town is mended!").</summary>
        public static string RepairPopup(Theme theme)
        {
            string region = InWorldText.Region(theme);
            return char.ToUpperInvariant(region[0]) + region.Substring(1) + " is mended!";
        }

        /// <summary>The popup when a leader unseals a face's temple (4.14): "The temple of the Greenwood* is open!".</summary>
        public static string UnsealPopup(Theme theme) => $"The temple of {InWorldText.Region(theme)} is open!";

        /// <summary>The HUD's fragments label (the dice follow it, one per face, a pip per fragment).</summary>
        public const string FragmentsLabel = "Fragments";

        /// <summary>A face's fragment state on the HUD and map (4.14): "temple" once its temple is open, "gem" once its gem is won.</summary>
        public const string TempleTag = "temple", GemTag = "gem";

        /// <summary>One face's fragment count as the HUD and map spell it: "2/4", or "gem" once mended (its temple's gem won).</summary>
        public static string FragmentCount(int found, int count, bool mended) => mended ? GemTag : $"{found}/{count}";

        /// <summary>As FragmentCount, with the temple open (4.14): "2/4", "temple" or "gem".</summary>
        public static string FragmentCount(int found, int count, bool unsealed, bool mended) =>
            mended ? GemTag : unsealed ? TempleTag : $"{found}/{count}";

        /// <summary>
        /// The HUD's fragments line as text (the HUD draws it as dice): "Fragments: Town mended, the Greenwood* 2/4, ...", for
        /// the faces given (Town first), or null with none.
        /// </summary>
        public static string FragmentsLine(IEnumerable<(Theme Theme, int Found, int Count, bool Mended)> faces)
        {
            var parts = new List<string>();
            if (faces != null)
                foreach ((Theme theme, int found, int count, bool mended) in faces)
                    parts.Add(InWorldText.Region(theme) + " " + FragmentCount(found, count, mended));
            return parts.Count == 0 ? null : FragmentsLabel + ": " + string.Join(", ", parts);
        }

        /// <summary>The HUD's line with the temples (4.14): "Fragments: Town gem, the Greenwood* temple, Bridgelands* 2/4".</summary>
        public static string FragmentsLine(IEnumerable<(Theme Theme, int Found, int Count, bool Unsealed, bool Mended)> faces)
        {
            var parts = new List<string>();
            if (faces != null)
                foreach ((Theme theme, int found, int count, bool unsealed, bool mended) in faces)
                    parts.Add(InWorldText.Region(theme) + " " + FragmentCount(found, count, unsealed, mended));
            return parts.Count == 0 ? null : FragmentsLabel + ": " + string.Join(", ", parts);
        }

        public static string UpgradePopup(RunUpgrade upgrade)
        {
            switch (upgrade)
            {
                case RunUpgrade.Heart: return HeartPopup;
                case RunUpgrade.Speed: return SpeedPopup;
                default: return PowerPopup;
            }
        }

        /// <summary>A boost's short name on the HUD.</summary>
        public static string BoostLabel(RunUpgrade upgrade, int count)
        {
            switch (upgrade)
            {
                case RunUpgrade.Heart: return count == 1 ? "+1 heart" : $"+{count} hearts";
                case RunUpgrade.Speed: return count == 1 ? "+speed" : $"+{count} speed";
                default: return count == 1 ? "+power" : $"+{count} power";
            }
        }

        /// <summary>The HUD's rewards line ("Keys: 2 | Run boosts: +1 heart, +speed"), or null when there is nothing to show.</summary>
        public static string HudLine(int keys, IReadOnlyList<RunUpgrade> upgrades) => HudLine(keys, 0, upgrades, false);

        /// <summary>
        /// The HUD's rewards line ("Keys: 1 | Chest keys: 2 | Run boosts: +1 heart"), or null when there is nothing to show. With
        /// alwaysKeys (the run has locked arches, 4.9) the key count shows even at 0.
        /// </summary>
        public static string HudLine(int keys, int chestKeys, IReadOnlyList<RunUpgrade> upgrades, bool alwaysKeys)
        {
            var parts = new List<string>();
            if (keys > 0 || alwaysKeys) parts.Add($"Keys: {keys}");
            if (chestKeys > 0) parts.Add($"Chest keys: {chestKeys}");
            if (upgrades != null && upgrades.Count > 0)
            {
                var labels = new List<string>();
                foreach (RunUpgrade u in new[] { RunUpgrade.Heart, RunUpgrade.Speed, RunUpgrade.Power })
                {
                    int n = 0;
                    foreach (RunUpgrade x in upgrades) if (x == u) n++;
                    if (n > 0) labels.Add(BoostLabel(u, n));
                }
                parts.Add("Run boosts: " + string.Join(", ", labels));
            }
            return parts.Count == 0 ? null : string.Join(" | ", parts);
        }

        /// <summary>The between-runs line for chests opened.</summary>
        public static string ChestsLine(int count, int amount) => $"Chests opened ({count}): +{amount}";

        /// <summary>The between-runs line for keys cashed in at the run's end.</summary>
        public static string KeysLine(int count, int amount) => $"Unused keys cashed in ({count}): +{amount}";

        /// <summary>Every fixed text here, for the text scans.</summary>
        public static IEnumerable<string> All()
        {
            yield return KeyPopup;
            yield return ChestKeyPopup;
            yield return HudLine(0, 2, new[] { RunUpgrade.Power }, true);
            yield return HeartPopup;
            yield return SpeedPopup;
            yield return PowerPopup;
            yield return FragmentPopup(Theme.Town, 1, 1);
            yield return RepairPopup(Theme.Town);
            yield return UnsealPopup(Theme.Town);
            foreach (Theme t in CubeModel.ScienceThemes)
            {
                yield return FragmentPopup(t, 3, 5);
                yield return RepairPopup(t);
                yield return UnsealPopup(t);
            }
            yield return FragmentsLine(new[] { (Theme.Town, 1, 1, true, true), (Theme.Biology, 4, 4, true, false), (Theme.Math, 2, 6, false, false) });
            yield return FragmentsLine(new[] { (Theme.Town, 1, 1, true), (Theme.Biology, 2, 4, false), (Theme.Math, 0, 6, false) });
            yield return HudLine(2, new[] { RunUpgrade.Heart, RunUpgrade.Heart, RunUpgrade.Speed, RunUpgrade.Power });
            yield return HudLine(1, new[] { RunUpgrade.Speed });
            yield return ChestsLine(3, 15);
            yield return KeysLine(2, 10);
        }
    }
}
