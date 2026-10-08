using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// What the player took from roadblocks this run, besides items and Jumbles* (4.7), on the player: keys, in-run boosts and
    /// fragment slots found. A boost is one PlayerStats point at once (+1 max heart, also healed; +speed; +power) for the
    /// rest of the run. Keys are held for the run: 4.9's locked edges spend them (TrySpendKey); the RunLoop cashes in the
    /// rest at the run's end (CashInKeys, RewardPlacement.KeyCashIn each). A rebuild (a new run) forgets all of it and takes
    /// the boosts back off the stats; the RunLoop's player reset forgets it (Clear) before it sets the stats afresh.
    /// </summary>
    public class RunRewards : MonoBehaviour
    {
        [SerializeField] private CubeWorld world;

        private readonly List<RunUpgrade> upgrades = new List<RunUpgrade>();
        private readonly List<(FaceId Face, int Ordinal)> fragments = new List<(FaceId, int)>();
        private CubeWorld subscribed;

        public int Keys { get; private set; }

        /// <summary>The boosts taken this run, in order.</summary>
        public IReadOnlyList<RunUpgrade> Upgrades => upgrades;

        /// <summary>The fragments found this run (their face and number), in the order found (4.8).</summary>
        public IReadOnlyList<(FaceId Face, int Ordinal)> Fragments => fragments;

        /// <summary>How many of a face's fragments are held.</summary>
        public int FragmentsOf(FaceId face)
        {
            int n = 0;
            foreach ((FaceId f, int _) in fragments)
                if (f == face) n++;
            return n;
        }

        /// <summary>True when a face's fragment of that number is held.</summary>
        public bool HasFragment(FaceId face, int ordinal) => fragments.Contains((face, ordinal));

        /// <summary>Raised whenever keys, boosts or fragments change.</summary>
        public event Action<RunRewards> Changed;

        /// <summary>The HUD line, or null when nothing is held.</summary>
        public string HudText => RewardText.HudLine(Keys, upgrades);

        public CubeWorld World
        {
            get => world;
            set
            {
                world = value;
                Subscribe();
            }
        }

        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable()
        {
            if (subscribed != null) subscribed.Rebuilt -= ResetRun;
            subscribed = null;
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled) return;
            if (world == null && TryGetComponent(out CubeNavigator navigator)) world = navigator.World;
            if (subscribed == world) return;
            if (subscribed != null) subscribed.Rebuilt -= ResetRun;
            subscribed = world;
            if (subscribed != null) subscribed.Rebuilt += ResetRun;
        }

        /// <summary>Takes a reward found on a face (keys, boosts, fragment slots; Jumbles* and items go elsewhere). Returns true if taken.</summary>
        public bool Grant(Reward reward, FaceId face)
        {
            switch (reward.Kind)
            {
                case RewardKind.Key:
                    Keys++;
                    break;
                case RewardKind.Upgrade:
                    upgrades.Add(reward.Upgrade);
                    Apply(reward.Upgrade, +1);
                    break;
                case RewardKind.FragmentSlot:
                    if (fragments.Contains((face, reward.Ordinal))) return false;
                    fragments.Add((face, reward.Ordinal));
                    break;
                default:
                    return false;
            }
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>Spends a key (4.9's locks). False when none is held.</summary>
        public bool TrySpendKey()
        {
            if (Keys <= 0) return false;
            Keys--;
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>The run's end: returns the keys still held and lets them go (the RunLoop pays KeyCashIn for each).</summary>
        public int CashInKeys()
        {
            int keys = Keys;
            Keys = 0;
            if (keys > 0) Changed?.Invoke(this);
            return keys;
        }

        /// <summary>A new run: the boosts come off the stats, and keys and fragments are forgotten.</summary>
        public void ResetRun()
        {
            for (int i = upgrades.Count - 1; i >= 0; i--) Apply(upgrades[i], -1);
            Clear();
        }

        /// <summary>Forgets everything without touching the stats (the RunLoop then sets them afresh).</summary>
        public void Clear()
        {
            bool any = Keys > 0 || upgrades.Count > 0 || fragments.Count > 0;
            Keys = 0;
            upgrades.Clear();
            fragments.Clear();
            if (any) Changed?.Invoke(this);
        }

        private void Apply(RunUpgrade upgrade, int sign)
        {
            if (!TryGetComponent(out PlayerStats stats)) return;
            switch (upgrade)
            {
                case RunUpgrade.Heart:
                    stats.MaxHealth += sign; // Health.SetMax adds the new heart filled
                    break;
                case RunUpgrade.Speed:
                    stats.Speed += sign;
                    break;
                default:
                    stats.Power += sign;
                    break;
            }
        }
    }
}
