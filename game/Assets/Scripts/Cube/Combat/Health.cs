using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Hit points shared by the player, enemies and hostile NPCs. TakeDamage runs the amount through every
    /// enabled IDamageModifier on the object (defence, weakness), subtracts it and starts a short
    /// invulnerability window during which further damage is ignored. At 0 it raises Died exactly once;
    /// after that all damage is ignored. A disabled Health (a peaceful NPC) takes no damage.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float max = 6f;
        [SerializeField, Min(0f)] private float invulnerableSeconds = 0.8f;

        private float current = -1f;
        private float invulnerableUntil = float.NegativeInfinity;

        /// <summary>Raised after damage is taken, with the amount actually taken and the item that dealt it (null for bare/contact).</summary>
        public event Action<Health, float, ItemDefinition> Damaged;

        /// <summary>Raised once, when health first reaches 0.</summary>
        public event Action<Health> Died;

        public float Max => max;

        public float Current
        {
            get
            {
                if (current < 0f) current = max;
                return current;
            }
        }

        public bool IsDead { get; private set; }

        public float InvulnerableSeconds
        {
            get => invulnerableSeconds;
            set => invulnerableSeconds = Mathf.Max(0f, value);
        }

        /// <summary>Time source for the invulnerability window (Time.time unless set; EditMode tests set it).</summary>
        public Func<float> Clock { get; set; }

        private float Now => Clock != null ? Clock() : Time.time;

        public bool IsInvulnerable => Now < invulnerableUntil;

        /// <summary>Sets the maximum. Raising it adds the difference to current health; lowering it caps current.</summary>
        public void SetMax(float value)
        {
            value = Mathf.Max(1f, value);
            float before = Current;
            float gained = Mathf.Max(0f, value - max);
            max = value;
            if (!IsDead) current = Mathf.Min(max, before + gained);
        }

        /// <summary>Back to full health and alive (tests and tools; no run loop uses it yet).</summary>
        public void ResetHealth()
        {
            IsDead = false;
            current = max;
            invulnerableUntil = float.NegativeInfinity;
        }

        /// <summary>
        /// Applies damage from sourceItem (null for bare hits and contact). Returns the damage actually
        /// taken: 0 when disabled, dead, invulnerable or the amount is not positive.
        /// </summary>
        public float TakeDamage(float amount, ItemDefinition sourceItem)
        {
            if (!enabled || !gameObject.activeInHierarchy || IsDead || amount <= 0f || IsInvulnerable) return 0f;

            foreach (IDamageModifier modifier in GetComponents<IDamageModifier>())
            {
                if (modifier is Behaviour behaviour && !behaviour.enabled) continue;
                amount = modifier.ModifyIncoming(amount, sourceItem);
            }
            if (amount <= 0f) return 0f;

            float before = Current;
            current = Mathf.Max(0f, before - amount);
            float taken = before - current;
            if (invulnerableSeconds > 0f) invulnerableUntil = Now + invulnerableSeconds;
            Damaged?.Invoke(this, taken, sourceItem);
            if (current <= 0f && !IsDead)
            {
                IsDead = true;
                Died?.Invoke(this);
            }
            return taken;
        }
    }
}
