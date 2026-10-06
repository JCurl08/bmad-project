using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>One NPC's chosen parts plus the salt that picks its hint subject and lines. Pure data.</summary>
    public sealed class NpcSpec
    {
        public int Seed { get; }
        public Race Race { get; }
        public int Index { get; }
        public NpcPart Head { get; }
        public NpcPart Torso { get; }
        public NpcPart Legs { get; }

        /// <summary>Seeded value that picks the hint subject, the line variants and the wander path.</summary>
        public uint Salt { get; }

        public NpcSpec(int seed, Race race, int index, NpcPart head, NpcPart torso, NpcPart legs, uint salt)
        {
            Seed = seed;
            Race = race;
            Index = index;
            Head = head;
            Torso = torso;
            Legs = legs;
            Salt = salt;
        }

        public HintKind HintKind => Head.Hint;
        public NpcRole Role => Torso.Role;
        public Movement Movement => Legs.Movement;

        /// <summary>Dinosaurs take their variant from the head (then torso); other races have none.</summary>
        public DinoVariant Variant => Head.Variant != DinoVariant.None ? Head.Variant : Torso.Variant;

        public string DisplayName => $"{RaceName(Race, Variant)} {RoleName(Role)}";

        /// <summary>The same NPC with one part swapped (tests and later part-swapping mechanics).</summary>
        public NpcSpec With(NpcPart part)
        {
            switch (part.Slot)
            {
                case PartSlot.Head: return new NpcSpec(Seed, Race, Index, part, Torso, Legs, Salt);
                case PartSlot.Torso: return new NpcSpec(Seed, Race, Index, Head, part, Legs, Salt);
                default: return new NpcSpec(Seed, Race, Index, Head, Torso, part, Salt);
            }
        }

        /// <summary>Part ids joined, for comparisons and logs.</summary>
        public string Signature() => $"{Race}:{Head.Id}/{Torso.Id}/{Legs.Id}#{Salt}";

        public override string ToString() => $"{DisplayName} ({Signature()})";

        public static string RaceName(Race race, DinoVariant variant)
        {
            switch (race)
            {
                case Race.Mushroom: return "Mushroom";
                case Race.Dinosaur: return variant == DinoVariant.Avian ? "Avian Dinosaur" : "Terrestrial Dinosaur";
                case Race.Townsfolk: return "Townsperson";
                default: return race.ToString();
            }
        }

        public static string RoleName(NpcRole role) => role == NpcRole.QuestGiver ? "Quest-Giver" : role.ToString();
    }

    /// <summary>
    /// Builds NPCs. Part choice is pure: (seed, race, index) seeds its own PCG32 stream, so the same seed
    /// always gives the same NPCs and choosing one never disturbs another. Spawn builds the runtime object:
    /// three stacked outlined part sprites (legs, torso, head; drawn per race by NpcPartArt), a collider, an NpcTalker, an NpcMover and the
    /// dormant enemy parts (HostileNpc) that wake while its race is hostile.
    /// Placing NPCs in the world is up to the town and face code (and the F3 debug spawn).
    /// </summary>
    public static class NpcFactory
    {
        /// <summary>PCG32 stream for NPC parts (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.NpcParts;

        public const int SortingOrder = 6;

        /// <summary>Collider width; the height is the stacked parts' height (at most MaxBodyHeight).</summary>
        public const float BodyWidth = 0.6f;
        public const float MaxBodyHeight = 1.4f;
        public const float BodyMass = 5f;

        /// <summary>
        /// True if an NPC standing with its feet at this point would overlap no solid collider (walls,
        /// closed gates, the player, other NPCs). Triggers are ignored. Used to pick spawn spots.
        /// </summary>
        public static bool IsClear(Vector2 feet, float margin = 0.1f)
        {
            Physics2D.SyncTransforms();
            var size = new Vector2(BodyWidth + 2f * margin, MaxBodyHeight + 2f * margin);
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(feet + new Vector2(0f, MaxBodyHeight / 2f), size, 0f))
                if (hit != null && hit.enabled && !hit.isTrigger) return false;
            return true;
        }

        /// <summary>The seed of an NPC's own random stream: seed, race and index packed so none collide.</summary>
        public static ulong NpcSeed(int seed, Race race, int index) =>
            ((ulong)(uint)seed << 32) | ((ulong)(uint)race << 24) | ((uint)index & 0xFFFFFFu);

        public static NpcSpec ChooseParts(int seed, Race race, int index)
        {
            RacePartSet set = RacePartSets.For(race);
            var rng = new SeededRng(NpcSeed(seed, race, index), RngStream);
            NpcPart head = set.Heads[rng.NextInt(set.Heads.Count)];
            NpcPart torso = set.Torsos[rng.NextInt(set.Torsos.Count)];
            NpcPart legs = set.Legs[rng.NextInt(set.Legs.Count)];
            return new NpcSpec(seed, race, index, head, torso, legs, rng.NextUInt());
        }

        /// <summary>
        /// Builds an NPC at a position. It wanders and flees inside bounds (its screen), talks to the given
        /// player (or the scene's player when null) and is silenced while relations marks its race hostile.
        /// </summary>
        public static NpcTalker Spawn(NpcSpec spec, IReadOnlyList<string> lines, Vector2 position, Rect bounds,
            RaceRelations relations, Transform player = null, Transform parent = null, Material material = null)
        {
            var go = new GameObject($"NPC {spec.DisplayName}");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;

            // Dynamic and velocity-driven (NpcMover), so walls and closed gates stop it and contacts with the
            // player are solved by physics instead of a kinematic shove.
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.mass = BodyMass;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Legs at the feet, torso above, head on top; the root sits at the feet's centre.
            float y = 0f;
            var renderers = new List<SpriteRenderer>();
            foreach (NpcPart part in new[] { spec.Legs, spec.Torso, spec.Head })
            {
                var visual = new GameObject(part.Slot.ToString());
                visual.transform.SetParent(go.transform, false);
                visual.transform.localPosition = new Vector3(0f, y + part.Size.y / 2f, 0f);
                // The race's outlined part (ArtCatalog.NpcPart), scaled to exactly the part's size.
                Sprite sprite = ArtCatalog.NpcPart(spec.Race, part);
                Vector2 spriteSize = sprite.bounds.size;
                visual.transform.localScale = new Vector3(part.Size.x / spriteSize.x, part.Size.y / spriteSize.y, 1f);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                if (material != null) renderer.sharedMaterial = material;
                renderer.color = part.Color;
                renderer.sortingOrder = SortingOrder + (int)part.Slot;
                renderers.Add(renderer);
                y += part.Size.y;
            }

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(BodyWidth, y);
            collider.offset = new Vector2(0f, y / 2f);

            var mover = go.AddComponent<NpcMover>();
            mover.Configure(spec.Movement, bounds, player, spec.Salt);

            var talker = go.AddComponent<NpcTalker>();
            talker.Configure(spec, lines, relations, player, renderers.ToArray());

            // Peaceful until its race turns hostile; then it chases, hurts and can be hurt like any enemy.
            HostileNpc.Attach(talker, bounds, player);
            return talker;
        }

        /// <summary>A white generated shape, one world unit across (made once by the ArtCatalog).</summary>
        public static Sprite ShapeSprite(PartShape shape) => ArtCatalog.Shape(shape);
    }
}
