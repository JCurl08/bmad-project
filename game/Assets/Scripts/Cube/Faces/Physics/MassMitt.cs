using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// Einstein's Mass Mitt as data: the Physics item (one logical item for ItemPlacement; its id stays the 1.4
    /// placeholder's "physics-item"). With it equipped the player can grab boulders and drag them.
    /// </summary>
    public static class MassMittItem
    {
        /// <summary>The Physics item's id: the 1.4 placeholder's, kept stable.</summary>
        public const string Id = "physics-item";

        public const string Name = "Mass Mitt";

        public static readonly Color ItemColor = new Color(0.45f, 0.75f, 1f);

        /// <summary>An unsaved runtime mitt item (tests and tools); the catalog builder keeps the real asset.</summary>
        public static ItemDefinition CreateItem() => ItemDefinition.Create(Id, Name, Theme.Physics, ItemColor);
    }

    /// <summary>
    /// The player's Mass Mitt: lives on the player beside its Inventory and Equipment. With the mitt item equipped,
    /// a press of Player/Interact grabs the nearest boulder within GrabRange (or lets go of the held one), and a
    /// swing with the mitt does the same to a boulder it hits (Boulder is an IAttackReceiver). A held boulder
    /// follows the player (Boulder), and is let go on a second press, when the mitt is no longer equipped, or when
    /// the player gets too far from it (stuck on a wall, or the player left the screen). Without the mitt a boulder
    /// cannot be moved at all. A HUD line says what the mitt is doing.
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(Rigidbody2D))]
    public class MassMitt : MonoBehaviour
    {
        /// <summary>Boulders whose centres are this close to the player's can be grabbed.</summary>
        public const float GrabRange = 1.8f;

        /// <summary>A held boulder this far from where it should be (stuck) is let go.</summary>
        public const float StuckDistance = 1.0f;

        /// <summary>A held boulder this far from the player is let go.</summary>
        public const float LetGoDistance = 2.6f;

        [SerializeField] private ItemDefinition item;
        [SerializeField] private string interactActionPath = "Player/Interact";
        [SerializeField] private bool showHud = true;

        private Inventory inventory;
        private Equipment equipment;
        private Rigidbody2D body;
        private Collider2D[] colliders;
        private InputAction interact;
        private Boulder held;
        private int handledSwing = -1;
        private GUIStyle hudStyle;

        /// <summary>The mitt item this works with (the run's Physics item).</summary>
        public ItemDefinition Item
        {
            get => item;
            set
            {
                if (item != value) Release();
                item = value;
            }
        }

        public bool ShowHud
        {
            get => showHud;
            set => showHud = value;
        }

        public Inventory Inventory => inventory != null ? inventory : inventory = GetComponent<Inventory>();
        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();

        /// <summary>The player's own colliders (a held boulder ignores them).</summary>
        public Collider2D[] Colliders => colliders != null ? colliders : colliders = GetComponentsInChildren<Collider2D>();

        /// <summary>True when the player holds the mitt.</summary>
        public bool IsHeld => item != null && Inventory.Has(item);

        /// <summary>True when the mitt is held and equipped (what grabbing needs).</summary>
        public bool IsEquipped
        {
            get
            {
                if (equipment == null) equipment = GetComponent<Equipment>();
                return IsHeld && (equipment == null || equipment.Equipped == item);
            }
        }

        /// <summary>The boulder being dragged, or null.</summary>
        public Boulder Held => held;

        /// <summary>The HUD line, or null when the mitt is not held.</summary>
        public string HudText
        {
            get
            {
                if (!IsHeld) return null;
                if (held != null) return "Mass Mitt: dragging a boulder ([Interact] to let go)";
                if (!IsEquipped) return "Mass Mitt: equip it to grab boulders";
                return "Mass Mitt: [Interact] or swing at a boulder to grab it";
            }
        }

        private void OnEnable()
        {
            interact = NpcInput.Find(interactActionPath, this);
        }

        private void OnDisable()
        {
            interact = null; // shared project-wide action: never disabled here
            Release();
        }

        private void Update()
        {
            if (held != null && (!IsEquipped || !held.isActiveAndEnabled)) Release();
        }

        private void LateUpdate()
        {
            // After every Update, so a press that opened or closed a conversation this frame (whatever the script
            // order) is seen as the conversation's and never also grabs or lets go of a boulder.
            if (interact == null || !interact.WasPressedThisFrame()) return;
            DialogueBox box = DialogueBox.Shared;
            if (box != null && (box.IsOpen || box.OpenedFrame == Time.frameCount || box.ClosedFrame == Time.frameCount)) return;
            Interact();
        }

        /// <summary>
        /// A swing with the mitt reached a boulder. Each swing acts on at most one boulder: holding one, the swing lets
        /// go of it; otherwise it grabs the boulder in the swing's hitbox nearest the player. Later boulders of the same
        /// swing are ignored. Returns true for the boulder the swing acted on.
        /// </summary>
        public bool SwingAt(Boulder boulder, PlayerAttack attack)
        {
            if (boulder == null || !IsEquipped) return false;
            if (attack != null)
            {
                if (attack.SwingCount == handledSwing) return false;
                handledSwing = attack.SwingCount;
            }
            if (held != null)
            {
                Release();
                return true;
            }
            Boulder target = attack != null ? NearestInHitbox(attack) : null;
            return Grab(target != null ? target : boulder);
        }

        /// <summary>The enabled boulder in the swing's hitbox nearest the player, or null.</summary>
        private Boulder NearestInHitbox(PlayerAttack attack)
        {
            attack.Hitbox(out float distance, out Vector2 size);
            Vector2 facing = attack.Facing;
            Vector2 centre = Body.position + facing * distance;
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            Boulder best = null;
            float bestDistance = float.MaxValue;
            foreach (Collider2D c in Physics2D.OverlapBoxAll(centre, size, angle))
            {
                Boulder b = c != null ? c.GetComponent<Boulder>() : null;
                if (b == null || !b.isActiveAndEnabled) continue;
                float d = Vector2.Distance(b.Position, Body.position);
                if (d >= bestDistance) continue;
                best = b;
                bestDistance = d;
            }
            return best;
        }

        /// <summary>An Interact press: lets go of the held boulder, else grabs the nearest one in range. Returns true if it did either.</summary>
        public bool Interact()
        {
            if (held != null)
            {
                Release();
                return true;
            }
            Boulder nearest = Nearest();
            return nearest != null && Grab(nearest);
        }

        /// <summary>A swing (or press) aimed at a boulder: lets go of it if held, else grabs it. Returns true if it did either.</summary>
        public bool Toggle(Boulder boulder)
        {
            if (boulder == null) return false;
            if (held == boulder)
            {
                Release();
                return true;
            }
            return Grab(boulder);
        }

        /// <summary>Grabs a boulder in range with the mitt equipped. Without the mitt nothing happens. Returns true if it did.</summary>
        public bool Grab(Boulder boulder)
        {
            if (boulder == null || !boulder.isActiveAndEnabled || !IsEquipped) return false;
            if (Vector2.Distance(boulder.Position, Body.position) > GrabRange) return false;
            if (held != null && held != boulder) Release();
            held = boulder;
            boulder.AttachTo(this);
            return true;
        }

        /// <summary>Lets go of the held boulder (if any).</summary>
        public void Release()
        {
            if (held == null) return;
            Boulder was = held;
            held = null;
            if (was != null) was.Detach(this);
        }

        /// <summary>The nearest enabled boulder within GrabRange, or null.</summary>
        public Boulder Nearest()
        {
            Boulder best = null;
            float bestDistance = GrabRange;
            foreach (Boulder b in Boulder.All)
            {
                if (b == null) continue;
                float d = Vector2.Distance(b.Position, Body.position);
                if (d > bestDistance) continue;
                best = b;
                bestDistance = d;
            }
            return best;
        }

        private void OnGUI()
        {
            if (!showHud) return;
            string text = HudText;
            if (text == null) return;
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                hudStyle.normal.textColor = Color.white;
            }
            // Above the isotope's HUD line (bottom left, above the combat panel and the dialogue band).
            const float margin = 12f, dialogueBand = 150f, combatPanel = 62f, pip = 16f;
            float x = margin;
            float y = Screen.height - dialogueBand - margin - combatPanel - 66f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, 470f, 28f), Texture2D.whiteTexture);
            GUI.color = MassMittItem.ItemColor;
            GUI.DrawTexture(new Rect(x + 8f, y + 6f, pip, pip), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f + pip + 8f, y + 2f, 450f, 26f), text, hudStyle);
        }
    }
}
