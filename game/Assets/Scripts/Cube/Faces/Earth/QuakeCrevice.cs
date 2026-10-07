using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A quake crevice: a solid strip of split ground the player goes round (EarthPlan places it off the exit lanes, clear of
    /// walls, alcoves and everything that stands on the screen, so it never cuts anything off). The quake laid a fossil bare
    /// at its lip.
    /// </summary>
    public class QuakeCrevice : MonoBehaviour
    {
        public static readonly Color Tint = new Color(0.55f, 0.45f, 0.4f);
        public static readonly Color FossilColor = new Color(0.95f, 0.9f, 0.8f);

        [SerializeField] private Rect area;
        [SerializeField] private ScreenAddress screen;

        /// <summary>The strip's world rect (its collider).</summary>
        public Rect Area => area;

        public ScreenAddress Screen => screen;

        public BoxCollider2D Solid => GetComponent<BoxCollider2D>();

        /// <summary>The fossil at its lip.</summary>
        public SpriteRenderer Fossil { get; private set; }

        public static QuakeCrevice Create(Transform parent, ScreenAddress screen, Rect worldArea, Material material)
        {
            var go = new GameObject("Quake Crevice");
            go.transform.SetParent(parent, false);
            go.transform.position = worldArea.center;
            go.AddComponent<BoxCollider2D>().size = worldArea.size;
            var crevice = go.AddComponent<QuakeCrevice>();
            crevice.area = worldArea;
            crevice.screen = screen;
            ArtCatalog.AddSprite(go.transform, "Crack", ArtKey.Crevice, Vector2.zero, worldArea.size, Tint, -5, material, true);
            bool across = worldArea.width >= worldArea.height;
            Vector2 lip = across
                ? new Vector2(worldArea.width / 2f - 0.4f, worldArea.height / 2f + 0.3f)
                : new Vector2(worldArea.width / 2f + 0.3f, worldArea.height / 2f - 0.4f);
            crevice.Fossil = ArtCatalog.AddSprite(go.transform, "Fossil", ArtKey.Fossil, lip, new Vector2(0.6f, 0.6f), FossilColor, -4,
                material, false);
            return crevice;
        }
    }
}
