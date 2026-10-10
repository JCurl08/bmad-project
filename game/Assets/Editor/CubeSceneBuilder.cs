using System.Collections.Generic;
using System.Linq;
using Game.Cube;
using Game.Tracer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cube > Create Cube Scene: builds Assets/Scenes/Cube.unity from code with the player, the screen
/// camera, the CubeWorld and the debug overlay, and puts it first in the build list (Tracer is kept).
/// The CubeWorld is wired to the module library and the item catalog (each built first if missing);
/// the player carries an Inventory and the combat parts (Health, PlayerStats, Equipment, PlayerAttack), and a
/// CombatHud shows its hearts and equipped item. A TownPopulation fills the Town face with NPCs on every run start.
/// The CubeWorld also carries the face content hooks (IFaceContent): BiologyFace for Darwin's face and ChemistryFace
/// for Curie's (the player carries the Isotope that shows the held stone), and PhysicsFace for Einstein's (the player
/// carries the MassMitt it sets up for each run), MathFace for the Bridgekeeper*'s (gap gates, island screens and the trial island), and
/// EarthFace for the Wildlands* (Stormcaller* gates, hazards, trial and dinosaurs; the player carries the HazardReceiver
/// that feeds the hazards to PlayerMover's modifier hook).
/// The run's outcome lives in a RunState (one per scene, reset on every rebuild). The CoreArena (Maxwell's Demon) puts a
/// portal on each built face's active core-entrance slot on every reveal and raises RunState's run-end event on victory or
/// defeat; a CoreHud shows its Order ↔ Entropy meter and the outcome.
/// The run loop: a RunWallet collects the run's meta currency (trials, the Demon, hidden items), and the RunLoop banks it
/// on every run end (death anywhere or the boss outcome), saves the MetaSave, opens the BetweenRunsScreen (earnings and
/// upgrade shop) and starts the next run on Continue.
/// The Cube Map object carries the VisitTracker (screens visited this run) and the CubeMap pause map (M or Tab).
/// Everything is drawn from the ArtCatalog (built first if missing): the player is the hero tile, and the player carries a
/// PickupFeedback for item and Jumble popups.
/// Cube > Seed Sweep: runs the reachability, core-entrance and item-softlock sweep over 50 seeds and logs the result.
/// Both also work from the command line via -executeMethod.
/// </summary>
public static class CubeSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Cube.unity";

    private const string UnlitSpriteMaterialPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    private const int DefaultSeed = 1234;

    [MenuItem("Cube/Create Cube Scene")]
    public static void CreateCubeScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // The art catalog first: the player, walls, floors and the rest are drawn from it.
        ArtCatalog.Use(ArtCatalogBuilder.LoadOrBuild());

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Vector2 screen = ScreenMath.DefaultScreenSize;
        Sprite squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);

        // World: generates the faces at runtime from the seed.
        var worldObject = new GameObject("Cube World");
        var world = worldObject.AddComponent<CubeWorld>();
        world.Configure(squareSprite, material, DefaultSeed, ModuleLibraryBuilder.LoadOrBuild(),
            ItemCatalogBuilder.LoadOrBuild());

        // Player at the centre of the Town start screen (face Front sits at the world origin).
        Vector2 start = ScreenMath.ScreenCenter(CubeModel.StartCell(CubeSettings.DefaultFaceSize));
        var player = new GameObject("Player");
        player.transform.position = start;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(player.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        ArtCatalog.Apply(renderer, ArtKey.Player, Vector2.one * (ScreenMath.ContentScale * CubeSettings.PeopleArtScale)); // the hero: one tile of the first screens, scaled (4.1), drawn PeopleArtScale larger than its collider
        if (material != null) renderer.sharedMaterial = material;
        renderer.color = Color.white;
        renderer.sortingOrder = 10;
        player.AddComponent<CircleCollider2D>().radius = TimeField.PlayerRadius; // 0.4 on the first screens, scaled
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        player.AddComponent<PlayerMover>();
        player.AddComponent<Inventory>();
        // Pickup feedback: "+Thin Beak" and "+5 Jumbles" popups over the player.
        var feedback = player.AddComponent<PickupFeedback>();
        // Combat: stats (with the Health they feed), the equipped item, the melee attack and the HUD.
        var health = player.AddComponent<Health>();
        player.AddComponent<PlayerStats>();
        var equipment = player.AddComponent<Equipment>();
        player.AddComponent<PlayerAttack>();
        // The held Chemistry stone's label, halo and HUD line (read from the inventory; no timer).
        player.AddComponent<Isotope>();
        // The Mass Mitt: grabs and drags Physics boulders (set up for each run by PhysicsFace).
        player.AddComponent<MassMitt>();
        // The Earth hazards' way into movement (PlayerMover's one modifier hook); nothing off the Earth face.
        player.AddComponent<HazardReceiver>();
        player.AddComponent<IceSlider>(); // 4.10: tile-by-tile sliding on the Earth ice screens
        player.AddComponent<WindRider>(); // 4.11: tile-by-tile walking and riding on the Earth wind screens
        var navigator = player.AddComponent<CubeNavigator>();
        navigator.World = world;
        // Roadblock rewards (4.7): the keys, run boosts and fragment slots the player takes this run.
        var rewards = player.AddComponent<RunRewards>();
        rewards.World = world;

        // Face content: Darwin's face (beak gates, trial, finches and enemies) plugs in on the reveal.
        worldObject.AddComponent<BiologyFace>().Player = player.transform;
        // Curie's face (stage gates, trial, mushrooms and enemies) plugs in the same way.
        worldObject.AddComponent<ChemistryFace>().Player = player.transform;
        // Einstein's face (timed doors, boulders, trial, aliens, Newton and enemies) plugs in the same way.
        worldObject.AddComponent<PhysicsFace>().Player = player.transform;
        // The Bridgekeeper*'s face (gap gates, island screens, trial island, shape people and enemies) plugs in the same way.
        worldObject.AddComponent<MathFace>().Player = player.transform;
        // The Wildlands* (Stormcaller* gates, hazards, trial, dinosaurs and enemies) plugs in the same way.
        worldObject.AddComponent<EarthFace>().Player = player.transform;
        // Towns (4.5): every science face's town gets its leader and residents on the reveal (Town's are TownPopulation's).
        var squares = worldObject.AddComponent<TownSquares>();
        squares.World = world;
        squares.Player = player.transform;
        // Enterable buildings (4.6): every town's marked door leads into its race's room, off the cube net.
        var interiors = worldObject.AddComponent<TownInteriors>();
        interiors.World = world;
        interiors.Navigator = navigator;
        // The boss lair (4.13): every core portal but the run's real one leads down into its face's lair room, off the net.
        var lairRooms = worldObject.AddComponent<LairRooms>();
        lairRooms.World = world;
        lairRooms.Navigator = navigator;
        // The temples (4.14): every town's temple door, once unsealed, leads into its face's temple room, off the net.
        var templeRooms = worldObject.AddComponent<TempleRooms>();
        templeRooms.World = world;
        templeRooms.Navigator = navigator;

        // Letterbox camera: clears the whole window to black behind the main camera's viewport.
        var letterbox = new GameObject("Letterbox Camera").AddComponent<Camera>();
        letterbox.orthographic = true;
        letterbox.depth = -10;
        letterbox.cullingMask = 0;
        letterbox.clearFlags = CameraClearFlags.SolidColor;
        letterbox.backgroundColor = Color.black;

        // Main camera snapping to the player's screen.
        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = cameraObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = screen.y / 2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.depth = 0;
        cameraObject.transform.position = new Vector3(start.x, start.y, -10f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<ScreenCamera>().Target = player.transform;

        // Debug overlay and commands (self-disable outside editor / development builds).
        var debug = new GameObject("Cube Debug").AddComponent<CubeDebug>();
        debug.World = world;
        debug.Navigator = navigator;
        debug.Interiors = interiors;

        // Town: one NPC of each face race plus a few Townsfolk, respawned on every run start.
        var town = new GameObject("Town Population").AddComponent<TownPopulation>();
        town.World = world;
        town.Player = player.transform;

        var hud = new GameObject("Combat HUD").AddComponent<CombatHud>();
        hud.Health = health;
        hud.Equipment = equipment;
        hud.Rewards = rewards;

        // The run's outcome (one per scene) and the core arena: portals on every built face's core slot lead to
        // Maxwell's Demon; victory or defeat ends the run through the RunState.
        var runState = new GameObject("Run State").AddComponent<RunState>();
        runState.World = world;
        var arena = new GameObject("Core Arena").AddComponent<CoreArena>();
        arena.Configure(world, navigator, runState);
        arena.Lair = lairRooms;
        debug.CoreArena = arena;
        debug.Lair = lairRooms;
        new GameObject("Core HUD").AddComponent<CoreHud>().Arena = arena;

        // The roguelite loop: this run's earnings, the meta save and upgrades, and the between-runs screen.
        var wallet = new GameObject("Run Wallet").AddComponent<RunWallet>();
        wallet.Configure(world, arena, runState);
        feedback.Wallet = wallet;
        var loop = new GameObject("Run Loop").AddComponent<RunLoop>();
        loop.Configure(world, runState, arena, wallet, player.GetComponent<PlayerStats>());
        new GameObject("Between Runs Screen").AddComponent<BetweenRunsScreen>().Loop = loop;

        // The pause map (M or Tab): the cube net with the screens visited this run, their content and the player.
        var mapObject = new GameObject("Cube Map");
        var tracker = mapObject.AddComponent<VisitTracker>();
        tracker.World = world;
        tracker.Navigator = navigator;
        var map = mapObject.AddComponent<CubeMap>();
        map.World = world;
        map.Navigator = navigator;
        map.Tracker = tracker;
        map.Loop = loop;
        // The quest log (Q, 4.18): every quest given this run, on its own pause panel.
        var quests = mapObject.AddComponent<QuestLogView>();
        quests.World = world;
        quests.Navigator = navigator;
        quests.Map = map;
        quests.Loop = loop;

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError($"Cube: failed to save {ScenePath}.");
            return;
        }

        // Cube first, keep every other existing entry (Tracer's PlayMode tests load it by name).
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != ScenePath));
        if (scenes.All(s => s.path != TracerSceneBuilder.ScenePath))
            scenes.Add(new EditorBuildSettingsScene(TracerSceneBuilder.ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"Cube: created {ScenePath}; build scenes: {string.Join(", ", scenes.Select(s => s.path))}");
    }

    [MenuItem("Cube/Seed Sweep")]
    public static void RunSeedSweep()
    {
        ItemCatalog items = ItemCatalogBuilder.LoadOrBuild();
        List<SeedSweep.SeedResult> results =
            SeedSweep.Run(catalog: ModuleLibraryBuilder.LoadOrBuild(), itemThemes: items.Themes(),
                variantCounts: items.VariantCounts(), biologyVariantKinds: BiologyBeaks.VariantKinds(items),
                chemistryVariantKinds: ChemistryIsotope.VariantKinds(items), earthVariantKinds: StormcallerItem.VariantKinds(items));
        string report = SeedSweep.Describe(results);
        int thin = results.Count(r => r.Placement != null && r.Placement.RolledVariant(Theme.Biology) == (int)BeakKind.Thin);
        int withOptional = results.Count(r => r.Placement != null && r.Placement.OptionalGates.Count > 0);
        report += $"Beaks: {thin} thin, {results.Count - thin} thick; {withOptional} seeds with optional beak gates.\n";
        var stones = new int[ChemistryIsotope.VariantCount];
        int stageGates = 0, optionalStageGates = 0;
        List<IsotopeStage> stoneKinds = ChemistryIsotope.VariantKinds(items);
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Placement == null) continue;
            ChemistryPlan plan = ChemistryPlan.Create(new CubeModel(r.Seed), r.Placement, stoneKinds);
            if (plan == null) continue;
            stones[(int)plan.Stage]++;
            foreach (IsotopeGateSpec s in plan.Gates)
            {
                stageGates++;
                if (s.Optional) optionalStageGates++;
            }
        }
        report += $"Stones: {stones[0]} Glowstone*, {stones[1]} Wildstone*, {stones[2]} Leadstone*; " +
                  $"{stageGates} stage gates ({optionalStageGates} optional).\n";
        var required = new int[PhysicsPlan.MaxRequiredCap + 1];
        int doors = 0, offHome = 0, boulders = 0, trials = 0;
        ModuleLibrary library = ModuleLibraryBuilder.LoadOrBuild();
        List<BeakKind> kinds = BiologyBeaks.VariantKinds(items);
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Placement == null) continue;
            var model = new CubeModel(r.Seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            PhysicsPlan plan = PhysicsPlan.Create(model, r.Placement, SeedSweep.ModuleLookup(layout, library), kinds,
                PhysicsPlan.DefaultMaxRequired, stoneKinds);
            if (plan == null) continue;
            foreach (TimedDoorSpec d in plan.Doors)
            {
                doors++;
                if (!d.OnHome) offHome++;
                required[Mathf.Clamp(d.Required, 0, PhysicsPlan.MaxRequiredCap)]++;
                boulders += d.Boulders.Count;
            }
            if (plan.Trial != null) trials++;
        }
        int hiddenTotal = results.Sum(r => r.HiddenCurrency != null ? r.HiddenCurrency.Spots.Count : 0);
        report += $"Hidden meta currency: {hiddenTotal} items over {results.Count} seeds " +
                  $"({HiddenCurrencyPlacement.MinCount}-{HiddenCurrencyPlacement.MaxCount} per run, {HiddenCurrencyPlacement.Amount} each).\n";
        report += $"Timed doors: {doors} ({offHome} off-home), needing 1/2/3 boulders: {required[1]}/{required[2]}/{required[3]}; " +
                  $"{boulders} gate boulders; the Physics trial fits on {trials} seeds.\n";
        int islandScreens = 0, islands = 0, planks = 0, brokenSpans = 0, lookouts = 0, gapGates = 0, offHomeGaps = 0, mathTrials = 0,
            mathPickupsOnIslands = 0;
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Placement == null) continue;
            var model = new CubeModel(r.Seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            MathPlan plan = MathPlan.Create(model, r.Placement, SeedSweep.ModuleLookup(layout, library), kinds, stoneKinds);
            if (plan == null) continue;
            foreach (MathGateSpec s in plan.Gates)
            {
                gapGates++;
                if (!s.OnHome) offHomeGaps++;
            }
            foreach (IslandScreenSpec s in plan.IslandScreens)
            {
                islandScreens++;
                if (s.Layout == null) continue;
                islands += s.Layout.Islands.Count;
                planks += s.Layout.Bridges.Count;
                brokenSpans += s.Layout.Bridges.Count(b => b.Broken);
                int[] all = s.Layout.Components(true);
                foreach (Facing side in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
                {
                    int door = s.Layout.DoorIsland(side);
                    if (door >= 0 && s.Layout.Bridges.Any(b => b.Joins(door)) &&
                        s.Layout.DoorsReaching(door, true).Count == 1) lookouts++;
                }
            }
            if (plan.HasTrial) mathTrials++;
            if (r.Placement.TryGetPickup(Theme.Math, out PickupPlacement mathPickup) && plan.IsIslandScreen(mathPickup.Screen))
                mathPickupsOnIslands++;
        }
        report += $"Math: {islandScreens} island screens ({islands} islands, {planks} planks, {brokenSpans} broken spans, " +
                  $"{lookouts} lone doorways with a lookout island); {gapGates} gap gates ({offHomeGaps} off-home); the Math trial " +
                  $"island on {mathTrials} seeds; the gap-crosser* on an island on {mathPickupsOnIslands} seeds.\n";
        // 4.12: the island screens' "light every plank" puzzles, solver-checked from every doorway, with their difficulty per tier.
        var islandStats = new IslandStats();
        var islandTiers = new int[3];
        int islandSeeds = 0, islandFailing = 0;
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Islands == null) continue;
            islandSeeds++;
            if (r.IslandProblems != null && r.IslandProblems.Count > 0) islandFailing++;
            foreach (IslandScreenSpec s in r.Islands.Screens)
            {
                islandStats.Add(s);
                if (s.Layout != null) islandTiers[(int)s.Layout.Tier]++;
            }
        }
        report += $"Islands: {islandStats.Screens} island screens on {islandSeeds} seeds ({islandTiers[0]} early, {islandTiers[1]} mid, " +
                  $"{islandTiers[2]} late), {islandStats.Missing} without a puzzle, {islandStats.Templates} from the fallback template, " +
                  $"{islandFailing} seeds failing, {islandStats.Outside} doorway checks outside a target; every doorway solver-checked; " +
                  $"{(islandStats.Screens > 0 ? islandStats.Steps / islandStats.Screens : 0)} search steps a screen on average.\n" + islandStats.Describe();
        List<Phenomenon> wandKinds = StormcallerItem.VariantKinds(items);
        var rolledWands = new int[StormcallerItem.VariantCount];
        int earthGates = 0, offHomeEarth = 0, optionalEarth = 0, earthTrials = 0;
        var hazardCounts = new int[4];
        var levels = new SortedDictionary<int, int>();
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Placement == null) continue;
            var model = new CubeModel(r.Seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            EarthPlan plan = EarthPlan.Create(model, r.Placement, SeedSweep.ModuleLookup(layout, library), wandKinds, kinds, stoneKinds);
            if (plan == null) continue;
            if (plan.Rolled != Phenomenon.None) rolledWands[(int)plan.Rolled]++;
            foreach (EarthGateSpec g in plan.Gates)
            {
                earthGates++;
                if (g.Optional) optionalEarth++;
                else if (!g.OnHome) offHomeEarth++;
            }
            foreach (HazardSpec h in plan.Hazards) hazardCounts[(int)h.Kind]++;
            levels.TryGetValue(plan.Level, out int c);
            levels[plan.Level] = c + 1;
            if (plan.TrialSpots != null && plan.TrialSpots.Count >= EarthTrial.PieceCount) earthTrials++;
        }
        report += $"Earth: Stormcallers* {rolledWands[0]} quake, {rolledWands[1]} rain, {rolledWands[2]} snow, {rolledWands[3]} tornado; " +
                  $"{earthGates} gates ({offHomeEarth} off-home, {optionalEarth} optional); hazards " +
                  $"{hazardCounts[2]} freezing rain, {hazardCounts[3]} crevices; levels " +
                  $"{string.Join(", ", levels.Select(e => $"{e.Key}x{e.Value}"))}; the Earth trial fits on {earthTrials} seeds.\n";
        // 4.10: the ice screens, solver-checked from every doorway, with their difficulty per tier.
        var iceStats = new IceStats();
        var iceTiers = new int[3];
        int iceSeeds = 0, iceFailing = 0, iceTemplates = 0;
        long iceSteps = 0;
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Ice == null) continue;
            iceSeeds++;
            if (r.IceProblems != null && r.IceProblems.Count > 0) iceFailing++;
            foreach (IceScreenSpec s in r.Ice.Screens)
            {
                iceStats.Add(s);
                iceTiers[(int)s.Tier]++;
                if (s.Layout != null) iceSteps += s.Layout.SearchSteps;
                if (s.Layout != null && s.Layout.FromTemplate) iceTemplates++;
            }
        }
        report += $"Ice: {iceStats.Screens} ice screens on {iceSeeds} seeds ({iceTiers[0]} early, {iceTiers[1]} mid, {iceTiers[2]} late), " +
                  $"{iceStats.Missing} without a layout, {iceTemplates} from the fallback template, {iceFailing} seeds failing; every doorway solver-checked; " +
                  $"{(iceStats.Screens > 0 ? iceSteps / iceStats.Screens : 0)} search steps a screen on average.\n" + iceStats.Describe();
        // 4.11: the wind screens, solver-checked from every doorway, with their difficulty per tier.
        var windStats = new WindStats();
        var windTiers = new int[3];
        int windSeeds = 0, windFailing = 0, windTemplates = 0;
        long windSteps = 0;
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Wind == null) continue;
            windSeeds++;
            if (r.WindProblems != null && r.WindProblems.Count > 0) windFailing++;
            foreach (WindScreenSpec s in r.Wind.Screens)
            {
                windStats.Add(s);
                windTiers[(int)s.Tier]++;
                if (s.Layout != null) windSteps += s.Layout.SearchSteps;
                if (s.Layout != null && s.Layout.FromTemplate) windTemplates++;
            }
        }
        report += $"Wind: {windStats.Screens} wind screens on {windSeeds} seeds ({windTiers[0]} early, {windTiers[1]} mid, {windTiers[2]} late), " +
                  $"{windStats.Missing} without a layout, {windTemplates} from the fallback template, {windFailing} seeds failing; every doorway solver-checked; " +
                  $"{(windStats.Screens > 0 ? windSteps / windStats.Screens : 0)} search steps a screen on average.\n" + windStats.Describe();
        report += RewardReport(results, library);
        report += LockReportLine(results);
        report += LairReportLine(results);
        report += TempleReportLine(results);
        int towns = results.Sum(r => r.TownsChecked);
        report += $"Towns: {towns} checked over {results.Count} seeds (the Town face's and every built face's centre: its town " +
                  "module, slot-free, with a sound layout; no gate, pickup, core portal, puzzle screen, trial, flower or hidden spot on it).\n";
        report += QuestReportLine(results);
        if (results.All(r => r.Passed)) Debug.Log(report);
        else Debug.LogError(report);
    }

    /// <summary>
    /// The sweep's quest line (4.20): the Mayor's tour of Town walkable start to end per seed (its stops, from which screen and
    /// which way round), and the errands (4.18).
    /// </summary>
    private static string QuestReportLine(List<SeedSweep.SeedResult> results)
    {
        int seeds = 0, passed = 0, clockwise = 0;
        var starts = new SortedDictionary<string, int>();
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.QuestProblems == null) continue;
            seeds++;
            if (r.QuestProblems.Count == 0) passed++;
            if (r.TownTour == null) continue;
            if (r.TownTour.Direction > 0) clockwise++;
            string first = HintGenerator.ScreenPhrase(r.TownTour.Stops[0].Screen, r.TownTour.FaceSize);
            starts[first] = starts.TryGetValue(first, out int c) ? c + 1 : 1;
        }
        if (seeds == 0) return "Quests: not checked.\n";
        string from = string.Join(", ", starts.Select(kv => $"{kv.Key} {kv.Value}"));
        return $"Quests: the Mayor's tour of Town walkable on {passed}/{seeds} seeds ({TownTour.Lessons.Count} stops, one doorway " +
               $"apart; first stop {from}; clockwise on {clockwise}), and one errand in every town.\n";
    }

    /// <summary>
    /// The sweep's lair line (4.13): the real portal's face theme per seed, the lair rooms and prisoners, and whether the real
    /// portal is proven reachable (its screen from Town, and under every key-spending order: only on seeds where the lock check ran).
    /// </summary>
    /// <summary>The sweep's temple line (4.14): how many temples, of which kind, solvable; templates; mean stats by kind.</summary>
    private static string TempleReportLine(List<SeedSweep.SeedResult> results)
    {
        int seeds = 0, passed = 0, temples = 0, templates = 0;
        var kinds = new SortedDictionary<TemplePuzzleKind, int>();
        var samples = new SortedDictionary<TemplePuzzleKind, string>();
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.TempleProblems == null) continue;
            seeds++;
            if (r.TempleProblems.Count == 0) passed++;
            var model = new CubeModel(r.Seed);
            foreach (FaceId face in TemplePlan.TempleFaces(model))
            {
                TemplePuzzle t = TemplePlan.For(model, face);
                temples++;
                if (t.FromTemplate) templates++;
                kinds.TryGetValue(t.Kind, out int c);
                kinds[t.Kind] = c + 1;
                if (!samples.ContainsKey(t.Kind)) samples[t.Kind] = $"seed {r.Seed}: {t.Stats()}";
            }
        }
        if (seeds == 0) return "Temples: not checked.\n";
        string text = $"Temples: every temple solvable on {passed}/{seeds} seeds ({temples} temples: " +
                      string.Join(", ", kinds.Select(e => $"{e.Key} {e.Value}")) + $"; {templates} from a template).\n";
        foreach (KeyValuePair<TemplePuzzleKind, string> e in samples) text += $"  {e.Key}: {e.Value}\n";
        return text;
    }

    private static string LairReportLine(List<SeedSweep.SeedResult> results)
    {
        int seeds = 0, passed = 0, rooms = 0, prisoners = 0, realReachable = 0;
        var realThemes = new SortedDictionary<Theme, int>();
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Lair == null || r.LairProblems == null) continue;
            seeds++;
            if (r.LairProblems.Count == 0) passed++;
            var model = new CubeModel(r.Seed);
            Theme real = model.ThemeOf(r.Lair.RealFace);
            realThemes.TryGetValue(real, out int c);
            realThemes[real] = c + 1;
            rooms += r.Lair.LairFaces.Count;
            foreach (FaceId face in r.Lair.LairFaces) prisoners += r.Lair.PrisonersOf(face).Count;
            // 4.13 review: proven only where the lock check actually ran (and passed), not merely where it was skipped.
            if (r.LairProblems.Count == 0 && r.LockProblems != null && r.LockProblems.Count == 0) realReachable++;
        }
        if (seeds == 0) return "Lair: not checked.\n";
        return $"Lair: exactly one portal to the arena on {passed}/{seeds} seeds (the real portal by region: " +
               string.Join(", ", realThemes.Select(e => $"{e.Key} {e.Value}")) + $"); {rooms} lair rooms with {prisoners} prisoners, " +
               $"every room with a fight, benefit and real-portal hint; the real portal proven reachable on {realReachable}/{seeds} seeds.\n";
    }

    /// <summary>The sweep's lock line (4.9): the lock kinds, the first edges and their variety, and the spending states explored.</summary>
    private static string LockReportLine(List<SeedSweep.SeedResult> results)
    {
        int seeds = 0, keyLocks = 0, itemLocks = 0, states = 0, draws = 0, maxDraw = 0, fallbacks = 0, passed = 0;
        var firstCounts = new SortedDictionary<int, int>();
        var firstRegions = new SortedDictionary<Theme, int>();
        var itemLockThemes = new SortedDictionary<Theme, int>();
        var firstSets = new HashSet<string>();
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Locks == null || r.LockReport == null) continue;
            seeds++;
            if (r.LockProblems.Count == 0) passed++;
            var model = new CubeModel(r.Seed);
            keyLocks += r.Locks.Count(LockKind.Key);
            itemLocks += r.Locks.Count(LockKind.Item);
            foreach (EdgeLock l in r.Locks.Locks)
                if (l.Kind == LockKind.Item)
                {
                    itemLockThemes.TryGetValue(l.Item, out int c);
                    itemLockThemes[l.Item] = c + 1;
                }
            states += r.LockReport.States;
            draws += r.Locks.Attempts;
            maxDraw = Mathf.Max(maxDraw, r.Locks.Attempts);
            if (r.Locks.FellBack) fallbacks++;
            firstCounts.TryGetValue(r.LockReport.FirstEdges.Count, out int n);
            firstCounts[r.LockReport.FirstEdges.Count] = n + 1;
            var names = new List<Theme>();
            foreach (CubeEdge e in r.LockReport.FirstEdges)
            {
                Theme t = model.ThemeOf(e.A == CubeModel.StartFace ? e.B : e.A);
                names.Add(t);
                firstRegions.TryGetValue(t, out int c);
                firstRegions[t] = c + 1;
            }
            names.Sort();
            firstSets.Add(string.Join(",", names));
        }
        if (seeds == 0) return "Locks: not checked.\n";
        return $"Locks: {keyLocks} key and {itemLocks} item locks over {seeds} seeds (12 edges a run; item locks by item: " +
               string.Join(", ", itemLockThemes.Select(e => $"{e.Key} {e.Value}")) + $"); no spending order soft-locks on {passed}/{seeds} seeds " +
               $"({states} spending states explored); first edges per seed: " + string.Join(", ", firstCounts.Select(e => $"{e.Key} on {e.Value}")) +
               "; first routes by region: " + string.Join(", ", firstRegions.Select(e => $"{e.Key} {e.Value}")) +
               $"; {firstSets.Count} different sets of first routes; draws kept {draws} in all (at most {maxDraw}); fallbacks {fallbacks}.\n";
    }

    /// <summary>The sweep's roadblock-reward lines (4.7): the kinds, the roadblocks and each face theme's mix per run.</summary>
    private static string RewardReport(List<SeedSweep.SeedResult> results, ModuleLibrary library)
    {
        var kinds = new int[6];
        var bonus = 0;
        var roadblocks = new int[4];
        int lane = 0, hiddenJumbles = 0, jumbles = 0, requiredJumbles = 0, everyKind = 0, seeds = 0, minSpots = int.MaxValue, maxSpots = 0;
        var boosts = new int[3];
        var perTheme = new Dictionary<Theme, int[]>();
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Rewards == null) continue;
            seeds++;
            var model = new CubeModel(r.Seed);
            var seen = new HashSet<RewardKind>();
            minSpots = Mathf.Min(minSpots, r.Rewards.Spots.Count);
            maxSpots = Mathf.Max(maxSpots, r.Rewards.Spots.Count);
            foreach (RewardSpot s in r.Rewards.Spots)
            {
                kinds[(int)s.What.Kind]++;
                seen.Add(s.What.Kind);
                roadblocks[(int)s.Where.Kind]++;
                if (!s.Required) bonus++;
                if (s.Where.IsLaneSpot) lane++;
                if (s.HiddenIndex >= 0) hiddenJumbles++;
                if (s.What.Kind == RewardKind.Jumbles)
                {
                    jumbles += s.What.Amount;
                    if (s.Required) requiredJumbles += s.What.Amount;
                }
                if (s.What.Kind == RewardKind.Upgrade) boosts[(int)s.What.Upgrade]++;
                Theme theme = model.ThemeOf(s.Face);
                if (!perTheme.TryGetValue(theme, out int[] mix)) perTheme[theme] = mix = new int[7];
                mix[(int)s.What.Kind]++;
                if (!s.Required) mix[6]++;
            }
            if (seen.Count == 6) everyKind++;
        }
        if (seeds == 0) return "Rewards: not checked.\n";
        string Avg(int count) => (count / (float)seeds).ToString("0.0");
        string text = $"Rewards: {kinds.Sum()} over {seeds} seeds ({minSpots}-{maxSpots} a run): {kinds[(int)RewardKind.Jumbles]} Jumbles* " +
                      $"({bonus} bonus behind optional gates, {hiddenJumbles} hidden; {Avg(requiredJumbles)} reachable Jumbles* a run), " +
                      $"{kinds[(int)RewardKind.Equipment]} equipment ({lane} under a lane mound), {kinds[(int)RewardKind.Upgrade]} boosts " +
                      $"({boosts[0]} heart, {boosts[1]} speed, {boosts[2]} power), {kinds[(int)RewardKind.Key]} keys, {kinds[(int)RewardKind.ChestKey]} chest keys, " +
                      $"{kinds[(int)RewardKind.FragmentSlot]} fragments; every kind on {everyKind}/{seeds} seeds. Roadblocks: " +
                      $"{roadblocks[(int)RoadblockKind.Gate]} gates, {roadblocks[(int)RoadblockKind.OptionalGate]} optional gates, " +
                      $"{roadblocks[(int)RoadblockKind.Mound]} mounds, {roadblocks[(int)RoadblockKind.Puzzle]} puzzle goals.\n";
        // 4.8: the die numbers and the fragments they place.
        int townOnes = 0, fragments = 0, reachableFragments = 0;
        var numbersByTheme = new Dictionary<Theme, SortedSet<int>>();
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Rewards == null) continue;
            var model = new CubeModel(r.Seed);
            DieNumbers dice = DieNumbers.Roll(model);
            if (dice.Of(CubeModel.StartFace) == DieNumbers.TownNumber) townOnes++;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                Theme theme = model.ThemeOf((FaceId)f);
                if (!numbersByTheme.TryGetValue(theme, out SortedSet<int> set)) numbersByTheme[theme] = set = new SortedSet<int>();
                set.Add(dice.Of((FaceId)f));
            }
            foreach (RewardSpot s in r.Rewards.Spots)
            {
                if (s.What.Kind != RewardKind.FragmentSlot) continue;
                fragments++;
                if (s.Required && r.RewardsChecked && r.RewardProblems.Count == 0) reachableFragments++; // proven only where the reward check ran
            }
        }
        text += $"Fragments: {fragments} on the science faces over {seeds} seeds ({reachableFragments} proven reachable; " +
                $"{DieNumbers.FragmentsPerRun - DieNumbers.TownNumber} a run, plus Town's 1 from its leader); Town numbered 1 on {townOnes}/{seeds} seeds; " +
                "numbers seen per region: " + string.Join(", ", numbersByTheme.OrderBy(e => e.Key).Select(e => $"{e.Key} {{{string.Join(",", e.Value)}}}")) + ".\n";
        text += "Reward mix per face a run (fragments/keys/chest keys/boosts/equipment/Jumbles* chests, of which bonus): " +
                string.Join("; ", CubeModel.ScienceThemes.Where(perTheme.ContainsKey).Select(t =>
                {
                    int[] m = perTheme[t];
                    return $"{t} {Avg(m[4])}/{Avg(m[3])}/{Avg(m[5])}/{Avg(m[2])}/{Avg(m[1])}/{Avg(m[0])} ({Avg(m[6])})";
                })) + ".\n";
        return text;
    }
}
