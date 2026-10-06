using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The short "it opened!" effect every gate plays when it opens (Gate.Open, and a timed door swinging ajar): a white
    /// ring bursts out of the gate and a handful of dust specks fly off, all fading over Seconds, then the effect removes
    /// itself and the gate shows its open state. Visual only: no colliders. Plays in play mode only.
    /// </summary>
    public class GateOpenEffect : MonoBehaviour
    {
        public const float Seconds = 0.4f;
        public const string ObjectName = "Gate Open Effect";
        private const int DustCount = 6;

        private static readonly Color RingColor = new Color(1f, 1f, 0.85f, 0.85f);
        private static readonly Color DustColor = new Color(0.95f, 0.9f, 0.75f, 1f);

        private SpriteRenderer ring;
        private readonly List<SpriteRenderer> dust = new List<SpriteRenderer>();
        private readonly List<Vector2> dustDirections = new List<Vector2>();
        private float age;
        private float spread = 1f;

        /// <summary>Effects started so far (tests).</summary>
        public static int Played { get; private set; }

        public float Age => age;

        /// <summary>Starts the effect on a gate (at its visual). Returns null outside play mode or without a gate.</summary>
        public static GateOpenEffect Play(Gate gate)
        {
            if (!Application.isPlaying || gate == null || !gate.isActiveAndEnabled) return null;
            Transform at = gate.Visual != null ? gate.Visual.transform : gate.transform;
            var go = new GameObject(ObjectName);
            go.transform.SetParent(gate.transform, false);
            go.transform.position = at.position;
            var effect = go.AddComponent<GateOpenEffect>();
            Vector2 size = gate.Solid != null ? gate.Solid.size : Vector2.one;
            effect.spread = Mathf.Max(1f, Mathf.Max(size.x, size.y) * 0.6f);
            Material material = gate.Visual != null ? gate.Visual.sharedMaterial : null;
            effect.ring = ArtCatalog.AddShape(go.transform, "Ring", PartShape.Circle, Vector2.zero, Vector2.one * 0.5f, RingColor, 12,
                material);
            for (int i = 0; i < DustCount; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / DustCount;
                effect.dustDirections.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                effect.dust.Add(ArtCatalog.AddShape(go.transform, $"Dust {i}", PartShape.Square, Vector2.zero, Vector2.one * 0.16f,
                    DustColor, 12, material));
            }
            Played++;
            effect.Apply(0f);
            return effect;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= Seconds)
            {
                Destroy(gameObject);
                return;
            }
            Apply(age / Seconds);
        }

        private void Apply(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            if (ring != null)
            {
                float size = Mathf.Lerp(0.5f, 1.6f * spread, ease);
                ring.transform.localScale = new Vector3(size, size, 1f);
                Color c = RingColor;
                c.a *= 1f - t;
                ring.color = c;
            }
            for (int i = 0; i < dust.Count; i++)
            {
                if (dust[i] == null) continue;
                dust[i].transform.localPosition = dustDirections[i] * (0.2f + ease * 0.9f * spread);
                Color c = DustColor;
                c.a = 1f - t;
                dust[i].color = c;
            }
        }
    }
}
