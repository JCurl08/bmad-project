using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A flower linked to a FlowerVineGate on an adjacent screen. A thin-beak swing that reaches it pollinates
    /// it; when its vine has grown (the gate opened) the flower is used and further swings do nothing. Walking
    /// over it does nothing.
    /// </summary>
    public class PollinationFlower : MonoBehaviour, IAttackReceiver
    {
        public const float Radius = 0.4f;
        public static readonly Color BudColor = new Color(0.95f, 0.55f, 0.85f);
        public static readonly Color BloomColor = new Color(1f, 0.9f, 0.3f);

        [SerializeField] private SpriteRenderer petals;

        public FlowerVineGate Vine { get; set; }

        /// <summary>True once the flower has grown its vine; a used flower ignores swings.</summary>
        public bool Used => Vine != null && Vine.IsOpen;

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (Vine == null || Used) return false;
            return Vine.Pollinate(item);
        }

        public void ShowBloom()
        {
            if (petals == null) return;
            petals.color = BloomColor;
            petals.transform.localScale = new Vector3(Radius * 2.6f, Radius * 2.6f, 1f);
        }

        public static PollinationFlower Create(Transform parent, Vector2 world, ItemDefinition item, Material material)
        {
            var go = new GameObject($"Flower ({item})");
            go.transform.SetParent(parent, false);
            go.transform.position = world;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Radius;
            BeakGate.AddSprite(go.transform, "Stem", PartShape.Square, new Vector2(0f, -0.35f), new Vector2(0.12f, 0.5f),
                FlowerVineGate.VineColor, -3, material);
            var flower = go.AddComponent<PollinationFlower>();
            flower.petals = BeakGate.AddSprite(go.transform, "Petals", PartShape.Diamond, Vector2.zero,
                new Vector2(Radius * 2f, Radius * 2f), BudColor, -2, material);
            Color centre = item != null ? item.PlaceholderColor : Color.white;
            BeakGate.AddSprite(go.transform, "Centre", PartShape.Circle, Vector2.zero, new Vector2(0.25f, 0.25f), centre, -1, material);
            return flower;
        }
    }
}
