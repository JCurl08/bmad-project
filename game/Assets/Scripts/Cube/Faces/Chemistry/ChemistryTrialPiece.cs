using UnityEngine;

namespace Game.Cube
{
    public enum ChemistryTrialPieceKind { Lamp = 0, CrackedWall = 1 }

    /// <summary>
    /// One piece of the Chemistry trial: the dark lamp (a trigger: lit by touching it, or swinging at it, with the
    /// glowing isotope) or the cracked wall (solid until blasted by a swing with the unstable isotope equipped).
    /// It passes every touch and swing to its ChemistryTrial, which decides. The trial's plate is a LeadPlate.
    /// </summary>
    public class ChemistryTrialPiece : MonoBehaviour, IAttackReceiver
    {
        public const float LampRadius = 0.35f;
        public const float WallSize = 0.7f;

        public static readonly Color DarkLampColor = new Color(0.12f, 0.1f, 0.16f);

        [SerializeField] private ChemistryTrial trial;
        [SerializeField] private ChemistryTrialPieceKind kind;
        [SerializeField] private SpriteRenderer body;

        private Collider2D solid;
        private Color baseColor;

        public ChemistryTrial Trial => trial;
        public ChemistryTrialPieceKind Kind => kind;

        /// <summary>Lit (lamp) or blasted (wall).</summary>
        public bool Done { get; private set; }

        private void OnTriggerEnter2D(Collider2D other) => Touch(other);

        private void OnTriggerStay2D(Collider2D other) => Touch(other);

        private void Touch(Collider2D other)
        {
            if (kind != ChemistryTrialPieceKind.Lamp || trial == null) return;
            Isotope isotope = Isotope.On(other);
            if (isotope != null) trial.OnPiece(this, isotope, false);
        }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (trial == null || item == null || item != trial.IsotopeItem) return false;
            return trial.OnPiece(this, Isotope.On(attacker), true);
        }

        public void SetDone(bool done)
        {
            Done = done;
            if (kind == ChemistryTrialPieceKind.CrackedWall && solid != null) solid.enabled = !done;
            if (body == null) return;
            Color c = baseColor;
            if (done)
            {
                c = kind == ChemistryTrialPieceKind.Lamp ? ChemistryIsotope.GlowColor : ChemistryIsotope.UnstableColor;
                if (kind == ChemistryTrialPieceKind.CrackedWall) c.a = 0.25f;
            }
            body.color = c;
        }

        public static ChemistryTrialPiece Create(ChemistryTrial trial, ChemistryTrialPieceKind kind, Vector2 world, Material material)
        {
            var go = new GameObject(kind == ChemistryTrialPieceKind.Lamp ? "Trial Lamp" : "Trial Cracked Wall");
            go.transform.SetParent(trial.transform, false);
            go.transform.position = world;
            Color color;
            PartShape shape;
            float size;
            if (kind == ChemistryTrialPieceKind.Lamp)
            {
                var trigger = go.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = LampRadius;
                color = DarkLampColor;
                shape = PartShape.Circle;
                size = LampRadius * 2f;
            }
            else
            {
                go.AddComponent<BoxCollider2D>().size = new Vector2(WallSize, WallSize);
                color = CrackedWallGate.WallColor;
                shape = PartShape.Square;
                size = WallSize;
            }
            var piece = go.AddComponent<ChemistryTrialPiece>();
            piece.trial = trial;
            piece.kind = kind;
            piece.solid = go.GetComponent<Collider2D>();
            piece.baseColor = color;
            // Same art as what each piece imitates: a lamp for the dark, the cracked wall of an unstable gate.
            ArtKey art = kind == ChemistryTrialPieceKind.Lamp ? ArtKey.TrialLamp : ArtKey.GateCrackedWall;
            piece.body = ArtCatalog.AddSprite(go.transform, "Body", art, Vector2.zero, new Vector2(size, size), color, -2, material,
                tiled: false, fallback: shape);
            // A mark in the colour of the stage the piece wants.
            IsotopeStage wants = kind == ChemistryTrialPieceKind.Lamp ? IsotopeStage.Glow : IsotopeStage.Unstable;
            BeakGate.AddSprite(go.transform, "Stage Mark", PartShape.Triangle, new Vector2(0f, size / 2f + 0.15f),
                new Vector2(0.18f, 0.18f), ChemistryIsotope.StageColor(wants), -1, material);
            return piece;
        }
    }
}
