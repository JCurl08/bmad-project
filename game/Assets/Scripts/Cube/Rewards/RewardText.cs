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
        public const string HeartPopup = "+1 heart (this run)";
        public const string SpeedPopup = "Quicker feet (this run)";
        public const string PowerPopup = "Harder bonk (this run)";

        /// <summary>A fragment slot's popup until 4.8 numbers the fragments: "A shard of the Greenwood*".</summary>
        public static string FragmentPopup(Theme theme) => "A shard of " + InWorldText.Region(theme);

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
        public static string HudLine(int keys, IReadOnlyList<RunUpgrade> upgrades)
        {
            var parts = new List<string>();
            if (keys > 0) parts.Add($"Keys: {keys}");
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
            yield return HeartPopup;
            yield return SpeedPopup;
            yield return PowerPopup;
            foreach (Theme t in CubeModel.ScienceThemes) yield return FragmentPopup(t);
            yield return HudLine(2, new[] { RunUpgrade.Heart, RunUpgrade.Heart, RunUpgrade.Speed, RunUpgrade.Power });
            yield return HudLine(1, new[] { RunUpgrade.Speed });
            yield return ChestsLine(3, 15);
            yield return KeysLine(2, 10);
        }
    }
}
