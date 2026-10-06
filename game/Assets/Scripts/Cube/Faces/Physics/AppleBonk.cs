using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Newton gag: every few seconds an apple drops onto the NPC's head and bounces off. Visual only (no
    /// colliders); counts the bonks.
    /// </summary>
    public class AppleBonk : MonoBehaviour
    {
        public const float Interval = 4f;
        public const float FallSeconds = 0.45f;

        public static readonly Color AppleColor = new Color(0.85f, 0.15f, 0.12f);

        [SerializeField] private SpriteRenderer apple;
        [SerializeField] private float headY = 1.3f;

        private float timer;

        public int Bonks { get; private set; }

        private void Update()
        {
            if (apple == null) return;
            timer += Time.deltaTime;
            float t = timer % Interval;
            bool falling = t < FallSeconds;
            apple.enabled = falling;
            if (falling)
                apple.transform.localPosition = new Vector3(0.05f, headY + 1.2f * (1f - t / FallSeconds), 0f);
            int count = Mathf.FloorToInt((timer - FallSeconds) / Interval) + 1;
            if (count > Bonks && timer >= FallSeconds) Bonks = count;
        }

        public static AppleBonk Add(GameObject npc, float headY, Material material)
        {
            var bonk = npc.AddComponent<AppleBonk>();
            bonk.headY = headY;
            bonk.apple = BeakGate.AddSprite(npc.transform, "Apple", PartShape.Circle, new Vector2(0.05f, headY + 1.2f),
                new Vector2(0.26f, 0.26f), AppleColor, 12, material);
            bonk.apple.enabled = false;
            return bonk;
        }
    }
}
