using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The damage rules, pure so they can be tested on their own. Power and speed scale by 25% per point
    /// above 1; defence subtracts from incoming damage with a floor of 1; an enemy's weakness item
    /// multiplies the damage it takes.
    /// </summary>
    public static class CombatMath
    {
        /// <summary>Damage of a bare (or non-weakness) player hit before power.</summary>
        public const float BaseAttack = 1f;

        /// <summary>Default damage multiplier for a hit with the enemy's weakness item.</summary>
        public const float DefaultWeaknessMultiplier = 4f;

        /// <summary>The weakness multiplier is never allowed below this (weakness hits are markedly stronger).</summary>
        public const float MinWeaknessMultiplier = 3f;

        /// <summary>Scale per stat point above 1 for power and speed.</summary>
        public const float PerPoint = 0.25f;

        /// <summary>Stat multiplier: 1 point = ×1.0, +0.25 per point above (never below ×0.25).</summary>
        public static float StatMultiplier(int points) => Mathf.Max(PerPoint, 1f + PerPoint * (points - 1));

        /// <summary>Damage a player hit deals before the target's modifiers: base × power multiplier.</summary>
        public static float Outgoing(float baseDamage, int power) => Mathf.Max(0f, baseDamage) * StatMultiplier(power);

        /// <summary>Damage actually taken by a defender: raw minus defence, never below 1 (for raw &gt; 0).</summary>
        public static float Incoming(float raw, int defence)
        {
            if (raw <= 0f) return 0f;
            return Mathf.Max(1f, raw - Mathf.Max(0, defence));
        }

        /// <summary>Damage an enemy takes from a hit carrying sourceItem: multiplied when it is the weakness.</summary>
        public static float AgainstWeakness(float raw, ItemDefinition sourceItem, ItemDefinition weakness, float multiplier)
        {
            if (raw <= 0f) return 0f;
            bool weak = weakness != null && sourceItem == weakness;
            return weak ? raw * Mathf.Max(MinWeaknessMultiplier, multiplier) : raw;
        }
    }

    /// <summary>A component that adjusts the damage its object's Health takes (defence, weakness, ...).</summary>
    public interface IDamageModifier
    {
        float ModifyIncoming(float amount, ItemDefinition sourceItem);
    }
}
