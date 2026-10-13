using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The Chemistry stone stages (one is rolled per run). None: no stone held.</summary>
    public enum IsotopeStage
    {
        None = -1,

        /// <summary>The Glowstone*: lights dark-room gates when swung at them.</summary>
        Glow = 0,

        /// <summary>The Wildstone*: blasts cracked walls when swung at them.</summary>
        Unstable = 1,

        /// <summary>The Leadstone*: weighs pressure plates down when swung at them (it is not used up).</summary>
        Lead = 2,
    }

    /// <summary>
    /// The Chemistry item as data, on the beak pattern (story 1.8): three stage variants (Glowstone*, Wildstone*,
    /// Leadstone*), listed in the catalog in VariantOrder; each run rolls one (ItemVariants.Roll), it is picked up once,
    /// kept for the run and used by swinging it. The stage of an item is always read from its id (KindOf), never from
    /// the catalog's order. The 1.4 placeholder entry ("chemistry-item") stays as the theme's base item, so the placer
    /// still sees one logical Chemistry item. In-world, the stones turn into one another between visits; within a run
    /// a stone never changes.
    /// </summary>
    public static class ChemistryIsotope
    {
        /// <summary>The Chemistry base item's id: the 1.4 placeholder's, kept stable.</summary>
        public const string Id = "chemistry-item";

        /// <summary>The base item's name (never shown in a run: the rolled stone replaces it).</summary>
        public const string Name = "Changing Stone*";

        public const string GlowId = "glowstone";
        public const string UnstableId = "wildstone";
        public const string LeadId = "leadstone";

        public const string GlowName = "Glowstone*";
        public const string UnstableName = "Wildstone*";
        public const string LeadName = "Leadstone*";

        public static readonly Color ItemColor = new Color(0.75f, 0.7f, 0.85f);
        public static readonly Color GlowColor = new Color(0.6f, 1f, 0.35f);
        public static readonly Color UnstableColor = new Color(1f, 0.45f, 0.15f);
        public static readonly Color LeadColor = new Color(0.5f, 0.52f, 0.58f);

        /// <summary>The order the catalog builder lists the stones in (0 Glowstone*, 1 Wildstone*, 2 Leadstone*). Consumers
        /// never read a stage from it: they read it from the item id (KindOf, VariantKinds).</summary>
        public static readonly IsotopeStage[] VariantOrder = { IsotopeStage.Glow, IsotopeStage.Unstable, IsotopeStage.Lead };

        public static int VariantCount => VariantOrder.Length;

        public static string IdOf(IsotopeStage stage)
        {
            switch (stage)
            {
                case IsotopeStage.Glow: return GlowId;
                case IsotopeStage.Unstable: return UnstableId;
                case IsotopeStage.Lead: return LeadId;
                default: return null;
            }
        }

        /// <summary>The stage an item is, from its id; null for anything else (the base item included).</summary>
        public static IsotopeStage? KindOf(ItemDefinition item)
        {
            if (item == null) return null;
            switch (item.Id)
            {
                case GlowId: return IsotopeStage.Glow;
                case UnstableId: return IsotopeStage.Unstable;
                case LeadId: return IsotopeStage.Lead;
                default: return null;
            }
        }

        /// <summary>True if an item is a Chemistry stone of this stage.</summary>
        public static bool Is(ItemDefinition item, IsotopeStage stage) => stage != IsotopeStage.None && KindOf(item) == stage;

        /// <summary>
        /// The stage of each Chemistry variant in a catalog, in variant-index order, read from the items' ids (so the
        /// catalog's order never matters). Null when the catalog has no Chemistry variants or one is not a stone.
        /// </summary>
        public static List<IsotopeStage> VariantKinds(ItemCatalog catalog) => ItemVariants.KindsOf<IsotopeStage>(catalog, Theme.Chemistry, KindOf);

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

        /// <summary>The stone's in-world name ("Glowstone*", "Wildstone*", "Leadstone*").</summary>
        public static string StageName(IsotopeStage stage)
        {
            switch (stage)
            {
                case IsotopeStage.Glow: return GlowName;
                case IsotopeStage.Unstable: return UnstableName;
                case IsotopeStage.Lead: return LeadName;
                default: return "None";
            }
        }

        /// <summary>What a stone does, as the HUD tells it.</summary>
        public static string StageUse(IsotopeStage stage)
        {
            switch (stage)
            {
                case IsotopeStage.Glow: return "swing it at dark doorways";
                case IsotopeStage.Unstable: return "swing it at cracked walls";
                case IsotopeStage.Lead: return "swing it at pressure plates";
                default: return "";
            }
        }

        /// <summary>Every in-world string the Chemistry item system shows (names and uses), for the text checks.</summary>
        public static IEnumerable<string> AllText()
        {
            yield return Name;
            foreach (IsotopeStage stage in VariantOrder)
            {
                yield return StageName(stage);
                yield return StageUse(stage);
            }
        }

        /// <summary>An unsaved runtime base item (tests and tools); the catalog builder keeps the real asset.</summary>
        public static ItemDefinition CreateItem() => ItemDefinition.Create(Id, Name, Theme.Chemistry, ItemColor);

        /// <summary>An unsaved runtime stone of a stage (tests and tools); the catalog builder makes the real assets.</summary>
        public static ItemDefinition CreateItem(IsotopeStage stage) =>
            ItemDefinition.Create(IdOf(stage), StageName(stage), Theme.Chemistry, StageColor(stage));
    }

    /// <summary>
    /// The player's held Chemistry stone, read from the Inventory (no timer, no state of its own): the stage of the stone
    /// the player holds, if any, and whether it is the equipped item (what a swing carries). A halo around the player
    /// shows the stone's colour, a label just above the player names it (StageLabel: the item's name), and a HUD line
    /// names it with what it does and a coloured pip. The stone never changes during a run.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class Isotope : MonoBehaviour
    {
        [SerializeField] private bool showHud = true;

        private Inventory inventory;
        private Equipment equipment;
        private IsotopeStage shown = IsotopeStage.None;
        private SpriteRenderer halo;
        private GUIStyle hudStyle;
        private GUIStyle labelStyle;

        public Inventory Inventory => inventory != null ? inventory : inventory = GetComponent<Inventory>();

        /// <summary>The Chemistry stone the player holds (the first in the inventory), or null.</summary>
        public ItemDefinition Item
        {
            get
            {
                foreach (ItemDefinition held in Inventory.Items)
                    if (ChemistryIsotope.KindOf(held) != null) return held;
                return null;
            }
        }

        public bool IsHeld => Item != null;

        /// <summary>True when the held stone is the equipped item (what a swing carries).</summary>
        public bool IsEquipped
        {
            get
            {
                if (equipment == null) equipment = GetComponent<Equipment>();
                ItemDefinition item = Item;
                return item != null && equipment != null && equipment.Equipped == item;
            }
        }

        /// <summary>The held stone's stage, or None. Read from the item's id; it never changes on its own.</summary>
        public IsotopeStage Stage => ChemistryIsotope.KindOf(Item) ?? IsotopeStage.None;

        /// <summary>Raised when the held stage changes (a stone picked up, or the inventory emptied for a new run).</summary>
        public event Action<Isotope, IsotopeStage> StageChanged;

        /// <summary>The HUD line ("Glowstone*: swing it at dark doorways"), or null when no stone is held.</summary>
        public string HudText
        {
            get
            {
                ItemDefinition item = Item;
                if (item == null) return null;
                return $"{item}: {ChemistryIsotope.StageUse(Stage)}";
            }
        }

        public bool ShowHud
        {
            get => showHud;
            set => showHud = value;
        }

        /// <summary>The label shown just above the player: the held stone's name, or null when none is held.</summary>
        public string StageLabel => Item?.ToString();

        /// <summary>The stage label's colour (the stone's colour).</summary>
        public Color StageLabelColor => ChemistryIsotope.StageColor(Stage);

        private void OnEnable()
        {
            Inventory.ItemAdded += OnInventoryChanged;
            Inventory.ItemRemoved += OnInventoryChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory == null) return;
            inventory.ItemAdded -= OnInventoryChanged;
            inventory.ItemRemoved -= OnInventoryChanged;
        }

        // Inventory.Clear (a new run) raises no event, so the halo is also kept in step here.
        private void Update() => Refresh();

        private void OnInventoryChanged(ItemDefinition item)
        {
            if (ChemistryIsotope.KindOf(item) != null) Refresh();
        }

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
            color.a = stage == IsotopeStage.Glow ? 0.3f : 0.2f;
            halo.color = color;
            float size = (stage == IsotopeStage.Glow ? 2.2f : 1.3f) * ScreenMath.ContentScale;
            halo.transform.localScale = new Vector3(size, size, 1f);
        }

        private void OnGUI()
        {
            if (!showHud) return;
            Vector2 view = HudScale.Begin(); // 4.21: the shared scale
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
            float y = view.y - dialogueBand - margin - combatPanel - 34f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, 420f, 28f), Texture2D.whiteTexture);
            GUI.color = ChemistryIsotope.StageColor(Stage);
            GUI.DrawTexture(new Rect(x + 8f, y + 6f, pip, pip), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f + pip + 8f, y + 2f, 400f, 26f), text, hudStyle);
        }

        /// <summary>The stone's name, in its colour, on a dark tag just above the player's head.</summary>
        private void DrawStageLabel()
        {
            string label = StageLabel;
            Camera cam = Camera.main;
            if (label == null || cam == null) return;
            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Vector2? at = HudScale.WorldToGui(cam, transform.position + Vector3.up * (0.75f * ScreenMath.ContentScale));
            if (at == null) return;
            var rect = new Rect(at.Value.x - 52f, at.Value.y - 22f, 104f, 20f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = StageLabelColor;
            GUI.Label(rect, label, labelStyle);
            GUI.color = Color.white;
        }
    }
}
