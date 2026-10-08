using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// One NPC's chosen parts plus the salt that picks its hint subject and lines, and whether it is its race's leader.
    /// Pure data. The parts drive the hint, role and movement whether or not they are drawn (NpcFactory.PartArt).
    /// </summary>
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

        /// <summary>True for a race's leader (4.5's quest-givers): drawn with its race's leader design.</summary>
        public bool IsLeader { get; }

        public NpcSpec(int seed, Race race, int index, NpcPart head, NpcPart torso, NpcPart legs, uint salt, bool leader = false)
        {
            IsLeader = leader;
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

        /// <summary>The speaker name the dialogue box shows, sentence case ("Finch merchant", "Avian dinosaur gossip", "Finch leader").</summary>
        public string DisplayName => $"{RaceName(Race, Variant)} {(IsLeader ? "leader" : RoleName(Role).ToLowerInvariant())}";

        /// <summary>The same NPC marked as its race's leader (drawn with the leader design). NpcFactory.ChooseLeader gives a
        /// leader from its own index space; this keeps the parts and salt as they are.</summary>
        public NpcSpec AsLeader() => new NpcSpec(Seed, Race, Index, Head, Torso, Legs, Salt, true);

        /// <summary>The same NPC with one part swapped (tests and later part-swapping mechanics).</summary>
        public NpcSpec With(NpcPart part)
        {
            switch (part.Slot)
            {
                case PartSlot.Head: return new NpcSpec(Seed, Race, Index, part, Torso, Legs, Salt, IsLeader);
                case PartSlot.Torso: return new NpcSpec(Seed, Race, Index, Head, part, Legs, Salt, IsLeader);
                default: return new NpcSpec(Seed, Race, Index, Head, Torso, part, Salt, IsLeader);
            }
        }

        /// <summary>Part ids joined, for comparisons and logs.</summary>
        public string Signature() => $"{Race}:{Head.Id}/{Torso.Id}/{Legs.Id}#{Salt}{(IsLeader ? "+leader" : "")}";

        public override string ToString() => $"{DisplayName} ({Signature()})";

        public static string RaceName(Race race, DinoVariant variant)
        {
            switch (race)
            {
                case Race.Mushroom: return "Mushroom";
                case Race.Dinosaur: return variant == DinoVariant.Avian ? "Avian dinosaur" : "Terrestrial dinosaur";
                case Race.Shape: return "Shape person";
                case Race.Townsfolk: return "Townsperson";
                default: return race.ToString();
            }
        }

        public static string RoleName(NpcRole role) => role == NpcRole.QuestGiver ? "Quest-Giver" : role.ToString();
    }

    /// <summary>
    /// Builds NPCs. Part choice is pure: (seed, race, index) seeds its own PCG32 stream, so the same seed
    /// always gives the same NPCs and choosing one never disturbs another. Spawn builds the runtime object:
    /// its race's hand-drawn design (one "Body" sprite, NpcDesignArt; a leader's design for a leader spec), a collider,
    /// an NpcTalker, an NpcMover and the dormant enemy parts (HostileNpc) that wake while its race is hostile.
    /// With PartArt on (off by default; kept for epic-varied-runs, CAP-9) it draws the three stacked swappable part
    /// sprites instead (legs, torso, head; NpcPartArt). Either way the parts drive hints, roles and movement.
    /// Placing NPCs in the world is up to the town and face code (and the F3 debug spawn).
    /// </summary>
    public static class NpcFactory
    {
        /// <summary>PCG32 stream for NPC parts (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.NpcParts;

        public const int SortingOrder = 6;

        /// <summary>Collider width; the height is the stacked parts' height (at most MaxBodyHeight).</summary>
        public const float BodyWidth = 0.6f * ScreenMath.ContentScale;
        public const float MaxBodyHeight = 1.4f * ScreenMath.ContentScale;
        public const float BodyMass = 5f;

        /// <summary>
        /// Draw NPCs as their three swappable parts (NpcPartArt) instead of their race's design. Off by default (4.3); the
        /// parts system is kept for epic-varied-runs (CAP-9), and its tests turn this on.
        /// </summary>
        public static bool PartArt { get; set; }

        /// <summary>Resets PartArt when play starts (domain reload is off, so statics would survive between plays and tests).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => PartArt = false;

        /// <summary>
        /// The leaders' own index space: ChooseLeader(seed, race, i) draws parts and salt at LeaderIndexBase + i, above
        /// every population's base (Town 1000 ... Earth 6000) and the F3 batches, inside NpcSeed's 24-bit index field. So
        /// a leader is never the ordinary NPC at the same index: its salt, lines and wander differ.
        /// </summary>
        public const int LeaderIndexBase = 0x800000;

        /// <summary>
        /// The drawn size of a design (a square): the player's, one tile of the first screens scaled to the screen and
        /// drawn PeopleArtScale larger (4.1). Visual only: the collider keeps the parts' height (at most MaxBodyHeight).
        /// </summary>
        public const float DesignSize = ScreenMath.ContentScale * CubeSettings.PeopleArtScale;

        /// <summary>
        /// The drawn top of an NPC's head above its feet (where Newton's apple lands): its design's top opaque row (the
        /// outline included), or the stacked parts' top with PartArt on.
        /// </summary>
        public static float HeadTop(NpcSpec spec) => PartArt
            ? MaxBodyHeight * CubeSettings.PeopleArtScale
            : DesignSize * NpcDesignArt.DrawnRows(NpcDesignArt.LookOf(spec), spec.IsLeader) / NpcDesignArt.Size;

        /// <summary>
        /// True if an NPC standing with its feet at this point would overlap no solid collider (walls,
        /// closed gates, the player, other NPCs). Triggers are ignored. Used to pick spawn spots.
        /// </summary>
        public static bool IsClear(Vector2 feet, float margin = 0.1f * ScreenMath.ContentScale)
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
        /// A race's leader (for 4.5), marked as the leader so Spawn draws its race's leader design. Its parts (hint, role,
        /// movement) and salt come from the leaders' own index space (LeaderIndexBase + index), so it is never the
        /// ordinary NPC at that index.
        /// </summary>
        public static NpcSpec ChooseLeader(int seed, Race race, int index) =>
            ChooseParts(seed, race, LeaderIndexBase + index).AsLeader();

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

            // The collider keeps the parts' own height (bodyHeight) whichever art is drawn, so spots and physics are unchanged.
            float bodyHeight = 0f;
            foreach (NpcPart part in new[] { spec.Legs, spec.Torso, spec.Head })
                bodyHeight += part.Size.y * ScreenMath.ContentScale;
            SpriteRenderer[] renderers = PartArt ? AddPartSprites(go.transform, spec, material) : new[] { AddDesign(go.transform, spec, material) };

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(BodyWidth, bodyHeight);
            collider.offset = new Vector2(0f, bodyHeight / 2f);

            var mover = go.AddComponent<NpcMover>();
            mover.Configure(spec.Movement, bounds, player, spec.Salt);

            var talker = go.AddComponent<NpcTalker>();
            talker.Configure(spec, lines, relations, player, renderers);

            // Peaceful until its race turns hostile; then it chases, hurts and can be hurt like any enemy.
            HostileNpc.Attach(talker, bounds, player);
            return talker;
        }

        /// <summary>The race's design (its leader's for a leader), DesignSize square, standing on the feet (the root).</summary>
        private static SpriteRenderer AddDesign(Transform root, NpcSpec spec, Material material)
        {
            var visual = new GameObject("Body");
            visual.transform.SetParent(root, false);
            visual.transform.localPosition = new Vector3(0f, DesignSize / 2f, 0f);
            Sprite sprite = NpcDesignArt.SpriteFor(spec);
            Vector2 spriteSize = sprite.bounds.size;
            visual.transform.localScale = new Vector3(DesignSize / spriteSize.x, DesignSize / spriteSize.y, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (material != null) renderer.sharedMaterial = material;
            renderer.color = Color.white; // drawn in its own colours
            renderer.sortingOrder = SortingOrder + 1;
            return renderer;
        }

        /// <summary>
        /// The swappable parts (PartArt on): legs at the feet, torso above, head on top; the root sits at the feet's centre.
        /// Drawn PeopleArtScale larger than the body (visual only).
        /// </summary>
        private static SpriteRenderer[] AddPartSprites(Transform root, NpcSpec spec, Material material)
        {
            float y = 0f;
            var renderers = new List<SpriteRenderer>();
            foreach (NpcPart part in new[] { spec.Legs, spec.Torso, spec.Head })
            {
                Vector2 partBody = part.Size * ScreenMath.ContentScale; // parts are authored at the first screens' scale
                Vector2 size = partBody * CubeSettings.PeopleArtScale;
                var visual = new GameObject(part.Slot.ToString());
                visual.transform.SetParent(root, false);
                visual.transform.localPosition = new Vector3(0f, y + size.y / 2f, 0f);
                // The race's outlined part (ArtCatalog.NpcPart), scaled to exactly the part's size.
                Sprite sprite = ArtCatalog.NpcPart(spec.Race, part);
                Vector2 spriteSize = sprite.bounds.size;
                visual.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                if (material != null) renderer.sharedMaterial = material;
                renderer.color = NpcPartArt.TintFor(spec.Race, part); // white for parts drawn in full colour (finches)
                renderer.sortingOrder = SortingOrder + (int)part.Slot;
                renderers.Add(renderer);
                y += size.y;
            }
            return renderers.ToArray();
        }

        /// <summary>A white generated shape, one world unit across (made once by the ArtCatalog).</summary>
        public static Sprite ShapeSprite(PartShape shape) => ArtCatalog.Shape(shape);
    }
}
