using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>Bubbles rising from a vent hut's pipe (4.5, the Chemistry towns): a few small rings drifting up and fading, on a loop.</summary>
    public class TownBubbles : MonoBehaviour
    {
        public const int Count = 3;
        public const float Rise = 1.2f, Period = 1.6f;

        private readonly List<SpriteRenderer> bubbles = new List<SpriteRenderer>();
        private float time;

        public void Configure(Material material)
        {
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject($"Bubble {i}");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = TownArt.Bubble();
                if (material != null) renderer.sharedMaterial = material;
                renderer.sortingOrder = TownSquareView.ArtOrder + 1;
                bubbles.Add(renderer);
            }
            Step(0f);
        }

        private void Update() => Step(Time.deltaTime);

        private void Step(float dt)
        {
            time += dt;
            for (int i = 0; i < bubbles.Count; i++)
            {
                SpriteRenderer b = bubbles[i];
                if (b == null) continue;
                float t = Mathf.Repeat(time / Period + i / (float)Count, 1f);
                b.transform.localPosition = new Vector3(Mathf.Sin((t + i) * 6.3f) * 0.15f, t * Rise, 0f);
                b.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.1f, t);
                b.color = new Color(1f, 1f, 1f, 1f - t * t);
            }
        }
    }
}
