using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The distant button of a ButtonGate: a small trigger on a post that only a peck counts on (walking over
    /// it does nothing). A swing that reaches it passes the carried item to its door, which opens only for the
    /// thin beak. Out of reach, nothing happens: the swing never touches it.
    /// </summary>
    public class BeakButton : MonoBehaviour, IAttackReceiver
    {
        public const float Radius = 0.35f;
        public static readonly Color UpColor = new Color(0.95f, 0.3f, 0.35f);
        public static readonly Color DownColor = new Color(0.35f, 0.85f, 0.4f);

        [SerializeField] private SpriteRenderer cap;

        public ButtonGate Door { get; set; }

        public bool IsPressed => Door != null && Door.IsOpen;

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (Door == null || Door.IsOpen) return false;
            return Door.Press(item);
        }

        public void ShowPressed()
        {
            if (cap == null) return;
            cap.color = DownColor;
            cap.transform.localScale = new Vector3(Radius * 1.2f, Radius * 0.7f, 1f);
        }

        public static BeakButton Create(Transform parent, Vector2 world, ItemDefinition item, Material material)
        {
            var go = new GameObject($"Beak Button ({item})");
            go.transform.SetParent(parent, false);
            go.transform.position = world;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Radius;
            Color ring = item != null ? Color.Lerp(Color.white, item.PlaceholderColor, 0.35f) : Color.white;
            ArtCatalog.AddSprite(go.transform, "Post", ArtKey.BeakButton, Vector2.zero, new Vector2(Radius * 2.6f, Radius * 2.6f), ring, -3,
                material);
            var button = go.AddComponent<BeakButton>();
            button.cap = BeakGate.AddSprite(go.transform, "Cap", PartShape.Circle, Vector2.zero,
                new Vector2(Radius * 1.1f, Radius * 1.1f), UpColor, -2, material);
            return button;
        }
    }
}
