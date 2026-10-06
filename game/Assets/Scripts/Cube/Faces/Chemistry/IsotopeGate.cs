using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Base of the Chemistry stage gates. A Gate (same alcove slot, IsOpen, Opened) bound to the isotope item that
    /// opens only to one decay stage: a dark-room gate to a glowing isotope (touch), a cracked wall to an unstable
    /// one (swing), a lead-plate door to lead dropped on its plate. The wrong stage, another item, a bare swing
    /// or no isotope leaves it shut. A small "Stage Mark" in the stage's colour says which stage it wants.
    /// </summary>
    public abstract class IsotopeGate : Gate
    {
        /// <summary>The decay stage that opens this gate.</summary>
        public abstract IsotopeStage Stage { get; }

        protected override bool OpensOnTouch => false;

        /// <summary>True if this isotope is the gate's item, held, in the gate's stage.</summary>
        protected bool Matches(Isotope isotope) => isotope != null && isotope.Is(RequiredItem, Stage);

        /// <summary>Hides the "Stage Mark" once open.</summary>
        protected override void OnOpened()
        {
            Transform mark = transform.Find("Stage Mark");
            if (mark != null) mark.gameObject.SetActive(false);
        }

        /// <summary>Common set-up of a placeholder block (root BoxCollider2D, "Visual" child) as a stage gate.</summary>
        protected static T Setup<T>(GameObject block, string name, ItemDefinition item, PartShape markShape, Material material)
            where T : IsotopeGate
        {
            block.name = name;
            var gate = block.AddComponent<T>();
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            BeakGate.AddSprite(block.transform, "Stage Mark", markShape, Vector2.zero, new Vector2(0.35f, 0.35f),
                ChemistryIsotope.StageColor(gate.Stage), -3, material);
            return gate;
        }
    }
}
