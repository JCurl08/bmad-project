using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The player's combat HUD (OnGUI placeholder): hearts (one per health point, partly filled for a fraction, e.g. quarter
    /// hearts after defence) with the exact health as a number, the equipped item's name, and a plain message once the player
    /// has died. Under them (4.8) dice, one per face whose region is known (Town from the start, the rest once revealed), in
    /// die-number order: a pip per fragment, lit once found, and a gold frame once mended. 4.25: all of it in the left side
    /// column (SideHud, StatusLayout), with the held stone's and the Mass Mitt's lines under it, never over the play area.
    /// </summary>
    public class CombatHud : MonoBehaviour
    {
        public const string DeathMessage = "You were defeated";
        public const string BareHandsLabel = "Bare hands (a very sincere bonk)";

        [SerializeField] private Health health;
        [SerializeField] private Equipment equipment;
        [SerializeField] private RunRewards rewards;

        private GUIStyle textStyle;
        private GUIStyle deathStyle;

        public Health Health
        {
            get => health;
            set => health = value;
        }

        public Equipment Equipment
        {
            get => equipment;
            set => equipment = value;
        }

        /// <summary>The player's roadblock rewards (4.7): keys and run boosts, shown on a third line when there are any.</summary>
        public RunRewards Rewards
        {
            get => rewards;
            set => rewards = value;
        }

        /// <summary>The rewards line as shown ("Keys: 1 | Run boosts: +speed"), or null when nothing is held.</summary>
        public string RewardsText => rewards != null ? rewards.HudText : null;

        /// <summary>One die of the fragments row: a face, its region, the fragments found of its count, and whether it is mended.</summary>
        public readonly struct FaceDie
        {
            public readonly FaceId Face;
            public readonly Theme Theme;
            public readonly int Found;
            public readonly int Count;
            public readonly bool Mended;

            /// <summary>True once the face's temple is open (4.14).</summary>
            public readonly bool Unsealed;

            /// <summary>True once the face's gem is won (4.14: the same as Mended).</summary>
            public bool HasGem => Mended;

            public FaceDie(FaceId face, Theme theme, int found, int count, bool mended, bool unsealed = false)
            {
                Face = face;
                Theme = theme;
                Found = found;
                Count = count;
                Mended = mended;
                Unsealed = unsealed || mended;
            }
        }

        /// <summary>
        /// The fragments row (4.8), in die-number order: Town always, the science faces once revealed (their regions are
        /// unknown before). Empty without a world.
        /// </summary>
        public List<FaceDie> FragmentDice
        {
            get
            {
                var dice = new List<FaceDie>();
                CubeWorld world = rewards != null ? rewards.World : null;
                if (world == null || world.Model == null || world.Dice == null) return dice;
                for (int number = 1; number <= DieNumbers.Highest; number++)
                {
                    FaceId face = world.Dice.FaceNumbered(number);
                    if (face != CubeModel.StartFace && (!world.ScienceRevealed || !CubeLayout.IsLaidOut(world.Model, face))) continue;
                    dice.Add(new FaceDie(face, world.Model.ThemeOf(face), rewards.FragmentsOf(face), number,
                        world.Repairs != null && world.Repairs.IsRepaired(face), world.Repairs != null && world.Repairs.IsUnsealed(face)));
                }
                return dice;
            }
        }

        /// <summary>The fragments row as text ("Fragments: Town mended, the Greenwood* 2/4"), or null with no world.</summary>
        public string FragmentsText
        {
            get
            {
                var parts = new List<(Theme, int, int, bool, bool)>();
                foreach (FaceDie d in FragmentDice) parts.Add((d.Theme, d.Found, d.Count, d.Unsealed, d.Mended));
                return RewardText.FragmentsLine(parts);
            }
        }

        /// <summary>The equipped item line as shown.</summary>
        public string EquippedText =>
            "Equipped: " + (equipment != null && equipment.Equipped != null ? equipment.Equipped.ToString() : BareHandsLabel);

        /// <summary>Health as shown next to the hearts, to two decimals (defence makes quarter-heart hits).</summary>
        public string HealthText => health != null ? $"{health.Current:0.##}/{health.Max:0.##}" : "";

        /// <summary>The death message while the player is dead, otherwise null.</summary>
        public string MessageText => health != null && health.IsDead ? DeathMessage : null;

        /// <summary>The held stone's and the Mass Mitt's HUD lines (4.25: drawn here, in the left column), with their colours.</summary>
        public List<(string Text, Color Color)> ItemLines
        {
            get
            {
                var lines = new List<(string, Color)>();
                GameObject player = health != null ? health.gameObject : null;
                if (player == null) return lines;
                if (player.TryGetComponent(out Isotope stone) && stone.ShowHud && stone.HudText != null)
                    lines.Add((stone.HudText, ChemistryIsotope.StageColor(stone.Stage)));
                if (player.TryGetComponent(out MassMitt mitt) && mitt.ShowHud && mitt.HudText != null)
                    lines.Add((InputPrompts.Fill(mitt.HudText), MassMittItem.ItemColor));
                return lines;
            }
        }

        private GUIStyle healthStyle, smallStyle;

        private void OnGUI()
        {
            if (health == null) return;
            Vector2 view = HudScale.Begin(); // 4.21: one shared scale, laid out in a virtual screen of at least 960x600
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = TextSize, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
                textStyle.normal.textColor = Color.white;
                healthStyle = new GUIStyle(textStyle) { fontSize = HealthSize };
                smallStyle = new GUIStyle(textStyle) { fontSize = SmallSize, fontStyle = FontStyle.Normal };
                deathStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                deathStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
            }

            // 4.25: the player's status lives in the left side column (SideHud), never over the play area.
            SideHud.Frame frame = SideHud.Layout(view.x, view.y);
            int hearts = Mathf.CeilToInt(health.Max);
            string rewardsLine = RewardsText;
            List<FaceDie> dice = FragmentDice;
            List<(string Text, Color Color)> items = ItemLines;
            var itemTexts = new List<string>();
            foreach ((string text, _) in items) itemTexts.Add(text);
            StatusRects s = StatusLayout(frame.Left, hearts, EquippedText, rewardsLine, dice.Count, itemTexts);

            for (int i = 0; i < hearts; i++)
            {
                float fill = Mathf.Clamp01(health.Current - i);
                Rect rect = s.Heart(i);
                SideHud.Fill(rect, new Color(0.3f, 0.05f, 0.08f));
                if (fill > 0f) SideHud.Fill(new Rect(rect.x, rect.y, rect.width * fill, rect.height), new Color(1f, 0.25f, 0.35f));
            }
            GUI.color = Color.white;
            GUI.Label(s.Health, HealthText, healthStyle);
            SideHud.Label(s.Equipped, EquippedText, TextSize, textStyle);
            if (rewardsLine != null) SideHud.Label(s.Rewards, RewardsLines(rewardsLine), SmallSize, smallStyle);
            if (dice.Count > 0) DrawDice(dice, s);
            for (int i = 0; i < items.Count && i < s.Items.Count; i++)
            {
                Rect r = s.Items[i];
                SideHud.Fill(new Rect(r.x, r.y + 3f, 8f, 8f), items[i].Color);
                SideHud.Label(new Rect(r.x + 12f, r.y, r.width - 12f, r.height), items[i].Text, SmallSize, smallStyle);
            }

            string message = MessageText;
            if (message == null) return;
            // The death message: centred on the play area.
            var panel = new Rect(frame.Play.center.x - Mathf.Min(320f, frame.Play.width / 2f), frame.Play.center.y - 60f,
                Mathf.Min(640f, frame.Play.width), 120f);
            SideHud.Fill(panel, new Color(0f, 0f, 0f, 0.75f));
            GUI.Label(panel, message, deathStyle);
        }

        /// <summary>The rewards line split onto lines of its own ("Keys: 1", "Run boosts: +speed").</summary>
        public static string RewardsLines(string line) => line?.Replace(" | ", "\n");

        /// <summary>Font sizes in the left column (GUI units).</summary>
        public const int TextSize = 13, HealthSize = 14, SmallSize = 12;

        /// <summary>A heart's size (GUI units), the gap between hearts, and a die's size and gap in the fragments rows.</summary>
        public const float HeartSize = 18f, HeartGap = 4f, DieSize = 20f, DieGap = 8f;

        /// <summary>Where each part of the status column goes (GUI units), from StatusLayout.</summary>
        public sealed class StatusRects
        {
            public Rect Column, Inner, Hearts, Health, Equipped, Rewards, DiceLabel, Dice;
            public int HeartsPerRow, DicePerRow, HeartCount, DieCount;
            public readonly List<Rect> Items = new List<Rect>();

            /// <summary>The lowest point any part reaches.</summary>
            public float Bottom;

            public Rect Heart(int i) => new Rect(Hearts.x + i % HeartsPerRow * (HeartSize + HeartGap), Hearts.y + i / HeartsPerRow * (HeartSize + HeartGap),
                HeartSize, HeartSize);

            public Rect Die(int i) => new Rect(Dice.x + 3f + i % DicePerRow * (DieSize + DieGap), Dice.y + 8f + i / DicePerRow * (DieSize + DieGap + 4f),
                DieSize, DieSize);

            /// <summary>Every part's rect, for the layout tests.</summary>
            public IEnumerable<Rect> All()
            {
                for (int i = 0; i < HeartCount; i++) yield return Heart(i);
                yield return Health;
                yield return Equipped;
                if (Rewards.height > 0f) yield return Rewards;
                if (DieCount > 0)
                {
                    yield return DiceLabel;
                    for (int i = 0; i < DieCount; i++) yield return Die(i);
                }
                foreach (Rect r in Items) yield return r;
            }
        }

        /// <summary>
        /// 4.25: the status column's layout (GUI units) in a column (SideHud's left one): hearts in rows, the health number,
        /// the equipped item (wrapped), the keys and boosts (a line each), the fragment dice in rows under their label, and the
        /// stone's and the mitt's lines, top to bottom.
        /// </summary>
        public static StatusRects StatusLayout(Rect column, int hearts, string equipped, string rewards, int dice, IReadOnlyList<string> items)
        {
            var s = new StatusRects { Column = column, Inner = SideHud.Inner(column), HeartCount = Mathf.Max(0, hearts), DieCount = Mathf.Max(0, dice) };
            Rect inner = s.Inner;
            float y = inner.y;
            s.HeartsPerRow = Mathf.Max(1, Mathf.FloorToInt((inner.width + HeartGap) / (HeartSize + HeartGap)));
            int heartRows = Mathf.Max(1, Mathf.CeilToInt(s.HeartCount / (float)s.HeartsPerRow));
            s.Hearts = new Rect(inner.x, y, inner.width, heartRows * (HeartSize + HeartGap) - HeartGap);
            y = s.Hearts.yMax + 4f;
            s.Health = new Rect(inner.x, y, inner.width, SideHud.LineHeight(HealthSize));
            y = s.Health.yMax + 6f;
            s.Equipped = new Rect(inner.x, y, inner.width, SideHud.TextHeight(equipped, TextSize, inner.width));
            y = s.Equipped.yMax + 6f;
            if (rewards != null)
            {
                s.Rewards = new Rect(inner.x, y, inner.width, SideHud.TextHeight(RewardsLines(rewards), SmallSize, inner.width));
                y = s.Rewards.yMax + 6f;
            }
            if (s.DieCount > 0)
            {
                s.DiceLabel = new Rect(inner.x, y, inner.width, SideHud.LineHeight(TextSize));
                y = s.DiceLabel.yMax + 2f;
                s.DicePerRow = Mathf.Max(1, Mathf.FloorToInt((inner.width - 6f + DieGap) / (DieSize + DieGap)));
                int rows = Mathf.CeilToInt(s.DieCount / (float)s.DicePerRow);
                s.Dice = new Rect(inner.x, y, inner.width, rows * (DieSize + DieGap + 4f) + 8f);
                y = s.Dice.yMax + 6f;
            }
            if (items != null)
                foreach (string text in items)
                {
                    var r = new Rect(inner.x, y, inner.width, SideHud.TextHeight(text, SmallSize, inner.width - 12f));
                    s.Items.Add(r);
                    y = r.yMax + 6f;
                }
            s.Bottom = y - 6f;
            return s;
        }

        private static readonly Color MendedGold = new Color(1f, 0.82f, 0.25f);

        /// <summary>The fragments rows: their label, then a die per face (region-coloured frame, a pip per fragment).</summary>
        private void DrawDice(List<FaceDie> dice, StatusRects s)
        {
            GUI.Label(s.DiceLabel, RewardText.FragmentsLabel, textStyle);
            for (int i = 0; i < dice.Count; i++)
            {
                FaceDie d = dice[i];
                Rect box = s.Die(i);
                Color frame = d.Mended ? MendedGold : CubeWorld.ThemeColor(d.Theme);
                SideHud.Fill(new Rect(box.x - 3f, box.y - 3f, box.width + 6f, box.height + 6f), Color.black);
                SideHud.Fill(new Rect(box.x - 2f, box.y - 2f, box.width + 4f, box.height + 4f), frame);
                SideHud.Fill(box, new Color(0.12f, 0.1f, 0.14f));
                Vector2Int[] pips = TownArt.PipLayouts[Mathf.Clamp(d.Count, 1, 6)];
                float cell = DieSize / 7f;
                for (int p = 0; p < pips.Length; p++)
                {
                    bool found = rewards != null && rewards.HasFragment(d.Face, p + 1);
                    Color pip = d.Mended ? MendedGold : found ? Color.white : new Color(0.4f, 0.38f, 0.45f);
                    // Pip (x, y) on the 7x7 face, y up; GUI y runs down.
                    SideHud.Fill(new Rect(box.x + pips[p].x * cell - cell * 0.25f, box.y + (6 - pips[p].y) * cell - cell * 0.25f, cell * 1.5f, cell * 1.5f), pip);
                }
                if (d.HasGem)
                {
                    // 4.14: the face's gem, a diamond in its region's colour on the die's corner.
                    Color gem = TempleArt.GemColor(d.Theme);
                    var g = new Rect(box.xMax - 6f, box.y - 8f, 12f, 12f);
                    SideHud.Fill(new Rect(g.x + 3f, g.y, 6f, 12f), Color.black);
                    SideHud.Fill(new Rect(g.x, g.y + 3f, 12f, 6f), Color.black);
                    SideHud.Fill(new Rect(g.x + 4f, g.y + 1f, 4f, 10f), gem);
                    SideHud.Fill(new Rect(g.x + 1f, g.y + 4f, 10f, 4f), gem);
                    SideHud.Fill(new Rect(g.x + 4f, g.y + 4f, 2f, 2f), Color.white);
                }
                else if (d.Unsealed)
                {
                    // The temple open: a pale bar under the die, waiting for its gem.
                    SideHud.Fill(new Rect(box.x - 4f, box.yMax + 3f, box.width + 8f, 2f), new Color(0.85f, 0.85f, 1f));
                }
            }
        }
    }
}
