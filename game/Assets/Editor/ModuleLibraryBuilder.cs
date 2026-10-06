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
/// Town modules are dressed as mixed-style ruins: their pillars are tinted in the science faces' colours
/// (a different face per pillar and per module), and a "Ruins" group adds one visual-only piece per science
/// face (pillar, arch, round stone, diamond tile or broken column, a mismatched shape per module) in that
/// face's colour. Ruin pieces have no colliders and are placed where KeepsExitsOpen holds, clear of walls,
/// alcoves and the hidden-item slot.
/// Walls (pillars, bars and alcove walls) are the ArtCatalog's stone-wall tile, repeated over their size and softly tinted
/// with the face's colour; each ruin piece is its shape's ruin art (see Assets/Art/ART-MAP.md).
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
        ArtCatalog.Use(ArtCatalogBuilder.LoadOrBuild()); // walls and ruins come from the art catalog
        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);

        // Town: one fixed module per cell (row-major), ruins in every face's style, no core entrance.
        var town = new List<ScreenModule>();
        for (int i = 0; i < Designs.Length; i++)
        {
            Vector2 mirror = MirrorFor(Theme.Town, i);
            town.Add(SaveModule(Theme.Town, $"Town {i} {Designs[i].Name.Substring(2)}", Designs[i], mirror, false,
                sprite, material, i, round));
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
        Sprite sprite, Material material, int townIndex = -1, Sprite round = null)
    {
        bool town = theme == Theme.Town;
        var root = new GameObject(name);
        try
        {
            root.AddComponent<ScreenModule>().Theme = theme;
            Color themeColor = CubeWorld.ThemeColor(theme);
            // Walls are the stone-wall art with a soft tint of the face's colour.
            Color obstacleColor = Color.Lerp(Color.white, themeColor, 0.3f);

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
                // Town pillars are ruins of every face: each one in a different science face's colour.
                Color color = town
                    ? Color.Lerp(Color.white, CubeWorld.ThemeColor(ScienceThemes[(i + townIndex) % ScienceThemes.Length]), 0.45f)
                    : obstacleColor;
                AddVisual(obstacle, block.Size, color, -5, sprite, material, ArtKey.Wall);
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
                    AddVisual(piece, wall.size, obstacleColor, -5, sprite, material, ArtKey.Wall);
                }
            }

            if (town)
                AddRuins(name, root.transform, townIndex, obstacleRects, alcoveRects, Vector2.Scale(design.HiddenItem, mirror),
                    sprite, round, material);

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

    /// <summary>Shapes of the visual-only ruin pieces in town.</summary>
    private enum RuinShape { Pillar = 0, Arch = 1, Stone = 2, Tile = 3, Broken = 4 }

    /// <summary>Bounding size of each ruin shape (module-local units).</summary>
    private static Vector2 RuinSize(RuinShape shape)
    {
        switch (shape)
        {
            case RuinShape.Pillar: return new Vector2(0.5f, 1.3f);
            case RuinShape.Arch: return new Vector2(1.6f, 1.55f); // posts reach -0.75, the tilted lintel ~0.77
            case RuinShape.Stone: return new Vector2(0.9f, 0.9f);
            case RuinShape.Tile: return new Vector2(1.1f, 1.1f);
            default: return new Vector2(1.6f, 1.0f);
        }
    }

    /// <summary>Gap kept between a ruin piece and walls, alcoves, other pieces and the hidden-item slot.</summary>
    private const float RuinGap = 0.15f;

    /// <summary>
    /// One ruin piece per science face, in that face's colour, with the shape rotating per module so
    /// neighbouring screens never match. Each piece takes the first free spot of a fixed scan that starts in
    /// a different quadrant per piece. Visual only (no colliders), and still kept where KeepsExitsOpen holds.
    /// </summary>
    private static void AddRuins(string module, Transform root, int townIndex, List<Rect> obstacles, List<Rect> alcoves,
        Vector2 hidden, Sprite square, Sprite round, Material material)
    {
        Transform ruins = Child("Ruins", root, Vector2.zero);
        var placed = new List<Rect>();
        var hiddenRect = new Rect(hidden - new Vector2(0.4f, 0.4f), new Vector2(0.8f, 0.8f));
        for (int k = 0; k < ScienceThemes.Length; k++)
        {
            Theme theme = ScienceThemes[k];
            var shape = (RuinShape)((k + townIndex) % ScienceThemes.Length);
            // Full size where it fits; crowded screens get a smaller (more broken) piece.
            Vector2 at = default, size = default;
            float scale = 0f;
            foreach (float s in new[] { 1f, 0.8f, 0.6f })
            {
                size = RuinSize(shape) * s;
                if (!TryRuinSpot(size, (k + townIndex) % 4, obstacles, alcoves, placed, hiddenRect, out at)) continue;
                scale = s;
                break;
            }
            if (scale <= 0f) throw new InvalidOperationException($"{module}: no room for the {theme} ruin ({shape})");
            placed.Add(new Rect(at - size / 2f, size));
            Color color = Color.Lerp(Color.white, CubeWorld.ThemeColor(theme), 0.55f);
            Transform piece = Child($"Ruin {theme} {shape}", ruins, at);
            piece.localScale = new Vector3(scale, scale, 1f);
            BuildRuinPiece(piece, shape, color, square, round != null ? round : square, material);
        }
    }

    private static bool TryRuinSpot(Vector2 size, int quadrant, List<Rect> obstacles, List<Rect> alcoves,
        List<Rect> placed, Rect hidden, out Vector2 at)
    {
        Vector2 half = CubeWorld.ScreenSize / 2f;
        const float step = 0.25f;
        int nx = Mathf.FloorToInt(half.x / step), ny = Mathf.FloorToInt(half.y / step);
        for (int q = 0; q < 4; q++)
        {
            int quad = (quadrant + q) % 4;
            float sx = quad == 0 || quad == 3 ? 1f : -1f;
            float sy = quad < 2 ? 1f : -1f;
            // Scan from the outer corner of the quadrant inward.
            for (int iy = ny; iy >= 0; iy--)
            {
                for (int ix = nx; ix >= 0; ix--)
                {
                    var centre = new Vector2(sx * ix * step, sy * iy * step);
                    var rect = new Rect(centre - size / 2f, size);
                    if (!ScreenModule.KeepsExitsOpen(rect)) continue;
                    Rect padded = Grow(rect, RuinGap);
                    if (padded.Overlaps(hidden) || OverlapsAny(padded, obstacles) || OverlapsAny(padded, alcoves) ||
                        OverlapsAny(padded, placed)) continue;
                    at = centre;
                    return true;
                }
            }
        }
        at = default;
        return false;
    }

    /// <summary>The art of each ruin shape (see Assets/Art/ART-MAP.md).</summary>
    private static ArtKey RuinArt(RuinShape shape)
    {
        switch (shape)
        {
            case RuinShape.Pillar: return ArtKey.RuinPillar;
            case RuinShape.Arch: return ArtKey.RuinArch;
            case RuinShape.Stone: return ArtKey.RuinStone;
            case RuinShape.Tile: return ArtKey.RuinTile;
            default: return ArtKey.RuinBroken;
        }
    }

    /// <summary>
    /// One ruin piece: its shape's art in the face's colour, the largest square that fits inside the shape's bounding box
    /// (RuinSize, which the placement keeps clear), so the pixel art is never stretched. Without art, the old multi-part placeholder.
    /// </summary>
    private static void BuildRuinPiece(Transform piece, RuinShape shape, Color color, Sprite square, Sprite round,
        Material material)
    {
        const int order = -7; // above the floor (-10), below walls (-5) and NPCs
        ArtKey art = RuinArt(shape);
        if (ArtCatalog.TryGet(art, out _))
        {
            Vector2 bounds = RuinSize(shape);
            float side = Mathf.Min(bounds.x, bounds.y);
            Transform visual = Child("Art", piece, Vector2.zero);
            var renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            ArtCatalog.Apply(renderer, art, new Vector2(side, side));
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
            return;
        }
        BuildPlaceholderRuin(piece, shape, color, square, round, material);
    }

    private static void BuildPlaceholderRuin(Transform piece, RuinShape shape, Color color, Sprite square, Sprite round,
        Material material)
    {
        const int order = -7; // above the floor (-10), below walls (-5) and NPCs
        Color dark = Color.Lerp(color, Color.black, 0.3f);
        switch (shape)
        {
            case RuinShape.Pillar:
                AddPiece(piece, "Shaft", Vector2.zero, new Vector2(0.4f, 1.3f), 0f, color, order, square, material);
                AddPiece(piece, "Capital", new Vector2(0f, 0.55f), new Vector2(0.5f, 0.2f), 0f, dark, order + 1, square, material);
                break;
            case RuinShape.Arch:
                AddPiece(piece, "Post L", new Vector2(-0.65f, -0.1f), new Vector2(0.3f, 1.3f), 0f, color, order, square, material);
                AddPiece(piece, "Post R", new Vector2(0.65f, -0.25f), new Vector2(0.3f, 1.0f), 0f, color, order, square, material);
                AddPiece(piece, "Lintel", new Vector2(-0.1f, 0.55f), new Vector2(1.3f, 0.3f), -6f, dark, order + 1, square, material);
                break;
            case RuinShape.Stone:
                AddPiece(piece, "Stone", Vector2.zero, new Vector2(0.9f, 0.9f), 0f, color, order, round, material);
                AddPiece(piece, "Ring", Vector2.zero, new Vector2(0.45f, 0.45f), 0f, dark, order + 1, round, material);
                break;
            case RuinShape.Tile:
                AddPiece(piece, "Tile", Vector2.zero, new Vector2(0.75f, 0.75f), 45f, color, order, square, material);
                AddPiece(piece, "Inlay", Vector2.zero, new Vector2(0.35f, 0.35f), 45f, dark, order + 1, square, material);
                break;
            default:
                AddPiece(piece, "Stump", new Vector2(-0.5f, -0.15f), new Vector2(0.45f, 0.6f), 0f, color, order, square, material);
                AddPiece(piece, "Fallen", new Vector2(0.25f, -0.1f), new Vector2(1.0f, 0.35f), 15f, dark, order + 1, square, material);
                break;
        }
    }

    private static void AddPiece(Transform parent, string name, Vector2 at, Vector2 size, float angle, Color color,
        int sortingOrder, Sprite sprite, Material material)
    {
        Transform t = Child(name, parent, at);
        t.localRotation = Quaternion.Euler(0f, 0f, angle);
        AddVisual(t, size, color, sortingOrder, sprite, material);
    }

    private static Rect Grow(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

    private static bool OverlapsAny(Rect rect, List<Rect> others)
    {
        foreach (Rect o in others)
            if (o.Overlaps(rect)) return true;
        return false;
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

    /// <summary>A "Visual" child: the art of a key from the ArtCatalog (walls tile it), else the placeholder sprite scaled to size.</summary>
    private static void AddVisual(Transform parent, Vector2 size, Color color, int sortingOrder, Sprite sprite,
        Material material, ArtKey art = ArtKey.None)
    {
        var visual = new GameObject("Visual");
        visual.transform.SetParent(parent, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        if (art != ArtKey.None && ArtCatalog.TryGet(art, out _))
            ArtCatalog.Apply(renderer, art, size);
        else if (sprite != null)
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
