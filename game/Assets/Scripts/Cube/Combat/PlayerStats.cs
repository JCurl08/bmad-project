using System;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The player's stats in one place: max health, defence, power and speed. Movement (PlayerMover, via
    /// IMoveSpeedScale), damage taken (Health, via IDamageModifier) and PlayerAttack all read them, so
    /// upgrades only change these values. It also pushes max health into the player's Health and, when
    /// that Health dies, stops player control (movement, attack, item cycling; NPCs refuse to talk) and does nothing more.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerStats : MonoBehaviour, IMoveSpeedScale, IDamageModifier
    {
        public const int DefaultMaxHealth = 6;

        [SerializeField, Min(1)] private int maxHealth = DefaultMaxHealth;
        [SerializeField, Min(0)] private int defence;
        [SerializeField, Min(1)] private int power = 1;
        [SerializeField, Min(1)] private int speed = 1;

        private Health health;

        /// <summary>Raised whenever any stat value changes.</summary>
        public event Action<PlayerStats> Changed;

        /// <summary>Raised once when the player dies (after control has stopped).</summary>
        public event Action<PlayerStats> Died;

        public int MaxHealth
        {
            get => maxHealth;
            set => Set(ref maxHealth, Mathf.Max(1, value));
        }

        /// <summary>Subtracted from every hit taken (a hit always does at least 1).</summary>
        public int Defence
        {
            get => defence;
            set => Set(ref defence, Mathf.Max(0, value));
        }

        /// <summary>Outgoing damage multiplier points: 1 = ×1.0, +0.25 per point.</summary>
        public int Power
        {
            get => power;
            set => Set(ref power, Mathf.Max(1, value));
        }

        /// <summary>Movement multiplier points: 1 = ×1.0, +0.25 per point.</summary>
        public int Speed
        {
            get => speed;
            set => Set(ref speed, Mathf.Max(1, value));
        }

        public float PowerMultiplier => CombatMath.StatMultiplier(power);
        public float MoveSpeedMultiplier => CombatMath.StatMultiplier(speed);

        public Health Health => health != null ? health : health = GetComponent<Health>();

        public bool IsDead => Health != null && Health.IsDead;

        private void Awake()
        {
            if (Health != null) Health.SetMax(maxHealth);
        }

        private void OnEnable()
        {
            if (Health != null) Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (Health != null) Health.Died -= OnDied;
        }

        public float ModifyIncoming(float amount, ItemDefinition sourceItem) => CombatMath.Incoming(amount, defence);

        private void Set(ref int field, int value)
        {
            if (field == value) return;
            field = value;
            if (Health != null && !Mathf.Approximately(Health.Max, maxHealth)) Health.SetMax(maxHealth);
            Changed?.Invoke(this);
        }

        private void OnDied(Health _)
        {
            var mover = GetComponent<PlayerMover>();
            if (mover != null) mover.enabled = false;
            var attack = GetComponent<PlayerAttack>();
            if (attack != null) attack.enabled = false;
            var equipment = GetComponent<Equipment>();
            if (equipment != null) equipment.enabled = false;
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
            Died?.Invoke(this);
        }
    }
}
