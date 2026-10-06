using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// The player's melee swing on Player/Attack: a short box in front of the last move direction that hits
    /// each Health in it once, for base damage × the power multiplier, carrying the equipped item (so an
    /// enemy weak to it takes far more). With nothing equipped it is a bare hit at base damage. Has a
    /// cooldown, and shows a brief placeholder swing.
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        [SerializeField] private string attackActionPath = "Player/Attack";
        [SerializeField, Min(0f)] private float baseDamage = CombatMath.BaseAttack;
        [SerializeField, Min(0f)] private float cooldown = 0.35f;
        [SerializeField, Min(0f)] private float reach = 0.85f;
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.1f, 1.1f);
        [SerializeField, Min(0f)] private float swingVisibleSeconds = 0.12f;

        private InputAction attack;
        private float readyAt = float.NegativeInfinity;
        private PlayerStats stats;
        private Equipment equipment;
        private PlayerMover mover;
        private Health ownHealth;
        private SpriteRenderer swing;
        private float swingHideAt;

        /// <summary>Raised after every swing, with the item carried (null = bare) and the number of targets that took damage.</summary>
        public event Action<ItemDefinition, int> Swung;

        public float BaseDamage
        {
            get => baseDamage;
            set => baseDamage = Mathf.Max(0f, value);
        }

        public float Cooldown
        {
            get => cooldown;
            set => cooldown = Mathf.Max(0f, value);
        }

        public float Reach => reach;
        public Vector2 HitboxSize => hitboxSize;

        /// <summary>Damage a swing deals before the target's modifiers (weakness, defence).</summary>
        public float Damage => CombatMath.Outgoing(baseDamage, Stats != null ? Stats.Power : 1);

        public ItemDefinition EquippedItem => Equipment != null ? Equipment.Equipped : null;

        /// <summary>Direction the swing goes: the mover's last move direction (down before any move).</summary>
        public Vector2 Facing
        {
            get
            {
                if (mover == null) mover = GetComponent<PlayerMover>();
                return mover != null ? mover.Facing : Vector2.down;
            }
        }

        public bool Ready => Time.time >= readyAt;

        private PlayerStats Stats => stats != null ? stats : stats = GetComponent<PlayerStats>();
        private Equipment Equipment => equipment != null ? equipment : equipment = GetComponent<Equipment>();

        private void Awake()
        {
            ownHealth = GetComponent<Health>();
        }

        private void OnEnable()
        {
            attack = NpcInput.Find(attackActionPath, this);
        }

        private void OnDisable()
        {
            attack = null; // shared project-wide action: never disabled here
            if (swing != null) swing.enabled = false;
        }

        private void Update()
        {
            if (swing != null && swing.enabled && Time.time >= swingHideAt) swing.enabled = false;
            if (attack == null || !attack.WasPressedThisFrame()) return;
            // A conversation owns Interact, not Attack, but swinging mid-dialogue would still be odd.
            if (DialogueOpen()) return;
            TryAttack();
        }

        /// <summary>Swings if alive and off cooldown. Returns the number of targets that took damage, or -1 if no swing happened.</summary>
        public int TryAttack()
        {
            if (!isActiveAndEnabled || !Ready) return -1;
            if (Stats != null && Stats.IsDead) return -1;
            readyAt = Time.time + cooldown;

            Vector2 facing = Facing;
            Vector2 centre = (Vector2)transform.position + facing * reach;
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            ShowSwing(centre, angle);

            ItemDefinition item = EquippedItem;
            float damage = Damage;
            var hit = new HashSet<Health>();
            int damaged = 0;
            Physics2D.SyncTransforms();
            foreach (Collider2D collider in Physics2D.OverlapBoxAll(centre, hitboxSize, angle))
            {
                if (collider == null) continue;
                Health target = collider.attachedRigidbody != null
                    ? collider.attachedRigidbody.GetComponent<Health>()
                    : collider.GetComponentInParent<Health>();
                if (target == null || target == ownHealth || !hit.Add(target)) continue;
                // Each target is tried once per swing; only those that actually took damage count.
                if (target.TakeDamage(damage, item) > 0f) damaged++;
            }
            Swung?.Invoke(item, damaged);
            return damaged;
        }

        private static bool DialogueOpen()
        {
            var box = FindAnyObjectByType<DialogueBox>();
            return box != null && box.IsOpen;
        }

        private void ShowSwing(Vector2 centre, float angle)
        {
            if (swingVisibleSeconds <= 0f) return;
            if (swing == null)
            {
                var visual = GetComponentInChildren<SpriteRenderer>();
                var go = new GameObject("Swing");
                go.transform.SetParent(transform, false);
                swing = go.AddComponent<SpriteRenderer>();
                swing.sprite = NpcFactory.ShapeSprite(PartShape.Diamond);
                swing.sortingOrder = 11;
                if (visual != null) swing.sharedMaterial = visual.sharedMaterial;
            }
            ItemDefinition item = EquippedItem;
            Color colour = item != null ? item.PlaceholderColor : Color.white;
            colour.a = 0.7f;
            swing.color = colour;
            swing.transform.position = new Vector3(centre.x, centre.y, transform.position.z);
            swing.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            swing.transform.localScale = new Vector3(hitboxSize.x, hitboxSize.y * 0.6f, 1f);
            swing.enabled = true;
            swingHideAt = Time.time + swingVisibleSeconds;
        }
    }
}
