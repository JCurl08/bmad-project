using Game.Tracer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tracer > Create Tracer Scene: builds Assets/Scenes/Tracer.unity from code (no hand-written YAML)
/// with two adjacent 16x10 screens, the player, the screen camera and the audio/save probes,
/// and makes it the only build scene.
/// </summary>
public static class TracerSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Tracer.unity";

    private const string GeneratedSpritePath = "Assets/Scenes/TracerGenerated/WhiteSquare.asset";
    private const string UnlitSpriteMaterialPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    private const float WallThickness = 1f;
    private const float DoorwayHeight = 3f;

    [MenuItem("Tracer/Create Tracer Scene")]
    public static void CreateTracerScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Vector2 screen = ScreenMath.DefaultScreenSize;
        Sprite squareSprite = LoadBuiltinSprite("UI/Skin/UISprite.psd");
        Sprite roundSprite = LoadBuiltinSprite("UI/Skin/Knob.psd");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);

        // Floors: one tinted rectangle per screen so the two screens are distinguishable.
        var world = new GameObject("World").transform;
        CreateBlock("Screen A Floor", world, squareSprite, material, ScreenMath.ScreenCenter(new Vector2Int(0, 0)),
            screen, new Color(0.18f, 0.32f, 0.20f), -10, collider: false);
        CreateBlock("Screen B Floor", world, squareSprite, material, ScreenMath.ScreenCenter(new Vector2Int(1, 0)),
            screen, new Color(0.20f, 0.22f, 0.36f), -10, collider: false);

        // Walls: a perimeter around both screens, plus a divider with a doorway between A and B.
        float totalWidth = screen.x * 2f;
        float h = screen.y;
        float t = WallThickness;
        var wallColor = new Color(0.55f, 0.45f, 0.35f);
        var walls = new GameObject("Walls").transform;
        walls.SetParent(world, false);
        CreateBlock("Wall Bottom", walls, squareSprite, material, new Vector2(totalWidth / 2f, t / 2f), new Vector2(totalWidth, t), wallColor, 0, true);
        CreateBlock("Wall Top", walls, squareSprite, material, new Vector2(totalWidth / 2f, h - t / 2f), new Vector2(totalWidth, t), wallColor, 0, true);
        CreateBlock("Wall Left", walls, squareSprite, material, new Vector2(t / 2f, h / 2f), new Vector2(t, h), wallColor, 0, true);
        CreateBlock("Wall Right", walls, squareSprite, material, new Vector2(totalWidth - t / 2f, h / 2f), new Vector2(t, h), wallColor, 0, true);

        float dividerX = screen.x;
        float segment = (h - DoorwayHeight) / 2f - t;
        CreateBlock("Divider Lower", walls, squareSprite, material, new Vector2(dividerX, t + segment / 2f), new Vector2(t, segment), wallColor, 0, true);
        CreateBlock("Divider Upper", walls, squareSprite, material, new Vector2(dividerX, h - t - segment / 2f), new Vector2(t, segment), wallColor, 0, true);

        // Player.
        GameObject player = CreateBlock("Player", null, roundSprite, material, new Vector2(4f, h / 2f), new Vector2(0.8f, 0.8f),
            new Color(1f, 0.85f, 0.3f), 10, collider: false);
        var playerCollider = player.AddComponent<CircleCollider2D>();
        playerCollider.radius = 0.4f;
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        player.AddComponent<PlayerMover>();

        // Letterbox camera: clears the whole window to black behind the main camera's viewport.
        var letterbox = new GameObject("Letterbox Camera").AddComponent<Camera>();
        letterbox.orthographic = true;
        letterbox.depth = -10;
        letterbox.cullingMask = 0;
        letterbox.clearFlags = CameraClearFlags.SolidColor;
        letterbox.backgroundColor = Color.black;

        // Main camera.
        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = cameraObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = screen.y / 2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.depth = 0;
        Vector2 startCentre = ScreenMath.ScreenCenter(new Vector2Int(0, 0));
        cameraObject.transform.position = new Vector3(startCentre.x, startCentre.y, -10f);
        cameraObject.AddComponent<AudioListener>();
        var screenCamera = cameraObject.AddComponent<ScreenCamera>();
        screenCamera.Target = player.transform;

        // Probes.
        var probes = new GameObject("Probes");
        probes.AddComponent<AudioSource>();
        probes.AddComponent<AudioUnlock>();
        probes.AddComponent<SaveProbe>();

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            EditorUtility.DisplayDialog("Tracer", $"Failed to save {ScenePath}.", "OK");
            return;
        }

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"Tracer: created {ScenePath} and set it as the only build scene.");
    }

    private static GameObject CreateBlock(string name, Transform parent, Sprite sprite, Material material,
        Vector2 centre, Vector2 size, Color color, int sortingOrder, bool collider)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = centre;

        // The visual is a scaled child so colliders on the parent keep world-unit sizes.
        // Simple draw mode: the built-in sprites are not Full Rect, so Sliced/Tiled would warn.
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        Vector2 spriteSize = sprite.bounds.size;
        visual.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);

        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (material != null) renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (collider)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
        }

        return go;
    }

    private static Sprite LoadBuiltinSprite(string path)
    {
        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        return sprite != null ? sprite : GetOrCreateGeneratedSprite();
    }

    /// <summary>Fallback placeholder: a white square generated in code and stored as an asset.</summary>
    private static Sprite GetOrCreateGeneratedSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedSpritePath);
        if (existing != null) return existing;

        string folder = System.IO.Path.GetDirectoryName(GeneratedSpritePath)?.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Scenes", "TracerGenerated");

        const int size = 16;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "WhiteSquare", filterMode = FilterMode.Point };
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        sprite.name = "WhiteSquareSprite";

        AssetDatabase.CreateAsset(texture, GeneratedSpritePath);
        AssetDatabase.AddObjectToAsset(sprite, GeneratedSpritePath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedSpritePath);
    }
}
