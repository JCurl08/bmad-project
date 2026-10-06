using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>Where one Town NPC stands: a Town screen and its feet relative to that screen's centre.</summary>
    public readonly struct TownSpot : IEquatable<TownSpot>
    {
        public readonly bool Found;
        public readonly ScreenAddress Screen;

        /// <summary>Feet position relative to the screen centre (module-local units).</summary>
        public readonly Vector2 Local;

        public TownSpot(ScreenAddress screen, Vector2 local)
        {
            Found = true;
            Screen = screen;
            Local = local;
        }

        // Exact component comparison (Vector2 == is approximate), consistent with GetHashCode.
        public bool Equals(TownSpot other) => Found == other.Found && Screen == other.Screen &&
                                              Local.x.Equals(other.Local.x) && Local.y.Equals(other.Local.y);
        public override bool Equals(object obj) => obj is TownSpot other && Equals(other);
        public override int GetHashCode() => (Screen.GetHashCode() * 397) ^ Local.GetHashCode();
        public override string ToString() => Found ? $"{Screen} {Local}" : "(none)";
    }

    /// <summary>
    /// The Town population of a run, as pure data: which NPCs stand in town (one of each face race plus
    /// MinTownsfolk..MaxTownsfolk Townsfolk) and where. Everything comes from the run seed on RngStream, so
    /// the same seed always gives the same NPCs on the same spots.
    /// A spot is valid when the NPC's footprint (plus Margin) obeys ScreenModule.KeepsExitsOpen (inside the
    /// edge band, off both centre lanes and so off the screen centre) and overlaps no blocked rect of its
    /// module (colliders and alcove pockets) and no other Town NPC (plus Spacing).
    /// </summary>
    public static class TownPlan
    {
        /// <summary>PCG32 stream for Town population (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.TownPlan;

        public const int MinTownsfolk = 2;
        public const int MaxTownsfolk = 4;

        /// <summary>NPC index base for Town NPCs, so their parts never repeat the F3 debug batches (0, 1, ...).</summary>
        public const int IndexBase = 1000;

        /// <summary>Half the drawn NPC width (the torso is wider than the collider).</summary>
        public const float HalfWidth = 0.35f;

        /// <summary>Clearance kept around an NPC's footprint from walls, lanes and the edge band.</summary>
        public const float Margin = 0.15f;

        /// <summary>Extra gap kept between two Town NPCs.</summary>
        public const float Spacing = 0.6f;

        /// <summary>Candidate grid step (module-local units).</summary>
        public const float GridStep = 0.25f;

        /// <summary>The five face races, one NPC each, in a fixed order.</summary>
        public static readonly Race[] FaceRaces = { Race.Finch, Race.Mushroom, Race.Alien, Race.Shape, Race.Dinosaur };

        /// <summary>How many Townsfolk stand in town this run (MinTownsfolk..MaxTownsfolk).</summary>
        public static int TownsfolkCount(int seed)
        {
            var rng = new SeededRng(((ulong)(uint)seed << 32) | 0x544Fu, RngStream);
            return MinTownsfolk + rng.NextInt(MaxTownsfolk - MinTownsfolk + 1);
        }

        /// <summary>
        /// The Town NPCs of a run: the greeter (Townsfolk 0) first, then one of each face race, then the other
        /// Townsfolk. Face races take their own part sets; Townsfolk are already mixed, so their head and torso
        /// come from any race's set (TownsfolkParts).
        /// </summary>
        public static List<NpcSpec> Specs(int seed)
        {
            int townsfolk = TownsfolkCount(seed);
            var specs = new List<NpcSpec> { TownsfolkParts(seed, 0) };
            foreach (Race race in FaceRaces)
                specs.Add(NpcFactory.ChooseParts(seed, race, IndexBase));
            for (int i = 1; i < townsfolk; i++)
                specs.Add(TownsfolkParts(seed, i));
            return specs;
        }

        /// <summary>True for the greeter: Townsfolk 0, who adds the TownSign welcome.</summary>
        public static bool IsGreeter(NpcSpec spec) =>
            spec != null && spec.Race == Race.Townsfolk && spec.Index == IndexBase;

        /// <summary>A Townsperson whose head and torso may come from any race's part set; legs are Townsfolk legs.</summary>
        public static NpcSpec TownsfolkParts(int seed, int townsfolkIndex)
        {
            int index = IndexBase + townsfolkIndex;
            var rng = new SeededRng(NpcFactory.NpcSeed(seed, Race.Townsfolk, index), RngStream);
            Race[] races = RaceExtensions.All;
            RacePartSet headSet = RacePartSets.For(races[rng.NextInt(races.Length)]);
            NpcPart head = headSet.Heads[rng.NextInt(headSet.Heads.Count)];
            RacePartSet torsoSet = RacePartSets.For(races[rng.NextInt(races.Length)]);
            NpcPart torso = torsoSet.Torsos[rng.NextInt(torsoSet.Torsos.Count)];
            RacePartSet legSet = RacePartSets.For(Race.Townsfolk);
            NpcPart legs = legSet.Legs[rng.NextInt(legSet.Legs.Count)];
            return new NpcSpec(seed, Race.Townsfolk, index, head, torso, legs, rng.NextUInt());
        }

        /// <summary>The local rect an NPC with its feet here covers, grown by a margin on every side.</summary>
        public static Rect Footprint(Vector2 feet, float margin)
        {
            float halfWidth = HalfWidth + margin;
            return Rect.MinMaxRect(feet.x - halfWidth, feet.y - margin, feet.x + halfWidth,
                feet.y + NpcFactory.MaxBodyHeight + margin);
        }

        /// <summary>True when an NPC may stand here: clear of the edge band, both lanes, the centre and every blocked rect.</summary>
        public static bool IsValid(Vector2 feet, IReadOnlyList<Rect> blocked)
        {
            Rect footprint = Footprint(feet, Margin);
            if (!ScreenModule.KeepsExitsOpen(footprint)) return false;
            if (blocked != null)
                foreach (Rect b in blocked)
                    if (b.Overlaps(footprint)) return false;
            return true;
        }

        /// <summary>
        /// The feet rect an NPC standing at this spot may move in: its quadrant, off both lanes and inside the
        /// edge band (local units; add the screen centre for world units).
        /// </summary>
        public static Rect QuadrantBounds(Vector2 feet)
        {
            Vector2 half = ScreenMath.DefaultScreenSize / 2f;
            float lane = ScreenModule.ExitLaneHalfWidth;
            float edge = ScreenModule.EdgeClearance;
            float xNear = lane + HalfWidth + Margin, xFar = half.x - edge - HalfWidth - Margin;
            float yLow, yHigh;
            if (feet.y >= 0f)
            {
                yLow = lane + Margin;
                yHigh = half.y - edge - NpcFactory.MaxBodyHeight - Margin;
            }
            else
            {
                yLow = -half.y + edge + Margin;
                yHigh = -lane - NpcFactory.MaxBodyHeight - Margin;
            }
            return feet.x >= 0f
                ? Rect.MinMaxRect(xNear, yLow, xFar, yHigh)
                : Rect.MinMaxRect(-xFar, yLow, -xNear, yHigh);
        }

        /// <summary>Every valid feet position on a screen with these blocked rects, in a fixed grid order.</summary>
        public static List<Vector2> Candidates(IReadOnlyList<Rect> blocked)
        {
            var list = new List<Vector2>();
            Vector2 half = ScreenMath.DefaultScreenSize / 2f;
            int nx = Mathf.FloorToInt(half.x / GridStep);
            int ny = Mathf.FloorToInt(half.y / GridStep);
            for (int iy = -ny; iy <= ny; iy++)
            {
                for (int ix = -nx; ix <= nx; ix++)
                {
                    var feet = new Vector2(ix * GridStep, iy * GridStep);
                    if (IsValid(feet, blocked)) list.Add(feet);
                }
            }
            return list;
        }

        /// <summary>
        /// Spots for count NPCs on the Town face (index 0, the greeter, prefers the start screen; the rest go
        /// round the other screens in a seeded order). Each pick is a seeded choice among the screen's valid,
        /// still-free candidates; accept (optional, e.g. a physics check) can veto a pick, and the next one is
        /// tried. When a screen has nothing left the next screen is tried; an NPC that fits nowhere gets an
        /// unfound spot, never an invalid one.
        /// </summary>
        public static List<TownSpot> Spots(int seed, int faceSize, int count, Func<Vector2Int, IReadOnlyList<Rect>> blockedFor,
            Func<ScreenAddress, Vector2, bool> accept = null)
        {
            var rng = new SeededRng(((ulong)(uint)seed << 32) | 0x5350u, RngStream);

            var screens = new List<ScreenAddress>();
            var start = new ScreenAddress(CubeModel.StartFace, Vector2Int.zero);
            for (int y = 0; y < faceSize; y++)
                for (int x = 0; x < faceSize; x++)
                    if (x != 0 || y != 0) screens.Add(new ScreenAddress(CubeModel.StartFace, x, y));
            rng.Shuffle(screens);
            screens.Insert(0, start);

            var candidates = new Dictionary<ScreenAddress, List<Vector2>>();
            var taken = new Dictionary<ScreenAddress, List<Rect>>();
            foreach (ScreenAddress screen in screens)
            {
                candidates[screen] = Candidates(blockedFor != null ? blockedFor(screen.Cell) : null);
                taken[screen] = new List<Rect>();
            }

            var spots = new List<TownSpot>(count);
            for (int i = 0; i < count; i++)
            {
                TownSpot spot = default;
                for (int k = 0; k < screens.Count && !spot.Found; k++)
                {
                    ScreenAddress screen = screens[(i + k) % screens.Count];
                    var free = new List<Vector2>();
                    foreach (Vector2 c in candidates[screen])
                        if (!OverlapsAny(Footprint(c, Spacing / 2f), taken[screen])) free.Add(c);
                    while (free.Count > 0)
                    {
                        int pick = rng.NextInt(free.Count);
                        Vector2 feet = free[pick];
                        if (accept == null || accept(screen, feet))
                        {
                            spot = new TownSpot(screen, feet);
                            taken[screen].Add(Footprint(feet, Spacing / 2f));
                            break;
                        }
                        free.RemoveAt(pick);
                    }
                }
                spots.Add(spot);
            }
            return spots;
        }

        private static bool OverlapsAny(Rect rect, List<Rect> others)
        {
            foreach (Rect o in others)
                if (o.Overlaps(rect)) return true;
            return false;
        }

        /// <summary>
        /// The rects NPCs must keep off in a module, local to its centre: every solid collider, and every gate
        /// alcove (walls, opening and pocket). Works on prefab assets and on placed instances alike.
        /// </summary>
        public static List<Rect> BlockedRects(ScreenModule module)
        {
            var rects = new List<Rect>();
            if (module == null) return rects;
            Transform root = module.transform;
            foreach (BoxCollider2D box in module.GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (box.isTrigger) continue;
                Vector2 centre = root.InverseTransformPoint(box.transform.TransformPoint(box.offset));
                Vector2 size = Vector2.Scale(box.size, (Vector2)box.transform.lossyScale);
                size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
                rects.Add(new Rect(centre - size / 2f, size));
            }
            foreach (GateSlot gate in module.Gates)
            {
                Vector2 opening = root.InverseTransformPoint(gate.transform.position);
                Vector2 pocket = opening + gate.PocketOffset;
                float halfWidth = GateSlot.GateWidth / 2f + GateSlot.GateThickness;
                // The pocket centre is midway between the opening and the back wall: the alcove's outer edges
                // sit half a wall beyond the opening and the back wall.
                float outward = Mathf.Sign(pocket.y - opening.y);
                float far = pocket.y + (pocket.y - opening.y) + outward * GateSlot.GateThickness / 2f;
                float near = opening.y - outward * GateSlot.GateThickness / 2f;
                rects.Add(Rect.MinMaxRect(opening.x - halfWidth, Mathf.Min(near, far), opening.x + halfWidth, Mathf.Max(near, far)));
            }
            return rects;
        }
    }
}
