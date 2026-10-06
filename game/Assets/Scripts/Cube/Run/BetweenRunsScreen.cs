using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// The between-runs screen (OnGUI placeholder), drawn over everything while RunLoop.ScreenOpen: how the run ended,
    /// what it earned (trials, the Demon, stray Jumbles) and the balance; the four upgrades with level, next cost and next
    /// effect, each with a Buy button (keys 1-4); and Continue (Enter) back to Town on a new seed. The ended run's seed is
    /// shown, and in debug builds a button replays it. Campy copy; the currency is "Jumbles".
    /// </summary>
    public class BetweenRunsScreen : MonoBehaviour
    {
        public const string CurrencyName = "Jumbles";
        public const string VictoryTitle = "VICTORY! The Demon's tidy little cube is now a glorious mess.";
        public const string DefeatTitle = "You fell apart. Relax: falling apart is kind of your whole thing.";
        public const string ContinueLabel = "Back to Town (new seed)  [Enter]";

        [SerializeField] private RunLoop loop;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;

        public RunLoop Loop
        {
            get => loop;
            set => loop = value;
        }

        public bool Visible => loop != null && loop.ScreenOpen && loop.LastRun != null;

        public string Title => loop?.LastRun == null ? null : loop.LastRun.Victory ? VictoryTitle : DefeatTitle;

        /// <summary>The earnings lines as shown (one per source, then the total banked and the balance).</summary>
        public List<string> EarningsLines()
        {
            var lines = new List<string>();
            if (loop?.LastRun == null) return lines;
            RunEarnings e = loop.LastRun.Earnings;
            lines.Add($"Trials solved ({e.CountOf(EarningSource.Trial)}): +{e.Of(EarningSource.Trial)}");
            lines.Add($"Demon dethroned: +{e.Of(EarningSource.Boss)}");
            lines.Add($"Stray {CurrencyName} found ({e.CountOf(EarningSource.Hidden)}): +{e.Of(EarningSource.Hidden)}");
            lines.Add($"Banked this run: {loop.LastRun.Banked} {CurrencyName}");
            lines.Add($"In the jar: {loop.Meta.Balance} {CurrencyName}");
            return lines;
        }

        /// <summary>One upgrade's shop line: name, level, next cost and what the next level gives.</summary>
        public string UpgradeLine(UpgradeStat stat)
        {
            if (loop == null || loop.Meta == null) return "";
            int level = UpgradeShop.Level(loop.Meta, stat);
            int baseValue = loop.BaseStats.Get(stat);
            string now = UpgradeShop.EffectText(stat, baseValue, level);
            if (level >= UpgradeShop.MaxLevel)
                return $"{UpgradeShop.Name(stat)}  Lv {level}/{UpgradeShop.MaxLevel}  ({now})  MAXED";
            string next = UpgradeShop.EffectText(stat, baseValue, level + 1);
            return $"{UpgradeShop.Name(stat)}  Lv {level}/{UpgradeShop.MaxLevel}  ({now} -> {next})  " +
                   $"{UpgradeShop.Cost(level)} {CurrencyName}";
        }

        private void Update()
        {
            if (!Visible) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) loop.Buy(UpgradeStat.Health);
            if (keyboard.digit2Key.wasPressedThisFrame) loop.Buy(UpgradeStat.Defence);
            if (keyboard.digit3Key.wasPressedThisFrame) loop.Buy(UpgradeStat.Power);
            if (keyboard.digit4Key.wasPressedThisFrame) loop.Buy(UpgradeStat.Speed);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) loop.Continue();
        }

        private void OnGUI()
        {
            if (!Visible) return;
            GUI.depth = -100; // over the HUDs
            EnsureStyles();

            GUI.color = new Color(0.05f, 0.02f, 0.08f, 0.9f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(760f, Screen.width - 32f);
            float x = (Screen.width - width) / 2f;
            float y = Mathf.Max(16f, Screen.height / 2f - 290f);

            RunSummary run = loop.LastRun;
            GUI.Label(new Rect(x, y, width, 64f), Title, titleStyle);
            y += 66f;
            GUI.Label(new Rect(x, y, width, 22f),
                $"Run {run.RunNumber} on seed {run.Seed}   Victories so far: {loop.Meta.Victories}", smallStyle);
            y += 28f;

            foreach (string line in EarningsLines())
            {
                GUI.Label(new Rect(x, y, width, 24f), line, textStyle);
                y += 24f;
            }
            y += 10f;

            GUI.Label(new Rect(x, y, width, 24f), "Spend your Jumbles (kicks in next run):", textStyle);
            y += 28f;
            for (int i = 0; i < UpgradeShop.All.Length; i++)
            {
                UpgradeStat stat = UpgradeShop.All[i];
                bool maxed = UpgradeShop.IsMaxed(loop.Meta, stat);
                bool affordable = !maxed && loop.Meta.Balance >= UpgradeShop.NextCost(loop.Meta, stat);
                GUI.enabled = !maxed;
                GUI.color = affordable || maxed ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                if (GUI.Button(new Rect(x, y, 90f, 30f), $"Buy [{i + 1}]", buttonStyle)) loop.Buy(stat);
                GUI.enabled = true;
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 100f, y, width - 100f, 22f), UpgradeLine(stat), textStyle);
                GUI.Label(new Rect(x + 100f, y + 20f, width - 100f, 18f), UpgradeShop.Blurb(stat), smallStyle);
                y += 44f;
            }

            if (!string.IsNullOrEmpty(loop.LastMessage))
            {
                GUI.color = new Color(1f, 0.85f, 0.45f);
                GUI.Label(new Rect(x, y, width, 24f), loop.LastMessage, textStyle);
                GUI.color = Color.white;
            }
            y += 32f;

            if (GUI.Button(new Rect(x, y, 320f, 40f), ContinueLabel, buttonStyle)) loop.Continue();
            if (CubeDebug.Available && GUI.Button(new Rect(x + 336f, y, 220f, 40f), $"Replay seed {run.Seed} (debug)", buttonStyle))
                loop.StartRun(run.Seed);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
            titleStyle.normal.textColor = new Color(1f, 0.6f, 0.85f);
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            textStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Italic };
            smallStyle.normal.textColor = new Color(0.8f, 0.8f, 0.9f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }
    }
}
