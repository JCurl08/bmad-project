using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The kinds of town building, in each race's own style (4.5).</summary>
    public enum TownBuildingKind
    {
        /// <summary>Town: a red-roofed cottage (Kenney Tiny Town roof, wall and door tiles).</summary>
        Cottage = 0,
        /// <summary>Town: a grey-roofed stone cottage (Kenney tiles).</summary>
        StoneCottage = 1,
        /// <summary>Town: a market stall under a striped awning, with a cart (Kenney) beside it.</summary>
        MarketStall = 2,
        /// <summary>Finches: a plank hut on a trunk under a leafy crown, the door in the trunk.</summary>
        Treehouse = 3,
        /// <summary>Finches: a great woven nest with a round door and a leafy roof.</summary>
        Nest = 4,
        /// <summary>Chemistry folk: a hut in a giant spotted mushroom.</summary>
        MushroomHut = 5,
        /// <summary>Chemistry folk: a mushroom hut with a bubbling vent pipe.</summary>
        VentHut = 6,
        /// <summary>Physics folk: a round tower under a slit dome with a telescope.</summary>
        Observatory = 7,
        /// <summary>Physics folk: a low banded dome with an antenna.</summary>
        Dome = 8,
        /// <summary>Shape people: a tall square tower with a pointed triangle roof.</summary>
        Tower = 9,
        /// <summary>Shape people: a stacked block of cubes.</summary>
        Block = 10,
        /// <summary>Dinosaurs: a log lodge under a steep roof, a bone over the door.</summary>
        Lodge = 11,
        /// <summary>Dinosaurs: a little log cabin with a stone chimney.</summary>
        Cabin = 12,
    }

    /// <summary>One town building: its kind, its solid footprint, the area its art covers, its door and whether that door is the one 4.6 makes enterable.</summary>
    public readonly struct TownBuilding
    {
        public readonly TownBuildingKind Kind;

        /// <summary>The solid part (local to the screen centre): what the player and NPCs bump into.</summary>
        public readonly Rect Solid;

        /// <summary>Where the art is drawn (local): the solid base and the roof above it.</summary>
        public readonly Rect Art;

        /// <summary>The middle of the door's sill (local), on the solid's lane-side edge.</summary>
        public readonly Vector2 Door;

        /// <summary>The way the door faces (out of the building).</summary>
        public readonly Facing DoorFacing;

        /// <summary>True for the door marked for story 4.6 (exactly one per town).</summary>
        public readonly bool Enterable;

        public TownBuilding(TownBuildingKind kind, Rect solid, Rect art, Vector2 door, Facing doorFacing, bool enterable)
        {
            Kind = kind;
            Solid = solid;
            Art = art;
            Door = door;
            DoorFacing = doorFacing;
            Enterable = enterable;
        }

        public override string ToString() => $"{Kind}{(Enterable ? " (marked door)" : "")} at {Solid}";
    }

    /// <summary>
    /// The town on a face's centre screen (4.5) as pure geometry, local to the screen centre and fixed per race: a row of
    /// four buildings in the race's style along the north half, their doors opening onto the horizontal lane; a paved plaza
    /// over the crossroads and the south half, with the race's statue (its leader in stone) just off the crossroads; the
    /// leader's stand beside the statue; and a row of stands for the residents. Everything solid keeps
    /// ScreenModule.KeepsExitsOpen, so the four doorways and both lanes stay clear, and every stand is a valid TownPlan spot
    /// (inside its quadrant, off the lanes, clear of every solid). One door per town is marked for story 4.6 (Enterable).
    /// TownPlan.BlockedRects adds Solids to a town module's blocked rects, so pure plans and the built town agree.
    /// </summary>
    public sealed class TownLayout
    {
        /// <summary>Art pixels per world unit: the people's (a 16-pixel design is DesignSize = 2 units across).</summary>
        public const float PixelsPerUnit = 16f / NpcFactory.DesignSize;

        /// <summary>A building's width, its solid height, and how tall its art stands (its roof reaches above the solid).</summary>
        public const float BuildingWidth = 4f, SolidHeight = 2.8f, ArtHeight = 4f;

        /// <summary>The bottom of every building: just past the horizontal lane, so its door opens onto the lane.</summary>
        public const float BuildingBase = 2f;

        /// <summary>The feet height of every stand: in the south quadrants' row (inside TownPlan.QuadrantBounds).</summary>
        public const float StandY = -4.6f;

        /// <summary>The leader stands at most this far from the statue's centre.</summary>
        public const float LeaderReach = 3f;

        /// <summary>The buildings' left edges, west to east: two in each north quadrant.</summary>
        private static readonly float[] BuildingLefts = { -10.6f, -6.3f, 2.3f, 6.6f };

        /// <summary>Which of the four is the marked (enterable) building: the inner one west of the crossroads.</summary>
        public const int MarkedBuilding = 1;

        /// <summary>The statue's pedestal and figure (art), and its solid base, in the south-east quadrant by the crossroads.</summary>
        public static readonly Rect StatueArtRect = new Rect(1.95f, -5.5f, 20f / PixelsPerUnit, 24f / PixelsPerUnit);
        public static readonly Rect StatueSolidRect = Rect.MinMaxRect(2.3f, -5.5f, 4.1f, -4.1f);

        /// <summary>The plaza's paving (visual only): the crossroads and the south half.</summary>
        public static readonly Rect PlazaRect = Rect.MinMaxRect(-10f, -6f, 10f, 1.6f);

        /// <summary>The leader's stand: just east of the statue.</summary>
        public static readonly Vector2 LeaderStandLocal = new Vector2(5.2f, StandY);

        /// <summary>The residents' stands: four in the south-west quadrant, two east of the leader, a little staggered.</summary>
        private static readonly Vector2[] StandLocals =
        {
            new Vector2(-2.8f, StandY + 0.2f), new Vector2(-4.9f, StandY - 0.4f), new Vector2(-7f, StandY + 0.1f),
            new Vector2(-9.1f, StandY - 0.3f), new Vector2(7.2f, StandY - 0.3f), new Vector2(9.3f, StandY + 0.2f),
        };

        private static readonly Dictionary<Theme, TownLayout> Cache = new Dictionary<Theme, TownLayout>();

        private readonly List<TownBuilding> buildings;

        public Theme Theme { get; }
        public IReadOnlyList<TownBuilding> Buildings => buildings;
        public Rect StatueSolid => StatueSolidRect;
        public Rect StatueArt => StatueArtRect;
        public Rect Plaza => PlazaRect;
        public Vector2 LeaderStand => LeaderStandLocal;

        /// <summary>The residents' stands (feet, local), in the order residents (or the Town face's first NPCs) take them.</summary>
        public IReadOnlyList<Vector2> Stands => StandLocals;

        private TownLayout(Theme theme, List<TownBuilding> buildings)
        {
            Theme = theme;
            this.buildings = buildings;
        }

        /// <summary>The town of a face theme (made once).</summary>
        public static TownLayout For(Theme theme)
        {
            if (Cache.TryGetValue(theme, out TownLayout cached)) return cached;
            TownBuildingKind[] kinds = KindsFor(theme);
            var list = new List<TownBuilding>();
            for (int i = 0; i < BuildingLefts.Length; i++)
            {
                float left = BuildingLefts[i];
                var solid = new Rect(left, BuildingBase, BuildingWidth, SolidHeight);
                var art = new Rect(left, BuildingBase, BuildingWidth, ArtHeight);
                var door = new Vector2(left + DoorOffset(kinds[i]), BuildingBase);
                list.Add(new TownBuilding(kinds[i], solid, art, door, Facing.South, i == MarkedBuilding));
            }
            var layout = new TownLayout(theme, list);
            Cache[theme] = layout;
            return layout;
        }

        /// <summary>Each race's buildings, west to east (the second is the marked one).</summary>
        public static TownBuildingKind[] KindsFor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Biology: return new[] { TownBuildingKind.Nest, TownBuildingKind.Treehouse, TownBuildingKind.Nest, TownBuildingKind.Treehouse };
                case Theme.Chemistry: return new[] { TownBuildingKind.VentHut, TownBuildingKind.MushroomHut, TownBuildingKind.VentHut, TownBuildingKind.MushroomHut };
                case Theme.Physics: return new[] { TownBuildingKind.Dome, TownBuildingKind.Observatory, TownBuildingKind.Dome, TownBuildingKind.Observatory };
                case Theme.Math: return new[] { TownBuildingKind.Block, TownBuildingKind.Tower, TownBuildingKind.Block, TownBuildingKind.Tower };
                case Theme.EarthAtmosphere: return new[] { TownBuildingKind.Cabin, TownBuildingKind.Lodge, TownBuildingKind.Cabin, TownBuildingKind.Lodge };
                default: return new[] { TownBuildingKind.StoneCottage, TownBuildingKind.Cottage, TownBuildingKind.MarketStall, TownBuildingKind.Cottage };
            }
        }

        /// <summary>The race whose style a building kind is in (Town's for the cottages and the market).</summary>
        public static Theme ThemeOf(TownBuildingKind kind)
        {
            switch (kind)
            {
                case TownBuildingKind.Treehouse:
                case TownBuildingKind.Nest: return Theme.Biology;
                case TownBuildingKind.MushroomHut:
                case TownBuildingKind.VentHut: return Theme.Chemistry;
                case TownBuildingKind.Observatory:
                case TownBuildingKind.Dome: return Theme.Physics;
                case TownBuildingKind.Tower:
                case TownBuildingKind.Block: return Theme.Math;
                case TownBuildingKind.Lodge:
                case TownBuildingKind.Cabin: return Theme.EarthAtmosphere;
                default: return Theme.Town;
            }
        }

        /// <summary>
        /// The door's distance from a building's left edge, where its art draws it: the cottages' door is their left Kenney
        /// tile, the block's and the cabin's sit left of centre (TownArt), the rest are centred.
        /// </summary>
        public static float DoorOffset(TownBuildingKind kind)
        {
            switch (kind)
            {
                case TownBuildingKind.Cottage:
                case TownBuildingKind.StoneCottage: return BuildingWidth / 4f;
                case TownBuildingKind.Block: return 10f / PixelsPerUnit;
                case TownBuildingKind.Cabin: return 12f / PixelsPerUnit;
                default: return BuildingWidth / 2f;
            }
        }

        /// <summary>The marked (enterable) building.</summary>
        public TownBuilding Marked
        {
            get
            {
                foreach (TownBuilding b in buildings)
                    if (b.Enterable) return b;
                return buildings[0];
            }
        }

        /// <summary>Every solid rect of the town (local): the buildings and the statue.</summary>
        public List<Rect> Solids
        {
            get
            {
                var rects = new List<Rect>();
                foreach (TownBuilding b in buildings) rects.Add(b.Solid);
                rects.Add(StatueSolidRect);
                return rects;
            }
        }

        /// <summary>
        /// Problems with the layout (empty when all is well): at least three buildings, one statue and one marked door; every
        /// solid keeps the exits open (off both lanes, inside the edge band) and apart from the others; every building's art
        /// stays inside the screen's content area; every door sits on its building's lane-side edge with open floor in front;
        /// the leader stands within LeaderReach of the statue; and every stand, the leader's too, is a valid TownPlan spot
        /// (clear of every solid, inside its quadrant bounds) apart from every other stand.
        /// </summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            if (buildings.Count < 3) problems.Add($"{Theme}: only {buildings.Count} buildings");
            int marked = 0;
            foreach (TownBuilding b in buildings) if (b.Enterable) marked++;
            if (marked != 1) problems.Add($"{Theme}: {marked} marked doors (want 1)");

            List<Rect> solids = Solids;
            for (int i = 0; i < solids.Count; i++)
            {
                if (!ScreenModule.KeepsExitsOpen(solids[i])) problems.Add($"{Theme}: solid {solids[i]} blocks a lane or the edge band");
                for (int j = i + 1; j < solids.Count; j++)
                    if (Grow(solids[i], 0.1f).Overlaps(solids[j])) problems.Add($"{Theme}: solids {solids[i]} and {solids[j]} touch");
            }
            Vector2 half = ScreenMath.DefaultScreenSize / 2f - Vector2.one * ScreenModule.EdgeClearance;
            var content = Rect.MinMaxRect(-half.x, -half.y, half.x, half.y);
            foreach (TownBuilding b in buildings)
            {
                if (!Contains(content, b.Art)) problems.Add($"{Theme}: {b} draws outside the content area");
                if (!b.Art.Contains(b.Solid.min) || b.Art.xMax < b.Solid.xMax || b.Art.yMax < b.Solid.yMax)
                    problems.Add($"{Theme}: {b}'s art does not cover its solid");
                bool onEdge = Mathf.Abs(b.Door.y - b.Solid.yMin) < 1e-4f && b.Door.x > b.Solid.xMin && b.Door.x < b.Solid.xMax;
                if (b.DoorFacing != Facing.South || !onEdge) problems.Add($"{Theme}: {b}'s door is not on its south edge");
                Vector2 front = b.Door + Vector2.down * (TimeField.PlayerRadius + 0.05f);
                foreach (Rect s in solids)
                    if (s.Contains(front)) problems.Add($"{Theme}: {b}'s door is blocked by {s}");
                if (front.y < ScreenModule.ExitLaneHalfWidth - 2f * TimeField.PlayerRadius)
                    problems.Add($"{Theme}: {b}'s door is too far from the lane");
            }
            if (!Contains(content, StatueArtRect) || StatueArtRect.yMax > -ScreenModule.ExitLaneHalfWidth)
                problems.Add($"{Theme}: the statue draws outside its quadrant");
            if (Vector2.Distance(LeaderStandLocal + Vector2.up * (NpcFactory.MaxBodyHeight / 2f), StatueSolidRect.center) > LeaderReach)
                problems.Add($"{Theme}: the leader stands too far from the statue");

            var all = new List<Vector2>(StandLocals) { LeaderStandLocal };
            for (int i = 0; i < all.Count; i++)
            {
                Vector2 feet = all[i];
                if (!TownPlan.IsValid(feet, solids)) problems.Add($"{Theme}: stand {feet} is not a valid spot");
                Rect bounds = TownPlan.QuadrantBounds(feet);
                if (!bounds.Contains(feet)) problems.Add($"{Theme}: stand {feet} is outside its own movement bounds");
                for (int j = i + 1; j < all.Count; j++)
                    if (TownPlan.Footprint(feet, TownPlan.Spacing / 2f).Overlaps(TownPlan.Footprint(all[j], TownPlan.Spacing / 2f)))
                        problems.Add($"{Theme}: stands {feet} and {all[j]} are too close");
            }
            if (StandLocals.Length < TownSquare.ResidentCount) problems.Add($"{Theme}: {StandLocals.Length} stands for {TownSquare.ResidentCount} residents");
            return problems;
        }

        /// <summary>Compact text of the layout, for comparisons and logs.</summary>
        public string Signature()
        {
            var parts = new List<string>();
            foreach (TownBuilding b in buildings) parts.Add($"{b.Kind}{(b.Enterable ? "*" : "")}@{b.Solid.x:0.0},{b.Solid.y:0.0}");
            return $"{Theme}: {string.Join(" ", parts)} statue@{StatueSolidRect.center.x:0.0},{StatueSolidRect.center.y:0.0}";
        }

        private static Rect Grow(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        private static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 1e-4f && inner.xMax <= outer.xMax + 1e-4f && inner.yMin >= outer.yMin - 1e-4f && inner.yMax <= outer.yMax + 1e-4f;
    }
}
