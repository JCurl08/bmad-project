using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// The between-runs screen (OnGUI placeholder), drawn over everything while RunLoop.ScreenOpen: how the run ended,
    /// what it earned (trials, the Demon, stray Jumbles) and the balance; the four upgrades with level, next cost and next
    /// effect, each with a Buy button (keys 1-4); and Continue (Enter) back to Town on a new seed. The ended run's seed is
    /// shown, and in debug builds a button replays it. Campy copy; the currency is "Jumbles*" (InWorldText.Currency).
    /// <para>4.21: every control is an input action (GameActions): Buy1-4 (keys 1-4), Continue (Enter, the pad's Start), and a
    /// pick moved over the four upgrades and Continue with MenuUp/MenuDown (the arrow keys, the pad's d-pad or stick) and
    /// taken with Confirm (Space, the pad's A), so a pad can shop and carry on. 4.21 review: the screen opens with nothing
    /// picked (NoPick), and Confirm does nothing until the arrows or the pad have picked a row, so a stray Space or A never
    /// buys. The labels show the real key or button names
    /// (InputPrompts). The build's version is shown beside the seed (BuildLine), so a bug report can name both.</para>
    /// </summary>
    public class BetweenRunsScreen : MonoBehaviour
    {
        public const string CurrencyName = InWorldText.Currency;
        public const string VictoryTitle = "VICTORY! " + InWorldText.DemonFull + "'s tidy little cube is now a glorious mess.";
        public const string DefeatTitle = "You were defeated";
        public const string ContinueLabel = "Back to Town (new seed)  [Enter]";
        public const string SpendLabel = "Spend your " + CurrencyName + " (kicks in next run):";

        /// <summary>The line under the title: the run number, its seed and the victories so far.</summary>
        public static string RunLine(int runNumber, int seed, int victories) =>
            $"Run {runNumber} on seed {seed}   Victories so far: {victories}";

        /// <summary>A Buy button's label (key 1-4).</summary>
        public static string BuyLabel(int index) => $"Buy [{index + 1}]";

        /// <summary>A Buy button's label on a pad: the picked row shows its Confirm button, the rest just "Buy".</summary>
        public const string PadBuyPicked = "Buy [Confirm]";
        public const string PadBuy = "Buy";

        /// <summary>The build's version, beside the seed (4.21: a bug report names the build and the seed).</summary>
        public static string BuildLine(string version) => $"Build {version}";

        /// <summary>The pick's rows: the four upgrades (0-3), then Continue.</summary>
        public const int ContinueRow = 4;

        public const int Rows = 5;

        /// <summary>No row picked yet: the screen opens so, and Confirm then does nothing.</summary>
        public const int NoPick = -1;

        /// <summary>The picked row (4.21): a pad (or the arrow keys) moves it, Confirm takes it. NoPick on opening.</summary>
        public int Picked { get; private set; } = NoPick;

        /// <summary>
        /// The row the pick lands on from a row after one step (down: +1), wrapping round. From NoPick, down lands on the
        /// first upgrade and up on Continue.
        /// </summary>
        public static int Step(int row, int step)
        {
            if (row < 0 || row >= Rows) return step > 0 ? 0 : ContinueRow;
            return ((row + step) % Rows + Rows) % Rows;
        }

        private readonly InputAction[] buyActions = new InputAction[4];
        private InputAction continueAction, upAction, downAction, confirmAction;
        private int openedFrame = -1;
        private bool wasVisible;

        [SerializeField] private RunLoop loop;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle smallStyle;
        private GUIStyle buildStyle;
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
            if (loop?.LastRun == null) return new List<string>();
            return EarningsLinesFor(loop.LastRun.Earnings, loop.LastRun.Banked, loop.Meta.Balance);
        }

        /// <summary>The earnings lines for a run's earnings, what it banked and the balance after it.</summary>
        public static List<string> EarningsLinesFor(RunEarnings e, int banked, int balance)
        {
            var lines = new List<string>();
            lines.Add($"Trials solved ({e.CountOf(EarningSource.Trial)}): +{e.Of(EarningSource.Trial)}");
            lines.Add($"{NpcNames.Demon} dethroned: +{e.Of(EarningSource.Boss)}");
            lines.Add($"Stray {CurrencyName} found ({e.CountOf(EarningSource.Hidden)}): +{e.Of(EarningSource.Hidden)}");
            lines.Add(RewardText.ChestsLine(e.CountOf(EarningSource.Chest), e.Of(EarningSource.Chest)));
            lines.Add(RewardText.KeysLine(e.CountOf(EarningSource.Keys), e.Of(EarningSource.Keys)));
            lines.Add($"Banked this run: {banked} {CurrencyName}");
            lines.Add($"In the jar: {balance} {CurrencyName}");
            return lines;
        }

        /// <summary>One upgrade's shop line: name, level, next cost and what the next level gives.</summary>
        public string UpgradeLine(UpgradeStat stat)
        {
            if (loop == null || loop.Meta == null) return "";
            return UpgradeLineFor(stat, UpgradeShop.Level(loop.Meta, stat), loop.BaseStats.Get(stat));
        }

        /// <summary>An upgrade's shop line at a level over a base value.</summary>
        public static string UpgradeLineFor(UpgradeStat stat, int level, int baseValue)
        {
            string now = UpgradeShop.EffectText(stat, baseValue, level);
            if (level >= UpgradeShop.MaxLevel)
                return $"{UpgradeShop.Name(stat)}  Lv {level}/{UpgradeShop.MaxLevel}  ({now})  MAXED";
            string next = UpgradeShop.EffectText(stat, baseValue, level + 1);
            return $"{UpgradeShop.Name(stat)}  Lv {level}/{UpgradeShop.MaxLevel}  ({now} -> {next})  " +
                   $"{UpgradeShop.Cost(level)} {CurrencyName}";
        }

        private void OnEnable()
        {
            for (int i = 0; i < buyActions.Length; i++) buyActions[i] = GameActions.Find(GameActions.Buy(i));
            continueAction = GameActions.Find(GameActions.Continue);
            upAction = GameActions.Find(GameActions.MenuUp);
            downAction = GameActions.Find(GameActions.MenuDown);
            confirmAction = GameActions.Find(GameActions.Confirm);
        }

        private void OnDisable()
        {
            // Shared project-wide actions: never disabled here.
            for (int i = 0; i < buyActions.Length; i++) buyActions[i] = null;
            continueAction = upAction = downAction = confirmAction = null;
        }

        private void Update()
        {
            bool visible = Visible;
            if (visible && !wasVisible)
            {
                Picked = NoPick; // nothing picked: a stray Space or A neither buys nor skips the shop
                openedFrame = Time.frameCount;
            }
            wasVisible = visible;
            if (!visible || Time.frameCount == openedFrame) return; // the press that ended the run does nothing here
            for (int i = 0; i < buyActions.Length && i < UpgradeShop.All.Length; i++)
                if (GameActions.Pressed(buyActions[i])) loop.Buy(UpgradeShop.All[i]);
            if (GameActions.Pressed(upAction)) Picked = Step(Picked, -1);
            if (GameActions.Pressed(downAction)) Picked = Step(Picked, 1);
            if (GameActions.Pressed(confirmAction)) TakePicked();
            else if (GameActions.Pressed(continueAction)) loop.Continue();
        }

        /// <summary>Takes the picked row: buys its upgrade, or continues. Nothing while no row is picked.</summary>
        public void TakePicked()
        {
            if (!Visible || Picked == NoPick) return;
            if (Picked == ContinueRow) loop.Continue();
            else if (Picked >= 0 && Picked < UpgradeShop.All.Length) loop.Buy(UpgradeShop.All[Picked]);
        }

        private void OnGUI()
        {
            if (!Visible) return;
            GUI.depth = -100; // over the HUDs
            Vector2 view = HudScale.Begin(); // 4.21: the shared scale
            EnsureStyles();

            GUI.color = new Color(0.05f, 0.02f, 0.08f, 0.9f);
            GUI.DrawTexture(new Rect(0, 0, view.x, view.y), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(760f, view.x - 32f);
            float x = (view.x - width) / 2f;
            float y = Mathf.Max(16f, view.y / 2f - 290f);

            RunSummary run = loop.LastRun;
            GUI.Label(new Rect(x, y, width, 64f), Title, titleStyle);
            y += 66f;
            GUI.Label(new Rect(x, y, width, 22f),
                RunLine(run.RunNumber, run.Seed, loop.Meta.Victories), smallStyle);
            GUI.Label(new Rect(x, y, width, 22f), BuildLine(Application.version), buildStyle);
            y += 28f;

            foreach (string line in EarningsLines())
            {
                GUI.Label(new Rect(x, y, width, 24f), line, textStyle);
                y += 22f; // seven lines since 4.7: still clear of the bottom of the 960x600 canvas
            }
            y += 10f;

            GUI.Label(new Rect(x, y, width, 24f), SpendLabel, textStyle);
            y += 28f;
            for (int i = 0; i < UpgradeShop.All.Length; i++)
            {
                UpgradeStat stat = UpgradeShop.All[i];
                bool maxed = UpgradeShop.IsMaxed(loop.Meta, stat);
                bool affordable = !maxed && loop.Meta.Balance >= UpgradeShop.NextCost(loop.Meta, stat);
                if (Picked == i) Pick(new Rect(x - 4f, y - 3f, width + 8f, 42f));
                GUI.enabled = !maxed;
                GUI.color = affordable || maxed ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                string label = InputPrompts.UsingGamepad ? (Picked == i ? PadBuyPicked : PadBuy) : BuyLabel(i);
                if (GUI.Button(new Rect(x, y, 90f, 30f), InputPrompts.Fill(label), buttonStyle)) loop.Buy(stat);
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

            if (Picked == ContinueRow) Pick(new Rect(x - 4f, y - 4f, 328f, 48f));
            if (GUI.Button(new Rect(x, y, 320f, 40f), InputPrompts.Fill(ContinueLabel), buttonStyle)) loop.Continue();
            if (CubeDebug.Available && GUI.Button(new Rect(x + 336f, y, 220f, 40f), $"Replay seed {run.Seed} (debug)", buttonStyle))
                loop.StartRun(run.Seed);
        }

        /// <summary>The pick's frame: a gold outline round the picked row.</summary>
        private static void Pick(Rect r)
        {
            GUI.color = new Color(1f, 0.85f, 0.45f, 0.9f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
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
            buildStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.UpperRight };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }
    }
}
