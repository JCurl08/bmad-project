using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The player's combat HUD (OnGUI placeholder): a row of hearts at the bottom left (one per health point, partly filled for
    /// a fraction, e.g. quarter hearts after defence) with the exact health as a number, the equipped item's name, and a plain
    /// message once the player has died. Under them (4.8) a row of dice, one per face whose region is known (Town from the
    /// start, the rest once revealed), in die-number order: a pip per fragment, lit once found, and a gold frame once mended.
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

        private void OnGUI()
        {
            if (health == null) return;
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                textStyle.normal.textColor = Color.white;
                deathStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                deathStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
            }

            // Bottom left, just above the dialogue box's band (the bottom 150 px), so it never overlaps the debug
            // overlay (top left, about 156 px tall) or the dialogue box, at any width (the 960x600 WebGL canvas too).
            const float size = HeartSize, gap = 6f;
            int hearts = Mathf.CeilToInt(health.Max);
            float width = hearts * (size + gap) - gap;
            string rewardsLine = RewardsText;
            List<FaceDie> dice = FragmentDice;
            Rect back = PanelRect(Screen.width, Screen.height, hearts, rewardsLine != null, dice.Count > 0);
            float x = back.x + 8f, y = back.y + 6f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            for (int i = 0; i < hearts; i++)
            {
                float fill = Mathf.Clamp01(health.Current - i);
                var rect = new Rect(x + i * (size + gap), y, size, size);
                GUI.color = new Color(0.3f, 0.05f, 0.08f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                if (fill > 0f)
                {
                    GUI.color = new Color(1f, 0.25f, 0.35f);
                    GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fill, rect.height), Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(x + width + 10f, y - 2f, 120f, 26f), HealthText, textStyle);
            GUI.Label(new Rect(x, y + size + 4f, Mathf.Max(width, 300f), 26f), EquippedText, textStyle);
            if (rewardsLine != null)
                GUI.Label(new Rect(x, y + size + 28f, Mathf.Max(width + 130f, 420f), 26f), rewardsLine, textStyle);
            if (dice.Count > 0) DrawDice(dice, x, y + size + 28f + (rewardsLine != null ? 24f : 0f));

            string message = MessageText;
            if (message == null) return;
            var panel = new Rect(Screen.width / 2f - 320f, Screen.height / 2f - 60f, 640f, 120f);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(panel, message, deathStyle);
        }

        /// <summary>A heart's size (pixels).</summary>
        public const float HeartSize = 22f;

        /// <summary>
        /// The panel's rect on a screen (pixels, GUI y down; 4.19 review: layout tests): bottom left, above the dialogue band
        /// (the bottom 150 px), as tall as its rows (hearts and the item, the rewards line, the dice).
        /// </summary>
        public static Rect PanelRect(float screenWidth, float screenHeight, int hearts, bool rewardsLine, bool dice)
        {
            const float size = HeartSize, gap = 6f, margin = 12f, dialogueBand = 150f;
            float width = hearts * (size + gap) - gap;
            float x = margin + 8f;
            float extra = (rewardsLine ? 24f : 0f) + (dice ? DieRow : 0f);
            float y = screenHeight - dialogueBand - margin - (size + 40f + extra) + 6f;
            return new Rect(x - 8f, y - 6f, Mathf.Max(width + 130f, rewardsLine ? 420f : 300f) + 16f, size + 40f + extra);
        }

        /// <summary>Height of the fragments row (4.8).</summary>
        public const float DieRow = 30f;

        /// <summary>A die's size in the fragments row (pixels).</summary>
        public const float DieSize = 24f;

        private static readonly Color MendedGold = new Color(1f, 0.82f, 0.25f);

        /// <summary>The fragments row: its label, then a die per face (region-coloured frame, a pip per fragment).</summary>
        private void DrawDice(List<FaceDie> dice, float x, float y)
        {
            GUI.Label(new Rect(x, y + 1f, 100f, 26f), RewardText.FragmentsLabel, textStyle);
            float left = x + 100f;
            for (int i = 0; i < dice.Count; i++)
            {
                FaceDie d = dice[i];
                var box = new Rect(left + i * (DieSize + 8f), y + 3f, DieSize, DieSize);
                Color frame = d.Mended ? MendedGold : CubeWorld.ThemeColor(d.Theme);
                Fill(new Rect(box.x - 3f, box.y - 3f, box.width + 6f, box.height + 6f), Color.black);
                Fill(new Rect(box.x - 2f, box.y - 2f, box.width + 4f, box.height + 4f), frame);
                Fill(box, new Color(0.12f, 0.1f, 0.14f));
                Vector2Int[] pips = TownArt.PipLayouts[Mathf.Clamp(d.Count, 1, 6)];
                float cell = DieSize / 7f;
                for (int p = 0; p < pips.Length; p++)
                {
                    bool found = rewards != null && rewards.HasFragment(d.Face, p + 1);
                    Color pip = d.Mended ? MendedGold : found ? Color.white : new Color(0.4f, 0.38f, 0.45f);
                    // Pip (x, y) on the 7x7 face, y up; GUI y runs down.
                    var r = new Rect(box.x + pips[p].x * cell - cell * 0.25f, box.y + (6 - pips[p].y) * cell - cell * 0.25f, cell * 1.5f, cell * 1.5f);
                    Fill(r, pip);
                }
                if (d.HasGem)
                {
                    // 4.14: the face's gem, a diamond in its region's colour on the die's corner.
                    Color gem = TempleArt.GemColor(d.Theme);
                    var g = new Rect(box.xMax - 6f, box.y - 8f, 12f, 12f);
                    Fill(new Rect(g.x + 3f, g.y, 6f, 12f), Color.black);
                    Fill(new Rect(g.x, g.y + 3f, 12f, 6f), Color.black);
                    Fill(new Rect(g.x + 4f, g.y + 1f, 4f, 10f), gem);
                    Fill(new Rect(g.x + 1f, g.y + 4f, 10f, 4f), gem);
                    Fill(new Rect(g.x + 4f, g.y + 4f, 2f, 2f), Color.white);
                }
                else if (d.Unsealed)
                {
                    // The temple open: a pale bar under the die, waiting for its gem.
                    Fill(new Rect(box.x - 4f, box.yMax + 3f, box.width + 8f, 2f), new Color(0.85f, 0.85f, 1f));
                }
            }
        }

        private static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
