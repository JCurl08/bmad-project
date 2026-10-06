using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The enemy base: a weakness item whose hits are multiplied (at least x3, x4 by default), the damage it
    /// deals on contact (EnemyBrain applies it), and removal when its Health dies. Face-specific enemies
    /// build on this; hostile NPCs reuse it. Spawn builds a placeholder enemy.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour, IDamageModifier
    {
        public const float DefaultHealth = 8f;
        public const float DefaultContactDamage = 1f;
        public const float DefaultInvulnerableSeconds = 0.1f;
        public const float BodyRadius = 0.4f;
        public const float BodyMass = 5f;
        public const int SortingOrder = 7;

        [SerializeField] private ItemDefinition weakness;
        [SerializeField, Min(CombatMath.MinWeaknessMultiplier)] private float weaknessMultiplier = CombatMath.DefaultWeaknessMultiplier;
        [SerializeField, Min(0f)] private float contactDamage = DefaultContactDamage;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField, Min(0f)] private float flashSeconds = 0.1f;

        private Health health;
        private SpriteRenderer[] flashRenderers = Array.Empty<SpriteRenderer>();
        private Color[] flashBase = Array.Empty<Color>();
        private bool flashing;
        private float flashUntil;

        /// <summary>Raised once when this enemy dies (before it is removed).</summary>
        public event Action<Enemy> Died;

        /// <summary>Every enabled enemy (for tests and tools).</summary>
        public static readonly List<Enemy> Active = new List<Enemy>();

        public ItemDefinition Weakness
        {
            get => weakness;
            set => weakness = value;
        }

        public float WeaknessMultiplier
        {
            get => weaknessMultiplier;
            set => weaknessMultiplier = Mathf.Max(CombatMath.MinWeaknessMultiplier, value);
        }

        public float ContactDamage
        {
            get => contactDamage;
            set => contactDamage = Mathf.Max(0f, value);
        }

        public bool DestroyOnDeath
        {
            get => destroyOnDeath;
            set => destroyOnDeath = value;
        }

        public Health Health => health != null ? health : health = GetComponent<Health>();

        /// <summary>Renderers flashed white on a hit (placeholder feedback; empty for none).</summary>
        public void SetFlashRenderers(SpriteRenderer[] renderers)
        {
            EndFlash();
            flashRenderers = renderers ?? Array.Empty<SpriteRenderer>();
            flashBase = new Color[flashRenderers.Length];
        }

        public float ModifyIncoming(float amount, ItemDefinition sourceItem) =>
            CombatMath.AgainstWeakness(amount, sourceItem, weakness, weaknessMultiplier);

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            Health.Died += OnDied;
            Health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (health != null)
            {
                health.Died -= OnDied;
                health.Damaged -= OnDamaged;
            }
            EndFlash();
        }

        private void Update()
        {
            if (flashing && Time.time >= flashUntil) EndFlash();
        }

        private void OnDamaged(Health _, float amount, ItemDefinition item)
        {
            if (flashSeconds <= 0f || flashRenderers.Length == 0) return;
            if (!flashing)
                for (int i = 0; i < flashRenderers.Length; i++)
                    if (flashRenderers[i] != null) flashBase[i] = flashRenderers[i].color;
            foreach (SpriteRenderer r in flashRenderers)
                if (r != null) r.color = Color.white;
            flashing = true;
            flashUntil = Time.time + flashSeconds;
        }

        private void EndFlash()
        {
            if (!flashing) return;
            for (int i = 0; i < flashRenderers.Length; i++)
                if (flashRenderers[i] != null) flashRenderers[i].color = flashBase[i];
            flashing = false;
        }

        private void OnDied(Health _)
        {
            Died?.Invoke(this);
            if (!destroyOnDeath) return;
            gameObject.SetActive(false); // gone this frame (no more contact damage), destroyed at frame end
            Destroy(gameObject);
        }

        /// <summary>
        /// Builds a placeholder enemy: a diamond tinted by its weakness item's colour on a dynamic,
        /// velocity-driven body (like NPCs, so walls and closed gates stop it), with Health, Enemy and an
        /// EnemyBrain that chases the player (or the scene's player when null) inside bounds.
        /// </summary>
        public static Enemy Spawn(ItemDefinition weakness, Vector2 position, Rect bounds, Transform player = null,
            Transform parent = null, Material material = null)
        {
            var go = new GameObject(weakness != null ? "Enemy (weak to " + weakness + ")" : "Enemy");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.mass = BodyMass;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<CircleCollider2D>().radius = BodyRadius;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(BodyRadius * 2.2f, BodyRadius * 2.2f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = NpcFactory.ShapeSprite(PartShape.Diamond);
            if (material != null) renderer.sharedMaterial = material;
            Color colour = weakness != null ? weakness.PlaceholderColor : new Color(0.8f, 0.8f, 0.8f);
            renderer.color = Color.Lerp(colour, new Color(0.6f, 0f, 0.1f), 0.45f);
            renderer.sortingOrder = SortingOrder;

            var health = go.AddComponent<Health>();
            health.SetMax(DefaultHealth);
            health.InvulnerableSeconds = DefaultInvulnerableSeconds;

            var enemy = go.AddComponent<Enemy>();
            enemy.Weakness = weakness;
            enemy.SetFlashRenderers(new[] { renderer });

            var brain = go.AddComponent<EnemyBrain>();
            brain.Configure(bounds, player);
            return enemy;
        }
    }
}
