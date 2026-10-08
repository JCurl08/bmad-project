using System;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Base of the Chemistry stage gates, on the beak-gate pattern: a Gate (same alcove slot, IsOpen, Opened) bound to
    /// one stone that touching never opens. It opens after HitsToOpen swings with its RequiredItem equipped, through its
    /// own mechanic (a dark room lit, a cracked wall blasted, a plate weighed down). Difficulty is the number of stage
    /// gates on its face (ChemistryPlan.HitsFor). Swings with any other item (another stone included), or bare, do
    /// nothing. A small "Stage Mark" in the stone's colour says which stone it wants.
    /// </summary>
    public abstract class IsotopeGate : Gate
    {
        [Tooltip("The number of stage gates on this gate's face; sets the swings it takes (ChemistryPlan.HitsFor).")]
        [SerializeField, Min(1)] private int difficulty = 1;

        [Tooltip("Screens per side of this gate's face (N); the swings scale with the gate density per screen.")]
        [SerializeField, Min(1)] private int faceSize = CubeSettings.DefaultFaceSize;

        private int hits;

        /// <summary>The stone stage that opens this gate.</summary>
        public abstract IsotopeStage Stage { get; }

        public int Difficulty
        {
            get => difficulty;
            set => difficulty = Mathf.Max(1, value);
        }

        /// <summary>N of the face the gate's difficulty counts gates on (ChemistryFace sets it from the plan).</summary>
        public int FaceSize
        {
            get => faceSize;
            set => faceSize = Mathf.Max(1, value);
        }

        public int HitsToOpen => ChemistryPlan.HitsFor(difficulty, faceSize);

        /// <summary>Counted swings so far.</summary>
        public int Hits => hits;

        /// <summary>True for the other stones' optional gates (guard nothing required).</summary>
        public bool Optional { get; set; }

        /// <summary>Raised on every counted swing, with the swings so far.</summary>
        public event Action<IsotopeGate, int> Struck;

        protected override bool OpensOnTouch => false;

        /// <summary>True if item is this gate's stone (its RequiredItem, of the gate's stage).</summary>
        public bool Matches(ItemDefinition item) =>
            item != null && item == RequiredItem && ChemistryIsotope.Is(item, Stage);

        /// <summary>Counts a swing if item is the gate's stone and the gate is shut; opens it on the last one.</summary>
        protected bool RegisterHit(ItemDefinition item)
        {
            if (IsOpen || !Matches(item)) return false;
            hits++;
            Struck?.Invoke(this, hits);
            if (hits >= HitsToOpen) OpenNow();
            return true;
        }

        /// <summary>Hides the "Stage Mark" once open.</summary>
        protected override void OnOpened()
        {
            Transform mark = transform.Find("Stage Mark");
            if (mark != null) mark.gameObject.SetActive(false);
        }

        /// <summary>Common set-up of a placeholder block (root BoxCollider2D, "Visual" child) as a stage gate.</summary>
        protected static T Setup<T>(GameObject block, string name, ItemDefinition item, PartShape markShape, int difficulty,
            bool optional, int faceSize, Material material)
            where T : IsotopeGate
        {
            block.name = name;
            var gate = block.AddComponent<T>();
            gate.Difficulty = difficulty;
            gate.FaceSize = faceSize;
            gate.Optional = optional;
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            BeakGate.AddSprite(block.transform, "Stage Mark", markShape, Vector2.zero, new Vector2(0.35f, 0.35f) * ScreenMath.ContentScale,
                ChemistryIsotope.StageColor(gate.Stage), -3, material);
            return gate;
        }
    }
}
