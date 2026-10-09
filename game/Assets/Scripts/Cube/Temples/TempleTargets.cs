using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An ordering temple in play (4.14, TempleOrderSpec): its targets on the floor, struck in order. Any swing counts in a
    /// temple (assumption: the temple lends the face's power, so the puzzle never needs the face's item). A swing that
    /// reaches several targets strikes only the one nearest the player (decided after the swing, in LateUpdate). The right
    /// target lights up; a wrong one puts every target back (no damage) and says the rule again; Chemistry's timer starts
    /// on the first strike and puts every target back if it runs out (a bar over the altar shows the time left). The last
    /// target solves it (Solved, once).
    /// </summary>
    public class TempleTargets : MonoBehaviour
    {
        public static readonly Color DoneColor = new Color(0.35f, 1f, 0.55f);
        public static readonly Color TimerColor = new Color(1f, 0.85f, 0.35f);

        private readonly List<TempleTarget> targets = new List<TempleTarget>();
        private readonly List<TempleTarget> struckThisSwing = new List<TempleTarget>();
        private Transform striker;
        private int swing = -1;
        private SpriteRenderer timerBar;
        private Material material;
        private PickupPopup lastPopup;

        public TempleOrderSpec Spec { get; private set; }
        public Theme Theme { get; private set; }
        public Vector2 Centre { get; private set; }

        public IReadOnlyList<TempleTarget> Targets => targets;

        /// <summary>Targets struck in order so far.</summary>
        public int Progress { get; private set; }

        /// <summary>Seconds left on the timer (0 when none is running).</summary>
        public float TimeLeft { get; private set; }

        public bool TimerRunning => TimeLeft > 0f;

        public bool IsSolved { get; private set; }

        /// <summary>Times the targets were put back (a wrong strike or the timer).</summary>
        public int Resets { get; private set; }

        public event Action<TempleTargets> Solved;

        public void Configure(TempleOrderSpec spec, Theme theme, Vector2 centre, Material mat)
        {
            Spec = spec;
            Theme = theme;
            Centre = centre;
            material = mat;
            transform.position = centre;
            for (int i = 0; i < spec.Count; i++)
                targets.Add(TempleTarget.Create(this, i, spec.Kind, spec.Marks[i], spec.Looks[i], centre + spec.Spots[i], mat));
            if (spec.TimerSeconds > 0f)
            {
                timerBar = ArtCatalog.AddShape(transform, "Timer Bar", PartShape.Square, TempleRoom.AltarSpot + new Vector2(0f, 1.5f),
                    new Vector2(3f, 0.25f), TimerColor, 6, mat);
                timerBar.enabled = false;
            }
        }

        /// <summary>A swing reached a target: noted, and resolved once the swing is over (the nearest target struck).</summary>
        internal bool Hit(TempleTarget target, GameObject attacker)
        {
            if (IsSolved || target == null) return false;
            PlayerAttack attack = attacker != null ? attacker.GetComponentInParent<PlayerAttack>() : null;
            int count = attack != null ? attack.SwingCount : Time.frameCount;
            if (count != swing)
            {
                swing = count;
                struckThisSwing.Clear();
            }
            striker = attacker != null ? attacker.transform : null;
            if (!struckThisSwing.Contains(target)) struckThisSwing.Add(target);
            return true;
        }

        private void LateUpdate()
        {
            if (struckThisSwing.Count == 0) return;
            TempleTarget best = null;
            float bestDistance = float.MaxValue;
            Vector2 from = striker != null ? (Vector2)striker.position : Centre;
            foreach (TempleTarget t in struckThisSwing)
            {
                if (t == null) continue;
                float d = Vector2.Distance(from, t.transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = t;
                }
            }
            struckThisSwing.Clear();
            if (best != null) Strike(best);
        }

        /// <summary>Strikes a target (tests and tools call it directly): the right one lights, a wrong one puts all back.</summary>
        public bool Strike(TempleTarget target)
        {
            if (IsSolved || target == null || target.Done) return false;
            if (target.Index != Progress)
            {
                PutBack(TempleText.WrongPopup);
                return true;
            }
            target.SetDone(true);
            Progress++;
            if (Progress == 1 && Spec.TimerSeconds > 0f) TimeLeft = Spec.TimerSeconds;
            if (Progress >= targets.Count)
            {
                IsSolved = true;
                TimeLeft = 0f;
                if (timerBar != null) timerBar.enabled = false;
                Solved?.Invoke(this);
            }
            return true;
        }

        private void PutBack(string why)
        {
            Progress = 0;
            TimeLeft = 0f;
            Resets++;
            foreach (TempleTarget t in targets) t.SetDone(false);
            if (timerBar != null) timerBar.enabled = false;
            // 4.14 review: one popup at a time (a new one replaces the last), and the rule is said only on walking in.
            if (lastPopup != null) Destroy(lastPopup.gameObject);
            lastPopup = PickupPopup.Show(Centre + TempleRoom.AltarSpot + new Vector2(0f, -1.6f), why, new Color(1f, 0.7f, 0.6f), 2f);
        }

        private void Update()
        {
            if (!TimerRunning || IsSolved) return;
            TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
            if (timerBar != null)
            {
                timerBar.enabled = true;
                timerBar.transform.localScale = new Vector3(3f * TimeLeft / Spec.TimerSeconds, 0.25f, 1f);
            }
            if (TimeLeft <= 0f) PutBack(TempleText.TooSlowPopup);
        }
    }

    /// <summary>
    /// One target of an ordering temple (4.14): a seed sized by its mark, a stone glowing by its mark, or a token in a face's
    /// colour showing its pips. A trigger (never solid, so a put-back target never traps the player); every swing that
    /// reaches it goes to its TempleTargets.
    /// </summary>
    public class TempleTarget : MonoBehaviour, IAttackReceiver
    {
        public const float TriggerRadius = 0.75f;

        private TempleTargets owner;
        private SpriteRenderer body, glow;
        private Color baseColor;

        public int Index { get; private set; }
        public int Mark { get; private set; }
        public bool Done { get; private set; }
        public SpriteRenderer Body => body;

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker) => owner != null && owner.Hit(this, attacker);

        public void SetDone(bool done)
        {
            Done = done;
            if (body != null) body.color = done ? Color.Lerp(baseColor, TempleTargets.DoneColor, 0.65f) : baseColor;
            if (glow != null)
                glow.color = done ? new Color(TempleTargets.DoneColor.r, TempleTargets.DoneColor.g, TempleTargets.DoneColor.b, 0.5f) : GlowOf(Mark);
        }

        /// <summary>A stone's glow by its mark: 0 the brightest, each step fainter.</summary>
        public static Color GlowOf(int mark)
        {
            float t = Mathf.Clamp01(mark / 5f);
            return new Color(1f, Mathf.Lerp(0.97f, 0.45f, t), Mathf.Lerp(0.75f, 0.2f, t), Mathf.Lerp(0.85f, 0.18f, t));
        }

        /// <summary>A seed's size (units across) by its mark: 0 the smallest.</summary>
        public static float SeedSize(int mark) => 0.6f + 0.2f * mark;

        public static TempleTarget Create(TempleTargets owner, int index, TemplePuzzleKind kind, int mark, Theme look, Vector2 world,
            Material material)
        {
            var go = new GameObject($"Temple Target {index + 1} (mark {mark})");
            go.transform.SetParent(owner.transform, false);
            go.transform.position = world;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = TriggerRadius;
            var t = go.AddComponent<TempleTarget>();
            t.owner = owner;
            t.Index = index;
            t.Mark = mark;
            // A plinth under every target, so each reads as part of the puzzle.
            ArtCatalog.AddShape(go.transform, "Plinth", PartShape.Circle, new Vector2(0f, -0.15f), new Vector2(1.5f, 0.9f),
                new Color(0.35f, 0.32f, 0.38f), -6, material);
            switch (kind)
            {
                case TemplePuzzleKind.SeedOrder:
                {
                    float s = SeedSize(mark);
                    t.baseColor = new Color(0.62f, 0.4f, 0.24f);
                    t.body = ArtCatalog.AddShape(go.transform, "Seed", PartShape.Circle, new Vector2(0f, s * 0.3f), new Vector2(s, s * 0.8f),
                        t.baseColor, 3, material);
                    ArtCatalog.AddShape(go.transform, "Seed Line", PartShape.Square, new Vector2(0f, s * 0.3f), new Vector2(0.06f, s * 0.6f),
                        new Color(0.35f, 0.2f, 0.12f), 4, material);
                    break;
                }
                case TemplePuzzleKind.StoneOrder:
                {
                    t.glow = ArtCatalog.AddShape(go.transform, "Glow", PartShape.Circle, new Vector2(0f, 0.35f),
                        Vector2.one * Mathf.Lerp(1.6f, 0.75f, mark / 5f), GlowOf(mark), 2, material);
                    t.baseColor = Color.Lerp(new Color(1f, 0.95f, 0.8f), new Color(0.45f, 0.3f, 0.3f), mark / 5f);
                    t.body = ArtCatalog.AddShape(go.transform, "Stone", PartShape.Diamond, new Vector2(0f, 0.35f), new Vector2(0.55f, 0.7f),
                        t.baseColor, 3, material);
                    break;
                }
                default:
                {
                    t.baseColor = CubeWorld.ThemeColor(look);
                    t.body = ArtCatalog.AddShape(go.transform, "Token", PartShape.Square, new Vector2(0f, 0.35f), new Vector2(1.3f, 1.3f),
                        t.baseColor, 3, material);
                    var pips = new GameObject("Pips").AddComponent<SpriteRenderer>();
                    pips.transform.SetParent(go.transform, false);
                    pips.transform.localPosition = new Vector2(0f, 0.35f);
                    pips.sprite = TownArt.DieFace(mark);
                    pips.transform.localScale = Vector3.one * 0.6f;
                    if (material != null) pips.sharedMaterial = material;
                    pips.sortingOrder = 4;
                    break;
                }
            }
            t.SetDone(false);
            return t;
        }
    }
}
