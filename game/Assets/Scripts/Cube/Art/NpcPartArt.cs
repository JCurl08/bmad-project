using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The NPC Lego parts, drawn in code as clean outlined pixel sprites (32 px per world unit, sized to the part).
    /// The race gives the silhouette (a finch's beak, a mushroom cap, alien antennae and big eyes, a polyhedron, a dinosaur
    /// snout, a townsperson's hat), and what the slot controls gives the variant: heads by HintKind, torsos by role
    /// (a merchant's bag, a quest-giver's sash, a smith's apron, a gossip's frill), legs by movement. Sprites are white
    /// with a light shade and a dark outline, so the part's colour tints them and the outline stays dark.
    /// </summary>
    public static class NpcPartArt
    {
        public const float PixelsPerUnit = 32f;

        private const byte Empty = 0, Fill = 1, Shade = 2, Dark = 3, Light = 4, Edge = 5;

        private static readonly Color32[] Palette =
        {
            new Color32(0, 0, 0, 0),
            new Color32(222, 222, 222, 255),
            new Color32(160, 160, 160, 255),
            new Color32(45, 40, 52, 255),
            new Color32(255, 255, 255, 255),
            new Color32(24, 20, 30, 255),
        };

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>The sprite of a part of a race (made once, then cached).</summary>
        public static Sprite SpriteFor(Race race, NpcPart part)
        {
            if (part == null) return ArtCatalog.Shape(PartShape.Square);
            race = RaceOf(part, race);
            string key = $"{race}/{part.Slot}/{part.Id}";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            int w = Mathf.Max(6, Mathf.RoundToInt(part.Size.x * PixelsPerUnit));
            int h = Mathf.Max(6, Mathf.RoundToInt(part.Size.y * PixelsPerUnit));
            Canvas canvas = Draw(race, part, w, h);
            Sprite sprite = canvas.ToSprite($"NPC {key}");
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// The race a part belongs to, from its id prefix (a part swapped onto another race's NPC keeps its own look);
        /// fallback when the id has no known prefix.
        /// </summary>
        public static Race RaceOf(NpcPart part, Race fallback)
        {
            string id = part != null ? part.Id ?? "" : "";
            if (id.StartsWith("finch-", StringComparison.Ordinal)) return Race.Finch;
            if (id.StartsWith("shroom-", StringComparison.Ordinal)) return Race.Mushroom;
            if (id.StartsWith("alien-", StringComparison.Ordinal)) return Race.Alien;
            if (id.StartsWith("shape-", StringComparison.Ordinal)) return Race.Shape;
            if (id.StartsWith("dino-", StringComparison.Ordinal)) return Race.Dinosaur;
            if (id.StartsWith("town-", StringComparison.Ordinal)) return Race.Townsfolk;
            return fallback;
        }

        /// <summary>The finished pixel grid of a part (tests read it: silhouette and outline).</summary>
        public static byte[] Pixels(Race race, NpcPart part, out int width, out int height)
        {
            race = RaceOf(part, race);
            width = Mathf.Max(6, Mathf.RoundToInt(part.Size.x * PixelsPerUnit));
            height = Mathf.Max(6, Mathf.RoundToInt(part.Size.y * PixelsPerUnit));
            return Draw(race, part, width, height).Cells;
        }

        /// <summary>True for a pixel value that is the dark outline.</summary>
        public static bool IsOutline(byte value) => value == Edge;

        private static Canvas Draw(Race race, NpcPart part, int w, int h)
        {
            var c = new Canvas(w, h);
            switch (part.Slot)
            {
                case PartSlot.Head: Head(c, race, part); break;
                case PartSlot.Torso: Torso(c, race, part); break;
                default: Legs(c, race, part); break;
            }
            c.Outline();
            return c;
        }

        // ---------------------------------------------------------------- heads

        private static void Head(Canvas c, Race race, NpcPart part)
        {
            int variant = (int)part.Hint;
            switch (race)
            {
                case Race.Finch:
                    c.Ellipse(0.42f, 0.5f, 0.3f, 0.38f, Fill);
                    c.Ellipse(0.36f, 0.42f, 0.16f, 0.16f, Light);
                    switch (variant)
                    {
                        case 0: c.Poly(Fill, 0.66f, 0.56f, 0.66f, 0.44f, 0.97f, 0.5f); break; // thin, long
                        case 1: c.Poly(Shade, 0.62f, 0.74f, 0.62f, 0.26f, 0.92f, 0.5f); break; // thick, blunt
                        case 2: // hooked
                            c.Poly(Fill, 0.64f, 0.68f, 0.64f, 0.4f, 0.9f, 0.62f);
                            c.Rect(0.84f, 0.38f, 0.92f, 0.62f, Fill);
                            break;
                        default: // crested
                            c.Poly(Fill, 0.66f, 0.58f, 0.66f, 0.42f, 0.86f, 0.5f);
                            c.Poly(Shade, 0.2f, 0.78f, 0.34f, 0.84f, 0.14f, 0.98f);
                            c.Poly(Shade, 0.34f, 0.84f, 0.48f, 0.86f, 0.36f, 0.99f);
                            break;
                    }
                    c.Dot(0.55f, 0.6f, Dark);
                    break;

                case Race.Mushroom:
                    switch (variant)
                    {
                        case 0: c.Ellipse(0.5f, 0.38f, 0.47f, 0.58f, Fill, minV: 0.38f); break; // round dome
                        case 1: c.RoundRect(0.04f, 0.38f, 0.96f, 0.82f, Fill); break; // flat shell cap
                        case 2: c.Poly(Fill, 0.04f, 0.38f, 0.96f, 0.38f, 0.5f, 0.97f); break; // pointy skull cap
                        default: c.Poly(Fill, 0.1f, 0.38f, 0.9f, 0.38f, 0.62f, 0.97f); break; // leaning cone
                    }
                    c.Rect(0.12f, 0.3f, 0.88f, 0.4f, Shade); // gills
                    c.Ellipse(0.32f, 0.6f, 0.08f, 0.1f, Light);
                    c.Ellipse(0.66f, 0.68f, 0.06f, 0.08f, Light);
                    c.Rect(0.32f, 0.02f, 0.68f, 0.3f, Fill); // face under the cap
                    c.Dot(0.42f, 0.18f, Dark);
                    c.Dot(0.58f, 0.18f, Dark);
                    if (variant == 2) c.Rect(0.4f, 0.04f, 0.6f, 0.08f, Dark); // grin
                    break;

                case Race.Alien:
                    if (variant == 1) c.Ellipse(0.5f, 0.5f, 0.38f, 0.42f, Fill); // big-brain dome
                    else c.Ellipse(0.5f, 0.38f, 0.3f, 0.34f, Fill);
                    if (variant == 1) c.Line(0.3f, 0.66f, 0.7f, 0.66f, Shade);
                    if (variant == 3) // wild hair
                    {
                        c.Poly(Light, 0.18f, 0.6f, 0.08f, 0.96f, 0.36f, 0.68f);
                        c.Poly(Light, 0.4f, 0.7f, 0.5f, 0.99f, 0.6f, 0.7f);
                        c.Poly(Light, 0.64f, 0.68f, 0.92f, 0.96f, 0.82f, 0.6f);
                    }
                    else // antennae with balls
                    {
                        c.Line(0.36f, 0.66f, 0.22f, 0.9f, Fill);
                        c.Line(0.64f, 0.66f, 0.78f, 0.9f, Fill);
                        c.Ellipse(0.2f, 0.9f, 0.07f, 0.09f, Light);
                        c.Ellipse(0.8f, 0.9f, 0.07f, 0.09f, Light);
                    }
                    if (variant == 2) // one big eye
                    {
                        c.Ellipse(0.5f, 0.4f, 0.13f, 0.16f, Light);
                        c.Ellipse(0.5f, 0.4f, 0.06f, 0.08f, Dark);
                    }
                    else
                    {
                        c.Ellipse(0.38f, 0.38f, 0.08f, 0.12f, Dark);
                        c.Ellipse(0.62f, 0.38f, 0.08f, 0.12f, Dark);
                    }
                    break;

                case Race.Shape:
                    switch (part.Shape)
                    {
                        case PartShape.Circle: c.Ellipse(0.5f, 0.5f, 0.38f, 0.45f, Fill); break;
                        case PartShape.Triangle: c.Poly(Fill, 0.1f, 0.06f, 0.9f, 0.06f, 0.5f, 0.96f); break;
                        case PartShape.Diamond: c.Poly(Fill, 0.5f, 0.04f, 0.92f, 0.5f, 0.5f, 0.96f, 0.08f, 0.5f); break;
                        default:
                            c.Rect(0.16f, 0.06f, 0.76f, 0.78f, Fill);
                            c.Poly(Shade, 0.16f, 0.78f, 0.76f, 0.78f, 0.88f, 0.94f, 0.28f, 0.94f); // top face
                            c.Poly(Shade, 0.76f, 0.06f, 0.88f, 0.22f, 0.88f, 0.94f, 0.76f, 0.78f); // side face
                            break;
                    }
                    c.Ellipse(0.42f, 0.62f, 0.07f, 0.06f, Light); // shine
                    c.Dot(0.4f, 0.4f, Dark);
                    c.Dot(0.6f, 0.4f, Dark);
                    break;

                case Race.Dinosaur:
                    c.Ellipse(0.3f, 0.5f, 0.24f, 0.34f, Fill);
                    if (variant == 1) c.Poly(Fill, 0.42f, 0.66f, 0.42f, 0.24f, 0.97f, 0.42f); // feathered beak
                    else if (variant == 3) c.Poly(Fill, 0.46f, 0.6f, 0.46f, 0.34f, 0.78f, 0.46f); // crow's short beak
                    else
                    {
                        c.RoundRect(0.36f, 0.14f, 0.96f, 0.6f, Fill); // long snout
                        c.Line(0.5f, 0.24f, 0.92f, 0.24f, Shade); // mouth
                        for (int t = 0; t < 4; t++) c.Dot(0.56f + t * 0.1f, 0.2f, Light); // teeth
                        c.Dot(0.9f, 0.5f, Dark); // nostril
                    }
                    if (variant == 0) // crest
                    {
                        c.Poly(Shade, 0.08f, 0.66f, 0.2f, 0.98f, 0.3f, 0.78f);
                        c.Poly(Shade, 0.26f, 0.8f, 0.4f, 0.98f, 0.46f, 0.74f);
                    }
                    else if (variant == 2) // horns
                    {
                        c.Poly(Light, 0.3f, 0.78f, 0.38f, 0.98f, 0.46f, 0.74f);
                        c.Poly(Light, 0.66f, 0.58f, 0.74f, 0.82f, 0.8f, 0.56f);
                    }
                    else if (variant == 3) // storm-crow tuft
                        c.Poly(Shade, 0.12f, 0.74f, 0.04f, 0.98f, 0.3f, 0.82f);
                    c.Dot(0.3f, 0.62f, Dark);
                    break;

                default: // Townsfolk: a round face and a hat
                    c.Ellipse(0.5f, 0.34f, 0.26f, 0.32f, Fill);
                    c.Dot(0.42f, 0.34f, Dark);
                    c.Dot(0.58f, 0.34f, Dark);
                    switch (variant)
                    {
                        case 0: // straw hat
                            c.Rect(0.04f, 0.58f, 0.96f, 0.68f, Light);
                            c.RoundRect(0.3f, 0.66f, 0.7f, 0.94f, Light);
                            c.Line(0.3f, 0.7f, 0.7f, 0.7f, Shade);
                            break;
                        case 1: // bucket helmet
                            c.Rect(0.22f, 0.4f, 0.78f, 0.96f, Shade);
                            c.Rect(0.3f, 0.5f, 0.7f, 0.58f, Dark);
                            break;
                        case 2: // ruin-stone mask
                            c.Poly(Shade, 0.5f, 0.02f, 0.82f, 0.4f, 0.5f, 0.96f, 0.18f, 0.4f);
                            c.Dot(0.4f, 0.44f, Dark);
                            c.Dot(0.6f, 0.44f, Dark);
                            break;
                        default: // patchwork bonnet
                            c.Ellipse(0.5f, 0.56f, 0.4f, 0.4f, Shade, minV: 0.4f);
                            c.Ellipse(0.5f, 0.34f, 0.22f, 0.26f, Fill);
                            c.Dot(0.42f, 0.34f, Dark);
                            c.Dot(0.58f, 0.34f, Dark);
                            c.Poly(Light, 0.74f, 0.2f, 0.92f, 0.1f, 0.92f, 0.3f);
                            break;
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- torsos

        private static void Torso(Canvas c, Race race, NpcPart part)
        {
            switch (race)
            {
                case Race.Finch:
                    c.Ellipse(0.5f, 0.48f, 0.42f, 0.46f, Fill);
                    c.Ellipse(0.62f, 0.4f, 0.22f, 0.28f, Light); // breast
                    c.Ellipse(0.3f, 0.52f, 0.2f, 0.26f, Shade); // wing
                    break;
                case Race.Mushroom:
                    c.RoundRect(0.24f, 0.02f, 0.76f, 0.9f, Fill); // stem
                    c.Ellipse(0.5f, 0.86f, 0.4f, 0.12f, Shade); // ring
                    break;
                case Race.Alien:
                    c.Poly(Fill, 0.18f, 0.02f, 0.82f, 0.02f, 0.7f, 0.94f, 0.3f, 0.94f); // suit
                    c.Rect(0.06f, 0.5f, 0.2f, 0.86f, Fill); // arms
                    c.Rect(0.8f, 0.5f, 0.94f, 0.86f, Fill);
                    c.Ellipse(0.5f, 0.7f, 0.08f, 0.1f, Light); // chest light
                    break;
                case Race.Shape:
                    string id = part.Id ?? "";
                    if (id.Contains("hexagon")) c.Poly(Fill, 0.28f, 0.04f, 0.72f, 0.04f, 0.94f, 0.5f, 0.72f, 0.96f, 0.28f, 0.96f, 0.06f, 0.5f);
                    else if (id.Contains("trapezoid")) c.Poly(Fill, 0.06f, 0.04f, 0.94f, 0.04f, 0.74f, 0.94f, 0.26f, 0.94f);
                    else if (id.Contains("kite")) c.Poly(Fill, 0.5f, 0.02f, 0.9f, 0.62f, 0.5f, 0.98f, 0.1f, 0.62f);
                    else
                    {
                        c.Rect(0.14f, 0.04f, 0.74f, 0.78f, Fill);
                        c.Poly(Shade, 0.14f, 0.78f, 0.74f, 0.78f, 0.88f, 0.96f, 0.28f, 0.96f);
                        c.Poly(Shade, 0.74f, 0.04f, 0.88f, 0.22f, 0.88f, 0.96f, 0.74f, 0.78f);
                    }
                    c.Line(0.3f, 0.3f, 0.5f, 0.6f, Light); // facet glint
                    break;
                case Race.Dinosaur:
                    c.Ellipse(0.5f, 0.46f, 0.44f, 0.44f, Fill);
                    c.Ellipse(0.58f, 0.38f, 0.24f, 0.26f, Light); // belly
                    if (part.Variant == DinoVariant.Avian)
                        c.Poly(Shade, 0.1f, 0.7f, 0.04f, 0.2f, 0.4f, 0.42f); // folded wing
                    else
                        for (int s = 0; s < 3; s++) // back plates
                            c.Poly(Shade, 0.1f + s * 0.18f, 0.8f, 0.2f + s * 0.18f, 0.99f, 0.3f + s * 0.18f, 0.84f);
                    break;
                default: // Townsfolk shirt with arms
                    c.Rect(0.24f, 0.02f, 0.76f, 0.84f, Fill);
                    c.Rect(0.06f, 0.42f, 0.24f, 0.84f, Fill);
                    c.Rect(0.76f, 0.42f, 0.94f, 0.84f, Fill);
                    c.Rect(0.42f, 0.84f, 0.58f, 0.96f, Light); // neck
                    break;
            }

            // The role's accessory.
            switch (part.Role)
            {
                case NpcRole.Merchant: // a bag on a strap
                    c.Line(0.3f, 0.86f, 0.7f, 0.3f, Dark);
                    c.RoundRect(0.62f, 0.08f, 0.86f, 0.34f, Shade);
                    break;
                case NpcRole.QuestGiver: // a sash with a scroll
                    c.Line(0.26f, 0.78f, 0.74f, 0.1f, Light);
                    c.Rect(0.18f, 0.18f, 0.36f, 0.3f, Light);
                    break;
                case NpcRole.Smith: // a sooty apron
                    c.Rect(0.36f, 0.04f, 0.64f, 0.56f, Dark);
                    c.Rect(0.42f, 0.24f, 0.58f, 0.3f, Shade);
                    break;
                default: // a gossip's frill
                    for (int z = 0; z < 4; z++) c.Poly(Light, 0.3f + z * 0.1f, 0.74f, 0.4f + z * 0.1f, 0.74f, 0.35f + z * 0.1f, 0.62f);
                    break;
            }
        }

        // ---------------------------------------------------------------- legs

        private static void Legs(Canvas c, Race race, NpcPart part)
        {
            Movement move = part.Movement;
            switch (race)
            {
                case Race.Finch:
                    c.Line(0.36f, 0.95f, 0.36f, 0.2f, Fill);
                    c.Line(0.64f, 0.95f, move == Movement.Wander ? 0.74f : 0.64f, 0.2f, Fill);
                    foreach (float x in new[] { 0.36f, move == Movement.Wander ? 0.74f : 0.64f })
                    {
                        c.Line(x, 0.18f, x - 0.14f, 0.06f, Fill);
                        c.Line(x, 0.18f, x + 0.14f, 0.06f, Fill);
                    }
                    if (move == Movement.Stand) c.Rect(0.1f, 0.04f, 0.9f, 0.12f, Shade); // a perch
                    if (move == Movement.Flee) // flutter feathers
                    {
                        c.Poly(Shade, 0.04f, 0.9f, 0.24f, 0.7f, 0.06f, 0.5f);
                        c.Poly(Shade, 0.96f, 0.9f, 0.76f, 0.7f, 0.94f, 0.5f);
                    }
                    break;
                case Race.Mushroom:
                    if (move == Movement.Flee) // skeleton legs
                    {
                        c.Line(0.36f, 0.96f, 0.36f, 0.12f, Light);
                        c.Line(0.64f, 0.96f, 0.64f, 0.12f, Light);
                        c.Ellipse(0.36f, 0.5f, 0.07f, 0.09f, Light);
                        c.Ellipse(0.64f, 0.5f, 0.07f, 0.09f, Light);
                        c.Rect(0.22f, 0.04f, 0.42f, 0.14f, Light);
                        c.Rect(0.58f, 0.04f, 0.78f, 0.14f, Light);
                    }
                    else
                    {
                        c.Poly(Fill, 0.28f, 0.98f, 0.72f, 0.98f, 0.84f, 0.12f, 0.16f, 0.12f); // stalk base
                        if (move == Movement.Stand) // roots
                        {
                            c.Line(0.2f, 0.12f, 0.06f, 0.02f, Shade);
                            c.Line(0.5f, 0.12f, 0.5f, 0.02f, Shade);
                            c.Line(0.8f, 0.12f, 0.94f, 0.02f, Shade);
                        }
                        else // shuffling feet
                        {
                            c.Ellipse(0.26f, 0.12f, 0.16f, 0.12f, Shade);
                            c.Ellipse(0.74f, 0.12f, 0.16f, 0.12f, Shade);
                        }
                    }
                    break;
                case Race.Alien:
                    if (move == Movement.Wander) // hover pad
                    {
                        c.Rect(0.4f, 0.4f, 0.6f, 0.98f, Fill);
                        c.Ellipse(0.5f, 0.3f, 0.44f, 0.2f, Fill);
                        c.Line(0.2f, 0.08f, 0.8f, 0.08f, Light);
                    }
                    else if (move == Movement.Flee) // tentacles
                        for (int t = 0; t < 4; t++)
                        {
                            float x = 0.2f + t * 0.2f;
                            c.Line(x, 0.96f, x + (t % 2 == 0 ? -0.08f : 0.08f), 0.5f, Fill);
                            c.Line(x + (t % 2 == 0 ? -0.08f : 0.08f), 0.5f, x, 0.08f, Fill);
                        }
                    else // mag-boots
                    {
                        c.Rect(0.28f, 0.3f, 0.4f, 0.98f, Fill);
                        c.Rect(0.6f, 0.3f, 0.72f, 0.98f, Fill);
                        c.RoundRect(0.12f, 0.02f, 0.44f, 0.32f, Shade);
                        c.RoundRect(0.56f, 0.02f, 0.88f, 0.32f, Shade);
                    }
                    break;
                case Race.Shape:
                    switch (part.Shape)
                    {
                        case PartShape.Triangle: c.Poly(Fill, 0.1f, 0.04f, 0.9f, 0.04f, 0.5f, 0.98f); break;
                        case PartShape.Diamond: c.Poly(Fill, 0.5f, 0.02f, 0.94f, 0.5f, 0.5f, 0.98f, 0.06f, 0.5f); break;
                        default:
                            c.Ellipse(0.5f, 0.46f, 0.3f, 0.44f, Fill);
                            c.Ellipse(0.5f, 0.46f, 0.1f, 0.14f, Shade); // wheel hub
                            break;
                    }
                    break;
                case Race.Dinosaur:
                    if (part.Variant == DinoVariant.Avian && move == Movement.Flee) // flapping wings
                    {
                        c.Poly(Fill, 0.48f, 0.96f, 0.02f, 0.7f, 0.12f, 0.2f);
                        c.Poly(Fill, 0.52f, 0.96f, 0.98f, 0.7f, 0.88f, 0.2f);
                        c.Line(0.1f, 0.5f, 0.4f, 0.8f, Shade);
                        c.Line(0.9f, 0.5f, 0.6f, 0.8f, Shade);
                    }
                    else if (part.Variant == DinoVariant.Avian) // talons
                    {
                        c.Line(0.34f, 0.96f, 0.34f, 0.18f, Fill);
                        c.Line(0.66f, 0.96f, 0.66f, 0.18f, Fill);
                        foreach (float x in new[] { 0.34f, 0.66f })
                        {
                            c.Line(x, 0.18f, x - 0.14f, 0.04f, Dark);
                            c.Line(x, 0.18f, x + 0.14f, 0.04f, Dark);
                        }
                    }
                    else // stompy legs
                    {
                        c.Rect(0.12f, 0.12f, 0.42f, 0.98f, Fill);
                        c.Rect(0.58f, 0.12f, 0.88f, 0.98f, Fill);
                        c.Rect(0.08f, 0.02f, 0.46f, 0.16f, Shade);
                        c.Rect(0.54f, 0.02f, 0.92f, 0.16f, Shade);
                        foreach (float x in new[] { 0.12f, 0.22f, 0.32f, 0.6f, 0.7f, 0.8f }) c.Dot(x, 0.04f, Light); // claws
                    }
                    break;
                default: // Townsfolk legs and shoes
                    c.Rect(0.26f, 0.2f, 0.44f, 0.98f, Fill);
                    c.Rect(0.56f, 0.2f, 0.74f, 0.98f, Fill);
                    switch (move)
                    {
                        case Movement.Stand: // clogs
                            c.RoundRect(0.1f, 0.02f, 0.46f, 0.26f, Shade);
                            c.RoundRect(0.54f, 0.02f, 0.9f, 0.26f, Shade);
                            break;
                        case Movement.Wander: // sandals
                            c.Rect(0.18f, 0.02f, 0.46f, 0.1f, Shade);
                            c.Rect(0.54f, 0.02f, 0.82f, 0.1f, Shade);
                            break;
                        default: // sneakers
                            c.RoundRect(0.14f, 0.02f, 0.48f, 0.24f, Light);
                            c.RoundRect(0.52f, 0.02f, 0.86f, 0.24f, Light);
                            c.Dot(0.32f, 0.16f, Dark);
                            c.Dot(0.7f, 0.16f, Dark);
                            break;
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- canvas

        /// <summary>A small pixel grid drawn in normalised coordinates (0..1, origin bottom left).</summary>
        private sealed class Canvas
        {
            public readonly int W, H;
            public readonly byte[] Cells;

            public Canvas(int w, int h)
            {
                W = w;
                H = h;
                Cells = new byte[w * h];
            }

            private void Each(Func<float, float, bool> inside, byte value)
            {
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                        if (inside(u, v)) Cells[y * W + x] = value;
                    }
                }
            }

            public void Ellipse(float cx, float cy, float rx, float ry, byte value, float minV = float.NegativeInfinity) =>
                Each((u, v) =>
                {
                    float a = (u - cx) / rx, b = (v - cy) / ry;
                    return v >= minV && a * a + b * b <= 1f;
                }, value);

            public void Rect(float x0, float y0, float x1, float y1, byte value) =>
                Each((u, v) => u >= x0 && u <= x1 && v >= y0 && v <= y1, value);

            /// <summary>A rectangle with its corner pixels knocked off.</summary>
            public void RoundRect(float x0, float y0, float x1, float y1, byte value)
            {
                float px = 1.2f / W, py = 1.2f / H;
                Each((u, v) =>
                {
                    if (u < x0 || u > x1 || v < y0 || v > y1) return false;
                    bool nearX = u < x0 + px || u > x1 - px, nearY = v < y0 + py || v > y1 - py;
                    return !(nearX && nearY);
                }, value);
            }

            /// <summary>A filled polygon from x,y pairs.</summary>
            public void Poly(byte value, params float[] xy)
            {
                int n = xy.Length / 2;
                Each((u, v) =>
                {
                    bool inside = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float xi = xy[2 * i], yi = xy[2 * i + 1], xj = xy[2 * j], yj = xy[2 * j + 1];
                        if ((yi > v) != (yj > v) && u < (xj - xi) * (v - yi) / (yj - yi) + xi) inside = !inside;
                    }
                    return inside;
                }, value);
            }

            /// <summary>A line about one pixel thick.</summary>
            public void Line(float x0, float y0, float x1, float y1, byte value)
            {
                Vector2 a = new Vector2(x0 * W, y0 * H), b = new Vector2(x1 * W, y1 * H);
                Vector2 ab = b - a;
                float len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                        if ((a + ab * t - p).sqrMagnitude <= 0.36f) Cells[y * W + x] = value;
                    }
                }
            }

            /// <summary>One pixel.</summary>
            public void Dot(float u, float v, byte value)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(u * W), 0, W - 1), y = Mathf.Clamp(Mathf.FloorToInt(v * H), 0, H - 1);
                Cells[y * W + x] = value;
            }

            /// <summary>
            /// Rings the silhouette with a dark outline: every empty pixel touching a drawn one, and drawn pixels on the
            /// canvas border (so a silhouette that touches the edge is still closed).
            /// </summary>
            public void Outline()
            {
                var outline = new bool[Cells.Length];
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        byte cell = Cells[y * W + x];
                        if (cell != Empty)
                        {
                            if (x == 0 || y == 0 || x == W - 1 || y == H - 1) outline[y * W + x] = true;
                            continue;
                        }
                        if ((x > 0 && Cells[y * W + x - 1] != Empty) || (x < W - 1 && Cells[y * W + x + 1] != Empty) ||
                            (y > 0 && Cells[(y - 1) * W + x] != Empty) || (y < H - 1 && Cells[(y + 1) * W + x] != Empty))
                            outline[y * W + x] = true;
                    }
                }
                for (int i = 0; i < Cells.Length; i++)
                    if (outline[i]) Cells[i] = Edge;
            }

            public Sprite ToSprite(string name)
            {
                var texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    name = name,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var pixels = new Color32[Cells.Length];
                for (int i = 0; i < Cells.Length; i++) pixels[i] = Palette[Cells[i]];
                texture.SetPixels32(pixels);
                texture.Apply();
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), PixelsPerUnit);
                sprite.name = name;
                return sprite;
            }
        }
    }
}
