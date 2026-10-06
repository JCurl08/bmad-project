using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The four meta upgrades. Each raises one PlayerStats value by one point per level.</summary>
    public enum UpgradeStat { Health = 0, Defence = 1, Power = 2, Speed = 3 }

    /// <summary>What a purchase attempt did.</summary>
    public enum PurchaseOutcome { Bought, NotEnough, Maxed }

    public readonly struct PurchaseResult
    {
        public readonly PurchaseOutcome Outcome;
        public readonly UpgradeStat Stat;
        public readonly int Level;
        public readonly int Cost;
        public readonly string Message;

        public PurchaseResult(PurchaseOutcome outcome, UpgradeStat stat, int level, int cost, string message)
        {
            Outcome = outcome;
            Stat = stat;
            Level = level;
            Cost = cost;
            Message = message;
        }

        public bool Bought => Outcome == PurchaseOutcome.Bought;
    }

    /// <summary>The player's base stat values, before upgrades (read from the scene's PlayerStats at start).</summary>
    public readonly struct StatBlock : IEquatable<StatBlock>
    {
        public readonly int MaxHealth;
        public readonly int Defence;
        public readonly int Power;
        public readonly int Speed;

        public StatBlock(int maxHealth, int defence, int power, int speed)
        {
            MaxHealth = maxHealth;
            Defence = defence;
            Power = power;
            Speed = speed;
        }

        public static StatBlock Of(PlayerStats stats) => new StatBlock(stats.MaxHealth, stats.Defence, stats.Power, stats.Speed);

        public static readonly StatBlock Default = new StatBlock(PlayerStats.DefaultMaxHealth, 0, 1, 1);

        public int Get(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Health: return MaxHealth;
                case UpgradeStat.Defence: return Defence;
                case UpgradeStat.Power: return Power;
                default: return Speed;
            }
        }

        public void ApplyTo(PlayerStats stats)
        {
            stats.MaxHealth = MaxHealth;
            stats.Defence = Defence;
            stats.Power = Power;
            stats.Speed = Speed;
        }

        public bool Equals(StatBlock o) => MaxHealth == o.MaxHealth && Defence == o.Defence && Power == o.Power && Speed == o.Speed;
        public override bool Equals(object obj) => obj is StatBlock o && Equals(o);
        public override int GetHashCode() => ((MaxHealth * 31 + Defence) * 31 + Power) * 31 + Speed;
        public override string ToString() => $"health {MaxHealth}, defence {Defence}, power {Power}, speed {Speed}";
    }

    /// <summary>
    /// The upgrade rules, pure: cost = BaseCost × (level + 1) for the next level, MaxLevel levels per stat, +1 stat point
    /// per level (a heart, a point of defence = 15% less damage taken, multiplicative; +25% power, +25% speed). Purchases spend from a MetaSave; their effect
    /// reaches the player only through PlayerStats (Apply), at the start of the next run.
    /// </summary>
    public static class UpgradeShop
    {
        public const int MaxLevel = 5;
        public const int BaseCost = 20;

        /// <summary>Stat points added per upgrade level.</summary>
        public const int PointsPerLevel = 1;

        public static readonly UpgradeStat[] All = { UpgradeStat.Health, UpgradeStat.Defence, UpgradeStat.Power, UpgradeStat.Speed };

        /// <summary>The save id of an upgrade (stable: never rename).</summary>
        public static string Id(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Health: return "health";
                case UpgradeStat.Defence: return "defence";
                case UpgradeStat.Power: return "power";
                default: return "speed";
            }
        }

        public static string Name(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Health: return "Extra Heart";
                case UpgradeStat.Defence: return "Thick Skin";
                case UpgradeStat.Power: return "Big Bonk";
                default: return "Zoomies";
            }
        }

        public static string Blurb(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Health: return "More of you to fall apart.";
                case UpgradeStat.Defence: return "Bonks bounce off. Mostly.";
                case UpgradeStat.Power: return "Your swings mix things up harder.";
                default: return "Diffuse faster than a sneeze in a lift.";
            }
        }

        /// <summary>Cost of buying the level after `level` (level 0 → 1 costs BaseCost).</summary>
        public static int Cost(int level) => BaseCost * (Mathf.Max(0, level) + 1);

        public static int Level(MetaSave save, UpgradeStat stat) =>
            save != null ? Mathf.Clamp(save.UpgradeLevel(Id(stat)), 0, MaxLevel) : 0;

        public static bool IsMaxed(MetaSave save, UpgradeStat stat) => Level(save, stat) >= MaxLevel;

        /// <summary>Cost of the next level, or 0 when maxed.</summary>
        public static int NextCost(MetaSave save, UpgradeStat stat) => IsMaxed(save, stat) ? 0 : Cost(Level(save, stat));

        /// <summary>Spends the balance on the next level of a stat. Refused (nothing changes) when maxed or too poor.</summary>
        public static PurchaseResult TryBuy(MetaSave save, UpgradeStat stat)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            int level = Level(save, stat);
            if (level >= MaxLevel)
                return new PurchaseResult(PurchaseOutcome.Maxed, stat, level, 0,
                    $"{Name(stat)} is maxed out. Any more and you'd be a different shape.");
            int cost = Cost(level);
            if (save.Balance < cost)
                return new PurchaseResult(PurchaseOutcome.NotEnough, stat, level, cost,
                    $"Not enough Jumbles for {Name(stat)} ({save.Balance}/{cost}). The Demon calls that \"balanced\".");
            save.Balance -= cost;
            save.SetUpgradeLevel(Id(stat), level + 1);
            return new PurchaseResult(PurchaseOutcome.Bought, stat, level + 1, cost,
                $"{Name(stat)} level {level + 1}! It kicks in next run.");
        }

        /// <summary>The stats for a run: the base plus every upgrade level.</summary>
        public static StatBlock Apply(StatBlock baseStats, MetaSave save) =>
            new StatBlock(
                baseStats.MaxHealth + PointsPerLevel * Level(save, UpgradeStat.Health),
                baseStats.Defence + PointsPerLevel * Level(save, UpgradeStat.Defence),
                baseStats.Power + PointsPerLevel * Level(save, UpgradeStat.Power),
                baseStats.Speed + PointsPerLevel * Level(save, UpgradeStat.Speed));

        /// <summary>What a stat is at `level` levels over its base value, as shown in the shop.</summary>
        public static string EffectText(UpgradeStat stat, int baseValue, int level)
        {
            int value = baseValue + PointsPerLevel * level;
            switch (stat)
            {
                case UpgradeStat.Health: return $"{value} hearts";
                case UpgradeStat.Defence: return $"-{Mathf.RoundToInt((1f - CombatMath.DefenceMultiplier(value)) * 100f)}% damage taken";
                case UpgradeStat.Power: return $"x{CombatMath.StatMultiplier(value):0.##} damage";
                default: return $"x{CombatMath.StatMultiplier(value):0.##} speed";
            }
        }
    }
}
