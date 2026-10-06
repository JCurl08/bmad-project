using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The decay stages of the Chemistry isotope, in order. None: no isotope held.</summary>
    public enum IsotopeStage
    {
        None = -1,

        /// <summary>Fresh: glows, and lights dark-room gates on touch.</summary>
        Glow = 0,

        /// <summary>Middle-aged: unstable, blasts cracked walls when swung.</summary>
        Unstable = 1,

        /// <summary>Spent: lead, holds a pressure plate down when dropped on it (and is used up). Lead stays lead.</summary>
        Lead = 2,
    }

    /// <summary>
    /// Curie's isotope as data: the Chemistry item (one logical item for ItemPlacement; its id stays the
    /// placeholder's "chemistry-item") and its decay timing. With half-life H, a fresh isotope glows for H seconds,
    /// is unstable until H * (1 + UnstableFraction), then is lead for good. The default H = 12 gives glow 0-12 s,
    /// unstable 12-20 s, then lead. H is the difficulty knob: shorter is harder.
    /// </summary>
    public static class ChemistryIsotope
    {
        /// <summary>The Chemistry item's id: the 1.4 placeholder's, kept stable.</summary>
        public const string Id = "chemistry-item";

        public const string Name = "Isotope";

        public const float DefaultHalfLife = 12f;
        public const float MinHalfLife = 0.5f;

        /// <summary>The unstable stage lasts this fraction of the half-life (8 s at the default 12 s).</summary>
        public const float UnstableFraction = 2f / 3f;

        public static readonly Color ItemColor = new Color(0.55f, 1f, 0.35f);
        public static readonly Color GlowColor = new Color(0.6f, 1f, 0.35f);
        public static readonly Color UnstableColor = new Color(1f, 0.45f, 0.15f);
        public static readonly Color LeadColor = new Color(0.5f, 0.52f, 0.58f);

        /// <summary>The age (seconds) at which the isotope stops glowing and turns unstable.</summary>
        public static float GlowEnds(float halfLife) => Mathf.Max(MinHalfLife, halfLife);

        /// <summary>The age (seconds) at which the isotope turns to lead.</summary>
        public static float UnstableEnds(float halfLife) => GlowEnds(halfLife) * (1f + UnstableFraction);

        /// <summary>The stage of an isotope of this age. Pure; lead from UnstableEnds on, for good.</summary>
        public static IsotopeStage StageAt(float age, float halfLife)
        {
            if (age < GlowEnds(halfLife)) return IsotopeStage.Glow;
            if (age < UnstableEnds(halfLife)) return IsotopeStage.Unstable;
            return IsotopeStage.Lead;
        }

        /// <summary>The first age at which an isotope is in a stage (0 for glow).</summary>
        public static float StageStart(IsotopeStage stage, float halfLife)
        {
            switch (stage)
            {
                case IsotopeStage.Unstable: return GlowEnds(halfLife);
                case IsotopeStage.Lead: return UnstableEnds(halfLife);
                default: return 0f;
            }
        }

        public static Color StageColor(IsotopeStage stage)
        {
            switch (stage)
            {
                case IsotopeStage.Glow: return GlowColor;
                case IsotopeStage.Unstable: return UnstableColor;
                case IsotopeStage.Lead: return LeadColor;
                default: return Color.grey;
            }
        }

        public static string StageName(IsotopeStage stage)
        {
            switch (stage)
            {
                case IsotopeStage.Glow: return "Glowing";
                case IsotopeStage.Unstable: return "Unstable";
                case IsotopeStage.Lead: return "Lead";
                default: return "None";
            }
        }

        /// <summary>What a stage does, as the HUD tells it.</summary>
        public static string StageUse(IsotopeStage stage)
        {
            switch (stage)
            {
                case IsotopeStage.Glow: return "lights dark rooms";
                case IsotopeStage.Unstable: return "swing it at cracked walls";
                case IsotopeStage.Lead: return "drop it on a plate";
                default: return "";
            }
        }

        /// <summary>An unsaved runtime isotope item (tests and tools); the catalog builder keeps the real asset.</summary>
        public static ItemDefinition CreateItem() => ItemDefinition.Create(Id, Name, Theme.Chemistry, ItemColor);
    }

    /// <summary>
    /// The player's held isotope: lives on the player beside its Inventory. Whenever the isotope item is added
    /// (a dispenser hands one out) it starts fresh at the glow stage and decays on its half-life timer while
    /// held: glow, then unstable, then lead, which stays until a plate takes it (ConsumeLead removes it from the
    /// inventory, so a dispenser can hand out another). The stage is what the equipped isotope does to the
    /// Chemistry gates. A halo around the player shows the stage, a label in the stage's colour names it just
    /// above the player (StageLabel), and a HUD line names it with a coloured pip.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class Isotope : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;

        [Tooltip("Difficulty: seconds of glow; unstable lasts 2/3 of it more, then lead. Shorter is harder.")]
        [SerializeField, Min(ChemistryIsotope.MinHalfLife)] private float halfLife = ChemistryIsotope.DefaultHalfLife;

        [SerializeField] private bool showHud = true;

        private Inventory inventory;
        private Equipment equipment;
        private float age;
        private int serial;
        private IsotopeStage shown = IsotopeStage.None;
        private SpriteRenderer halo;
        private GUIStyle hudStyle;
        private GUIStyle labelStyle;

        /// <summary>The isotope item this tracks (the run's Chemistry item).</summary>
        public ItemDefinition Item
        {
            get => item;
            set
            {
                item = value;
                Refresh();
            }
        }

        /// <summary>The half-life difficulty knob (seconds of glow).</summary>
        public float HalfLife
        {
            get => halfLife;
            set
            {
                halfLife = Mathf.Max(ChemistryIsotope.MinHalfLife, value);
                Refresh();
            }
        }

        public Inventory Inventory => inventory != null ? inventory : inventory = GetComponent<Inventory>();

        public bool IsHeld => item != null && Inventory.Has(item);

        /// <summary>True when the held isotope is the equipped item (what a swing carries).</summary>
        public bool IsEquipped
        {
            get
            {
                if (equipment == null) equipment = GetComponent<Equipment>();
                return IsHeld && equipment != null && equipment.Equipped == item;
            }
        }

        /// <summary>Seconds since the held isotope was handed out (settable, for tests and tools).</summary>
        public float Age
        {
            get => age;
            set
            {
                age = Mathf.Max(0f, value);
                Refresh();
            }
        }

        /// <summary>Counts isotopes handed out; tells one isotope from the next (the trial needs one isotope throughout).</summary>
        public int Serial => serial;

        /// <summary>The held isotope's stage, or None.</summary>
        public IsotopeStage Stage => IsHeld ? ChemistryIsotope.StageAt(age, halfLife) : IsotopeStage.None;

        /// <summary>Seconds until the next stage (0 once lead or when none is held).</summary>
        public float SecondsToNextStage
        {
            get
            {
                IsotopeStage stage = Stage;
                if (stage == IsotopeStage.Glow) return ChemistryIsotope.GlowEnds(halfLife) - age;
                if (stage == IsotopeStage.Unstable) return ChemistryIsotope.UnstableEnds(halfLife) - age;
                return 0f;
            }
        }

        /// <summary>Raised when the stage changes (including to None when the lead is used up).</summary>
        public event Action<Isotope, IsotopeStage> StageChanged;

        /// <summary>Raised when a fresh isotope is received.</summary>
        public event Action<Isotope> Received;

        /// <summary>The HUD line, or null when no isotope is held.</summary>
        public string HudText
        {
            get
            {
                IsotopeStage stage = Stage;
                if (stage == IsotopeStage.None) return null;
                string next = stage == IsotopeStage.Lead ? "" : $" ({Mathf.CeilToInt(SecondsToNextStage)}s)";
                return $"Isotope: {ChemistryIsotope.StageName(stage)}{next}, {ChemistryIsotope.StageUse(stage)}";
            }
        }

        public bool ShowHud
        {
            get => showHud;
            set => showHud = value;
        }

        /// <summary>The stage label shown just above the player ("GLOWING", "UNSTABLE", "LEAD"), or null when none is held.</summary>
        public string StageLabel
        {
            get
            {
                IsotopeStage stage = Stage;
                return stage == IsotopeStage.None ? null : ChemistryIsotope.StageName(stage).ToUpperInvariant();
            }
        }

        /// <summary>The stage label's colour (the stage's colour).</summary>
        public Color StageLabelColor => ChemistryIsotope.StageColor(Stage);

        private void OnEnable()
        {
            Inventory.ItemAdded += OnItemAdded;
            Inventory.ItemRemoved += OnItemRemoved;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory == null) return;
            inventory.ItemAdded -= OnItemAdded;
            inventory.ItemRemoved -= OnItemRemoved;
        }

        private void Update()
        {
            if (IsHeld) age += Time.deltaTime;
            Refresh();
        }

        private void OnItemAdded(ItemDefinition added)
        {
            if (item == null || added != item) return;
            age = 0f;
            serial++;
            shown = IsotopeStage.None; // a fresh isotope always announces its glow
            Received?.Invoke(this);
            Refresh();
        }

        private void OnItemRemoved(ItemDefinition removed)
        {
            if (item != null && removed == item) Refresh();
        }

        /// <summary>A new run (the inventory was emptied): the isotope clock restarts and the halo goes out.</summary>
        public void ResetForRun()
        {
            age = 0f;
            Refresh();
        }

        /// <summary>Uses up the held isotope if it is lead (a plate took it). Returns true if it did.</summary>
        public bool ConsumeLead()
        {
            if (Stage != IsotopeStage.Lead) return false;
            Inventory.Remove(item);
            Refresh();
            return true;
        }

        /// <summary>The isotope of whatever a collider belongs to (the player), or null.</summary>
        public static Isotope On(Collider2D collider)
        {
            Inventory inventory = ItemPickup.FindInventory(collider);
            return inventory != null ? inventory.GetComponent<Isotope>() : null;
        }

        /// <summary>The isotope of an attacker or toucher object, or null.</summary>
        public static Isotope On(GameObject owner) => owner != null ? owner.GetComponentInParent<Isotope>() : null;

        /// <summary>True if this isotope is item, held and in stage.</summary>
        public bool Is(ItemDefinition required, IsotopeStage stage) =>
            required != null && item == required && Stage == stage;

        private void Refresh()
        {
            IsotopeStage stage = Stage;
            if (stage == shown) return;
            shown = stage;
            ShowHalo(stage);
            StageChanged?.Invoke(this, stage);
        }

        private void ShowHalo(IsotopeStage stage)
        {
            if (halo == null)
            {
                if (stage == IsotopeStage.None) return;
                var visual = GetComponentInChildren<SpriteRenderer>();
                var go = new GameObject("Isotope Halo");
                go.transform.SetParent(transform, false);
                halo = go.AddComponent<SpriteRenderer>();
                halo.sprite = ArtCatalog.Shape(PartShape.Circle);
                halo.sortingOrder = 9;
                if (visual != null) halo.sharedMaterial = HitFlash.NormalMaterial(visual);
            }
            halo.enabled = stage != IsotopeStage.None;
            Color color = ChemistryIsotope.StageColor(stage);
            color.a = stage == IsotopeStage.Glow ? 0.35f : 0.2f;
            halo.color = color;
            float size = stage == IsotopeStage.Glow ? 2.6f : stage == IsotopeStage.Unstable ? 1.5f : 1.1f;
            halo.transform.localScale = new Vector3(size, size, 1f);
        }

        private void OnGUI()
        {
            if (!showHud) return;
            DrawStageLabel();
            string text = HudText;
            if (text == null) return;
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                hudStyle.normal.textColor = Color.white;
            }
            // Just above the CombatHud panel (bottom left, above the dialogue band).
            const float margin = 12f, dialogueBand = 150f, combatPanel = 62f, pip = 16f;
            float x = margin;
            float y = Screen.height - dialogueBand - margin - combatPanel - 34f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, 420f, 28f), Texture2D.whiteTexture);
            GUI.color = ChemistryIsotope.StageColor(Stage);
            GUI.DrawTexture(new Rect(x + 8f, y + 6f, pip, pip), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f + pip + 8f, y + 2f, 400f, 26f), text, hudStyle);
        }

        /// <summary>The stage, in its colour, on a dark tag just above the player's head.</summary>
        private void DrawStageLabel()
        {
            string label = StageLabel;
            Camera cam = Camera.main;
            if (label == null || cam == null) return;
            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.75f);
            if (screen.z < 0f) return;
            var rect = new Rect(screen.x - 46f, Screen.height - screen.y - 22f, 92f, 20f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = StageLabelColor;
            GUI.Label(rect, label, labelStyle);
            GUI.color = Color.white;
        }
    }
}
