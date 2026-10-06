using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Base for the tagged marker points inside a screen module. Slots are markers only: later stories
    /// fill them with gates, hidden items and the core entrance. A debug marker (sprite) can be shown on
    /// each slot; CubeDebug draws the labels.
    /// </summary>
    public abstract class ModuleSlot : MonoBehaviour
    {
        private const float MarkerSize = 0.6f;
        private const int MarkerSortingOrder = 20;

        private SpriteRenderer marker;

        /// <summary>Short text shown next to the debug marker.</summary>
        public abstract string Label { get; }

        /// <summary>Debug marker colour, one per slot kind.</summary>
        public abstract Color MarkerColor { get; }

        /// <summary>True while the debug marker exists and is enabled and the slot itself is active.</summary>
        public bool MarkerVisible => marker != null && marker.enabled && gameObject.activeInHierarchy;

        /// <summary>Shows or hides the debug marker, creating it on first use.</summary>
        public void SetMarkerVisible(bool visible, Sprite sprite, Material material)
        {
            if (marker == null)
            {
                if (!visible || sprite == null) return;
                var go = new GameObject("Debug Marker");
                go.transform.SetParent(transform, false);
                Vector2 spriteSize = sprite.bounds.size;
                go.transform.localScale = new Vector3(MarkerSize / spriteSize.x, MarkerSize / spriteSize.y, 1f);
                marker = go.AddComponent<SpriteRenderer>();
                marker.sprite = sprite;
                if (material != null) marker.sharedMaterial = material;
                marker.color = MarkerColor;
                marker.sortingOrder = MarkerSortingOrder;
            }
            marker.enabled = visible;
        }
    }
}
