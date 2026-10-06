using UnityEngine;

namespace Game.Cube
{
    public enum TrialTargetKind { Rock = 0, Button = 1, Flower = 2 }

    /// <summary>
    /// One piece of the Biology trial: a numbered rock (solid until broken), a distant button or the final
    /// flower (triggers). Swings that reach it are passed to its BiologyTrial, which decides what happens.
    /// </summary>
    public class TrialTarget : MonoBehaviour, IAttackReceiver
    {
        public const float RockSize = 0.7f;
        public const float TriggerRadius = 0.35f;

        [SerializeField] private BiologyTrial trial;
        [SerializeField] private TrialTargetKind kind;
        [SerializeField] private int index;
        [SerializeField] private SpriteRenderer body;

        private Collider2D solid;
        private Color baseColor;

        public BiologyTrial Trial => trial;
        public TrialTargetKind Kind => kind;

        /// <summary>Order in the puzzle: rock number (0-based), button number, or the flower last.</summary>
        public int Index => index;

        /// <summary>Broken (rock), pressed (button) or pollinated (flower).</summary>
        public bool Done { get; private set; }

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker) =>
            trial != null && trial.OnTargetHit(this, item);

        public void SetDone(bool done)
        {
            Done = done;
            if (kind == TrialTargetKind.Rock && solid != null) solid.enabled = !done;
            if (body == null) return;
            Color c = done ? BiologyTrial.DoneColor : baseColor;
            if (done && kind == TrialTargetKind.Rock) c.a = 0.25f;
            body.color = c;
        }

        public static TrialTarget Create(BiologyTrial trial, TrialTargetKind kind, int index, Vector2 world, Material material)
        {
            var go = new GameObject($"Trial {kind} {index + 1}");
            go.transform.SetParent(trial.transform, false);
            go.transform.position = world;
            Color color;
            PartShape shape;
            if (kind == TrialTargetKind.Rock)
            {
                go.AddComponent<BoxCollider2D>().size = new Vector2(RockSize, RockSize);
                // Lighter is earlier: rock 1 is the palest.
                color = Color.Lerp(BiologyTrial.TrialRockColor, Color.black, 0.18f * index);
                shape = PartShape.Square;
            }
            else
            {
                var trigger = go.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = TriggerRadius;
                color = kind == TrialTargetKind.Button ? BeakButton.UpColor : PollinationFlower.BudColor;
                shape = kind == TrialTargetKind.Button ? PartShape.Circle : PartShape.Diamond;
            }
            var target = go.AddComponent<TrialTarget>();
            target.trial = trial;
            target.kind = kind;
            target.index = index;
            target.solid = go.GetComponent<Collider2D>();
            target.baseColor = color;
            float size = kind == TrialTargetKind.Rock ? RockSize : TriggerRadius * 2f;
            // Same art as what each target imitates: a rock gate's rock, a beak button, a pollination flower.
            ArtKey art = kind == TrialTargetKind.Rock ? ArtKey.GateRock : kind == TrialTargetKind.Button ? ArtKey.BeakButton : ArtKey.Flower;
            target.body = ArtCatalog.AddSprite(go.transform, "Body", art, Vector2.zero, new Vector2(size, size), color, -2, material,
                tiled: false, fallback: shape);
            // Pips show the order (rocks and buttons): one per number.
            if (kind != TrialTargetKind.Flower)
                for (int p = 0; p <= index; p++)
                    BeakGate.AddSprite(go.transform, $"Pip {p + 1}", PartShape.Circle,
                        new Vector2((p - index / 2f) * 0.18f, size / 2f + 0.15f), new Vector2(0.12f, 0.12f), Color.white, -1, material);
            return target;
        }
    }
}
