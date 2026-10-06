using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A floor switch linked to a TimedDoorGate. Stepping on it (the player: anything with an Inventory) opens the
    /// door, and the door's clock restarts every step the player stays on it and once more as the player steps off,
    /// so the door's open time always counts from the moment the player leaves the switch. Swings, boulders, NPCs
    /// and enemies do nothing to it.
    /// </summary>
    public class DoorSwitch : MonoBehaviour
    {
        public static readonly Color PadColor = new Color(0.3f, 0.45f, 0.7f);
        public static readonly Color PressedColor = new Color(0.65f, 0.85f, 1f);

        [SerializeField] private TimedDoorGate door;
        [SerializeField] private SpriteRenderer top;

        private int touching;

        public TimedDoorGate Door
        {
            get => door;
            set => door = value;
        }

        /// <summary>Times the switch has been stepped on.</summary>
        public int Presses { get; private set; }

        public bool IsPressed => touching > 0;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            touching++;
            Presses++;
            Press(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (IsPlayer(other)) Press(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            touching = Mathf.Max(0, touching - 1);
            Press(other);
            if (top != null && touching == 0) top.color = PadColor;
        }

        private static bool IsPlayer(Collider2D other) => other != null && !other.isTrigger && ItemPickup.FindInventory(other) != null;

        /// <summary>The player's actual speed (PlayerMover.EffectiveSpeed, with speed points); base speed without a mover.</summary>
        public static float PlayerSpeed(Collider2D player)
        {
            Inventory inventory = ItemPickup.FindInventory(player);
            var mover = inventory != null ? inventory.GetComponent<Game.Tracer.PlayerMover>() : null;
            return mover != null ? mover.EffectiveSpeed : TimeField.BasePlayerSpeed;
        }

        private void Press(Collider2D player)
        {
            if (door != null) door.Trigger(PlayerSpeed(player));
            if (top != null) top.color = PressedColor;
        }

        public static DoorSwitch Create(Transform parent, Vector2 world, TimedDoorGate door, Material material)
        {
            var go = new GameObject("Door Switch");
            go.transform.SetParent(parent, false);
            go.transform.position = world;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = TimeField.SwitchRadius;
            BeakGate.AddSprite(go.transform, "Rim", PartShape.Circle, Vector2.zero,
                new Vector2(TimeField.SwitchRadius * 2.3f, TimeField.SwitchRadius * 2.3f), new Color(0.15f, 0.18f, 0.25f), -3, material);
            var sw = go.AddComponent<DoorSwitch>();
            sw.door = door;
            sw.top = BeakGate.AddSprite(go.transform, "Top", PartShape.Circle, Vector2.zero,
                new Vector2(TimeField.SwitchRadius * 1.7f, TimeField.SwitchRadius * 1.7f), PadColor, -2, material);
            // A clock hand: this switch starts a door's clock.
            BeakGate.AddSprite(go.transform, "Hand", PartShape.Square, new Vector2(0f, 0.09f), new Vector2(0.05f, 0.2f),
                new Color(0.1f, 0.1f, 0.15f), -1, material);
            return sw;
        }
    }
}
