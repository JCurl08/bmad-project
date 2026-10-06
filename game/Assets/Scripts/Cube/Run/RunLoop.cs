using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What the between-runs screen shows about the run that just ended.</summary>
    public sealed class RunSummary
    {
        public int Seed;
        public bool Victory;
        public RunEarnings Earnings;
        public int Banked;
        public int RunNumber;
    }

    /// <summary>
    /// The roguelite loop: run, end, spend, run again. It loads the MetaSave at start and applies it (stats = base +
    /// upgrades through PlayerStats; hint density from the saved run count: sparse on the first run, full afterwards).
    /// The player's death outside a core fight ends the run through RunState (the arena ends its own fights). On
    /// RunEnded, win or lose, it banks the RunWallet, counts the run (and a victory), saves, stops player control and
    /// opens the between-runs screen. Purchases (Buy) spend the balance and save at once; their effect starts with the
    /// next run. Continue starts it: a new non-repeating seed (time-based; the old one stays on the screen for replays),
    /// the world rebuilt (back in Town, the arena and RunState reset), and the player reset (empty inventory, bare hands,
    /// fresh isotope, mitt released, upgraded stats, full health, control back). Each item's first pickup in a run adds to
    /// its per-ability count (Epic 2), saved with the run.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Awake before CubeWorld's, so the first run is built once, on its fresh seed
    public class RunLoop : MonoBehaviour
    {
        /// <summary>
        /// Test/debug override: keep the scene's seed for the session's first run instead of a fresh one (tests stay
        /// deterministic). Reset on every play start.
        /// </summary>
        public static bool KeepSceneSeed { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => KeepSceneSeed = false;

        [Tooltip("Debug: start the first run on the scene's seed instead of a fresh one.")]
        [SerializeField] private bool keepSceneSeed;

        [SerializeField] private CubeWorld world;
        [SerializeField] private RunState runState;
        [SerializeField] private CoreArena arena;
        [SerializeField] private RunWallet wallet;
        [SerializeField] private PlayerStats player;

        private readonly HashSet<string> collectedThisRun = new HashSet<string>();
        private bool baseCaptured;
        private StatBlock baseStats = StatBlock.Default;
        private Inventory subscribedInventory;
        private PlayerStats subscribedPlayer;
        private RunState subscribedRunState;
        private CubeWorld subscribedWorld;

        /// <summary>The meta progress (loaded at start, saved at run end and after each purchase).</summary>
        public MetaSave Meta { get; private set; }

        /// <summary>How the save loaded at start.</summary>
        public MetaLoadStatus LoadStatus { get; private set; }

        /// <summary>True while the between-runs screen is up (from run end until Continue).</summary>
        public bool ScreenOpen { get; private set; }

        /// <summary>The run that just ended (null before the first run end).</summary>
        public RunSummary LastRun { get; private set; }

        /// <summary>The last purchase attempt's message for the screen (null when none since the screen opened).</summary>
        public string LastMessage { get; private set; }

        /// <summary>The player's stats before upgrades (captured from the scene at start).</summary>
        public StatBlock BaseStats => baseStats;

        /// <summary>The stats the next run starts with: base + upgrades.</summary>
        public StatBlock RunStats => UpgradeShop.Apply(baseStats, Meta);

        /// <summary>Hint density of the current run (from the saved run count when it started).</summary>
        public HintDensity HintDensity => HintGenerator.DensityFor(Meta != null ? Meta.RunCount : 0);

        /// <summary>Raised after the end-of-run bookkeeping, when the screen opens.</summary>
        public event Action<RunSummary> ScreenOpened;

        /// <summary>Raised after Continue has started the next run.</summary>
        public event Action<int> RunStarted;

        public CubeWorld World => world;
        public RunState RunState => runState;
        public RunWallet Wallet => wallet;
        public PlayerStats Player => player;

        public void Configure(CubeWorld cubeWorld, RunState run, CoreArena coreArena, RunWallet runWallet, PlayerStats playerStats)
        {
            Unsubscribe();
            world = cubeWorld;
            runState = run;
            arena = coreArena;
            wallet = runWallet;
            player = playerStats;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            StartFirstRunSeed();
            LoadMeta();
        }

        /// <summary>The session's first run gets a fresh, non-repeating seed (as Continue does), unless the scene seed is kept.</summary>
        private void StartFirstRunSeed()
        {
            if (world == null || KeepSceneSeed || keepSceneSeed) return;
            world.StartOnSeed(NextSeed(world.Seed, DateTime.UtcNow.Ticks));
        }

        private void Start()
        {
            ApplyHintDensity();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        /// <summary>(Re)loads the save and applies it to the current run: upgraded stats at full health, hint density.</summary>
        public void LoadMeta()
        {
            Meta = MetaSave.Load(MetaSave.PrefsKey, out MetaLoadStatus status);
            LoadStatus = status;
            CaptureBaseStats();
            ApplyStats(true);
            ApplyHintDensity();
        }

        private void CaptureBaseStats()
        {
            if (baseCaptured || player == null) return;
            baseStats = StatBlock.Of(player);
            baseCaptured = true;
        }

        private void Subscribe()
        {
            Unsubscribe();
            if (runState != null)
            {
                subscribedRunState = runState;
                runState.RunEnded += OnRunEnded;
            }
            if (player != null)
            {
                subscribedPlayer = player;
                player.Died += OnPlayerDied;
                subscribedInventory = player.GetComponent<Inventory>();
                if (subscribedInventory != null) subscribedInventory.ItemAdded += OnItemAdded;
            }
            if (world != null)
            {
                subscribedWorld = world;
                world.Rebuilt += OnRebuilt;
            }
        }

        private void Unsubscribe()
        {
            if (subscribedRunState != null) subscribedRunState.RunEnded -= OnRunEnded;
            if (subscribedPlayer != null) subscribedPlayer.Died -= OnPlayerDied;
            if (subscribedInventory != null) subscribedInventory.ItemAdded -= OnItemAdded;
            if (subscribedWorld != null) subscribedWorld.Rebuilt -= OnRebuilt;
            subscribedRunState = null;
            subscribedPlayer = null;
            subscribedInventory = null;
            subscribedWorld = null;
        }

        /// <summary>Death anywhere ends the run as a defeat; in a live core fight the arena does it (as its last step).</summary>
        private void OnPlayerDied(PlayerStats _)
        {
            if (arena != null && arena.FightActive && arena.PlayerInArena) return;
            if (runState != null) runState.EndRun(false);
        }

        private void OnRunEnded(bool victory)
        {
            if (Meta == null) Meta = new MetaSave();
            int banked = wallet != null ? wallet.Bank(Meta) : 0;
            Meta.RunCount++;
            if (victory) Meta.Victories++;
            SaveMeta();

            LastRun = new RunSummary
            {
                Seed = world != null ? world.Seed : 0,
                Victory = victory,
                Earnings = wallet != null ? wallet.Earnings.Copy() : new RunEarnings(),
                Banked = banked,
                RunNumber = Meta.RunCount,
            };
            LastMessage = null;
            FreezePlayer();
            ScreenOpen = true;
            ScreenOpened?.Invoke(LastRun);
        }

        private void OnItemAdded(ItemDefinition item)
        {
            if (item == null || Meta == null) return;
            string id = !string.IsNullOrEmpty(item.Id) ? item.Id : item.name;
            if (collectedThisRun.Add(id)) Meta.IncrementAbility(id);
        }

        /// <summary>
        /// Any rebuild is a new run: the per-run pickup tracking restarts and the hint density follows the save. A rebuild
        /// from elsewhere (the debug reroll) while the screen is up closes it and gives a living player control back, and a
        /// rebuild while the player is dead resets the player fully (as Continue does). Otherwise only Continue (StartRun)
        /// resets the player's stats, health and items.
        /// </summary>
        private void OnRebuilt()
        {
            collectedThisRun.Clear();
            ApplyHintDensity();
            bool wasOpen = ScreenOpen;
            ScreenOpen = false;
            LastMessage = null;
            if (player == null) return;
            if (player.IsDead)
            {
                // A rebuild after death (the debug reroll) is a new run: reset the player fully, as Continue does.
                ResetPlayer();
                return;
            }
            if (!wasOpen) return;
            if (player.TryGetComponent(out PlayerMover mover)) mover.enabled = true;
            if (player.TryGetComponent(out PlayerAttack attack)) attack.enabled = true;
            if (player.TryGetComponent(out Equipment equipment)) equipment.enabled = true;
        }

        /// <summary>Buys the next level of an upgrade from the balance and saves. Refused (with a message) when too poor or maxed.</summary>
        public PurchaseResult Buy(UpgradeStat stat)
        {
            if (Meta == null) Meta = new MetaSave();
            PurchaseResult result = UpgradeShop.TryBuy(Meta, stat);
            if (result.Bought) SaveMeta();
            LastMessage = result.Message;
            return result;
        }

        /// <summary>Starts the next run on a fresh seed (never the one just played). Returns the seed.</summary>
        public int Continue()
        {
            int current = world != null ? world.Seed : 0;
            int seed = NextSeed(current, DateTime.UtcNow.Ticks);
            StartRun(seed);
            return seed;
        }

        /// <summary>Starts a run on a given seed (Continue, and the debug replay of a shown seed).</summary>
        public void StartRun(int seed)
        {
            ApplyHintDensity();
            MassMitt mitt = player != null ? player.GetComponent<MassMitt>() : null;
            if (mitt != null) mitt.Release();
            if (world != null) world.Rebuild(seed);
            ResetPlayer();
            ScreenOpen = false;
            LastMessage = null;
            RunStarted?.Invoke(seed);
        }

        /// <summary>A seed from a non-repeating source (the clock), never equal to the current one, in 0..int.MaxValue.</summary>
        public static int NextSeed(int current, long ticks)
        {
            var entropy = new SeededRng(unchecked((ulong)ticks ^ ((ulong)(uint)current << 17)));
            int next;
            do next = (int)(entropy.NextUInt() & 0x7FFFFFFF);
            while (next == current);
            return next;
        }

        /// <summary>The player back to a run start: empty-handed, upgraded stats, full health, control on.</summary>
        public void ResetPlayer()
        {
            if (player == null) return;
            GameObject go = player.gameObject;
            if (go.TryGetComponent(out Inventory inventory)) inventory.Clear();
            if (go.TryGetComponent(out Equipment equipment))
            {
                equipment.Equip(null);
                equipment.enabled = true;
            }
            if (go.TryGetComponent(out Isotope isotope)) isotope.ResetForRun();
            if (go.TryGetComponent(out MassMitt mitt)) mitt.Release();
            ApplyStats(true);
            if (go.TryGetComponent(out PlayerMover mover)) mover.enabled = true;
            if (go.TryGetComponent(out PlayerAttack attack)) attack.enabled = true;
            if (go.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = Vector2.zero;
            collectedThisRun.Clear();
        }

        /// <summary>Sets the player's stats to base + upgrades (through PlayerStats only); fullHealth also heals and revives.</summary>
        private void ApplyStats(bool fullHealth)
        {
            if (player == null || Meta == null) return;
            RunStats.ApplyTo(player);
            if (fullHealth && player.Health != null)
            {
                player.Health.SetMax(player.MaxHealth);
                player.Health.ResetHealth();
            }
        }

        /// <summary>Stops the player while the screen is up (a victorious player is still alive in the arena).</summary>
        private void FreezePlayer()
        {
            if (player == null) return;
            if (player.TryGetComponent(out PlayerMover mover)) mover.enabled = false;
            if (player.TryGetComponent(out PlayerAttack attack)) attack.enabled = false;
            if (player.TryGetComponent(out Equipment equipment)) equipment.enabled = false;
            if (player.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = Vector2.zero;
        }

        /// <summary>Sets every hint-making component's density from the saved run count.</summary>
        public void ApplyHintDensity()
        {
            HintDensity density = HintDensity;
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (behaviour is IHintDensityTarget target) target.HintDensity = density;
        }

        private void SaveMeta()
        {
            try
            {
                Meta.Save(MetaSave.PrefsKey);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RunLoop: could not save meta progress ({e.Message}).");
            }
        }
    }
}
