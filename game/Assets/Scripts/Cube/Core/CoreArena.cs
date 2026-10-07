using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>How the core fight ended (None while it has not).</summary>
    public enum CoreOutcome { None, Victory, Defeat }

    /// <summary>
    /// The cube's core: one 16x10 screen in its own region, far below the faces (screen ArenaScreen, so ScreenCamera frames
    /// it exactly). Walls all round; a central membrane splits a warm half (left) from a cool half (right). The membrane has
    /// the Demon's door in its middle and a gap at each end that the Demon cannot block, so mixing is always possible.
    /// On every reveal it puts a CorePortal on each built face's active core-entrance slot. Entering (CorePortal) suspends
    /// cube navigation, moves the player in and starts the fight: ParticlesPerSide warm and cool particles spawn ordered,
    /// the Order ↔ Entropy meter (EntropyMeter) is computed every physics step, and the boss's phase list (BossFight) runs.
    /// Defeating the last phase is a victory; the player's death in the fight is a defeat. Each raises RunState.EndRun once.
    /// Leaving mid-fight (the exit door in the west wall, or a debug jump) abandons it with no run end; the exit door
    /// (CAP-27) puts the player back on the screen and face of the portal they came in by, and is gone once the fight is
    /// won. A world rebuild (new run) resets everything.
    /// UseFallbackBoss swaps the mixing phase for the simpler FallbackBoss.
    /// </summary>
    public class CoreArena : MonoBehaviour, IBossArena
    {
        /// <summary>RNG stream for particle drift (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.CoreArenaDrift;

        /// <summary>The arena's screen on the world grid: below the faces (which start at y = 0), at x = 0.</summary>
        public static readonly Vector2Int DefaultArenaScreen = new Vector2Int(0, -3);

        /// <summary>Half the walkable inside (the walls sit just outside it): 15 x 9.</summary>
        public static readonly Vector2 InnerHalf = new Vector2(7.5f, 4.5f);

        public const float WallThickness = 0.5f;
        public const float MembraneThickness = 0.3f;
        public const float DoorHalfHeight = 1f;

        /// <summary>Height of the always-open gap at each end of the membrane.</summary>
        public const float GapHeight = 1.5f;

        public const int DefaultParticlesPerSide = 8;

        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;
        [SerializeField] private RunState runState;
        [SerializeField] private bool useFallbackBoss;
        [SerializeField] private Vector2Int arenaScreen = DefaultArenaScreen;
        [SerializeField, Range(0.1f, 1f)] private float winThreshold = EntropyMeter.DefaultWinThreshold;
        [SerializeField, Min(1)] private int particlesPerSide = DefaultParticlesPerSide;

        private Transform root;
        private Transform particleRoot;
        private Transform pulseRoot;
        private MaxwellDemon demon;
        private readonly List<Collider2D> membraneColliders = new List<Collider2D>();
        private readonly List<SpriteRenderer> membraneRenderers = new List<SpriteRenderer>();
        private readonly List<Particle> particles = new List<Particle>();
        private readonly List<OrderPulse> pulses = new List<OrderPulse>();
        private readonly List<CorePortal> portals = new List<CorePortal>();
        private readonly List<float> xs = new List<float>();
        private readonly List<ArenaSide> homes = new List<ArenaSide>();
        private PhysicsMaterial2D bounce;
        private Health playerHealth;
        private BossFight fight;
        private CubeWorld subscribed;
        private int fightIndex;

        private static readonly Color MembraneColor = new Color(0.85f, 0.8f, 1f);
        private static readonly Color CrackedColor = new Color(0.35f, 0.33f, 0.4f, 0.5f);

        /// <summary>Raised when a fight starts.</summary>
        public event Action FightStarted;

        /// <summary>Raised when a fight ends with an outcome (true = victory).</summary>
        public event Action<bool> FightEnded;

        public CubeWorld World => world;
        public RunState RunState => runState;

        /// <summary>The setting that swaps the mixing phase for the simpler fallback boss (applies to the next fight).</summary>
        public bool UseFallbackBoss
        {
            get => useFallbackBoss;
            set => useFallbackBoss = value;
        }

        public float WinThreshold
        {
            get => winThreshold;
            set => winThreshold = Mathf.Clamp(value, 0.1f, 1f);
        }

        public int ParticlesPerSide => particlesPerSide;

        public Vector2 Centre => ScreenMath.ScreenCenter(arenaScreen, ScreenMath.DefaultScreenSize);
        public float MembraneX => Centre.x;
        public Vector2 DoorCentre => Centre;

        /// <summary>The membrane runs this far above and below the centre; the gaps lie beyond it.</summary>
        public float MembraneHalfSpan => InnerHalf.y - GapHeight;

        /// <summary>The walkable inside of the arena (world).</summary>
        public Rect Inner => new Rect(Centre - InnerHalf, InnerHalf * 2f);

        /// <summary>Centres of the two membrane gaps (top, bottom), which the Demon cannot block.</summary>
        public Vector2[] GapCentres
        {
            get
            {
                float y = MembraneHalfSpan + GapHeight / 2f;
                return new[] { Centre + new Vector2(0f, y), Centre - new Vector2(0f, y) };
            }
        }

        /// <summary>Where the player arrives: the far left of the warm half.</summary>
        public Vector2 EntryPoint => Centre + new Vector2(-6.5f, -3.5f);

        public MaxwellDemon Demon
        {
            get
            {
                EnsureBuilt();
                return demon;
            }
        }

        public IReadOnlyList<Particle> Particles => particles;

        public IReadOnlyList<OrderPulse> Pulses
        {
            get
            {
                pulses.RemoveAll(p => p == null || !p.isActiveAndEnabled);
                return pulses;
            }
        }

        public IReadOnlyList<CorePortal> Portals
        {
            get
            {
                portals.RemoveAll(p => p == null);
                return portals;
            }
        }

        // ---------------------------------------------------------------- the exit (CAP-27)

        /// <summary>Half the height of the exit doorway in the west wall: just wider than the player (radius 0.4), so only walking straight in gets through.</summary>
        public const float ExitHalfHeight = 0.5f;

        /// <summary>Height of the exit doorway's centre above the arena's centre (the upper west wall, far from the entry point).</summary>
        public const float ExitOffsetY = 2.75f;

        /// <summary>How deep the recess behind the doorway runs, from the arena's inner west edge (through the wall and beyond).</summary>
        public const float RecessDepth = 2.0f;

        /// <summary>
        /// How far past the inner west edge the player's centre must be to leave: down the recess, past the screen's edge (a
        /// walk of 1.6 units through the doorway). Walking along or against the wall never gets there, and nor does steering
        /// at a point just behind the wall (anything aimed at the playfield lies within about a unit of it).
        /// </summary>
        public const float ExitDepth = 1.6f;

        private Transform exitDoor;
        private Collider2D exitBlocker;
        private bool doorwayOpen;

        /// <summary>The player's collider radius (the scene builds it at 0.4).</summary>
        public const float PlayerRadius = 0.4f;

        /// <summary>How far off the doorway's centre line the player may be and still be let in (walking straight west).</summary>
        public const float DoorwayAim = 0.25f;
        private bool hasEntry;
        private ScreenAddress entryScreen;
        private Facing entryFacing = Facing.East;

        /// <summary>Raised when the player walks out through the exit door.</summary>
        public event Action ExitedOnFoot;

        /// <summary>Centre of the exit doorway, in the west wall.</summary>
        public Vector2 ExitDoorCentre => Centre + new Vector2(-InnerHalf.x - WallThickness / 2f, ExitOffsetY);

        /// <summary>The recess behind the doorway (world): through the west wall and beyond it, outside the playfield.</summary>
        public Rect Recess
        {
            get
            {
                Rect inner = Inner;
                return new Rect(inner.xMin - RecessDepth, Centre.y + ExitOffsetY - ExitHalfHeight, RecessDepth, ExitHalfHeight * 2f);
            }
        }

        /// <summary>Where the player's centre must be to leave: deep in the recess, past the inner west edge by ExitDepth.</summary>
        public Rect ExitZone
        {
            get
            {
                Rect recess = Recess;
                return new Rect(recess.xMin, recess.yMin, recess.width - ExitDepth, recess.height);
            }
        }

        /// <summary>
        /// True while the exit can be used: while no fight in this run has ended (a victory ends the run, and so does a
        /// defeat). Whatever way the player came in (a portal, or a controlled entry by the debug tools or the skill check).
        /// </summary>
        public bool ExitAvailable => Outcome == CoreOutcome.None;

        /// <summary>The exit door's visuals (shown while ExitAvailable).</summary>
        public GameObject ExitDoor
        {
            get
            {
                EnsureBuilt();
                return exitDoor != null ? exitDoor.gameObject : null;
            }
        }

        /// <summary>
        /// The whole west wall, doorway included: particles, pulses and enemies never pass it; the player passes it only at
        /// the doorway, walking straight in, while the exit is available (UpdateDoorway).
        /// </summary>
        public Collider2D ExitBlocker
        {
            get
            {
                EnsureBuilt();
                return exitBlocker;
            }
        }

        /// <summary>
        /// Where the exit leads: the screen of the portal the player came in by, else (a controlled entry) the screen the
        /// player stood on when entering, else Town's start screen.
        /// </summary>
        public ScreenAddress ExitScreen =>
            hasEntry ? entryScreen : world != null && world.Model != null ? world.Model.StartScreen : default;

        /// <summary>Times the player walked out through the exit in this run.</summary>
        public int Exits { get; private set; }

        /// <summary>True while the player is in the arena or in the exit recess behind its doorway.</summary>
        private bool PlayerInArenaOrRecess =>
            PlayerTransform != null && (PlayerInArena || Recess.Contains(PlayerTransform.position));

        /// <summary>
        /// Walks the player out through the exit door: the fight (if running) is abandoned as on any departure (no run end),
        /// and the player is put back on the screen and face they came in from, facing the way they entered. Refused
        /// (false) outside the arena or once a fight has ended.
        /// </summary>
        public bool LeaveThroughExit()
        {
            if (navigator == null || world == null || world.Model == null || !navigator.InCoreArena || !PlayerInArenaOrRecess ||
                !ExitAvailable) return false;
            if (FightActive) AbandonFight();
            navigator.TeleportTo(ExitScreen, entryFacing);
            Exits++;
            ExitedOnFoot?.Invoke();
            return true;
        }

        /// <summary>Shows the exit door exactly while the exit is available (and shuts the doorway when it is not).</summary>
        private void RefreshExit()
        {
            if (exitDoor != null) exitDoor.gameObject.SetActive(ExitAvailable);
            if (!ExitAvailable) SetDoorway(false);
        }

        /// <summary>
        /// The doorway, without any collider of its own (so the arena's physics, and every fight, are exactly as without
        /// it): the whole west wall stops everything, the player included, until the player walks straight west (facing
        /// west, within DoorwayAim of the doorway's centre line) into the wall at the doorway. Then the wall lets the player
        /// alone through, and the recess behind it is kept by clamping the player's position to its lane; walking back out
        /// shuts it again. Brushing, sliding along or pushing diagonally into the wall never opens it.
        /// </summary>
        private void UpdateDoorway()
        {
            if (navigator == null || exitBlocker == null || !navigator.InCoreArena || !ExitAvailable)
            {
                SetDoorway(false);
                return;
            }
            Rigidbody2D body = navigator.GetComponent<Rigidbody2D>();
            Vector2 p = body != null ? body.position : (Vector2)navigator.transform.position;
            float wallLine = Inner.xMin;
            float doorY = Centre.y + ExitOffsetY;
            if (!doorwayOpen)
            {
                bool atDoorway = p.x <= wallLine + PlayerRadius + 0.05f && Mathf.Abs(p.y - doorY) <= DoorwayAim;
                PlayerMover mover = navigator.GetComponent<PlayerMover>();
                bool walkingWest = mover != null && mover.Facing.x < -0.99f;
                if (atDoorway && walkingWest) SetDoorway(true);
                else return;
            }
            if (p.x > wallLine + PlayerRadius + 0.05f)
            {
                SetDoorway(false); // walked back out of the doorway
                return;
            }
            // In the doorway or the recess: keep the player in its lane (the doorway's width less the player, and the back wall).
            float lane = ExitHalfHeight - PlayerRadius;
            var kept = new Vector2(Mathf.Max(p.x, Recess.xMin + 0.3f), Mathf.Clamp(p.y, doorY - lane, doorY + lane));
            if (kept != p && body != null)
            {
                body.position = kept;
                Vector2 v = body.linearVelocity;
                if (kept.x != p.x) v.x = 0f;
                if (kept.y != p.y) v.y = 0f;
                body.linearVelocity = v;
            }
        }

        private void SetDoorway(bool open)
        {
            if (doorwayOpen == open || exitBlocker == null) return;
            doorwayOpen = open;
            if (navigator == null) return;
            foreach (Collider2D c in navigator.GetComponentsInChildren<Collider2D>())
                if (c != null) Physics2D.IgnoreCollision(exitBlocker, c, open);
        }

        /// <summary>True while the doorway lets the player through the west wall.</summary>
        public bool DoorwayOpen => doorwayOpen;

        public BossFight Fight => fight;
        public BossPhase CurrentPhase => fight?.Current;
        public bool FightActive { get; private set; }
        public CoreOutcome Outcome { get; private set; }

        /// <summary>The Order ↔ Entropy meter (0 ordered .. 1 mixed), updated every physics step of a fight.</summary>
        public float Meter { get; private set; }

        /// <summary>Physics steps run in the current (or last) fight.</summary>
        public int FightSteps { get; private set; }

        /// <summary>Game seconds of the current (or last) fight, counted in physics steps.</summary>
        public float FightSeconds => FightSteps * Time.fixedDeltaTime;

        /// <summary>Times the player has entered the arena.</summary>
        public int Entries { get; private set; }

        /// <summary>Fights started in this run (reset on every rebuild); part of the particles' drift seeds.</summary>
        public int FightIndex => fightIndex;

        public Transform PlayerTransform => navigator != null ? navigator.transform : null;

        public CubeNavigator Navigator => navigator;

        public bool PlayerInArena => PlayerTransform != null && Contains(PlayerTransform.position);

        public void Configure(CubeWorld cubeWorld, CubeNavigator player, RunState run)
        {
            Unsubscribe();
            world = cubeWorld;
            navigator = player;
            runState = run;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnwatchPlayer();
        }

        private void Subscribe()
        {
            if (world == null || subscribed == world) return;
            Unsubscribe();
            subscribed = world;
            world.Rebuilt += OnWorldRebuilt;
            world.Revealed += AttachPortals;
            if (world.ScienceRevealed) AttachPortals();
        }

        private void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.Rebuilt -= OnWorldRebuilt;
            subscribed.Revealed -= AttachPortals;
            subscribed = null;
        }

        /// <summary>True if a world point is inside the arena (with a little slack).</summary>
        public bool Contains(Vector2 point)
        {
            Rect r = Inner;
            const float slack = 0.5f;
            return point.x >= r.xMin - slack && point.x <= r.xMax + slack && point.y >= r.yMin - slack && point.y <= r.yMax + slack;
        }

        public ArenaSide SideOf(Vector2 point) => EntropyMeter.SideOf(point.x, MembraneX);

        /// <summary>A drift direction turned back inward when within margin of an outer wall and heading into it.</summary>
        public Vector2 AwayFromWalls(Vector2 position, Vector2 direction, float margin)
        {
            Rect r = Inner;
            if (position.x < r.xMin + margin && direction.x < 0f) direction.x = -direction.x;
            if (position.x > r.xMax - margin && direction.x > 0f) direction.x = -direction.x;
            if (position.y < r.yMin + margin && direction.y < 0f) direction.y = -direction.y;
            if (position.y > r.yMax - margin && direction.y > 0f) direction.y = -direction.y;
            return direction;
        }

        /// <summary>Puts a portal on every active core-entrance slot of the world (once per slot). Called on each reveal.</summary>
        public void AttachPortals()
        {
            if (world == null) return;
            portals.RemoveAll(p => p == null);
            foreach (ScreenModule module in world.AllModules)
            {
                if (module == null) continue;
                foreach (CoreEntranceSlot slot in module.ActiveCoreEntrances())
                {
                    CorePortal existing = slot.GetComponentInChildren<CorePortal>(true);
                    if (existing != null)
                    {
                        existing.Arena = this;
                        if (!portals.Contains(existing)) portals.Add(existing);
                        continue;
                    }
                    portals.Add(CorePortal.Create(slot.transform, this, world.Material));
                }
            }
        }

        /// <summary>
        /// Moves the player into the arena and suspends cube navigation; starts a fight unless one is running or this run
        /// has already ended. Returns false without a player. from is the screen of the portal used (CorePortal passes its
        /// own): the exit door returns the player there; without one (a controlled entry), to the screen they stood on.
        /// </summary>
        public bool Enter(CubeNavigator player, ScreenAddress? from = null)
        {
            if (player == null) return false;
            EnsureBuilt();
            if (navigator != player)
            {
                UnwatchPlayer();
                navigator = player;
            }
            // Remember where the player came from (the portal's screen), so the exit door can take them back there.
            if (!player.InCoreArena && world != null && world.Model != null)
            {
                entryScreen = from ?? player.Current;
                entryFacing = player.Facing;
                hasEntry = true;
            }
            player.EnterCoreArena(EntryPoint);
            RefreshExit();
            Entries++;
            bool runOver = (runState != null && runState.Ended) || Outcome != CoreOutcome.None;
            if (!FightActive && !runOver) StartFight();
            return true;
        }

        private void StartFight()
        {
            ClearFight();
            RestorePartition();
            fightIndex++;
            int seed = world != null ? world.Seed : 0;
            Sprite disc = ArtCatalog.Get(ArtKey.Particle);
            Material material = world != null ? world.Material : null;
            for (int i = 0; i < particlesPerSide * 2; i++)
            {
                ArenaSide home = i < particlesPerSide ? ArenaSide.Warm : ArenaSide.Cool;
                int k = i % particlesPerSide;
                var rng = new SeededRng(DriftSeed(seed, fightIndex, i), RngStream);
                particles.Add(Particle.Spawn(this, home, OrderedSpot(home, k), rng, particleRoot, disc, material, bounce));
            }
            demon.ResetForFight();
            WatchPlayer();
            Meter = 0f;
            FightSteps = 0;
            Outcome = CoreOutcome.None;
            fight = BossFight.Create(useFallbackBoss);
            RefreshExit();
            FightActive = true;
            fight.Begin(this);
            FightStarted?.Invoke();
        }

        /// <summary>
        /// The drift seed of particle index in the fight-th fight of a run on a seed: the three inputs are mixed (SplitMix64
        /// finaliser per step) so no two (fight, index) pairs collide and the same run replays the same drift.
        /// </summary>
        public static ulong DriftSeed(int seed, int fight, int index)
        {
            ulong h = Mix((ulong)(uint)seed);
            h = Mix(h ^ (ulong)(uint)fight);
            return Mix(h ^ ((ulong)(uint)index << 32 | 0x9E37UL));
        }

        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>The ordered starting spot of the k-th particle of a side: columns of four, mirrored across the membrane.</summary>
        public Vector2 OrderedSpot(ArenaSide side, int k)
        {
            int column = k / 4, row = k % 4;
            float x = 3f + 2f * column;
            float y = -2.25f + 1.5f * row;
            return Centre + new Vector2(side == ArenaSide.Warm ? -x : x, y);
        }

        private void FixedUpdate()
        {
            // The exit door: walking into it (while the fight is not won) takes the player back out.
            UpdateDoorway();
            if (ExitAvailable && doorwayOpen && navigator != null && navigator.InCoreArena && ExitZone.Contains(navigator.transform.position))
            {
                LeaveThroughExit();
                return;
            }
            if (!FightActive) return;
            if (!PlayerInArenaOrRecess)
            {
                // Left mid-fight (a debug jump): the fight is abandoned and starts afresh on the next entry.
                AbandonFight();
                return;
            }
            FightSteps++;
            Meter = ComputeMeter();
            if (fight.Tick(this, Time.fixedDeltaTime)) Win();
        }

        /// <summary>The meter from the particles' current positions.</summary>
        public float ComputeMeter()
        {
            xs.Clear();
            homes.Clear();
            foreach (Particle p in particles)
            {
                if (p == null) continue;
                xs.Add(p.Position.x);
                homes.Add(p.Home);
            }
            return EntropyMeter.Compute(xs, homes, MembraneX);
        }

        /// <summary>Particles currently away from home.</summary>
        public int WrongCount()
        {
            int wrong = 0;
            foreach (Particle p in particles)
                if (p != null && !p.IsHome) wrong++;
            return wrong;
        }

        public void SetDemonDuties(bool restoring, bool firing)
        {
            if (demon == null) return;
            demon.Restoring = restoring;
            demon.Firing = firing;
        }

        /// <summary>A swing landed on the Demon: the current phase decides whether it counts.</summary>
        public bool DemonHit(ItemDefinition item)
        {
            if (!FightActive || fight == null) return false;
            return fight.DemonHit(this, item);
        }

        public OrderPulse SpawnPulse(Vector2 at, Vector2 velocity, float damage)
        {
            EnsureBuilt();
            Rect bounds = Inner;
            bounds.min -= Vector2.one;
            bounds.max += Vector2.one;
            OrderPulse pulse = OrderPulse.Spawn(at, velocity, damage, ResolvePlayerHealth(), bounds, pulseRoot,
                world != null ? world.Material : null);
            pulses.Add(pulse);
            return pulse;
        }

        private void Win()
        {
            FightActive = false;
            Outcome = CoreOutcome.Victory;
            demon.Defeat();
            RefreshExit();
            ClearPulses();
            ReleaseParticles();
            CrackPartition();
            UnwatchPlayer();
            FightEnded?.Invoke(true);
            // Last: a RunEnded listener (the run loop) may start a new run straight away.
            if (runState != null) runState.EndRun(true);
        }

        private void Lose()
        {
            FightActive = false;
            Outcome = CoreOutcome.Defeat;
            demon.Stand();
            RefreshExit();
            ClearPulses();
            ReleaseParticles();
            UnwatchPlayer();
            FightEnded?.Invoke(false);
            // Last: a RunEnded listener (the run loop) may start a new run straight away.
            if (runState != null) runState.EndRun(false);
        }

        private void AbandonFight()
        {
            FightActive = false;
            if (demon != null) demon.ResetForFight();
            UnwatchPlayer();
            ClearFight();
        }

        private void OnWorldRebuilt()
        {
            AbandonFight();
            fightIndex = 0;
            Outcome = CoreOutcome.None;
            Meter = 0f;
            fight = null;
            RestorePartition();
            portals.Clear();
            hasEntry = false;
            Exits = 0;
            RefreshExit();
        }

        private void OnPlayerDied(Health _)
        {
            if (FightActive && PlayerInArena) Lose();
        }

        private Health ResolvePlayerHealth()
        {
            if (navigator == null) navigator = FindAnyObjectByType<CubeNavigator>();
            if (navigator == null) return null;
            if (playerHealth == null || playerHealth.gameObject != navigator.gameObject) playerHealth = navigator.GetComponent<Health>();
            return playerHealth;
        }

        private void WatchPlayer()
        {
            UnwatchPlayer();
            Health health = ResolvePlayerHealth();
            if (health != null) health.Died += OnPlayerDied;
        }

        private void UnwatchPlayer()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        private void ReleaseParticles()
        {
            foreach (Particle p in particles)
                if (p != null) p.Release();
        }

        private void ClearPulses()
        {
            foreach (OrderPulse p in pulses)
                if (p != null) Destroy(p.gameObject);
            pulses.Clear();
        }

        private void ClearFight()
        {
            ClearPulses();
            foreach (Particle p in particles)
            {
                if (p == null) continue;
                p.gameObject.SetActive(false);
                Destroy(p.gameObject);
            }
            particles.Clear();
        }

        /// <summary>The Partition cracks: the membrane greys out and stops blocking anything.</summary>
        private void CrackPartition()
        {
            foreach (Collider2D c in membraneColliders)
                if (c != null) c.enabled = false;
            foreach (SpriteRenderer r in membraneRenderers)
                if (r != null) r.color = CrackedColor;
        }

        private void RestorePartition()
        {
            foreach (Collider2D c in membraneColliders)
                if (c != null) c.enabled = true;
            foreach (SpriteRenderer r in membraneRenderers)
                if (r != null) r.color = MembraneColor;
        }

        /// <summary>Builds the arena's walls, halves, membrane, door and Demon once.</summary>
        private void EnsureBuilt()
        {
            if (root != null) return;
            Material material = world != null ? world.Material : null;
            bounce = new PhysicsMaterial2D("Particle Bounce") { bounciness = 0.9f, friction = 0f };

            root = new GameObject("Core Arena (built)").transform;
            root.SetParent(transform, false);
            root.position = Vector3.zero;
            particleRoot = new GameObject("Particles").transform;
            particleRoot.SetParent(root, false);
            pulseRoot = new GameObject("Order Pulses").transform;
            pulseRoot.SetParent(root, false);

            Vector2 c = Centre;
            Vector2 screen = ScreenMath.DefaultScreenSize;
            // The ordered halves: warm on the left, cool on the right, edge to edge of the screen.
            Block("Warm Half", c - new Vector2(screen.x / 4f, 0f), new Vector2(screen.x / 2f, screen.y),
                new Color(1f, 0.6f, 0.5f), -10, false, material, ArtKey.ArenaFloor);
            Block("Cool Half", c + new Vector2(screen.x / 4f, 0f), new Vector2(screen.x / 2f, screen.y),
                new Color(0.5f, 0.65f, 1f), -10, false, material, ArtKey.ArenaFloor);

            var wallColor = new Color(0.7f, 0.65f, 0.8f);
            float w = WallThickness;
            Block("Wall North", c + new Vector2(0f, InnerHalf.y + w / 2f), new Vector2(screen.x, w), wallColor, 0, true, material,
                ArtKey.ArenaWall);
            Block("Wall South", c - new Vector2(0f, InnerHalf.y + w / 2f), new Vector2(screen.x, w), wallColor, 0, true, material,
                ArtKey.ArenaWall);
            Block("Wall East", c + new Vector2(InnerHalf.x + w / 2f, 0f), new Vector2(w, screen.y), wallColor, 0, true, material,
                ArtKey.ArenaWall);
            // The whole west wall, as for everything but the player (the exit doorway in it is for the player only).
            GameObject westWall = Block("Wall West", c - new Vector2(InnerHalf.x + w / 2f, 0f), new Vector2(w, screen.y), wallColor, 0, true,
                material, ArtKey.ArenaWall);
            exitBlocker = westWall.GetComponent<Collider2D>();

            // The membrane: a segment above and below the door, leaving a gap at each end.
            float span = MembraneHalfSpan - DoorHalfHeight;
            float segmentY = DoorHalfHeight + span / 2f;
            foreach (float sign in new[] { 1f, -1f })
            {
                GameObject segment = Block(sign > 0f ? "Membrane North" : "Membrane South", c + new Vector2(0f, sign * segmentY),
                    new Vector2(MembraneThickness, span), MembraneColor, 1, true, material);
                membraneColliders.Add(segment.GetComponent<Collider2D>());
                membraneRenderers.Add(segment.GetComponentInChildren<SpriteRenderer>());
            }
            GameObject door = Block("Demon's Door", c, new Vector2(MembraneThickness, DoorHalfHeight * 2f),
                new Color(1f, 0.3f, 0.75f), 2, true, material);

            // The Demon: a dark triangle on a kinematic body, with a trigger for swings.
            var demonObject = new GameObject("Maxwell's Demon");
            demonObject.transform.SetParent(root, false);
            demonObject.transform.position = c;
            var demonBody = demonObject.AddComponent<Rigidbody2D>();
            demonBody.bodyType = RigidbodyType2D.Kinematic;
            var hit = demonObject.AddComponent<CircleCollider2D>();
            hit.isTrigger = true;
            hit.radius = MaxwellDemon.HitRadius;
            var demonVisual = new GameObject("Visual");
            demonVisual.transform.SetParent(demonObject.transform, false);
            var demonRenderer = demonVisual.AddComponent<SpriteRenderer>();
            ArtCatalog.Apply(demonRenderer, ArtKey.Demon, new Vector2(1.3f, 1.3f));
            if (material != null) demonRenderer.sharedMaterial = material;
            demonRenderer.color = new Color(0.85f, 0.55f, 1f);
            demonRenderer.sortingOrder = 8;

            var line = new GameObject("Telegraph");
            line.transform.SetParent(root, false);
            var lineRenderer = line.AddComponent<SpriteRenderer>();
            lineRenderer.sprite = ArtCatalog.Shape(PartShape.Square);
            if (material != null) lineRenderer.sharedMaterial = material;
            lineRenderer.color = new Color(0.85f, 0.9f, 1f, 0.45f);
            lineRenderer.sortingOrder = 11;
            lineRenderer.enabled = false;

            BuildExitDoor(material);

            demon = demonObject.AddComponent<MaxwellDemon>();
            demon.Configure(this, door.GetComponent<Collider2D>(), door.GetComponentInChildren<SpriteRenderer>(), demonRenderer,
                lineRenderer);
        }

        /// <summary>
        /// The exit's visuals: the dark doorway drawn over the west wall, a pale stone frame, and an arrow on the floor pointing
        /// out. No colliders: the doorway and its recess are the whole west wall plus UpdateDoorway (see there).
        /// </summary>
        private void BuildExitDoor(Material material)
        {
            float w = WallThickness, h = ExitHalfHeight * 2f;
            Block("Exit Doorway", ExitDoorCentre, new Vector2(w, h), new Color(0.08f, 0.06f, 0.1f), 1, false, material);

            exitDoor = new GameObject("Exit Door").transform;
            exitDoor.SetParent(root, false);
            exitDoor.position = ExitDoorCentre;
            ArtCatalog.AddShape(exitDoor, "Frame Top", PartShape.Square, new Vector2(0f, h / 2f + 0.1f), new Vector2(w + 0.3f, 0.2f),
                new Color(0.92f, 0.88f, 0.78f), 2, material);
            ArtCatalog.AddShape(exitDoor, "Frame Bottom", PartShape.Square, new Vector2(0f, -h / 2f - 0.1f), new Vector2(w + 0.3f, 0.2f),
                new Color(0.92f, 0.88f, 0.78f), 2, material);
            SpriteRenderer arrow = ArtCatalog.AddShape(exitDoor, "Arrow", PartShape.Triangle, new Vector2(w / 2f + 0.55f, 0f),
                new Vector2(0.5f, 0.45f), new Color(1f, 0.95f, 0.6f, 0.85f), 3, material);
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // apex up, turned to point out (west)
            RefreshExit();
        }

        /// <summary>A block of the arena: art (tiled) when given a key, else a plain square (the membrane and the Demon's door).</summary>
        private GameObject Block(string name, Vector2 centre, Vector2 size, Color color, int order, bool solid, Material material,
            ArtKey art = ArtKey.None)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = centre;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtCatalog.Shape(PartShape.Square);
            if (art != ArtKey.None) ArtCatalog.Apply(renderer, art, size);
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
            if (solid) go.AddComponent<BoxCollider2D>().size = size;
            return go;
        }

        private void OnDestroy()
        {
            if (bounce != null) Destroy(bounce);
        }
    }
}
