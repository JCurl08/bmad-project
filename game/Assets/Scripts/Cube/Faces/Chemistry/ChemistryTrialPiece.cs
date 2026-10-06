using UnityEngine;

namespace Game.Cube
{
    public enum ChemistryTrialPieceKind { Lamp = 0, CrackedWall = 1, Plate = 2 }

    /// <summary>
    /// One numbered target of the Chemistry trial: a dark lamp (Glowstone*), a cracked block (Wildstone*, solid until
    /// blasted) or a pressure plate (Leadstone*). Lamps and plates are triggers, so the player can stand on them. It
    /// passes every swing that reaches it to its ChemistryTrial, which decides. Pips above it show its number.
    /// </summary>
    public class ChemistryTrialPiece : MonoBehaviour, IAttackReceiver
    {
        public const float LampRadius = 0.35f;
        public const float WallSize = 0.7f;

        public static readonly Color DarkLampColor = new Color(0.12f, 0.1f, 0.16f);

        [SerializeField] private ChemistryTrial trial;
        [SerializeField] private ChemistryTrialPieceKind kind;
        [SerializeField] private int index;
        [SerializeField] private SpriteRenderer body;

        private Collider2D solid;
        private Color baseColor;

        public ChemistryTrial Trial => trial;
        public ChemistryTrialPieceKind Kind => kind;

        /// <summary>Order in the puzzle (0-based).</summary>
        public int Index => index;

        /// <summary>Lit (lamp), blasted (block) or weighed down (plate).</summary>
        public bool Done { get; private set; }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker) =>
            trial != null && trial.OnPieceHit(this, item);

        public void SetDone(bool done)
        {
            Done = done;
            if (kind == ChemistryTrialPieceKind.CrackedWall && solid != null) solid.enabled = !done;
            if (body == null) return;
            Color c = baseColor;
            if (done)
            {
                switch (kind)
                {
                    case ChemistryTrialPieceKind.Lamp:
                        c = ChemistryIsotope.GlowColor;
                        break;
                    case ChemistryTrialPieceKind.CrackedWall:
                        c = ChemistryIsotope.UnstableColor;
                        c.a = 0.25f;
                        break;
                    default:
                        c = LeadPlate.PressedColor;
                        break;
                }
            }
            body.color = c;
        }

        public static ChemistryTrialPiece Create(ChemistryTrial trial, ChemistryTrialPieceKind kind, int index, Vector2 world,
            Material material)
        {
            string label = kind == ChemistryTrialPieceKind.Lamp ? "Lamp" : kind == ChemistryTrialPieceKind.CrackedWall ? "Cracked Wall" : "Plate";
            var go = new GameObject($"Trial {label} {index + 1}");
            go.transform.SetParent(trial.transform, false);
            go.transform.position = world;
            Color color;
            PartShape shape;
            float size;
            ArtKey art;
            IsotopeStage wants;
            switch (kind)
            {
                case ChemistryTrialPieceKind.CrackedWall:
                    go.AddComponent<BoxCollider2D>().size = new Vector2(WallSize, WallSize);
                    color = CrackedWallGate.WallColor;
                    shape = PartShape.Square;
                    size = WallSize;
                    art = ArtKey.GateCrackedWall;
                    wants = IsotopeStage.Unstable;
                    break;
                case ChemistryTrialPieceKind.Plate:
                {
                    var trigger = go.AddComponent<CircleCollider2D>();
                    trigger.isTrigger = true;
                    trigger.radius = LeadPlate.Radius;
                    color = LeadPlate.PlateColor;
                    shape = PartShape.Square;
                    size = LeadPlate.Radius * 2f;
                    art = ArtKey.LeadPlate;
                    wants = IsotopeStage.Lead;
                    break;
                }
                default:
                {
                    var trigger = go.AddComponent<CircleCollider2D>();
                    trigger.isTrigger = true;
                    trigger.radius = LampRadius;
                    color = DarkLampColor;
                    shape = PartShape.Circle;
                    size = LampRadius * 2f;
                    art = ArtKey.TrialLamp;
                    wants = IsotopeStage.Glow;
                    break;
                }
            }
            var piece = go.AddComponent<ChemistryTrialPiece>();
            piece.trial = trial;
            piece.kind = kind;
            piece.index = index;
            piece.solid = go.GetComponent<Collider2D>();
            piece.baseColor = color;
            // Same art as what each piece imitates: a lamp for the dark, the cracked wall gate, the pressure plate.
            piece.body = ArtCatalog.AddSprite(go.transform, "Body", art, Vector2.zero, new Vector2(size, size), color, -2, material,
                tiled: false, fallback: shape);
            // Pips in the stone's colour show the order: one per number.
            for (int p = 0; p <= index; p++)
                BeakGate.AddSprite(go.transform, $"Pip {p + 1}", PartShape.Circle,
                    new Vector2((p - index / 2f) * 0.18f, size / 2f + 0.15f), new Vector2(0.12f, 0.12f),
                    ChemistryIsotope.StageColor(wants), -1, material);
            return piece;
        }
    }
}
