using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Base of the Biology beak gates. A Gate (same alcove slot, IsOpen, Opened) that touching never opens:
    /// it opens after HitsToOpen beak hits with its RequiredItem through its own mechanic (breaking, a button,
    /// a flower). Difficulty is the number of beak gates on its face; more gates on a face means tougher gates
    /// (BiologyPlan.HitsFor). Hits with any other item, or bare, do nothing.
    /// </summary>
    public abstract class BeakGate : Gate
    {
        [Tooltip("The number of beak gates on this gate's face; sets the beak hits it takes (BiologyPlan.HitsFor).")]
        [SerializeField, Min(1)] private int difficulty = 1;

        private int hits;

        public int Difficulty
        {
            get => difficulty;
            set => difficulty = Mathf.Max(1, value);
        }

        public int HitsToOpen => BiologyPlan.HitsFor(difficulty);

        /// <summary>Beak hits taken so far.</summary>
        public int Hits => hits;

        /// <summary>True for the non-rolled beak's optional gates (guard nothing required).</summary>
        public bool Optional { get; set; }

        public abstract BeakGateKind Kind { get; }

        /// <summary>Raised on every counted beak hit, with the hits so far.</summary>
        public event Action<BeakGate, int> Struck;

        protected override bool OpensOnTouch => false;

        /// <summary>Counts a beak hit if item is the required beak and the gate is shut; opens it on the last one.</summary>
        protected bool RegisterHit(ItemDefinition item)
        {
            if (IsOpen || item == null || item != RequiredItem) return false;
            hits++;
            Struck?.Invoke(this, hits);
            if (hits >= HitsToOpen) OpenNow();
            return true;
        }

        /// <summary>Hides the "Beak Mark" (which beak it wants) once open.</summary>
        protected override void OnOpened()
        {
            Transform mark = transform.Find("Beak Mark");
            if (mark != null) mark.gameObject.SetActive(false);
        }

        /// <summary>A placeholder child sprite (shape, size and colour) on a parent, for the face's dressing.</summary>
        public static SpriteRenderer AddSprite(Transform parent, string name, PartShape shape, Vector2 localPosition,
            Vector2 size, Color color, int sortingOrder, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = NpcFactory.ShapeSprite(shape);
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
