using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>Where a run's meta currency came from.</summary>
    public enum EarningSource { Trial = 0, Boss = 1, Hidden = 2, Chest = 3, Keys = 4 }

    /// <summary>
    /// A run's earnings, pure: each source key (one trial room, the boss, one hidden item) pays at most once per run, so
    /// re-triggering never double-counts. Totals per source feed the between-runs screen.
    /// </summary>
    public sealed class RunEarnings
    {
        private readonly HashSet<string> paid = new HashSet<string>();
        private const int SourceCount = 5;
        private readonly int[] bySource = new int[SourceCount];
        private readonly int[] countBySource = new int[SourceCount];

        public int Total { get; private set; }

        /// <summary>Adds amount from a source key. Returns false (nothing added) for a key already paid or a negative amount.</summary>
        public bool Add(EarningSource source, string key, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(key) || !paid.Add(key)) return false;
            bySource[(int)source] += amount;
            countBySource[(int)source]++;
            Total += amount;
            return true;
        }

        public bool HasPaid(string key) => paid.Contains(key);

        /// <summary>Currency earned from one kind of source.</summary>
        public int Of(EarningSource source) => bySource[(int)source];

        /// <summary>How many sources of a kind paid (trials completed, hidden items found, boss won).</summary>
        public int CountOf(EarningSource source) => countBySource[(int)source];

        public void Reset()
        {
            paid.Clear();
            Array.Clear(bySource, 0, bySource.Length);
            Array.Clear(countBySource, 0, countBySource.Length);
            Total = 0;
        }

        public RunEarnings Copy()
        {
            var copy = new RunEarnings();
            foreach (string key in paid) copy.paid.Add(key);
            Array.Copy(bySource, copy.bySource, bySource.Length);
            Array.Copy(countBySource, copy.countBySource, countBySource.Length);
            copy.Total = Total;
            return copy;
        }
    }

    /// <summary>
    /// This run's meta-currency earnings: every trial room of the run (TrialRoom.Completed, its own amount), a core-boss
    /// victory (BossVictory), each hidden currency item (HiddenCurrencyPickup.Collected, its amount) and each Jumbles* chest
    /// behind a roadblock (4.7, RewardChest.Opened), each counted once. Earnings stop once the run has ended, except what the
    /// run's end itself pays (AddAtRunEnd: the unused keys cashed in). Bank (called by the RunLoop on RunEnded, win or lose) adds them to the meta
    /// balance once per run. A world rebuild (a new run) starts from zero.
    /// </summary>
    public class RunWallet : MonoBehaviour
    {
        /// <summary>Meta currency for beating Maxwell's Demon.</summary>
        public const int BossVictory = 60;

        [SerializeField] private CubeWorld world;
        [SerializeField] private CoreArena arena;
        [SerializeField] private RunState runState;

        private readonly RunEarnings earnings = new RunEarnings();
        private readonly List<TrialRoom> trials = new List<TrialRoom>();
        private readonly List<HiddenCurrencyPickup> hidden = new List<HiddenCurrencyPickup>();
        private readonly List<RewardChest> chests = new List<RewardChest>();
        private CubeWorld subscribedWorld;
        private CoreArena subscribedArena;

        /// <summary>Raised whenever something pays during the run (source, amount): the pickup popups listen to it.</summary>
        public event Action<EarningSource, int> Earned;

        /// <summary>Raised for what the run's end pays (AddAtRunEnd: the keys cashed in), with no popup: the run is over.</summary>
        public event Action<EarningSource, int> EarnedAtRunEnd;

        public RunEarnings Earnings => earnings;

        public int Total => earnings.Total;

        /// <summary>True once this run's earnings were banked.</summary>
        public bool Banked { get; private set; }

        public void Configure(CubeWorld cubeWorld, CoreArena coreArena, RunState run)
        {
            Unsubscribe();
            world = cubeWorld;
            arena = coreArena;
            runState = run;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            Unsubscribe();
            if (world != null)
            {
                subscribedWorld = world;
                world.Rebuilt += ResetRun;
                world.Revealed += HookRun;
                world.RewardChestBuilt += HookChest;
                if (world.ScienceRevealed) HookRun();
            }
            if (arena != null)
            {
                subscribedArena = arena;
                arena.FightEnded += OnFightEnded;
            }
        }

        private void Unsubscribe()
        {
            if (subscribedWorld != null)
            {
                subscribedWorld.Rebuilt -= ResetRun;
                subscribedWorld.Revealed -= HookRun;
                subscribedWorld.RewardChestBuilt -= HookChest;
                subscribedWorld = null;
            }
            if (subscribedArena != null)
            {
                subscribedArena.FightEnded -= OnFightEnded;
                subscribedArena = null;
            }
            UnhookRun();
        }

        /// <summary>Adds currency from a source key, once per key and only while the run is on. Returns true if it paid.</summary>
        public bool Add(EarningSource source, string key, int amount)
        {
            if (Banked || (runState != null && runState.Ended)) return false;
            if (!earnings.Add(source, key, amount)) return false;
            Earned?.Invoke(source, amount);
            return true;
        }

        /// <summary>
        /// Adds currency the run's end itself pays (4.7: the unused keys cashed in), once per key: after the run has ended,
        /// but never once banked. Returns true if it paid.
        /// </summary>
        public bool AddAtRunEnd(EarningSource source, string key, int amount)
        {
            if (Banked || !earnings.Add(source, key, amount)) return false;
            EarnedAtRunEnd?.Invoke(source, amount);
            return true;
        }

        /// <summary>Adds this run's earnings to the save's balance (once per run). Returns the amount banked (0 if already banked).</summary>
        public int Bank(MetaSave save)
        {
            if (Banked || save == null) return 0;
            Banked = true;
            save.Balance += earnings.Total;
            return earnings.Total;
        }

        /// <summary>A new run: nothing earned, nothing banked, no hooks into the old run's trials and items.</summary>
        public void ResetRun()
        {
            UnhookRun();
            earnings.Reset();
            Banked = false;
        }

        /// <summary>Hooks the revealed run's trial rooms (only those in the current modules) and hidden currency items.</summary>
        private void HookRun()
        {
            UnhookRun();
            if (world == null) return;
            foreach (ScreenModule module in world.AllModules)
            {
                if (module == null) continue;
                foreach (TrialRoom trial in module.GetComponentsInChildren<TrialRoom>(true))
                {
                    trial.Completed += OnTrialCompleted(trial, trials.Count);
                    trials.Add(trial);
                }
            }
            foreach (HiddenCurrencyPickup pickup in world.HiddenCurrencyPickups)
            {
                if (pickup == null) continue;
                hidden.Add(pickup);
                pickup.Collected += OnHiddenCollected;
            }
            foreach (RewardChest chest in world.RewardChests) HookChest(chest);
        }

        private readonly Dictionary<TrialRoom, Action<int>> trialHandlers = new Dictionary<TrialRoom, Action<int>>();

        /// <summary>The trial's handler, keyed by its index among the run's trial rooms (a stable id within the run).</summary>
        private Action<int> OnTrialCompleted(TrialRoom trial, int index)
        {
            Action<int> handler = amount => Add(EarningSource.Trial, "trial:" + index, amount);
            trialHandlers[trial] = handler;
            return handler;
        }

        private void UnhookRun()
        {
            foreach (TrialRoom trial in trials)
                if (trial != null && trialHandlers.TryGetValue(trial, out Action<int> handler)) trial.Completed -= handler;
            trials.Clear();
            trialHandlers.Clear();
            foreach (HiddenCurrencyPickup pickup in hidden)
                if (pickup != null) pickup.Collected -= OnHiddenCollected;
            hidden.Clear();
            foreach (RewardChest chest in chests)
                if (chest != null) chest.Opened -= OnChestOpened;
            chests.Clear();
        }

        /// <summary>
        /// Hooks one reward chest (once): each chest as CubeWorld builds it (RewardChestBuilt, so the puzzle goals' chests that
        /// 4.10-4.12 build after the reveal pay too), and every chest of the run on Revealed.
        /// </summary>
        private void HookChest(RewardChest chest)
        {
            if (chest == null || chests.Contains(chest)) return;
            chests.Add(chest);
            chest.Opened += OnChestOpened;
        }

        /// <summary>A Jumbles* chest pays its amount (keys, boosts and fragments go to the player's RunRewards).</summary>
        private void OnChestOpened(RewardChest chest, GameObject collector)
        {
            if (chest.Kind == RewardKind.Jumbles) Add(EarningSource.Chest, "chest:" + chest.Spot.Index, chest.Spot.What.Amount);
        }

        private void OnHiddenCollected(HiddenCurrencyPickup pickup) =>
            Add(EarningSource.Hidden, "hidden:" + pickup.Index, pickup.Amount);

        private void OnFightEnded(bool victory)
        {
            if (victory) Add(EarningSource.Boss, "boss", BossVictory);
        }
    }
}
