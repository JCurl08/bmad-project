using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Runtime cube world. Owns the CubeModel for the current seed and lays each face out as an
    /// N x N block of screens in its own world region (one empty screen column between faces). Edges
    /// that lead into a sealed face get a wall. Every screen's edges are walls with one centred doorway each
    /// (ScreenEdges, 1.25): a gate arch where the doorway leads onto another face, a plain opening within a face. Town's
    /// are built with the run, each science face's on its reveal.
    /// A run starts with only Town laid out (its fixed modules); the built science faces stay dark and
    /// unpatterned until RevealScience() (called on the first crossing off Town) lays them out from
    /// CubeLayout and instantiates their modules. Sealed faces are drawn dark and never laid out.
    /// The reveal also places the items (ItemPlacement, resolved through the ItemCatalog): a gate in each
    /// chosen module gate slot and one pickup per item on its home face. Unused gate slots get no gate,
    /// so their alcove stays open. A theme with item variants uses the run's rolled variant (ItemCatalog.ItemFor),
    /// and the other variants' optional gates go into leftover home slots. Face content (IFaceContent
    /// components on this object: BiologyFace, ChemistryFace, PhysicsFace) builds its theme's gates and adds its trial and
    /// population on the reveal, and is cleared on every rebuild. Every item's pickup is a plain one-shot ItemPickup.
    /// Revealed is also where the CoreArena puts a portal to
    /// the cube's core on each built face's active core-entrance slot. The reveal also scatters the run's hidden
    /// meta-currency items (HiddenCurrencyPlacement) into hidden-item slots on the built science faces.
    /// Every face's centre screen holds its town module (4.5): placing it builds the town on it (TownSquareView), Town's with
    /// the run and each science face's on its reveal; its people come from TownPopulation (Town) and TownSquares (the rest).
    /// Every roadblock hides a reward (4.7, RewardPlacement): an item stands on a pedestal in its gated alcove or under a
    /// mound (RewardMound) on its screen's lane spot, every hidden currency item lies under a mound, and every other gate,
    /// optional gate and hidden spot of the run holds a RewardChest (Jumbles*, a key, a boost or a fragment slot).
    /// </summary>
    public class CubeWorld : MonoBehaviour
    {
        private const float WallThickness = ScreenEdges.EdgeWallThickness;

        [SerializeField] private int seed = 1234;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Material material;
        [SerializeField] private ModuleLibrary library;
        [SerializeField] private ItemCatalog itemCatalog;

        private Transform generated;
        private readonly Transform[] floorRoots = new Transform[CubeSettings.FaceCount];
        private readonly Transform[] moduleRoots = new Transform[CubeSettings.FaceCount];
        private readonly Dictionary<ScreenAddress, ScreenModule> modules = new Dictionary<ScreenAddress, ScreenModule>();
        private readonly List<Gate> gates = new List<Gate>();
        private readonly List<Gate> optionalGates = new List<Gate>();
        private readonly List<ItemPickup> pickups = new List<ItemPickup>();
        private readonly List<HiddenCurrencyPickup> hiddenCurrency = new List<HiddenCurrencyPickup>();
        private readonly List<RewardChest> rewardChests = new List<RewardChest>();
        private readonly List<RewardMound> rewardMounds = new List<RewardMound>();
        private readonly List<LockedArch> lockedArches = new List<LockedArch>();
        private readonly List<AlcoveLock> alcoveLocks = new List<AlcoveLock>();

        public CubeModel Model { get; private set; }
        public int Seed => Model != null ? Model.Seed : seed;
        /// <summary>N, read from the model (built from CubeSettings.DefaultFaceSize).</summary>
        public int FaceSize => Model != null ? Model.FaceSize : CubeSettings.DefaultFaceSize;

        public ModuleLibrary Library => library;

        public ItemCatalog ItemCatalog => itemCatalog;

        /// <summary>Where an open (unguarded) pickup sits, relative to its screen centre: in the clear vertical lane.</summary>
        public static readonly Vector2 OpenPickupOffset = new Vector2(0f, -2.5f) * ScreenMath.ContentScale;

        /// <summary>The item and gate placement of this run; null until the science faces are revealed.</summary>
        public ItemPlacement ItemPlacement { get; private set; }

        /// <summary>Every gate instantiated by the reveal (open or not).</summary>
        public IReadOnlyList<Gate> Gates => gates;

        /// <summary>Every optional gate (a non-rolled item variant's) instantiated by the reveal.</summary>
        public IReadOnlyList<Gate> OptionalGates => optionalGates;

        /// <summary>The face content hooks on this object (IFaceContent), e.g. BiologyFace.</summary>
        public IFaceContent[] FaceContents => GetComponents<IFaceContent>();

        /// <summary>The item this run uses for a theme (the rolled variant when it has variants); null without a catalog.</summary>
        public ItemDefinition ItemFor(Theme theme) => itemCatalog != null ? itemCatalog.ItemFor(theme, Seed) : null;

        /// <summary>Every pickup instantiated by the reveal; collected ones become null (destroyed).</summary>
        public IReadOnlyList<ItemPickup> Pickups => pickups;

        /// <summary>The hidden meta-currency placement of this run; null until the science faces are revealed.</summary>
        public HiddenCurrencyPlacement HiddenCurrency { get; private set; }

        /// <summary>Every hidden meta-currency pickup instantiated by the reveal (same order as HiddenCurrency.Spots); collected ones become null.</summary>
        public IReadOnlyList<HiddenCurrencyPickup> HiddenCurrencyPickups => hiddenCurrency;

        /// <summary>The run's roadblock rewards (4.7); null until the science faces are revealed.</summary>
        public RewardPlacement Rewards { get; private set; }

        /// <summary>
        /// The run's locks on the face-edge arches (4.9, EdgeLocks) and which are open; a fresh state on every rebuild. Its Plan is
        /// null (nothing locked) without a module library and an item catalog.
        /// </summary>
        public EdgeLockState Locks { get; private set; }

        /// <summary>Every locked arch built so far this run (Town's with the run, the others on the reveal); opened ones stay listed.</summary>
        public IReadOnlyList<LockedArch> LockedArches => lockedArches;

        /// <summary>The chest-key locks on this run's optional gates (4.9: the key-locked bonus alcoves).</summary>
        public IReadOnlyList<AlcoveLock> AlcoveLocks => alcoveLocks;

        /// <summary>The run's die numbers (4.8): Town 1, the other faces 2-6; rolled on every rebuild.</summary>
        public DieNumbers Dice { get; private set; }

        /// <summary>The faces repaired this run (4.8; a fresh record on every rebuild). Repairing a face lights its town (TownViewOf).</summary>
        public FaceRepairs Repairs { get; private set; } = new FaceRepairs();

        /// <summary>Raised when a face is repaired this run (after its town shows it).</summary>
        public event Action<FaceId> FaceRepaired;

        /// <summary>A face's town (its centre module's TownSquareView), or null (no town, or not built yet).</summary>
        public TownSquareView TownViewOf(FaceId face)
        {
            if (Model == null || !TownSquare.HasTown(FaceSize)) return null;
            ScreenModule module = ModuleAt(new ScreenAddress(face, TownSquare.Cell(FaceSize)));
            return module != null ? module.GetComponentInChildren<TownSquareView>(true) : null;
        }

        /// <summary>Repairs a face (its leader took all its fragments, LeaderQuest): records it and lights its town. False if it already was.</summary>
        public bool RepairFace(FaceId face)
        {
            if (Repairs == null || !Repairs.Repair(face)) return false;
            TownSquareView view = TownViewOf(face);
            if (view != null) view.Repaired = true;
            FaceRepaired?.Invoke(face);
            return true;
        }

        /// <summary>An edge opened (4.9): every arch built along it, on both faces, drops its bars.</summary>
        private void OnEdgeOpened(CubeEdge edge)
        {
            foreach (LockedArch arch in lockedArches)
                if (arch != null && arch.Edge == edge) arch.ShowOpen();
        }

        /// <summary>Opens every face-edge arch of the run (tests and the debug tools; nothing is spent).</summary>
        public void UnlockAllEdges()
        {
            if (Locks != null) Locks.OpenAll();
        }

        /// <summary>Every reward chest the reveal built (keys, boosts, fragment slots, Jumbles* chests).</summary>
        public IReadOnlyList<RewardChest> RewardChests => rewardChests;

        /// <summary>Raised for every reward chest BuildRewardChest builds (the RunWallet hooks each one as it is built).</summary>
        public event Action<RewardChest> RewardChestBuilt;

        /// <summary>Every mound (hidden spot) the reveal built: over chests, hidden currency items and lane-spot items.</summary>
        public IReadOnlyList<RewardMound> RewardMounds => rewardMounds;

        /// <summary>The run's planned gate looks (RunFacts.Of works them out once per run); cleared on every rebuild.</summary>
        public IReadOnlyDictionary<(ScreenAddress Screen, int Slot), string> PlannedGateLooks { get; set; }

        /// <summary>The science layout of this run; null until the science faces are revealed.</summary>
        public CubeLayout Layout { get; private set; }

        /// <summary>Per-run hostility flags per race (NPCs of a hostile race refuse to talk). Reset on every rebuild.</summary>
        public RaceRelations Relations { get; } = new RaceRelations();

        /// <summary>True once the built science faces have been laid out in this run.</summary>
        public bool ScienceRevealed { get; private set; }

        /// <summary>Sprite and material used for generated visuals (also used by debug markers).</summary>
        public Sprite Sprite => sprite != null ? sprite : GetFallbackSprite();
        public Material Material => material;

        /// <summary>One screen is ScreenMath.DefaultScreenSize (24x15); never duplicated here.</summary>
        public static Vector2 ScreenSize => ScreenMath.DefaultScreenSize;

        /// <summary>World size of one face: N screens by N screens.</summary>
        public Vector2 FaceExtent => ScreenSize * FaceSize;

        private readonly List<Theme> sealedThemes = new List<Theme>();

        /// <summary>
        /// Themes the next rebuilds seal on purpose (CubeModel's sealedThemes): empty in play, where every face is built;
        /// tests use it to keep the sealed-face paths covered. Takes effect on the next Rebuild.
        /// </summary>
        public List<Theme> SealedThemes => sealedThemes;

        /// <summary>Raised after every (re)build, once the new model and visuals exist.</summary>
        public event Action Rebuilt;

        /// <summary>Raised once per run, after the science faces have been laid out.</summary>
        public event Action Revealed;

        public void Configure(Sprite floorSprite, Material spriteMaterial, int startSeed, ModuleLibrary moduleLibrary,
            ItemCatalog items = null)
        {
            sprite = floorSprite;
            material = spriteMaterial;
            seed = startSeed;
            library = moduleLibrary;
            itemCatalog = items;
        }

        private void Awake()
        {
            if (Model == null) Rebuild(seed);
        }

        /// <summary>
        /// The seed of the session's first run (the RunLoop picks a fresh one): before this world has built, it only replaces
        /// the scene's seed so Awake builds once; afterwards it rebuilds.
        /// </summary>
        public void StartOnSeed(int startSeed)
        {
            if (Model == null) seed = startSeed;
            else if (Model.Seed != startSeed) Rebuild(startSeed);
        }

        /// <summary>
        /// Starts a new run: builds the model for the given seed, lays out Town and leaves the science
        /// faces unrevealed.
        /// </summary>
        public void Rebuild(int newSeed)
        {
            seed = newSeed;
            Model = new CubeModel(newSeed, CubeSettings.DefaultFaceSize, sealedThemes);
            Dice = DieNumbers.Roll(Model);
            lockedArches.Clear();
            alcoveLocks.Clear();
            if (Locks != null) Locks.Opened -= OnEdgeOpened;
            Locks = new EdgeLockState(library != null && itemCatalog != null
                ? EdgeLocks.ForRun(Model, library, itemCatalog.Themes(), itemCatalog.VariantCounts())
                : null);
            Locks.Opened += OnEdgeOpened;
            Repairs = new FaceRepairs();
            Layout = null;
            ItemPlacement = null;
            PlannedGateLooks = null;
            HiddenCurrency = null;
            Rewards = null;
            ScienceRevealed = false;
            Relations.Reset();
            modules.Clear();
            gates.Clear();
            optionalGates.Clear();
            pickups.Clear();
            hiddenCurrency.Clear();
            rewardChests.Clear();
            rewardMounds.Clear();
            foreach (IFaceContent content in FaceContents) content.Clear();

            if (generated != null) Destroy(generated.gameObject);
            generated = NewChild("Generated Faces", transform);

            for (int f = 0; f < CubeSettings.FaceCount; f++)
                BuildFace((FaceId)f);

            Rebuilt?.Invoke();
        }

        /// <summary>
        /// Lays out the built science faces from CubeLayout (seeded by the run seed only) and instantiates
        /// their modules, with exactly one active core-entrance slot per face. Does nothing if already
        /// revealed in this run. Returns true if it revealed.
        /// </summary>
        public bool RevealScience()
        {
            if (Model == null || ScienceRevealed) return false;
            ScienceRevealed = true;
            if (library != null) Layout = CubeLayout.Generate(Model, library);
            else Debug.LogWarning("CubeWorld: no ModuleLibrary assigned; science faces revealed without modules.");

            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (!CubeLayout.IsLaidOut(Model, face)) continue;
                Transform parent = floorRoots[f].parent;
                Destroy(floorRoots[f].gameObject);
                floorRoots[f] = NewChild("Floor", parent);
                parent.name = $"Face {face} ({Model.ThemeOf(face)})";
                Theme revealed = Model.ThemeOf(face);
                BuildFloor(face, floorRoots[f], ArtKeys.Floor(revealed), ArtKeys.FloorTint(revealed), true, revealed);
                BuildEdgeWalls(face, parent);
                BuildScienceModules(face);
            }
            PlaceItems();
            PlaceHiddenCurrency();
            PlaceRewards();

            Revealed?.Invoke();
            return true;
        }

        /// <summary>The module instance on a screen, or null (unrevealed or sealed face).</summary>
        public ScreenModule ModuleAt(ScreenAddress address) =>
            modules.TryGetValue(address, out ScreenModule module) ? module : null;

        /// <summary>Every module instance currently in the world.</summary>
        public IEnumerable<ScreenModule> AllModules => modules.Values;

        /// <summary>Lower-left corner of a face's region; aligned to the screen grid so ScreenCamera snaps cleanly.</summary>
        public Vector2 FaceOrigin(FaceId face)
        {
            return new Vector2((int)face * (FaceSize + 1) * ScreenSize.x, 0f);
        }

        public Vector2 ScreenCenter(ScreenAddress address)
        {
            return FaceOrigin(address.Face) + ScreenMath.ScreenCenter(address.Cell, ScreenSize);
        }

        /// <summary>
        /// Where a teleport onto a screen puts the player (CubeNavigator.TeleportTo: a new run, the F6 jump, leaving the core):
        /// the screen's centre, except on a Math island screen (1.25), whose centre may be water: there, the middle of the
        /// island of the doorway behind the arrival facing (the side the player is taken to have come in by), else of the
        /// south doorway's, else of any doorway's.
        /// </summary>
        public Vector2 LandingPoint(ScreenAddress address, Facing facing)
        {
            Vector2 centre = ScreenCenter(address);
            MathFace math = GetComponent<MathFace>();
            IslandLayout islands = math != null && math.Plan != null ? math.Plan.IslandAt(address) : null;
            if (islands == null) return centre;
            int island = islands.DoorIsland(facing.Opposite());
            if (island < 0) island = islands.DoorIsland(Facing.South);
            for (int f = 0; f < 4 && island < 0; f++) island = islands.DoorIsland((Facing)f);
            return island >= 0 ? centre + islands.Islands[island].center : centre;
        }

        /// <summary>The cell of the face that contains (or is nearest to) a world position.</summary>
        public Vector2Int CellAt(FaceId face, Vector2 worldPosition)
        {
            Vector2Int index = ScreenMath.ScreenIndex(worldPosition - FaceOrigin(face), ScreenSize);
            return new Vector2Int(Mathf.Clamp(index.x, 0, FaceSize - 1), Mathf.Clamp(index.y, 0, FaceSize - 1));
        }

        /// <summary>The screen (of any face) that contains a world position; false outside every face (e.g. the core arena).</summary>
        public bool TryScreenAt(Vector2 worldPosition, out ScreenAddress screen)
        {
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                Vector2 local = worldPosition - FaceOrigin(face);
                if (local.x < 0f || local.y < 0f || local.x >= FaceExtent.x || local.y >= FaceExtent.y) continue;
                screen = new ScreenAddress(face, CellAt(face, worldPosition));
                return true;
            }
            screen = default;
            return false;
        }

        /// <summary>
        /// A face's region colour, as on a solved Rubik's cube: Town white, Biology green, Chemistry red, Physics blue,
        /// Math yellow, Earth and Atmosphere orange. Walls, ruins, sealed floors and the map take it; floors take a
        /// per-tile tint of it (ArtKeys.FloorTint).
        /// </summary>
        public static Color ThemeColor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Town: return new Color(0.94f, 0.94f, 0.92f);
                case Theme.Biology: return new Color(0.16f, 0.66f, 0.27f);
                case Theme.Chemistry: return new Color(0.86f, 0.17f, 0.18f);
                case Theme.Physics: return new Color(0.16f, 0.38f, 0.88f);
                case Theme.Math: return new Color(0.99f, 0.84f, 0.12f);
                default: return new Color(1.00f, 0.52f, 0.10f);
            }
        }

        private void BuildFace(FaceId face)
        {
            int f = (int)face;
            Theme theme = Model.ThemeOf(face);
            bool sealedFace = Model.IsSealed(face);
            bool town = face == CubeModel.StartFace;
            string state = sealedFace ? ", sealed" : town ? "" : ", unrevealed";
            Transform root = NewChild($"Face {face} ({theme}{state})", generated);
            floorRoots[f] = NewChild("Floor", root);
            moduleRoots[f] = NewChild("Modules", root);

            if (sealedFace)
            {
                BuildFloor(face, floorRoots[f], ArtKey.FloorDark, Color.Lerp(ThemeColor(theme), Color.black, 0.45f), true);
                return;
            }

            if (town)
            {
                BuildFloor(face, floorRoots[f], ArtKeys.Floor(theme), ArtKeys.FloorTint(theme), true, theme);
                BuildEdgeWalls(face, root);
                BuildTownModules();
            }
            else
            {
                // Unrevealed science face: dark and unpatterned until the player first leaves town.
                BuildFloor(face, floorRoots[f], ArtKey.FloorDark, new Color(0.3f, 0.3f, 0.33f), false);
            }

            BuildSealedWalls(face, root);
        }

        /// <summary>
        /// A face's floor: its theme's floor art (tinted) over every screen, checkered so neighbouring screens differ. A region
        /// floor (pattern set) also gets its light, fixed texture (FloorPattern) just above it; dark floors stay plain.
        /// </summary>
        private void BuildFloor(FaceId face, Transform parent, ArtKey art, Color baseColor, bool checkered, Theme? pattern = null)
        {
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    var address = new ScreenAddress(face, x, y);
                    // Checkerboard shading so neighbouring screens are distinguishable.
                    Color color = !checkered || ((x + y) & 1) == 0 ? baseColor : Color.Lerp(baseColor, Color.black, 0.15f);
                    GameObject block = CreateBlock($"Screen {x},{y}", parent, ScreenCenter(address), ScreenSize * FloorPattern.FloorFraction, color, -10, false, art);
                    if (pattern.HasValue) AddFloorPattern(block.transform, pattern.Value, address, color);
                }
            }
        }

        /// <summary>
        /// The floor texture over one screen (exactly the floor block, a one-unit texel on each floor tile, so it never
        /// shows in the gap between screens and never straddles two tiles), and the Weightlands* glaze under it.
        /// </summary>
        private void AddFloorPattern(Transform block, Theme theme, ScreenAddress address, Color blockColor)
        {
            // The floor's own sorting order, a hair nearer the camera: over the floor tiles, under everything else (layout
            // water at -9 included).
            if (FloorPattern.GlazeAlpha(theme) > 0f)
            {
                var glaze = new GameObject("Floor Glaze");
                glaze.transform.SetParent(block, false);
                glaze.transform.localPosition = new Vector3(0f, 0f, -0.005f);
                glaze.transform.localScale = new Vector3(ScreenSize.x * FloorPattern.FloorFraction, ScreenSize.y * FloorPattern.FloorFraction, 1f);
                var glazeRenderer = glaze.AddComponent<SpriteRenderer>();
                glazeRenderer.sprite = ArtCatalog.Shape(PartShape.Square);
                if (material != null) glazeRenderer.sharedMaterial = material;
                glazeRenderer.color = FloorPattern.Glaze(theme, blockColor);
                glazeRenderer.sortingOrder = -10;
            }
            var go = new GameObject("Floor Pattern");
            go.transform.SetParent(block, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = FloorPattern.For(theme, address);
            if (material != null) renderer.sharedMaterial = material;
            renderer.sortingOrder = -10;
        }

        private void BuildTownModules()
        {
            if (library == null) return;
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    var address = new ScreenAddress(CubeModel.StartFace, x, y);
                    ScreenModule prefab = library.TownModule(address.Cell, FaceSize);
                    if (prefab != null) PlaceModule(prefab, address, -1);
                }
            }
        }

        private void BuildScienceModules(FaceId face)
        {
            if (Layout == null || !Layout.TryGetFace(face, out FaceLayout faceLayout)) return;
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    var cell = new Vector2Int(x, y);
                    ScreenModule prefab = library.Module(faceLayout.Theme, faceLayout.ModuleAt(cell));
                    int core = cell == faceLayout.CoreCell ? faceLayout.CoreSlot : -1;
                    PlaceModule(prefab, new ScreenAddress(face, cell), core);
                }
            }
        }

        /// <summary>Instantiates the run's gates and pickups from ItemPlacement (needs a layout and an item catalog).</summary>
        private void PlaceItems()
        {
            if (Layout == null) return;
            if (itemCatalog == null)
            {
                Debug.LogWarning("CubeWorld: no ItemCatalog assigned; science faces revealed without items or gates.");
                return;
            }

            ItemPlacement = ItemPlacement.ForRun(Model, Layout, library, itemCatalog.Themes(), itemCatalog.VariantCounts());
            IFaceContent[] contents = FaceContents;
            foreach (IFaceContent content in contents) content.BeginReveal(this);

            foreach (GatePlacement placement in ItemPlacement.Gates)
            {
                Gate gate = BuildGate(new FaceGateRequest(placement.Screen, placement.Slot, placement.Item, false),
                    itemCatalog.ItemFor(placement.Item, Seed), contents);
                if (gate != null) gates.Add(gate);
            }
            foreach (OptionalGatePlacement placement in ItemPlacement.OptionalGates)
            {
                List<ItemDefinition> variants = itemCatalog.VariantsOf(placement.Item);
                ItemDefinition item = placement.Variant >= 0 && placement.Variant < variants.Count ? variants[placement.Variant] : null;
                Gate gate = BuildGate(new FaceGateRequest(placement.Screen, placement.Slot, placement.Item, true), item, contents);
                if (gate == null) continue;
                optionalGates.Add(gate);
                // 4.9: a chest key opens the bonus alcove behind it.
                ScreenModule module = ModuleAt(placement.Screen);
                alcoveLocks.Add(AlcoveLock.Attach(gate, GateSize(module.Gates[placement.Slot]), material));
            }
            foreach (PickupPlacement placement in ItemPlacement.Pickups)
            {
                ScreenModule module = ModuleAt(placement.Screen);
                ItemDefinition item = itemCatalog.ItemFor(placement.Item, Seed);
                if (module == null || item == null || (placement.IsGuarded && placement.GuardSlot >= module.Gates.Length))
                {
                    Debug.LogError($"CubeWorld: cannot place {placement}");
                    continue;
                }
                Vector2 at = placement.IsGuarded
                    ? module.Gates[placement.GuardSlot].PocketCentre
                    : ScreenCenter(placement.Screen) + OpenPickupOffset;
                ItemPickup built = CreatePickup(module.transform, at, item);
                if (placement.IsGuarded && built != null) TuckIntoPocket(built.transform, module.Gates[placement.GuardSlot]);
                pickups.Add(built);
            }

            foreach (IFaceContent content in contents) content.EndReveal(this);
        }

        /// <summary>Instantiates the run's hidden meta-currency items in their hidden-item slots (needs a layout).</summary>
        private void PlaceHiddenCurrency()
        {
            if (Layout == null || library == null) return;
            HiddenCurrency = HiddenCurrencyPlacement.Generate(Model, Layout, library);
            for (int i = 0; i < HiddenCurrency.Spots.Count; i++)
            {
                HiddenCurrencySpot spot = HiddenCurrency.Spots[i];
                ScreenModule module = ModuleAt(spot.Screen);
                HiddenItemSlot[] slots = module != null ? module.HiddenItems : Array.Empty<HiddenItemSlot>();
                if (spot.Slot >= slots.Length)
                {
                    Debug.LogError($"CubeWorld: cannot place {spot}");
                    continue;
                }
                const float size = 0.7f * ScreenMath.ContentScale;
                Transform slot = slots[spot.Slot].transform;
                GameObject go = CreateBlock($"Hidden Jumble {i}", slot, slot.position, new Vector2(size, size),
                    Color.white, 4, false, ArtKey.Jumbles);
                var trigger = go.AddComponent<CircleCollider2D>();
                trigger.isTrigger = true;
                trigger.radius = HiddenCurrencyPickup.Radius;
                var pickup = go.AddComponent<HiddenCurrencyPickup>();
                pickup.Index = i;
                pickup.Amount = HiddenCurrencyPlacement.Amount;
                hiddenCurrency.Add(pickup);
            }
        }

        /// <summary>
        /// Builds the run's roadblock rewards (RewardPlacement, 4.7): a mound over each lane-spot item and each hidden currency
        /// item, and a chest behind every other roadblock (in its gate's alcove, or under a mound on its hidden-item slot).
        /// Puzzle goals (4.10-4.12) build their own chests with BuildRewardChest.
        /// </summary>
        private void PlaceRewards()
        {
            if (Layout == null || library == null || ItemPlacement == null) return;
            Rewards = RewardPlacement.Generate(Model, Layout, ItemPlacement, HiddenCurrency, library, null, Dice);
            foreach (RewardSpot spot in Rewards.Spots)
            {
                Roadblock where = spot.Where;
                ScreenModule module = ModuleAt(where.Screen);
                if (module == null)
                {
                    Debug.LogError($"CubeWorld: cannot place {spot}");
                    continue;
                }
                if (spot.What.Kind == RewardKind.Equipment)
                {
                    if (where.Kind != RoadblockKind.Mound) continue; // it stands in its gated alcove already
                    ItemPickup pickup = pickups.Find(p => p != null && p.Item != null && p.Item.HomeTheme == spot.What.Item);
                    if (pickup == null) continue;
                    RewardMound over = CreateMound(spot, pickup.transform.parent, pickup.transform.position);
                    over.Cover(pickup);
                    pickup.Guard = over;
                    continue;
                }
                if (spot.HiddenIndex >= 0)
                {
                    HiddenCurrencyPickup coin = spot.HiddenIndex < hiddenCurrency.Count ? hiddenCurrency[spot.HiddenIndex] : null;
                    if (coin == null) continue;
                    RewardMound over = CreateMound(spot, coin.transform.parent, coin.transform.position);
                    over.Cover(coin);
                    coin.Guard = over;
                    continue;
                }
                if (where.Kind == RoadblockKind.Puzzle) continue;

                Vector2 at;
                GateSlot alcove = null;
                if (where.Kind == RoadblockKind.Mound)
                {
                    HiddenItemSlot[] slots = module.HiddenItems;
                    if (where.Slot == Roadblock.LaneSpot) at = ScreenCenter(where.Screen) + OpenPickupOffset;
                    else if (where.Slot >= 0 && where.Slot < slots.Length) at = slots[where.Slot].transform.position;
                    else
                    {
                        Debug.LogError($"CubeWorld: cannot place {spot}");
                        continue;
                    }
                }
                else
                {
                    if (where.Slot < 0 || where.Slot >= module.Gates.Length)
                    {
                        Debug.LogError($"CubeWorld: cannot place {spot}");
                        continue;
                    }
                    alcove = module.Gates[where.Slot];
                    at = alcove.PocketCentre;
                }
                RewardMound mound = where.Kind == RoadblockKind.Mound ? CreateMound(spot, module.transform, at) : null;
                RewardChest chest = BuildRewardChest(spot, module.transform, at, mound);
                if (alcove != null)
                {
                    TuckIntoPocket(chest.transform, alcove);
                    chest.BlockingGate = alcove.GetComponentInChildren<Gate>(true);
                }
            }
        }

        /// <summary>
        /// A reward chest for a spot at a world point (with its icon over it for a key, boost or fragment slot), kept shut and
        /// hidden by guard (a mound) when given, and listed in RewardChests (the RunWallet pays its Jumbles*). It is also the
        /// hook 4.10-4.12 build their puzzle goals' rewards with, before Revealed is raised.
        /// </summary>
        public RewardChest BuildRewardChest(RewardSpot spot, Transform parent, Vector2 at, RewardMound guard = null)
        {
            Theme theme = Model.ThemeOf(spot.Face);
            GameObject go = CreateBlock($"Reward Chest {spot.Index} ({spot.What})", parent, at,
                Vector2.one * RewardChest.Size, Color.white, 5, false, ArtKey.Chest);
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = RewardChest.Radius;
            var chest = go.AddComponent<RewardChest>();
            chest.Spot = spot;
            chest.Theme = theme;
            chest.FragmentCount = Dice != null ? Dice.Of(spot.Face) : 0;
            SpriteRenderer icon = null;
            ArtKey iconKey = RewardChest.IconOf(spot.What);
            if (iconKey != ArtKey.None)
            {
                Color tint = spot.What.Kind == RewardKind.FragmentSlot ? ThemeColor(theme)
                    : spot.What.Kind == RewardKind.ChestKey ? RewardChest.ChestKeyTint : Color.white;
                icon = ArtCatalog.AddSprite(go.transform, "Icon", iconKey, new Vector2(0f, 0.55f * ScreenMath.ContentScale),
                    Vector2.one * RewardChest.IconSize, tint, 6, material);
            }
            if (spot.What.Kind == RewardKind.FragmentSlot)
            {
                // The fragment's number as die pips (4.8), on a badge beside its shard.
                var badge = new GameObject("Die Badge");
                badge.transform.SetParent(go.transform, false);
                badge.transform.localPosition = RewardChest.BadgeOffset;
                badge.transform.localScale = Vector3.one * RewardChest.BadgeScale;
                var badgeRenderer = badge.AddComponent<SpriteRenderer>();
                badgeRenderer.sprite = TownArt.DieFace(spot.What.Ordinal);
                if (material != null) badgeRenderer.sharedMaterial = material;
                badgeRenderer.sortingOrder = 7;
                chest.Badge = badgeRenderer;
            }
            chest.SetArt(go.transform.Find("Visual").GetComponent<SpriteRenderer>(), icon);
            if (guard != null)
            {
                guard.Cover(chest);
                chest.Guard = guard;
            }
            rewardChests.Add(chest);
            RewardChestBuilt?.Invoke(chest);
            return chest;
        }

        /// <summary>A hidden spot's heap of dirt at a world point (a trigger: it never blocks the way).</summary>
        private RewardMound CreateMound(RewardSpot spot, Transform parent, Vector2 at)
        {
            GameObject go = CreateBlock($"Mound {spot.Index}", parent, at, Vector2.one * RewardMound.Size, Color.white, 7, false,
                ArtKey.Mound);
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = RewardMound.Radius;
            var mound = go.AddComponent<RewardMound>();
            mound.Spot = spot;
            rewardMounds.Add(mound);
            return mound;
        }

        /// <summary>Builds one gate: through the face content of its item's theme when there is one, else a plain Gate.</summary>
        private Gate BuildGate(FaceGateRequest request, ItemDefinition item, IFaceContent[] contents)
        {
            ScreenModule module = ModuleAt(request.Screen);
            GateSlot[] slots = module != null ? module.Gates : Array.Empty<GateSlot>();
            if (request.Slot >= slots.Length || item == null)
            {
                Debug.LogError($"CubeWorld: cannot place {request}");
                return null;
            }
            GateSlot slot = slots[request.Slot];
            foreach (IFaceContent content in contents)
            {
                if (content.Theme != request.Item) continue;
                Gate built = content.CreateGate(this, request, slot, item);
                if (built != null) return built;
            }
            return CreateGate(slot, item);
        }

        /// <summary>How far a guarded pickup's visuals sit behind the pocket centre, clear of the gate art in front of it.</summary>
        public const float GuardedVisualDepth = 0.4f * ScreenMath.ContentScale;

        /// <summary>World size of a plain pickup's item art.</summary>
        public const float PickupSize = 0.85f * ScreenMath.ContentScale;

        /// <summary>
        /// Moves a guarded pickup's visuals (its sprite children; the trigger stays at the pocket centre) to the back of the
        /// pocket, so they never overlap the closed gate's art (which reaches ArtCatalog.GateArtReach into the pocket).
        /// </summary>
        public static void TuckIntoPocket(Transform pickup, GateSlot slot)
        {
            if (pickup == null || slot == null || slot.PocketOffset.sqrMagnitude < 1e-6f) return;
            Vector3 shift = slot.PocketOffset.normalized * GuardedVisualDepth;
            foreach (Transform child in pickup)
                if (child.GetComponent<SpriteRenderer>() != null) child.localPosition += shift;
        }

        /// <summary>The size of a gate in a slot: GateWidth across the alcove opening, GateThickness deep.</summary>
        public static Vector2 GateSize(GateSlot slot)
        {
            bool across = slot.Opening == Facing.North || slot.Opening == Facing.South;
            return across
                ? new Vector2(GateSlot.GateWidth, GateSlot.GateThickness)
                : new Vector2(GateSlot.GateThickness, GateSlot.GateWidth);
        }

        private Gate CreateGate(GateSlot slot, ItemDefinition item)
        {
            bool across = slot.Opening == Facing.North || slot.Opening == Facing.South;
            Vector2 size = across
                ? new Vector2(GateSlot.GateWidth, GateSlot.GateThickness)
                : new Vector2(GateSlot.GateThickness, GateSlot.GateWidth);
            GameObject go = CreateBlock($"Gate ({item})", slot.transform, slot.transform.position, size,
                item.PlaceholderColor, -4, true);
            var gate = go.AddComponent<Gate>();
            gate.Visual = go.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            ArtCatalog.DressGate(gate, ArtKey.GateGeneric);
            return gate;
        }

        private ItemPickup CreatePickup(Transform parent, Vector2 position, ItemDefinition item)
        {
            const float size = PickupSize;
            GameObject go = CreateBlock($"Pickup ({item})", parent, position, new Vector2(size, size),
                Color.white, 5, false, ArtKeys.Item(item));
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = ItemPickup.Radius;
            // 4.7: every item stands on a low pedestal behind its roadblock.
            ArtCatalog.AddSprite(go.transform, "Pedestal", ArtKey.Pedestal, new Vector2(0f, -0.3f * ScreenMath.ContentScale),
                new Vector2(size, size * 0.6f), Color.white, 4, material);
            var pickup = go.AddComponent<ItemPickup>();
            pickup.Item = item;
            return pickup;
        }

        private void PlaceModule(ScreenModule prefab, ScreenAddress address, int activeCoreSlot)
        {
            ScreenModule module = Instantiate(prefab, moduleRoots[(int)address.Face]);
            module.name = $"{prefab.name} @ {address.Cell.x},{address.Cell.y}";
            module.transform.position = ScreenCenter(address);
            module.SetActiveCoreEntrance(activeCoreSlot);
            // A town module (4.5) gets its race's town: buildings, plaza and statue (TownSquareView).
            if (module.IsTownScreen) TownSquareView.Build(module, material);
            // Exits whose edge leads into a sealed face are closed (that edge is a wall).
            foreach (ExitSlot exit in module.Exits)
                exit.gameObject.SetActive(Model.TryStep(address, exit.Facing, out _, out _));
            modules[address] = module;
        }

        /// <summary>
        /// The walls round every screen of a face (ScreenEdges): each edge that leads somewhere is a wall either side of a
        /// centred doorway, with a gate arch (solid posts, open middle) where it leads onto another face. Edges into a sealed
        /// face are left to BuildSealedWalls (one whole wall).
        /// </summary>
        private void BuildEdgeWalls(FaceId face, Transform root)
        {
            Transform walls = NewChild("Edge Walls", root);
            Color tint = ArtKeys.WallTint(Model.ThemeOf(face));
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    var address = new ScreenAddress(face, x, y);
                    Vector2 centre = ScreenCenter(address);
                    for (int e = 0; e < 4; e++)
                    {
                        var side = (Facing)e;
                        if (!Model.TryStep(address, side, out _, out _)) continue;
                        Rect[] pieces = ScreenEdges.WallRects(side);
                        for (int i = 0; i < pieces.Length; i++)
                            CreateBlock($"Wall {x},{y} {side} {i}", walls, centre + pieces[i].center, pieces[i].size, tint, -5, true, ArtKey.Wall);
                        if (!ScreenEdges.IsFaceEdge(FaceSize, address.Cell, side)) continue;
                        BuildGateArch($"Gate Arch {x},{y} {side}", walls, centre, side);
                        if (Locks != null && Locks.IsLocked(face, side)) lockedArches.Add(LockedArch.Build(this, walls, address, side, material));
                    }
                }
            }
        }

        /// <summary>A face-edge doorway's gate arch: the arch art over the doorway, its lintel on the edge, inside the wall band.</summary>
        private void BuildGateArch(string name, Transform parent, Vector2 screenCentre, Facing side)
        {
            Transform arch = NewChild(name, parent);
            arch.position = screenCentre + ScreenEdges.ArchCentre(side);
            SpriteRenderer art = ArtCatalog.AddSprite(arch, "Arch", ArtKey.DoorArch, Vector2.zero,
                new Vector2(ScreenEdges.ArchWidth, ScreenEdges.ArchDepth), Color.white, -4, material, false);
            art.transform.localRotation = Quaternion.Euler(0f, 0f, ScreenEdges.ArchAngle(side));
        }

        private void BuildSealedWalls(FaceId face, Transform root)
        {
            // Edges into a sealed face are walls (inside the face region, so the player never leaves it).
            Vector2 origin = FaceOrigin(face);
            Vector2 extent = FaceExtent;
            var wallColor = new Color(0.55f, 0.55f, 0.6f);
            for (int e = 0; e < 4; e++)
            {
                var edge = (Facing)e;
                if (!Model.IsSealed(CubeModel.NeighborFace(face, edge))) continue;

                Vector2 centre, size;
                switch (edge)
                {
                    case Facing.North:
                        centre = origin + new Vector2(extent.x / 2f, extent.y - WallThickness / 2f);
                        size = new Vector2(extent.x, WallThickness);
                        break;
                    case Facing.South:
                        centre = origin + new Vector2(extent.x / 2f, WallThickness / 2f);
                        size = new Vector2(extent.x, WallThickness);
                        break;
                    case Facing.East:
                        centre = origin + new Vector2(extent.x - WallThickness / 2f, extent.y / 2f);
                        size = new Vector2(WallThickness, extent.y);
                        break;
                    default:
                        centre = origin + new Vector2(WallThickness / 2f, extent.y / 2f);
                        size = new Vector2(WallThickness, extent.y);
                        break;
                }
                CreateBlock($"Sealed Wall {edge}", root, centre, size, wallColor, 0, true, ArtKey.WallSealed);
            }
        }

        private static Transform NewChild(string name, Transform parent)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// <summary>
        /// A block: a GameObject at centre with a "Visual" child sprite of the given size and colour, and (optionally) a
        /// solid BoxCollider2D of that size on the root. With an art key the visual is that key's art from the ArtCatalog
        /// (tiled for walls, floors and gates); without one it is the plain placeholder square. Face content uses it too.
        /// </summary>
        public GameObject CreateBlock(string name, Transform parent, Vector2 centre, Vector2 size, Color color,
            int sortingOrder, bool collider, ArtKey art = ArtKey.None)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;

            // Visual is a scaled child so the collider on the parent keeps world-unit sizes.
            Sprite s = Sprite;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            Vector2 spriteSize = s.bounds.size;
            visual.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = s;
            if (art != ArtKey.None) ArtCatalog.Apply(renderer, art, size);
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            if (collider) go.AddComponent<BoxCollider2D>().size = size;
            return go;
        }

        /// <summary>The plain placeholder square (the ArtCatalog's generated square).</summary>
        private static Sprite GetFallbackSprite() => ArtCatalog.Shape(PartShape.Square);
    }
}
