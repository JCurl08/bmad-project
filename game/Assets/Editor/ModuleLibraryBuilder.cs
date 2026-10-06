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
/// Each gate slot sits in the opening of an alcove: a pocket about 2x2 walled on three sides, open
/// toward the screen's horizontal centre lane. Alcoves follow ScreenModule.KeepsExitsOpen, so gates
/// only ever close the alcove, never a screen exit.
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

        /// <summary>Gate positions (the alcove openings) at (centre x, +-AlcoveOpeningY); the alcove extends away from the centre lane.</summary>
        public Vector2[] Gates;
        public Vector2 HiddenItem;
        public Vector2[] CoreEntrances;
    }

    // Alcove geometry (module-local units). The opening (gate) sits at |y| = AlcoveOpeningY, just outside
    // the horizontal centre lane; the pocket runs outward to the edge band.
    private const float AlcoveOpeningY = 1.4f;
    private const float AlcoveInnerWidth = GateSlot.GateWidth;
    private const float AlcoveWall = GateSlot.GateThickness;
    private const float AlcoveOuterY = 3.45f; // just inside the edge band (screen half height - EdgeClearance = 3.5)

    private static readonly Design[] Designs =
    {
        new Design
        {
            Name = "A Pillars",
            Obstacles = new[]
            {
                new Block(-4f, 2.4f, 1.2f, 1.2f), new Block(2.6f, 2.4f, 1.2f, 1.2f),
                new Block(-4f, -2.4f, 1.2f, 1.2f), new Block(4f, -2.4f, 1.2f, 1.2f),
            },
            Gates = new[] { new Vector2(4.9f, AlcoveOpeningY) },
            HiddenItem = new Vector2(6f, -3f),
            CoreEntrances = new[] { new Vector2(-2.5f, -3.5f) },
        },
        new Design
        {
            Name = "B Bars",
            Obstacles = new[] { new Block(-4f, 2.4f, 4.5f, 1f), new Block(4f, -2.4f, 4.5f, 1f) },
            Gates = new[] { new Vector2(3.4f, AlcoveOpeningY), new Vector2(-3.8f, -AlcoveOpeningY) },
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
            Gates = new[] { new Vector2(5f, AlcoveOpeningY) },
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
            Gates = new[] { new Vector2(-4.9f, AlcoveOpeningY), new Vector2(-3.4f, -AlcoveOpeningY) },
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
            var obstacleRects = new List<Rect>();
            for (int i = 0; i < design.Obstacles.Length; i++)
            {
                Block block = design.Obstacles[i];
                Vector2 centre = Vector2.Scale(block.Centre, mirror);
                var local = new Rect(centre - block.Size / 2f, block.Size);
                if (!ScreenModule.KeepsExitsOpen(local))
                    throw new InvalidOperationException($"{name}: obstacle {i} at {centre} blocks an exit lane or edge");
                obstacleRects.Add(local);
                Transform obstacle = Child($"Obstacle {i}", obstacles, centre);
                obstacle.gameObject.AddComponent<BoxCollider2D>().size = block.Size;
                AddVisual(obstacle, block.Size, obstacleColor, -5, sprite, material);
            }

            // Alcoves: three walls around each gate slot, open toward the screen centre.
            Transform alcoves = Child("Alcoves", root.transform, Vector2.zero);
            var alcoveRects = new List<Rect>();
            for (int i = 0; i < design.Gates.Length; i++)
            {
                Vector2 gate = Vector2.Scale(design.Gates[i], mirror);
                Transform alcove = Child($"Alcove {i}", alcoves, Vector2.zero);
                Rect footprint = AlcoveFootprint(gate);
                foreach (Rect a in alcoveRects)
                    if (a.Overlaps(footprint)) throw new InvalidOperationException($"{name}: alcove {i} overlaps another alcove");
                foreach (Rect o in obstacleRects)
                    if (o.Overlaps(footprint)) throw new InvalidOperationException($"{name}: alcove {i} overlaps an obstacle");
                alcoveRects.Add(footprint);
                Rect[] walls = AlcoveWalls(gate);
                for (int w = 0; w < walls.Length; w++)
                {
                    Rect wall = walls[w];
                    if (!ScreenModule.KeepsExitsOpen(wall))
                        throw new InvalidOperationException($"{name}: alcove {i} wall {w} blocks an exit lane or edge");
                    Transform piece = Child($"Wall {w}", alcove, wall.center);
                    piece.gameObject.AddComponent<BoxCollider2D>().size = wall.size;
                    AddVisual(piece, wall.size, obstacleColor, -5, sprite, material);
                }
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
            {
                Vector2 gate = Vector2.Scale(design.Gates[i], mirror);
                var slot = Child($"Gate {i}", slots, gate).gameObject.AddComponent<GateSlot>();
                float outward = Mathf.Sign(gate.y);
                slot.Opening = outward > 0f ? Facing.South : Facing.North;
                slot.PocketOffset = new Vector2(0f, outward * (PocketCentreY(gate) - Mathf.Abs(gate.y)));
            }
            Vector2 hidden = Vector2.Scale(design.HiddenItem, mirror);
            CheckOutsideAlcoves(name, "hidden item", hidden, alcoveRects);
            Child("Hidden Item", slots, hidden).gameObject.AddComponent<HiddenItemSlot>();
            if (withCore)
            {
                for (int i = 0; i < design.CoreEntrances.Length; i++)
                {
                    Vector2 core = Vector2.Scale(design.CoreEntrances[i], mirror);
                    CheckOutsideAlcoves(name, $"core entrance {i}", core, alcoveRects);
                    Child($"Core Entrance {i}", slots, core).gameObject.AddComponent<CoreEntranceSlot>();
                }
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

    /// <summary>|y| of the pocket centre: midway between the inside of the gate and the back wall.</summary>
    private static float PocketCentreY(Vector2 gate)
    {
        float inner = Mathf.Abs(gate.y) + AlcoveWall / 2f;
        float back = AlcoveOuterY - AlcoveWall;
        return (inner + back) / 2f;
    }

    /// <summary>Whole alcove (walls and opening), local rect.</summary>
    private static Rect AlcoveFootprint(Vector2 gate)
    {
        float halfOuter = AlcoveInnerWidth / 2f + AlcoveWall;
        float near = Mathf.Abs(gate.y) - AlcoveWall / 2f;
        return Signed(gate, gate.x - halfOuter, gate.x + halfOuter, near, AlcoveOuterY);
    }

    /// <summary>The two side walls and the back wall of an alcove, local rects.</summary>
    private static Rect[] AlcoveWalls(Vector2 gate)
    {
        float halfInner = AlcoveInnerWidth / 2f;
        float near = Mathf.Abs(gate.y) - AlcoveWall / 2f;
        return new[]
        {
            Signed(gate, gate.x - halfInner - AlcoveWall, gate.x - halfInner, near, AlcoveOuterY),
            Signed(gate, gate.x + halfInner, gate.x + halfInner + AlcoveWall, near, AlcoveOuterY),
            Signed(gate, gate.x - halfInner, gate.x + halfInner, AlcoveOuterY - AlcoveWall, AlcoveOuterY),
        };
    }

    /// <summary>A rect from x range and |y| range, on the same side of the centre lane as the gate.</summary>
    private static Rect Signed(Vector2 gate, float xMin, float xMax, float absYMin, float absYMax)
    {
        float yMin = gate.y >= 0f ? absYMin : -absYMax;
        float yMax = gate.y >= 0f ? absYMax : -absYMin;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static void CheckOutsideAlcoves(string module, string what, Vector2 point, List<Rect> alcoves)
    {
        foreach (Rect a in alcoves)
            if (a.Contains(point)) throw new InvalidOperationException($"{module}: {what} at {point} is inside an alcove");
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
