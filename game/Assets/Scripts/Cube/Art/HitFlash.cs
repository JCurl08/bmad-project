using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Hit feedback: whenever the Health on this object takes damage, its sprites flash solid white for Seconds (about
    /// 0.1 s). The flash swaps each renderer to the white flash material (ArtCatalog.Flash, the Hidden/Cube/HitFlash
    /// shader: the sprite's shape in white) and back, so colours and tints set by other code (a hostile NPC's tint) are
    /// never touched. Without the material it falls back to tinting white. Every Health adds one in play mode; the
    /// renderers are all of the object's enabled sprites (minus effects like the swing and the isotope halo) unless
    /// SetRenderers names them.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitFlash : MonoBehaviour
    {
        public const string ShaderName = "Hidden/Cube/HitFlash";
        public const float DefaultSeconds = 0.1f;

        /// <summary>Child sprites that never flash (effects that are not the body).</summary>
        private static readonly string[] Excluded = { "Swing", "Isotope Halo" };

        [SerializeField, Min(0f)] private float seconds = DefaultSeconds;

        private Health health;
        private SpriteRenderer[] fixedRenderers;
        private readonly List<SpriteRenderer> flashed = new List<SpriteRenderer>();
        private readonly List<Material> originalMaterials = new List<Material>();
        private readonly List<Color> originalColors = new List<Color>();
        private bool usedMaterial;
        private float until;

        public float Seconds
        {
            get => seconds;
            set => seconds = Mathf.Max(0f, value);
        }

        public bool IsFlashing { get; private set; }

        /// <summary>Flashes so far (tests).</summary>
        public int Flashes { get; private set; }

        /// <summary>The renderers of the current flash.</summary>
        public IReadOnlyList<SpriteRenderer> Flashed => flashed;

        /// <summary>Raised when a flash starts.</summary>
        public event Action<HitFlash> Started;

        /// <summary>The object's flash, added if missing.</summary>
        public static HitFlash For(GameObject owner)
        {
            if (owner == null) return null;
            return owner.TryGetComponent(out HitFlash flash) ? flash : owner.AddComponent<HitFlash>();
        }

        /// <summary>
        /// A renderer's own material: during a flash, the one it had before the flash (not the white flash material), so
        /// code that copies it onto a new renderer (the swing, the isotope halo) never copies the flash.
        /// </summary>
        public static Material NormalMaterial(SpriteRenderer renderer)
        {
            if (renderer == null) return null;
            HitFlash flash = renderer.GetComponentInParent<HitFlash>();
            if (flash != null && flash.IsFlashing && flash.usedMaterial)
            {
                int i = flash.flashed.IndexOf(renderer);
                if (i >= 0) return flash.originalMaterials[i];
            }
            return renderer.sharedMaterial;
        }

        /// <summary>Flashes exactly these renderers (null: every enabled sprite of the object).</summary>
        public void SetRenderers(SpriteRenderer[] renderers)
        {
            End();
            fixedRenderers = renderers;
        }

        private void OnEnable()
        {
            health = GetComponent<Health>();
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            End();
        }

        private void OnDamaged(Health _, float amount, ItemDefinition item) => Flash();

        /// <summary>Starts (or restarts) a flash.</summary>
        public void Flash()
        {
            if (seconds <= 0f) return;
            if (!IsFlashing)
            {
                flashed.Clear();
                originalMaterials.Clear();
                originalColors.Clear();
                foreach (SpriteRenderer r in Targets())
                {
                    if (r == null || !r.enabled) continue;
                    flashed.Add(r);
                    originalMaterials.Add(r.sharedMaterial);
                    originalColors.Add(r.color);
                }
                Material white = ArtCatalog.Flash;
                usedMaterial = white != null;
                foreach (SpriteRenderer r in flashed)
                {
                    if (usedMaterial) r.sharedMaterial = white;
                    else r.color = Color.white;
                }
                IsFlashing = true;
            }
            until = Time.time + seconds;
            Flashes++;
            Started?.Invoke(this);
        }

        private IEnumerable<SpriteRenderer> Targets()
        {
            if (fixedRenderers != null)
            {
                foreach (SpriteRenderer r in fixedRenderers) yield return r;
                yield break;
            }
            foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>())
                if (Array.IndexOf(Excluded, r.gameObject.name) < 0) yield return r;
        }

        private void Update()
        {
            if (IsFlashing && Time.time >= until) End();
        }

        /// <summary>Ends the flash now, restoring every renderer.</summary>
        public void End()
        {
            if (!IsFlashing) return;
            for (int i = 0; i < flashed.Count; i++)
            {
                SpriteRenderer r = flashed[i];
                if (r == null) continue;
                if (usedMaterial) r.sharedMaterial = originalMaterials[i];
                else r.color = originalColors[i];
            }
            flashed.Clear();
            IsFlashing = false;
        }
    }
}
