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
/// for Curie's (the player carries the Isotope it sets up for each run), and PhysicsFace for Einstein's (the player
/// carries the MassMitt it sets up for each run).
/// The run's outcome lives in a RunState (one per scene, reset on every rebuild). The CoreArena (Maxwell's Demon) puts a
/// portal on each built face's active core-entrance slot on every reveal and raises RunState's run-end event on victory or
/// defeat; a CoreHud shows its Order ↔ Entropy meter and the outcome.
/// The run loop: a RunWallet collects the run's meta currency (trials, the Demon, hidden items), and the RunLoop banks it
/// on every run end (death anywhere or the boss outcome), saves the MetaSave, opens the BetweenRunsScreen (earnings and
/// upgrade shop) and starts the next run on Continue.
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

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Vector2 screen = ScreenMath.DefaultScreenSize;
        Sprite squareSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);

        // World: generates the faces at runtime from the seed.
        var worldObject = new GameObject("Cube World");
        var world = worldObject.AddComponent<CubeWorld>();
        world.Configure(squareSprite, material, DefaultSeed, ModuleLibraryBuilder.LoadOrBuild(),
            ItemCatalogBuilder.LoadOrBuild());

        // Player at the centre of the Town start screen (face Front sits at the world origin).
        Vector2 start = ScreenMath.ScreenCenter(Vector2Int.zero);
        var player = new GameObject("Player");
        player.transform.position = start;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(player.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        if (roundSprite != null)
        {
            renderer.sprite = roundSprite;
            Vector2 spriteSize = roundSprite.bounds.size;
            visual.transform.localScale = new Vector3(0.8f / spriteSize.x, 0.8f / spriteSize.y, 1f);
        }
        if (material != null) renderer.sharedMaterial = material;
        renderer.color = new Color(1f, 0.85f, 0.3f);
        renderer.sortingOrder = 10;
        player.AddComponent<CircleCollider2D>().radius = 0.4f;
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        player.AddComponent<PlayerMover>();
        player.AddComponent<Inventory>();
        // Combat: stats (with the Health they feed), the equipped item, the melee attack and the HUD.
        var health = player.AddComponent<Health>();
        player.AddComponent<PlayerStats>();
        var equipment = player.AddComponent<Equipment>();
        player.AddComponent<PlayerAttack>();
        // The held Chemistry isotope's decay stage and its HUD line (set up for each run by ChemistryFace).
        player.AddComponent<Isotope>();
        // The Mass Mitt: grabs and drags Physics boulders (set up for each run by PhysicsFace).
        player.AddComponent<MassMitt>();
        var navigator = player.AddComponent<CubeNavigator>();
        navigator.World = world;

        // Face content: Darwin's face (beak gates, trial, finches and enemies) plugs in on the reveal.
        worldObject.AddComponent<BiologyFace>().Player = player.transform;
        // Curie's face (isotope dispenser, stage gates, trial, mushrooms and enemies) plugs in the same way.
        worldObject.AddComponent<ChemistryFace>().Player = player.transform;
        // Einstein's face (timed doors, boulders, trial, aliens, Newton and enemies) plugs in the same way.
        worldObject.AddComponent<PhysicsFace>().Player = player.transform;

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

        // Town: one NPC of each face race plus a few Townsfolk, respawned on every run start.
        var town = new GameObject("Town Population").AddComponent<TownPopulation>();
        town.World = world;
        town.Player = player.transform;

        var hud = new GameObject("Combat HUD").AddComponent<CombatHud>();
        hud.Health = health;
        hud.Equipment = equipment;

        // The run's outcome (one per scene) and the core arena: portals on every built face's core slot lead to
        // Maxwell's Demon; victory or defeat ends the run through the RunState.
        var runState = new GameObject("Run State").AddComponent<RunState>();
        runState.World = world;
        var arena = new GameObject("Core Arena").AddComponent<CoreArena>();
        arena.Configure(world, navigator, runState);
        debug.CoreArena = arena;
        new GameObject("Core HUD").AddComponent<CoreHud>().Arena = arena;

        // The roguelite loop: this run's earnings, the meta save and upgrades, and the between-runs screen.
        var wallet = new GameObject("Run Wallet").AddComponent<RunWallet>();
        wallet.Configure(world, arena, runState);
        var loop = new GameObject("Run Loop").AddComponent<RunLoop>();
        loop.Configure(world, runState, arena, wallet, player.GetComponent<PlayerStats>());
        new GameObject("Between Runs Screen").AddComponent<BetweenRunsScreen>().Loop = loop;

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
                variantCounts: items.VariantCounts(), biologyVariantKinds: BiologyBeaks.VariantKinds(items));
        string report = SeedSweep.Describe(results);
        int thin = results.Count(r => r.Placement != null && r.Placement.RolledVariant(Theme.Biology) == (int)BeakKind.Thin);
        int withOptional = results.Count(r => r.Placement != null && r.Placement.OptionalGates.Count > 0);
        report += $"Beaks: {thin} thin, {results.Count - thin} thick; {withOptional} seeds with optional beak gates.\n";
        int glow = 0, unstable = 0, lead = 0, guarded = 0;
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Placement == null) continue;
            ChemistryPlan plan = ChemistryPlan.Create(new CubeModel(r.Seed), r.Placement);
            if (plan == null) continue;
            glow += plan.CountOf(IsotopeStage.Glow);
            unstable += plan.CountOf(IsotopeStage.Unstable);
            lead += plan.CountOf(IsotopeStage.Lead);
            if (plan.Dispenser.IsGuarded) guarded++;
        }
        report += $"Isotope gates: {glow} glow, {unstable} unstable, {lead} lead; " +
                  $"{guarded} seeds with the dispenser behind another item's gate.\n";
        var required = new int[PhysicsPlan.MaxRequiredCap + 1];
        int doors = 0, offHome = 0, boulders = 0, trials = 0;
        ModuleLibrary library = ModuleLibraryBuilder.LoadOrBuild();
        List<BeakKind> kinds = BiologyBeaks.VariantKinds(items);
        foreach (SeedSweep.SeedResult r in results)
        {
            if (r.Placement == null) continue;
            var model = new CubeModel(r.Seed);
            CubeLayout layout = CubeLayout.Generate(model, library);
            PhysicsPlan plan = PhysicsPlan.Create(model, r.Placement, SeedSweep.ModuleLookup(layout, library), kinds);
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
        if (results.All(r => r.Passed)) Debug.Log(report);
        else Debug.LogError(report);
    }
}
