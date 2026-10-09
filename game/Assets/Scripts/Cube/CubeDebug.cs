using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// Debug overlay and commands (editor and development builds only):
    /// F1 toggles the overlay (4.19: two slim bars over the north and south wall bands, never over the floor: the seed and
    /// where the player is on top, every key at the bottom; OverlayBars), F2 toggles slot markers (a sprite on
    /// every active slot of every module, with labels on the visible screen), F5 rerolls to a new random
    /// seed and rebuilds, F6 jumps to the next unsealed screen.
    /// F3 spawns one NPC of each race around the player (true hints for the run at hintDensity; Shift+F3 their leaders), F4 toggles
    /// hostility for the race of the NPC nearest the player. F7 spawns a placeholder enemy near the player, weak
    /// to a random owned item (or the Biology item when nothing is owned). Spawned NPCs and enemies are
    /// removed on a rebuild. While the player is in the core arena, the overlay says so (instead of a face and cell) and
    /// F3/F7 spawn inside the arena's screen. F11 lights the whole cube-net map for the run (debug "visit all"); the
    /// overlay also reminds that M opens the map. While the map pauses the game (GamePause) only F1 works.
    /// Inside a town building (4.6) the overlay names the room, F3/F7 spawn inside it and F8 walks the player back out of
    /// its door; F5, F6 and F10 take the player out like any teleport. Down in a lair room (4.13) the same holds: the overlay
    /// says so, F3/F7 spawn on its walkway (never in a cell), F4 does nothing (prisoners are never hostile, and F4 never
    /// picks one anywhere), F8 walks the player back out by their portal.
    /// </summary>
    public class CubeDebug : MonoBehaviour, IHintDensityTarget
    {
        [SerializeField] private CubeWorld world;
        [SerializeField] private CubeNavigator navigator;
        [SerializeField] private CoreArena coreArena;
        [SerializeField] private bool overlayVisible; // off by default: F1 shows it
        [SerializeField] private bool slotMarkersVisible;
        [SerializeField] private HintDensity hintDensity = HintGenerator.FirstRunDensity;
        [SerializeField, Min(0.5f)] private float npcSpawnRadius = 2.5f * ScreenMath.ContentScale;
        [SerializeField, Min(0.5f)] private float enemySpawnRadius = 3f * ScreenMath.ContentScale;

        private readonly List<NpcTalker> debugNpcs = new List<NpcTalker>();
        /// <summary>RNG stream for F7 weakness picks (see RngStreams).</summary>
        public const ulong EnemySpawnRngStream = RngStreams.DebugEnemySpawn;

        private readonly List<Enemy> debugEnemies = new List<Enemy>();
        private int enemyCount;
        private Transform npcRoot;
        private int npcBatch;

        private GUIStyle style;
        private GUIStyle labelStyle;

        public CubeWorld World
        {
            get => world;
            set => world = value;
        }

        public CubeNavigator Navigator
        {
            get => navigator;
            set => navigator = value;
        }

        /// <summary>The core arena (found in the scene when not set).</summary>
        public CoreArena CoreArena
        {
            get
            {
                if (coreArena == null) coreArena = FindAnyObjectByType<CoreArena>();
                return coreArena;
            }
            set => coreArena = value;
        }

        /// <summary>True while the player is in the core arena.</summary>
        public bool PlayerInCoreArena => navigator != null && navigator.InCoreArena;

        /// <summary>The town buildings' rooms (found in the scene when not set).</summary>
        public TownInteriors Interiors
        {
            get
            {
                if (interiors == null) interiors = FindAnyObjectByType<TownInteriors>();
                return interiors;
            }
            set => interiors = value;
        }

        [SerializeField] private TownInteriors interiors;

        /// <summary>The boss lair's rooms (4.13; found in the scene when not set).</summary>
        public LairRooms Lair
        {
            get
            {
                if (lair == null) lair = FindAnyObjectByType<LairRooms>();
                return lair;
            }
            set => lair = value;
        }

        [SerializeField] private LairRooms lair;

        /// <summary>The temples' rooms (4.14; found in the scene when not set).</summary>
        public TempleRooms Temples
        {
            get
            {
                if (temples == null) temples = FindAnyObjectByType<TempleRooms>();
                return temples;
            }
            set => temples = value;
        }

        [SerializeField] private TempleRooms temples;

        /// <summary>True while the player is in a temple (4.14).</summary>
        public bool PlayerInTemple => navigator != null && navigator.InTemple;

        /// <summary>
        /// Shift+F8 (4.14): solves the temple the player is in (the magic moment plays and the gem is given). False outside a
        /// temple, or when it is solved already.
        /// </summary>
        public bool SolveTemple()
        {
            if (!PlayerInTemple || Temples == null || Temples.Current == null || Temples.Current.IsSolved) return false;
            Temples.Current.Solve();
            return true;
        }

        /// <summary>True while the player is inside a town building (or down in a lair room, which is a kind of interior).</summary>
        public bool PlayerInInterior => navigator != null && navigator.InInterior;

        /// <summary>True while the player is down in a lair room (4.13).</summary>
        public bool PlayerInLair => navigator != null && navigator.InLair;

        /// <summary>What the overlay shows in place of the cell while the player is inside a town building.</summary>
        public const string InteriorLabel = "Inside the town's";

        /// <summary>Centre of the screen the player is on: the arena's in the core arena, the room's inside a building, else the face screen's.</summary>
        public Vector2 PlayerScreenCentre()
        {
            if (PlayerInCoreArena && CoreArena != null) return CoreArena.Centre;
            if (PlayerInLair && Lair != null && Lair.Current != null) return Lair.Current.Centre;
            if (PlayerInTemple && Temples != null && Temples.Current != null) return Temples.Current.Centre;
            if (PlayerInInterior && Interiors != null && Interiors.Current != null) return Interiors.Current.Centre;
            ScreenAddress here = navigator != null ? navigator.Current : world.Model.StartScreen;
            return world.ScreenCenter(here);
        }

        /// <summary>What the overlay shows in place of the face while the player is in the core arena.</summary>
        public const string CoreArenaLabel = "Core arena";

        public bool OverlayVisible => overlayVisible;

        public bool SlotMarkersVisible => slotMarkersVisible;

        /// <summary>NPCs spawned by F3 in this run (destroyed ones are dropped).</summary>
        public IReadOnlyList<NpcTalker> DebugNpcs
        {
            get
            {
                debugNpcs.RemoveAll(n => n == null);
                return debugNpcs;
            }
        }

        public HintDensity HintDensity
        {
            get => hintDensity;
            set => hintDensity = value;
        }

        /// <summary>True only in the editor and in development builds.</summary>
        public static bool Available
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Debug.isDebugBuild;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (!Available) enabled = false;
        }

        private void OnEnable()
        {
            if (world == null) return;
            world.Rebuilt += ApplySlotMarkers;
            world.Rebuilt += ClearNpcs;
            world.Revealed += ApplySlotMarkers;
        }

        private void OnDisable()
        {
            if (world == null) return;
            world.Rebuilt -= ApplySlotMarkers;
            world.Rebuilt -= ClearNpcs;
            world.Revealed -= ApplySlotMarkers;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || world == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) overlayVisible = !overlayVisible;
            if (GamePause.Active) return; // the map pauses the game: no debug commands but the overlay toggle
            if (keyboard.f2Key.wasPressedThisFrame) SetSlotMarkersVisible(!slotMarkersVisible);
            if (keyboard.f3Key.wasPressedThisFrame) SpawnNpcs(keyboard.shiftKey.isPressed);
            if (keyboard.f4Key.wasPressedThisFrame) ToggleNearestHostility();
            if (keyboard.f5Key.wasPressedThisFrame) Reroll();
            if (keyboard.f6Key.wasPressedThisFrame) JumpToNextUnsealed();
            if (keyboard.f7Key.wasPressedThisFrame) SpawnEnemy();
            if (keyboard.f8Key.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed) SolveTemple();
                else LeaveCoreArena();
            }
            if (keyboard.f9Key.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed) GatherFragments();
                else Rest();
            }
            if (keyboard.f10Key.wasPressedThisFrame) StartNewRun();
            if (keyboard.f11Key.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed) UnlockArches();
                else RevealMap();
            }
        }

        /// <summary>
        /// F8: leaves the core arena for the Town start screen. The arena abandons the fight on its own once the
        /// player is outside it, and a fresh fight starts on the next portal entry. Inside a town building (4.6) it walks the
        /// player out of its door instead (TownInteriors.Leave). Does nothing anywhere else.
        /// </summary>
        public bool LeaveCoreArena()
        {
            if (PlayerInLair) return Lair != null && Lair.Leave();
            if (PlayerInTemple) return Temples != null && Temples.Leave();
            if (PlayerInInterior) return Interiors != null && Interiors.Leave();
            if (!PlayerInCoreArena || world.Model == null) return false;
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            return true;
        }

        /// <summary>
        /// Where F3 and F7 may spawn around the player: the player's screen, inset by a margin; down in a lair room (4.13)
        /// only the walkway below the bars, so nobody lands in a cell.
        /// </summary>
        public Rect SpawnBounds()
        {
            const float margin = 1f * ScreenMath.ContentScale;
            if (PlayerInLair && Lair != null && Lair.Current != null)
            {
                Rect walk = Lair.Current.Walkway;
                return Rect.MinMaxRect(walk.xMin + 0.6f, walk.yMin + 0.6f, walk.xMax - 0.6f, walk.yMax - 1.6f);
            }
            Vector2 screenCentre = PlayerScreenCentre();
            Vector2 half = CubeWorld.ScreenSize / 2f - new Vector2(margin, margin);
            return new Rect(screenCentre - half, half * 2f);
        }

        /// <summary>F9: back to full health. Only while alive; after a death use the between-runs screen or F10.</summary>
        public bool Rest()
        {
            if (navigator == null || !navigator.TryGetComponent(out Health health) || health.IsDead) return false;
            health.ResetHealth();
            return true;
        }

        /// <summary>
        /// Shift+F9 (4.8): gives the player every fragment of the face they stand on (Town's too), so a talk with its leader
        /// mends it. Returns the number given (0 in the core, without a run, or when all were held).
        /// </summary>
        public int GatherFragments()
        {
            if (navigator == null || navigator.InCoreArena || world.Model == null || world.Dice == null ||
                !navigator.TryGetComponent(out RunRewards rewards)) return 0;
            FaceId face = navigator.Current.Face;
            int given = 0;
            for (int i = 1; i <= world.Dice.Of(face); i++)
                if (rewards.Grant(Reward.Fragment(i), face)) given++;
            return given;
        }

        /// <summary>
        /// F10: abandons the current run and starts a fresh one on a new seed (back in Town, reset like Continue).
        /// No currency is banked and the run count is not changed.
        /// </summary>
        public bool StartNewRun()
        {
            RunLoop loop = FindAnyObjectByType<RunLoop>();
            if (loop == null) return false;
            loop.StartRun(RunLoop.NextSeed(world.Seed, DateTime.Now.Ticks));
            return true;
        }

        /// <summary>Shift+F11 (4.9): opens every locked face-edge arch of the run, spending nothing.</summary>
        public void UnlockArches()
        {
            if (world != null) world.UnlockAllEdges();
        }

        /// <summary>
        /// F11: lights the whole map for this run (reveals the science faces if they are not yet, then marks every unsealed
        /// screen visited). Returns false without a VisitTracker in the scene, or when it has no world.
        /// </summary>
        public bool RevealMap()
        {
            VisitTracker tracker = FindAnyObjectByType<VisitTracker>();
            if (tracker == null || tracker.World == null || world.Model == null) return false;
            if (!world.ScienceRevealed) world.RevealScience();
            tracker.VisitAll();
            CubeMap map = FindAnyObjectByType<CubeMap>();
            if (map != null) map.Refresh();
            return true;
        }

        /// <summary>Shows or hides the slot markers on every module in the world.</summary>
        public void SetSlotMarkersVisible(bool visible)
        {
            slotMarkersVisible = visible;
            ApplySlotMarkers();
        }

        private void ApplySlotMarkers()
        {
            if (world == null) return;
            foreach (ScreenModule module in world.AllModules)
            {
                if (module == null) continue;
                foreach (ModuleSlot slot in module.AllSlots)
                    slot.SetMarkerVisible(slotMarkersVisible, world.Sprite, world.Material);
            }
        }

        /// <summary>
        /// Spawns one NPC of each race in a ring around the player, inside the player's screen. Parts come from
        /// the run seed (each press uses the next index per race); lines carry true hints for the run. With leaders,
        /// each is its race's leader (NpcFactory.ChooseLeader), drawn with the leader design, from the debug leader indices
        /// (DebugLeaderIndexBase up), so a debug leader is never a town's own leader (TownSquare.LeaderIndex).
        /// </summary>
        /// <summary>The first leader index Shift+F3 spawns from (each press the next): far above TownSquare.LeaderIndex.</summary>
        public const int DebugLeaderIndexBase = 0x1000;

        public List<NpcTalker> SpawnNpcs(bool leaders = false)
        {
            var spawned = new List<NpcTalker>();
            if (world == null || world.Model == null) return spawned;
            if (npcRoot == null) npcRoot = new GameObject("Debug NPCs").transform;

            Transform player = navigator != null ? navigator.transform : null;
            Vector2 screenCentre = PlayerScreenCentre();
            Vector2 centre = player != null ? (Vector2)player.position : screenCentre;
            Rect bounds = SpawnBounds();
            RunFacts facts = RunFacts.Of(world);

            Race[] races = RaceExtensions.All;
            for (int i = 0; i < races.Length; i++)
            {
                Vector2 at = ClearSpawnSpot(centre, i, races.Length, bounds);
                NpcSpec spec = leaders
                    ? NpcFactory.ChooseLeader(world.Seed, races[i], DebugLeaderIndexBase + npcBatch)
                    : NpcFactory.ChooseParts(world.Seed, races[i], npcBatch);
                List<string> lines = Dialogue.For(spec, facts, hintDensity);
                NpcTalker npc = NpcFactory.Spawn(spec, lines, at, bounds, world.Relations, player, npcRoot, world.Material);
                spawned.Add(npc);
                debugNpcs.Add(npc);
            }
            npcBatch++;
            return spawned;
        }

        /// <summary>
        /// A spot for the i-th of count NPCs that overlaps no wall, closed gate, player or earlier NPC: its
        /// ring spot first, then the same angle at other radii, then other angles. Falls back to the ring spot.
        /// </summary>
        private Vector2 ClearSpawnSpot(Vector2 centre, int i, int count, Rect bounds)
        {
            float[] radii = { npcSpawnRadius, npcSpawnRadius * 0.7f, npcSpawnRadius * 1.4f, npcSpawnRadius * 1.8f };
            Vector2 first = Vector2.zero;
            bool haveFirst = false;
            for (int turn = 0; turn < 12; turn++)
            {
                float offset = 15f * ((turn + 1) / 2) * (turn % 2 == 0 ? 1 : -1); // 0, -15, +15, -30, ...
                float angle = (90f + i * 360f / count + offset) * Mathf.Deg2Rad;
                foreach (float radius in radii)
                {
                    Vector2 at = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    at = new Vector2(Mathf.Clamp(at.x, bounds.xMin, bounds.xMax), Mathf.Clamp(at.y, bounds.yMin, bounds.yMax));
                    if (!haveFirst)
                    {
                        first = at;
                        haveFirst = true;
                    }
                    if (NpcFactory.IsClear(at)) return at;
                }
            }
            return first;
        }

        /// <summary>Enemies spawned by F7 in this run (dead or destroyed ones are dropped).</summary>
        public IReadOnlyList<Enemy> DebugEnemies
        {
            get
            {
                debugEnemies.RemoveAll(e => e == null);
                return debugEnemies;
            }
        }

        /// <summary>
        /// Spawns a placeholder enemy near the player, inside the player's screen, weak to a random owned item
        /// (picked from the run seed and the spawn count) or, with nothing owned, the run's Biology item (its rolled beak).
        /// </summary>
        public Enemy SpawnEnemy()
        {
            if (world == null || world.Model == null) return null;
            if (npcRoot == null) npcRoot = new GameObject("Debug NPCs").transform;

            Transform player = navigator != null ? navigator.transform : null;
            Vector2 screenCentre = PlayerScreenCentre();
            Vector2 centre = player != null ? (Vector2)player.position : screenCentre;
            Rect bounds = SpawnBounds();

            ItemDefinition weakness = null;
            var inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (inventory != null && inventory.Items.Count > 0)
            {
                var rng = new SeededRng(((ulong)(uint)world.Seed << 32) | (uint)enemyCount, EnemySpawnRngStream);
                weakness = inventory.Items[rng.NextInt(inventory.Items.Count)];
            }
            else if (world.ItemCatalog != null)
            {
                weakness = world.ItemFor(Theme.Biology);
            }

            // First clear spot on a ring around the player (an enemy is smaller than an NPC, so IsClear is safe).
            Vector2 at = Vector2.zero;
            bool found = false;
            for (int turn = 0; turn < 16 && !found; turn++)
            {
                float angle = (enemyCount * 47f + turn * 360f / 16f) * Mathf.Deg2Rad;
                foreach (float radius in new[] { enemySpawnRadius, enemySpawnRadius * 1.5f, enemySpawnRadius * 0.7f })
                {
                    Vector2 candidate = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    candidate = new Vector2(Mathf.Clamp(candidate.x, bounds.xMin, bounds.xMax), Mathf.Clamp(candidate.y, bounds.yMin, bounds.yMax));
                    if (turn == 0 && radius == enemySpawnRadius) at = candidate;
                    if (!NpcFactory.IsClear(candidate - new Vector2(0f, Enemy.BodyRadius))) continue;
                    at = candidate;
                    found = true;
                    break;
                }
            }

            Enemy enemy = Enemy.Spawn(weakness, at, bounds, player, npcRoot, world.Material);
            debugEnemies.Add(enemy);
            enemyCount++;
            return enemy;
        }

        /// <summary>Toggles hostility for the race of the NPC nearest the player. Returns that race, or null if there is no NPC.</summary>
        public Race? ToggleNearestHostility()
        {
            if (world == null) return null;
            // 4.13 review: down in a lair room the nearest NPC is a prisoner, who is never hostile; F4 does nothing there
            // (and a prisoner is never the one whose race it turns, wherever the player stands).
            if (PlayerInLair) return null;
            Vector2 from = navigator != null ? (Vector2)navigator.transform.position : Vector2.zero;
            NpcTalker nearest = null;
            float best = float.MaxValue;
            foreach (NpcTalker npc in FindObjectsByType<NpcTalker>(FindObjectsSortMode.None))
            {
                if (npc.Spec == null || npc.GetComponentInParent<LairRoomView>() != null) continue;
                float d = ((Vector2)npc.transform.position - from).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                nearest = npc;
            }
            if (nearest == null) return null;
            RaceRelations relations = nearest.Relations ?? world.Relations;
            relations.Toggle(nearest.Race);
            return nearest.Race;
        }

        private void ClearNpcs()
        {
            foreach (NpcTalker npc in debugNpcs)
                if (npc != null) Destroy(npc.gameObject);
            debugNpcs.Clear();
            npcBatch = 0;
            foreach (Enemy enemy in debugEnemies)
                if (enemy != null) Destroy(enemy.gameObject);
            debugEnemies.Clear();
            enemyCount = 0;
        }

        /// <summary>Rebuilds the world from a new random seed (differs from the current one).</summary>
        public int Reroll()
        {
            int current = world.Seed;
            var entropy = new SeededRng(unchecked((ulong)DateTime.UtcNow.Ticks ^ (ulong)(uint)current));
            int next;
            do next = (int)(entropy.NextUInt() & 0x7FFFFFFF);
            while (next == current);
            world.Rebuild(next);
            return next;
        }

        /// <summary>Teleports to the next screen (in face/cell order, wrapping) that is not on a sealed face.</summary>
        public ScreenAddress JumpToNextUnsealed()
        {
            CubeModel model = world.Model;
            ScreenAddress current = navigator != null ? navigator.Current : model.StartScreen;
            var all = new System.Collections.Generic.List<ScreenAddress>(model.AllScreens());
            int index = Mathf.Max(0, all.IndexOf(current));
            for (int k = 1; k <= all.Count; k++)
            {
                ScreenAddress candidate = all[(index + k) % all.Count];
                if (model.IsSealed(candidate.Face) || candidate == current) continue;
                if (navigator != null) navigator.TeleportTo(candidate, navigator.Facing);
                return candidate;
            }
            return current;
        }

        /// <summary>
        /// The overlay's first line (4.19: the top bar): the seed, then where the player is (the face, region, cell, entry
        /// facing and module; or the core arena, a lair room, a temple or a town building), then hostility.
        /// </summary>
        public string LocationLine
        {
            get
            {
                if (world == null || world.Model == null) return "";
                CubeModel model = world.Model;
                string head = $"Seed {model.Seed}  N={model.FaceSize}  {(world.ScienceRevealed ? "revealed" : "unrevealed")}  |  ";
                string tail = $"  |  Hostile: {HostileText()}";
                if (PlayerInCoreArena) return head + $"{CoreArenaLabel} (outside every face)" + tail;
                ScreenAddress here = navigator != null ? navigator.Current : model.StartScreen;
                string region = InWorldText.Region(model.ThemeOf(here.Face));
                if (PlayerInLair)
                    return head + $"{LairText.OverlayLabel} Face {here.Face}  {region}  Portal cell ({here.Cell.x},{here.Cell.y})" + tail;
                if (PlayerInTemple)
                    return head + $"{TempleText.OverlayLabel} {region}  Face {here.Face}  Town cell ({here.Cell.x},{here.Cell.y})  Shift+F8 solve" + tail;
                if (PlayerInInterior)
                {
                    string room = Interiors != null && Interiors.Current != null ? Interiors.Current.Interior.Name : "building";
                    return head + $"{InteriorLabel} {room}  Face {here.Face}  {region}  Town cell ({here.Cell.x},{here.Cell.y})" + tail;
                }
                string facing = navigator != null ? navigator.Facing.ToString() : "-";
                ScreenModule module = world.ModuleAt(here);
                string moduleText = module != null ? module.name.Replace(module.Theme.ToString(), InWorldText.Region(module.Theme)) : model.IsSealed(here.Face) ? "-" : "(unrevealed)";
                return head + $"Face {here.Face}  {region}{(model.IsSealed(here.Face) ? " (sealed)" : "")}  Cell ({here.Cell.x},{here.Cell.y})  " +
                       $"entered facing {facing}  |  {moduleText}" + tail;
            }
        }

        /// <summary>The overlay's second line (4.19: the bottom bar): every debug key, compact.</summary>
        public const string KeysLine =
            "F1 bars  F2 slots  F3 NPCs/leaders  F4 hostile  F5 reroll  F6 next  F7 enemy  F8 leave/solve  F9 rest/fragments  " +
            "F10 run  F11 map/arches  M map  Q quests  (x/y: Shift)";

        /// <summary>The overlay text: the location line, then the keys line.</summary>
        public string OverlayText => world == null || world.Model == null ? "" : LocationLine + "\n" + KeysLine;

        /// <summary>
        /// The wall band's height in pixels (4.19): the screen-edge wall (ScreenEdges.EdgeWallThickness) as drawn, the camera
        /// framing the screen's height (ScreenCamera). 20 px at 960x600.
        /// </summary>
        public static float WallBandPixels(float screenHeight) =>
            ScreenEdges.EdgeWallThickness / ScreenMath.DefaultScreenSize.y * screenHeight;

        /// <summary>The overlay's font size at a screen height: about two thirds of the wall band, 11-20 px.</summary>
        public static int OverlayFontSize(float screenHeight) => Mathf.Clamp(Mathf.FloorToInt(WallBandPixels(screenHeight) * 0.62f), 11, 20);

        /// <summary>
        /// 4.19 (playtest 5: "the overlay menu covers the screen"): the overlay's two slim bars, each one line, lying over the
        /// screen's own wall bands (the north wall and the south wall), so no floor is covered: the top bar holds the
        /// location line, the bottom bar the keys. (They were one 820x170 panel in the top-left corner.)
        /// </summary>
        public static void OverlayBars(float screenWidth, float screenHeight, out Rect top, out Rect bottom)
        {
            float band = WallBandPixels(screenHeight);
            top = new Rect(0f, 0f, screenWidth, band);
            bottom = new Rect(0f, screenHeight - band, screenWidth, band);
        }

        /// <summary>True when the keys bar is drawn: not while a dialogue is open (the box overlaps the bottom wall band).</summary>
        public static bool BottomBarShown(bool dialogueOpen) => !dialogueOpen;

        private static GUIStyle measureStyle;

        /// <summary>
        /// A line's width in the overlay's font at a size (pixels), measured with GUIStyle.CalcSize in Unity's built-in font
        /// (what IMGUI labels use); 0 when no font can be loaded.
        /// </summary>
        public static float MeasuredWidth(string line, int fontSize)
        {
            if (measureStyle == null)
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) return 0f;
                measureStyle = new GUIStyle { font = font, wordWrap = false };
            }
            measureStyle.fontSize = fontSize;
            return measureStyle.CalcSize(new GUIContent(line)).x;
        }

        /// <summary>The font size a bar draws a line at (the size for the screen, shrunk until it fits the bar, 10 px at least).</summary>
        public static int FittedFontSize(string line, float screenWidth, float screenHeight)
        {
            int size = OverlayFontSize(screenHeight);
            while (size > 10 && MeasuredWidth(line, size) > screenWidth - 16f) size--;
            return size;
        }

        /// <summary>A safe width of a line in the overlay font (pixels): half the font size a character (Arial averages about 0.45 em on these lines).</summary>
        public static float EstimatedWidth(string line, int fontSize) => line.Length * fontSize * 0.5f;

        private void OnGUI()
        {
            if (world == null || world.Model == null) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false };
                style.padding = new RectOffset(0, 0, 0, 0);
                style.normal.textColor = Color.white;
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                labelStyle.normal.textColor = Color.white;
            }

            if (slotMarkersVisible) DrawSlotLabels();
            if (!overlayVisible) return;

            OverlayBars(Screen.width, Screen.height, out Rect top, out Rect bottom);
            Bar(top, LocationLine);
            // 4.19 review: the dialogue box reaches into the bottom wall band, so the keys bar steps aside while it is open.
            if (BottomBarShown(DialogueBox.Shared != null && DialogueBox.Shared.IsOpen)) Bar(bottom, KeysLine);
        }

        /// <summary>One bar: a dark strip over the wall band, the line in the largest font that fits its width (down to 10 px).</summary>
        private void Bar(Rect rect, string text)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var inner = new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height);
            int size = OverlayFontSize(Screen.height);
            var content = new GUIContent(text);
            style.fontSize = size;
            while (size > 10 && style.CalcSize(content).x > inner.width) style.fontSize = --size;
            GUI.Label(inner, content, style);
        }

        private string HostileText()
        {
            var hostile = new List<string>();
            foreach (Race race in RaceExtensions.All)
                if (world.Relations.IsHostile(race)) hostile.Add(race.ToString());
            return hostile.Count > 0 ? string.Join(", ", hostile) : "none";
        }

        /// <summary>Labels next to every visible slot marker that is on screen.</summary>
        private void DrawSlotLabels()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (ScreenModule module in world.AllModules)
            {
                if (module == null) continue;
                foreach (ModuleSlot slot in module.AllSlots)
                {
                    if (!slot.MarkerVisible) continue;
                    Vector3 viewport = cam.WorldToViewportPoint(slot.transform.position);
                    if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                    Vector3 screen = cam.WorldToScreenPoint(slot.transform.position);
                    var rect = new Rect(screen.x + 10f, Screen.height - screen.y - 10f, 160f, 22f);
                    GUI.color = slot.MarkerColor;
                    GUI.Label(rect, slot.Label, labelStyle);
                }
            }
            GUI.color = Color.white;
        }
    }
}
