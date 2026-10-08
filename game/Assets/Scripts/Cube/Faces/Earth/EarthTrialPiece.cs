using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// One numbered target of the Earth trial, of the rolled phenomenon's kind: a small rock pillar (quake, solid until
    /// toppled), a small fire (rain), a puddle (snow) or a debris heap (tornado, solid until blown away). Fires and puddles
    /// are triggers the player can stand on. It passes every swing that reaches it to its EarthTrial, which decides. Pips
    /// above it in the phenomenon's colour show its number.
    /// </summary>
    public class EarthTrialPiece : MonoBehaviour, IAttackReceiver
    {
        public const float Size = 0.7f * ScreenMath.ContentScale;

        [SerializeField] private EarthTrial trial;
        [SerializeField] private Phenomenon kind;
        [SerializeField] private int index;
        [SerializeField] private SpriteRenderer body;

        private Collider2D solid;
        private Color baseColor;

        public EarthTrial Trial => trial;
        public Phenomenon Kind => kind;

        /// <summary>Order in the puzzle (0-based).</summary>
        public int Index => index;

        /// <summary>Toppled, doused, frozen or blown away.</summary>
        public bool Done { get; private set; }

        /// <summary>True for the kinds that block the way until done (pillars and debris).</summary>
        public static bool IsSolid(Phenomenon kind) => kind == Phenomenon.Quake || kind == Phenomenon.Tornado;

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker) => trial != null && trial.OnPieceHit(this, item);

        /// <summary>
        /// True while a reset piece waits to turn solid again: it only does once nothing stands in its box (the player may be
        /// standing where the toppled pillar or blown-away heap was), so a reset never traps anyone inside it.
        /// </summary>
        public bool WaitingToBlock { get; private set; }

        public void SetDone(bool done)
        {
            Done = done;
            if (IsSolid(kind) && solid != null)
            {
                if (done)
                {
                    solid.enabled = false;
                    WaitingToBlock = false;
                }
                else if (!solid.enabled)
                {
                    WaitingToBlock = true;
                    TryBlock();
                }
            }
            if (body == null) return;
            Color c = baseColor;
            if (done)
            {
                c = kind == Phenomenon.Snow ? FrozenWaterGate.IceColor : EarthTrial.DoneColor;
                c.a = IsSolid(kind) ? 0.3f : 0.9f;
            }
            body.color = c;
        }

        private void FixedUpdate()
        {
            if (WaitingToBlock) TryBlock();
        }

        /// <summary>Turns the piece solid again if nothing solid (the player, an NPC, an enemy) overlaps its box.</summary>
        private void TryBlock()
        {
            if (Done || solid == null)
            {
                WaitingToBlock = false;
                return;
            }
            Physics2D.SyncTransforms();
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(transform.position, new Vector2(Size, Size), 0f))
                if (hit != null && hit != solid && hit.enabled && !hit.isTrigger) return;
            solid.enabled = true;
            WaitingToBlock = false;
        }

        public static EarthTrialPiece Create(EarthTrial trial, Phenomenon kind, int index, Vector2 world, Material material)
        {
            var go = new GameObject($"Trial {kind} {index + 1}");
            go.transform.SetParent(trial.transform, false);
            go.transform.position = world;
            if (IsSolid(kind)) go.AddComponent<BoxCollider2D>().size = new Vector2(Size, Size);
            else
            {
                var trigger = go.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = Size / 2f;
            }
            var piece = go.AddComponent<EarthTrialPiece>();
            piece.trial = trial;
            piece.kind = kind;
            piece.index = index;
            piece.solid = go.GetComponent<Collider2D>();
            piece.baseColor = PhenomenonGate.ClosedColorOf(kind);
            // The same art as the phenomenon's gate: what each piece is a small copy of.
            piece.body = ArtCatalog.AddSprite(go.transform, "Body", PhenomenonGate.ArtOf(kind), Vector2.zero, new Vector2(Size, Size),
                piece.baseColor, -2, material, tiled: false, fallback: PartShape.Square);
            for (int p = 0; p <= index; p++)
                BeakGate.AddSprite(go.transform, $"Pip {p + 1}", PartShape.Circle, new Vector2((p - index / 2f) * 0.18f * ScreenMath.ContentScale, Size / 2f + 0.15f * ScreenMath.ContentScale),
                    new Vector2(0.12f, 0.12f) * ScreenMath.ContentScale, StormcallerItem.ColorOf(kind), -1, material);
            return piece;
        }
    }
}
