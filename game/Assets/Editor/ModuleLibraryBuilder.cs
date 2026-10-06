using System;
using System.Collections.Generic;
using Game.Cube;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cube > Build Module Library: generates the placeholder screen modules as prefabs from code (the
/// 4 fixed Town modules, and a pool of 4 layouts with interior obstacles for each science theme) and
/// the ModuleLibrary asset that lists them. Every module has four open side exits, 1-2 gate slots and
/// 1 hidden-item slot; two layouts per science theme carry core-entrance slots (one has two).
/// Also works from the command line via -executeMethod ModuleLibraryBuilder.Build.
/// </summary>
public static class ModuleLibraryBuilder
{
    public const string Root = "Assets/Modules";
    public const string LibraryPath = Root + "/ModuleLibrary.asset";

    private const string UnlitSpriteMaterialPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    /// <summary>Science themes that get a placeholder pool (all of them; sealed ones are never laid out).</summary>
    private static readonly Theme[] ScienceThemes =
        { Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };

    private struct Block
    {
        public Vector2 Centre, Size;
        public Block(float x, float y, float w, float h)
        {
            Centre = new Vector2(x, y);
            Size = new Vector2(w, h);
        }
    }

    /// <summary>One hand-placed layout, in local coordinates with the screen centre at the origin (screen is 16x10).</summary>
    private sealed class Design
    {
        public string Name;
        public Block[] Obstacles;
        public Vector2[] Gates;
        public Vector2 HiddenItem;
        public Vector2[] CoreEntrances;
    }

    private static readonly Design[] Designs =
    {
        new Design
        {
            Name = "A Pillars",
            Obstacles = new[]
            {
                new Block(-4f, 2.4f, 1.2f, 1.2f), new Block(4f, 2.4f, 1.2f, 1.2f),
                new Block(-4f, -2.4f, 1.2f, 1.2f), new Block(4f, -2.4f, 1.2f, 1.2f),
            },
            Gates = new[] { new Vector2(5f, 0f) },
            HiddenItem = new Vector2(6f, -3f),
            CoreEntrances = new[] { new Vector2(-2.5f, -3.5f) },
        },
        new Design
        {
            Name = "B Bars",
            Obstacles = new[] { new Block(-4f, 2.4f, 4.5f, 1f), new Block(4f, -2.4f, 4.5f, 1f) },
            Gates = new[] { new Vector2(0f, 3f), new Vector2(-5f, 0f) },
            HiddenItem = new Vector2(6f, 3f),
            CoreEntrances = Array.Empty<Vector2>(),
        },
        new Design
        {
            Name = "C Corners",
            Obstacles = new[]
            {
                new Block(-5.75f, 2.4f, 1f, 2f), new Block(-4.75f, 3f, 1f, 0.8f),
                new Block(5.75f, -2.4f, 1f, 2f), new Block(4.75f, -3f, 1f, 0.8f),
            },
            Gates = new[] { new Vector2(0f, -3f) },
            HiddenItem = new Vector2(-4.75f, 2f),
            CoreEntrances = new[] { new Vector2(3f, 2.5f), new Vector2(-3f, -2.5f) },
        },
        new Design
        {
            Name = "D Scatter",
            Obstacles = new[]
            {
                new Block(-2.5f, 2f, 1f, 1f), new Block(-5.5f, -2.8f, 1.4f, 0.8f), new Block(3f, 2.8f, 2f, 0.8f),
                new Block(5.5f, -2f, 0.8f, 1.2f), new Block(2.5f, -3f, 1f, 0.8f),
            },
            Gates = new[] { new Vector2(3.5f, 0f), new Vector2(0f, 3f) },
            HiddenItem = new Vector2(-2.5f, 3f),
            CoreEntrances = Array.Empty<Vector2>(),
        },
    };

    [MenuItem("Cube/Build Module Library")]
    public static void Build()
    {
        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);

        // Town: one fixed module per cell (row-major), ruins in every face's style, no core entrance.
        var town = new List<ScreenModule>();
        for (int i = 0; i < Designs.Length; i++)
        {
            Vector2 mirror = MirrorFor(Theme.Town, i);
            town.Add(SaveModule(Theme.Town, $"Town {i} {Designs[i].Name.Substring(2)}", Designs[i], mirror, false,
                sprite, material));
        }

        var pools = new List<ModuleLibrary.ThemePool>();
        foreach (Theme theme in ScienceThemes)
        {
            var pool = new ModuleLibrary.ThemePool { Theme = theme };
            for (int i = 0; i < Designs.Length; i++)
                pool.Modules.Add(SaveModule(theme, $"{theme} {Designs[i].Name}", Designs[i], MirrorFor(theme, i), true,
                    sprite, material));
            pools.Add(pool);
        }

        var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<ModuleLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        library.Set(town, pools);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cube: built module library at {LibraryPath} ({town.Count} Town modules, {pools.Count} science pools of {Designs.Length}).");
    }

    /// <summary>Loads the library asset, building it first if it does not exist.</summary>
    public static ModuleLibrary LoadOrBuild()
    {
        var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
        if (library != null) return library;
        Build();
        return AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
    }

    /// <summary>Per-theme mirroring so pools look a little different; exits and lanes are symmetric, so the rules hold.</summary>
    private static Vector2 MirrorFor(Theme theme, int designIndex)
    {
        switch (theme)
        {
            case Theme.Town: return new Vector2(designIndex % 2 == 0 ? 1f : -1f, 1f);
            case Theme.Chemistry: return new Vector2(-1f, 1f);
            case Theme.Physics: return new Vector2(1f, -1f);
            case Theme.Math: return new Vector2(-1f, -1f);
            default: return Vector2.one;
        }
    }

    private static ScreenModule SaveModule(Theme theme, string name, Design design, Vector2 mirror, bool withCore,
        Sprite sprite, Material material)
    {
        var root = new GameObject(name);
        try
        {
            root.AddComponent<ScreenModule>().Theme = theme;
            Color themeColor = CubeWorld.ThemeColor(theme);
            Color obstacleColor = Color.Lerp(themeColor, Color.black, 0.55f);

            Transform obstacles = Child("Obstacles", root.transform, Vector2.zero);
            for (int i = 0; i < design.Obstacles.Length; i++)
            {
                Block block = design.Obstacles[i];
                Vector2 centre = Vector2.Scale(block.Centre, mirror);
                var local = new Rect(centre - block.Size / 2f, block.Size);
                if (!ScreenModule.KeepsExitsOpen(local))
                    throw new InvalidOperationException($"{name}: obstacle {i} at {centre} blocks an exit lane or edge");
                Transform obstacle = Child($"Obstacle {i}", obstacles, centre);
                obstacle.gameObject.AddComponent<BoxCollider2D>().size = block.Size;
                AddVisual(obstacle, block.Size, obstacleColor, -5, sprite, material);
            }

            Transform slots = Child("Slots", root.transform, Vector2.zero);
            Vector2 half = CubeWorld.ScreenSize / 2f;
            foreach (Facing facing in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
            {
                Vector2 dir = facing.ToVector();
                Vector2 at = new Vector2(dir.x * (half.x - 0.5f), dir.y * (half.y - 0.5f));
                Child($"Exit {facing}", slots, at).gameObject.AddComponent<ExitSlot>().Facing = facing;
            }
            for (int i = 0; i < design.Gates.Length; i++)
                Child($"Gate {i}", slots, Vector2.Scale(design.Gates[i], mirror)).gameObject.AddComponent<GateSlot>();
            Child("Hidden Item", slots, Vector2.Scale(design.HiddenItem, mirror)).gameObject.AddComponent<HiddenItemSlot>();
            if (withCore)
            {
                for (int i = 0; i < design.CoreEntrances.Length; i++)
                    Child($"Core Entrance {i}", slots, Vector2.Scale(design.CoreEntrances[i], mirror))
                        .gameObject.AddComponent<CoreEntranceSlot>();
            }

            string folder = EnsureFolder(theme.ToString());
            string path = $"{folder}/{name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || saved == null) throw new InvalidOperationException($"Failed to save {path}");
            return saved.GetComponent<ScreenModule>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Transform Child(string name, Transform parent, Vector2 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    private static void AddVisual(Transform parent, Vector2 size, Color color, int sortingOrder, Sprite sprite,
        Material material)
    {
        var visual = new GameObject("Visual");
        visual.transform.SetParent(parent, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        if (sprite != null)
        {
            renderer.sprite = sprite;
            Vector2 spriteSize = sprite.bounds.size;
            visual.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        }
        if (material != null) renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
    }

    private static string EnsureFolder(string sub)
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "Modules");
        string folder = $"{Root}/{sub}";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, sub);
        return folder;
    }
}
