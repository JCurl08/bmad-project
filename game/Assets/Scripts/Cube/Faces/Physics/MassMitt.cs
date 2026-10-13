using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// The Heavy Mitt* (code: the Mass Mitt) as data: the Physics item (one logical item for ItemPlacement; its id stays the 1.4
    /// placeholder's "physics-item"). With it equipped the player can grab boulders and drag them.
    /// </summary>
    public static class MassMittItem
    {
        /// <summary>The Physics item's id: the 1.4 placeholder's, kept stable.</summary>
        public const string Id = "physics-item";

        public const string Name = "Heavy Mitt*";

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
    /// 4.19 (playtest 5): Interact while holding a boulder always drops it, whichever way the player faces, and never
    /// refuses: the press is the mitt's (an NPC in talking range yields to it), and the boulder is set down where it is when
    /// that spot is free, else at the nearest free spot round the player (DropSpot). Grabbing is unchanged.
    /// </summary>
    [RequireComponent(typeof(Inventory), typeof(Rigidbody2D))]
    public class MassMitt : MonoBehaviour
    {
        /// <summary>Boulders whose centres are this close to the player's can be grabbed.</summary>
        public const float GrabRange = 1.8f * ScreenMath.ContentScale;

        /// <summary>A held boulder this far from where it should be (stuck) is let go.</summary>
        public const float StuckDistance = 1.0f * ScreenMath.ContentScale;

        /// <summary>A held boulder this far from the player is let go.</summary>
        public const float LetGoDistance = 2.6f * ScreenMath.ContentScale;

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

        private bool templeGrip;

        /// <summary>
        /// The Physics temple lends its grip (4.14; assumption: the temple's puzzle never needs the face's item): while set,
        /// Interact grabs a boulder as if the mitt were equipped. TempleRooms sets it inside the Physics temple only.
        /// </summary>
        public bool TempleGrip
        {
            get => templeGrip;
            set
            {
                if (templeGrip && !value) Release();
                templeGrip = value;
            }
        }

        /// <summary>True when the player holds the mitt (or the temple lends its grip).</summary>
        public bool IsHeld => templeGrip || (item != null && Inventory.Has(item));

        /// <summary>True when the mitt is held and equipped (what grabbing needs), or the temple lends its grip.</summary>
        public bool IsEquipped
        {
            get
            {
                if (templeGrip) return true;
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
                // 4.14 review: the temple's grip without the mitt says what to do, not the mitt's name.
                if (templeGrip && !(item != null && Inventory.Has(item))) return held != null ? TempleDropText : TempleGrabText;
                if (!IsHeld) return null;
                if (held != null) return DraggingText;
                if (!IsEquipped) return EquipText;
                return ReadyText;
            }
        }

        public const string DraggingText = MassMittItem.Name + ": dragging a boulder ([Interact] to let go)";
        public const string EquipText = MassMittItem.Name + ": equip it to grab boulders";
        public const string ReadyText = MassMittItem.Name + ": [Interact] or swing at a boulder to grab it";

        public const string TempleGrabText = "[Interact] grab a boulder";
        public const string TempleDropText = "[Interact] drop the boulder";

        /// <summary>Every HUD line the mitt can show (for the text checks).</summary>
        public static readonly string[] AllHudText = { DraggingText, EquipText, ReadyText, TempleGrabText, TempleDropText };

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
            if (GamePause.InputBlocked) return; // the map, the quest log or a conversation is open: gameplay is paused
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
            if (held != null) return Drop(); // 4.19 review: a swing drops it as E does (set down at a free spot)
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

        /// <summary>An Interact press: drops the held boulder (Drop), else grabs the nearest one in range. Returns true if it did either.</summary>
        public bool Interact()
        {
            if (held != null) return Drop();
            Boulder nearest = Nearest();
            return nearest != null && Grab(nearest);
        }

        /// <summary>Rings tried round the player for a drop: from just clear of the player, DropStep apart, out to DropReach.</summary>
        public const float DropStep = 0.3f, DropReach = 3.6f;

        /// <summary>Directions tried on each ring.</summary>
        public const int DropDirections = 16;

        /// <summary>Where the last drop set the boulder down (world).</summary>
        public Vector2 LastDropSpot { get; private set; }

        /// <summary>
        /// 4.19: lets go of the held boulder and sets it down (DropSpot), whichever way the player faces. Never refuses:
        /// with no free spot at all it stays where it is. True when a boulder was held.
        /// </summary>
        public bool Drop()
        {
            Boulder boulder = held;
            if (boulder == null) return false;
            Release();
            Vector2 spot = DropSpot(boulder);
            if ((spot - boulder.Position).sqrMagnitude > 1e-8f) boulder.Teleport(spot);
            LastDropSpot = spot;
            return true;
        }

        /// <summary>
        /// Where a dropped boulder goes: where it is, when that spot is free (FreeForBoulder) and within GrabRange; else the
        /// nearest free spot round the player (rings outward from just clear of the player's body, on each ring the direction
        /// nearest the boulder's own first) with a clear line from the player; else where it is.
        /// </summary>
        public Vector2 DropSpot(Boulder boulder)
        {
            Vector2 at = boulder.Position, me = Body.position;
            if (Vector2.Distance(at, me) <= GrabRange && FreeForBoulder(boulder, at)) return at;
            float start = PlayerReach() + Boulder.Radius + 0.05f;
            Vector2 toward = at - me;
            float baseAngle = toward.sqrMagnitude > 1e-6f ? Mathf.Atan2(toward.y, toward.x) : -Mathf.PI / 2f;
            for (float r = start; r <= DropReach + 1e-4f; r += DropStep)
                for (int k = 0; k < DropDirections; k++)
                {
                    // 0, +1, -1, +2, -2 ... steps from the boulder's own direction.
                    int step = (k + 1) / 2 * (k % 2 == 1 ? 1 : -1);
                    float a = baseAngle + step * (2f * Mathf.PI / DropDirections);
                    Vector2 p = me + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (FreeForBoulder(boulder, p) && LineClear(boulder, me, p)) return p;
                }
            return at;
        }

        /// <summary>The player's body radius (its widest collider's extent), for the first drop ring.</summary>
        private float PlayerReach()
        {
            float reach = 0f;
            foreach (Collider2D c in Colliders)
                if (c != null && !c.isTrigger) reach = Mathf.Max(reach, Mathf.Max(c.bounds.extents.x, c.bounds.extents.y));
            return reach > 0f ? reach : TimeField.PlayerRadius;
        }

        /// <summary>
        /// True when a boulder could stand at p: inside its bounds, and its circle touching nothing solid (walls, doors,
        /// other boulders, people, the player) nor a timed door's doorway (BoulderBlocker).
        /// </summary>
        public static bool FreeForBoulder(Boulder boulder, Vector2 p)
        {
            if (boulder.Bounds.HasValue && !boulder.Bounds.Value.Contains(p)) return false;
            foreach (Collider2D c in Physics2D.OverlapCircleAll(p, Boulder.Radius * 0.98f))
            {
                if (c == null || !c.enabled || c == boulder.Circle) continue;
                if (c.isTrigger && c.GetComponent<BoulderBlocker>() == null) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// True when the boulder could be carried from the player's centre to a spot (4.19 review): its circle cast along the
        /// way meets nothing solid but the player and itself, nor a timed door's doorway (BoulderBlocker), as Boulder.Follow
        /// moves it. Something already touching at the start blocks only a move deeper into it.
        /// </summary>
        private bool LineClear(Boulder boulder, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-4f) return true;
            Vector2 dir = delta / distance;
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(from, Boulder.Radius * 0.98f, dir, distance))
            {
                Collider2D c = hit.collider;
                if (c == null || !c.enabled || c == boulder.Circle) continue;
                if (c.isTrigger && c.GetComponent<BoulderBlocker>() == null) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody == Body) continue;
                if (hit.distance <= 0f)
                {
                    Vector2 toOther = c.ClosestPoint(from) - from;
                    if (toOther.sqrMagnitude < 1e-8f) toOther = (Vector2)c.bounds.center - from;
                    if (Vector2.Dot(dir, toOther) <= 0f) continue;
                }
                return false;
            }
            return true;
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

        /// <summary>True when an Interact press would act this frame (equipped and holding a boulder, or one within GrabRange; 4.9: locks yield to it).</summary>
        public bool WouldInteract => IsEquipped && (held != null || Nearest() != null);

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
            Vector2 view = HudScale.Begin(); // 4.21: the shared scale
            text = InputPrompts.Fill(text); // "[Interact]" shows the real key or button
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                hudStyle.normal.textColor = Color.white;
            }
            // Above the Chemistry stone's HUD line (bottom left, above the combat panel and the dialogue band).
            const float margin = 12f, dialogueBand = 150f, combatPanel = 62f, pip = 16f;
            float x = margin;
            float y = view.y - dialogueBand - margin - combatPanel - 66f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, 470f, 28f), Texture2D.whiteTexture);
            GUI.color = MassMittItem.ItemColor;
            GUI.DrawTexture(new Rect(x + 8f, y + 6f, pip, pip), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f + pip + 8f, y + 2f, 450f, 26f), text, hudStyle);
        }
    }
}
