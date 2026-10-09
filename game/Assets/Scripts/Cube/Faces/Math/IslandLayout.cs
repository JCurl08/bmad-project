using System;
using System.Collections.Generic;
using System.Text;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>One plank of an island screen: it joins two islands over the water, whole or broken (a gap in the middle).</summary>
    public readonly struct IslandBridge
    {
        /// <summary>The islands it joins (indices into IslandLayout.Islands).</summary>
        public readonly int A, B;

        /// <summary>The plank over the water, shore to shore (local rect).</summary>
        public readonly Rect Plank;

        /// <summary>True for a broken span: a one-square gap in the middle that only the gap-crosser* spans.</summary>
        public readonly bool Broken;

        public IslandBridge(int a, int b, Rect plank, bool broken, bool vertical)
        {
            A = a;
            B = b;
            Plank = plank;
            Broken = broken;
            Vertical = vertical;
        }

        /// <summary>True when the plank runs north-south (its islands share a lattice column).</summary>
        public bool Vertical { get; }

        /// <summary>The water the plank spans (its length between the two shores).</summary>
        public float Span => Vertical ? Plank.height : Plank.width;

        /// <summary>A broken span's gap: GapLength in the middle of the plank (the whole plank's width across).</summary>
        public Rect Gap
        {
            get
            {
                Vector2 c = Plank.center;
                float g = IslandLayout.GapLength / 2f;
                return Vertical
                    ? Rect.MinMaxRect(Plank.xMin, c.y - g, Plank.xMax, c.y + g)
                    : Rect.MinMaxRect(c.x - g, Plank.yMin, c.x + g, Plank.yMax);
            }
        }

        public bool Joins(int island) => A == island || B == island;

        public int Other(int island) => A == island ? B : A;

        /// <summary>The plank's width across (what the player walks on).</summary>
        public float Across => Vertical ? Plank.width : Plank.height;
    }

    /// <summary>
    /// A Math island screen as pure data (1.25), local to the screen centre: the whole screen inside its edge walls is
    /// solid water dotted with islands (axis-aligned land on a 5 x 3 lattice of spots), joined here and there by planks.
    /// Every doorway lands on its own island (DoorIsland: the island reaching from the doorway into the screen), so the
    /// player can always walk back out the way it came. Planks join spots in one row or column, neighbours or (4.12) two
    /// apart over an empty middle spot, so they never cross and are spread over the whole screen. Some planks are broken
    /// spans: a one-square gap only the gap-crosser* spans.
    /// 4.12 "light every plank": the lit network is every whole plank of the reward island's whole-plank component
    /// (IslandSolver); every open doorway but one sits on it, the third one dealt (the gap doorway) reaches it only over a
    /// broken span and carries the tier's red herrings (lookout planks off the network), and the trial island hangs off the
    /// network by a broken span. Lighting every network plank, never crossing a lit one twice, opens the reward roadblock on
    /// the reward island (GoalSpot; a region of its own for DoorwayReach, joined when every doorway can solve the screen).
    /// Generate searches the design for the tier (IslandGenerator), with a hand-checked template per tier past the budget.
    /// Problems checks all of it; Components gives what joins with or without the gap-crosser*.
    /// </summary>
    public sealed class IslandLayout : IScreenRegions
    {
        /// <summary>
        /// The lattice: island spots' x (five columns) and y (three rows), local to the screen centre (4.1). The columns
        /// spread over the water area, the outer ones at 0.8 of its half-width as on the first 16x10 screens, so the islands
        /// fill the wider screen (4.6 apart). The rows keep the first screens' 2.7, scaled (4.05 apart): the south doorway's
        /// island must still hold the open pickup at CubeWorld.OpenPickupOffset, and the doorway islands grow into the
        /// extra height the thinner walls leave instead.
        /// </summary>
        public static readonly float[] Columns = Lattice(WaterArea.xMax * 0.8f, 2);
        public static readonly float[] Rows = Lattice(2.7f * ScreenMath.ContentScale, 1);

        private static float[] Lattice(float outer, int perSide)
        {
            var at = new float[2 * perSide + 1];
            for (int i = 0; i < at.Length; i++) at[i] = outer * (i - perSide) / perSide;
            return at;
        }

        /// <summary>An inner island's size (a doorway's island is bigger: it reaches the doorway).</summary>
        public const float InnerWidth = 1.4f * ScreenMath.ContentScale, InnerHeight = 1.3f * ScreenMath.ContentScale;

        /// <summary>A doorway island's half-width across its doorway (a little over the doorway's).</summary>
        public const float DoorIslandHalfWidth = 1.3f * ScreenMath.ContentScale;

        /// <summary>How far a doorway island reaches past its lattice spot's line, into the screen.</summary>
        public const float DoorIslandReach = 0.6f * ScreenMath.ContentScale;

        /// <summary>A doorway island's depth into the screen: from the wall to just past its lattice spot (DoorIslandReach).</summary>
        public static float DoorIslandDepth(Facing side) =>
            side.IsHorizontal() ? WaterArea.xMax - Columns[Columns.Length - 1] + DoorIslandReach : WaterArea.yMax - Rows[Rows.Length - 1] + DoorIslandReach;

        /// <summary>A plank's width (the player is 1.2 across).</summary>
        public const float PlankWidth = 1.2f * ScreenMath.ContentScale;

        /// <summary>A broken span's gap: what the gap-crosser* spans (one square of the first screens, scaled).</summary>
        public const float GapLength = 1f * ScreenMath.ContentScale;

        /// <summary>Plank left on each side of a gap, at least.</summary>
        public const float MinPlankEnd = 0.2f * ScreenMath.ContentScale;

        /// <summary>Open water kept between two islands that no plank joins (so they read as separate land).</summary>
        public const float MinWater = 0.6f * ScreenMath.ContentScale;

        private readonly Vector2Int[] spots;
        private readonly Rect[] islands;
        private readonly IslandBridge[] bridges;
        private readonly int[] doorIslands;
        private IslandSolver solver;

        /// <summary>The water inside the edge walls (local rect): everything here that is not island or plank is water.</summary>
        public static Rect WaterArea
        {
            get
            {
                Vector2 h = ScreenEdges.Half;
                float t = ScreenEdges.EdgeWallThickness;
                return Rect.MinMaxRect(-h.x + t, -h.y + t, h.x - t, h.y - t);
            }
        }

        /// <summary>Land rects (local).</summary>
        public IReadOnlyList<Rect> Islands => islands;

        /// <summary>The lattice spot of each island.</summary>
        public IReadOnlyList<Vector2Int> Spots => spots;

        public IReadOnlyList<IslandBridge> Bridges => bridges;

        /// <summary>The island a trial stands on (reached only over a broken span), or -1.</summary>
        public int TrialIsland { get; }

        public bool HasTrial => TrialIsland >= 0;

        public bool HasBroken
        {
            get
            {
                foreach (IslandBridge b in bridges)
                    if (b.Broken) return true;
                return false;
            }
        }

        /// <summary>The island the reward roadblock stands on (4.12), or -1 for a layout with no lit network.</summary>
        public int RewardIsland { get; }

        /// <summary>The difficulty tier the layout was made for (4.12).</summary>
        public PuzzleTier Tier { get; }

        /// <summary>True when the layout has a lit network and a reward roadblock (every generated layout since 4.12).</summary>
        public bool HasPuzzle => RewardIsland >= 0;

        /// <summary>True when the hand-checked template stood in for the search (the sweep reports it).</summary>
        public bool FromTemplate { get; private set; }

        /// <summary>Search steps the generation took (the sweep reports the mean).</summary>
        public int SearchSteps { get; private set; }

        public IslandLayout(IEnumerable<Vector2Int> islandSpots, IEnumerable<(int a, int b, bool broken)> links, int[] doorIslandBySide,
            int trialIsland = -1, int rewardIsland = -1, PuzzleTier tier = PuzzleTier.Early)
        {
            RewardIsland = rewardIsland;
            Tier = tier;
            spots = new List<Vector2Int>(islandSpots).ToArray();
            islands = new Rect[spots.Length];
            for (int i = 0; i < spots.Length; i++) islands[i] = IslandRect(spots[i]);
            var list = new List<IslandBridge>();
            foreach ((int a, int b, bool broken) in links)
                list.Add(new IslandBridge(Math.Min(a, b), Math.Max(a, b), PlankRect(spots[a], spots[b]), broken, spots[a].x == spots[b].x));
            list.Sort((x, y) => x.A != y.A ? x.A.CompareTo(y.A) : x.B.CompareTo(y.B));
            bridges = list.ToArray();
            doorIslands = (int[])doorIslandBySide.Clone();
            TrialIsland = trialIsland;
        }

        /// <summary>The island a doorway lands on (-1 for a side with no doorway).</summary>
        public int DoorIsland(Facing side) => doorIslands[(int)side];

        // IScreenRegions (DoorwayReach): each island is a region, joined by its planks (broken spans only with the gap-crosser*);
        // 4.12: the reward roadblock is one more region (GoalRegion), joined to the reward island while every doorway can light
        // the network, as 4.10's goal pads are.
        public int RegionCount => islands.Length + (HasPuzzle ? 1 : 0);
        public int DoorRegion(Facing side) => DoorIsland(side);

        public int RegionAt(Vector2 local)
        {
            if (HasPuzzle && (local - GoalSpot).sqrMagnitude <= GoalRadius * GoalRadius) return GoalRegion;
            return IslandAt(local);
        }

        /// <summary>The reward roadblock's region (after the islands), or -1 without a puzzle.</summary>
        public int GoalRegion => HasPuzzle ? islands.Length : -1;

        /// <summary>How near GoalSpot a point counts as the roadblock's region (the chest's reach).</summary>
        public const float GoalRadius = RewardChest.Radius;

        /// <summary>Where the reward roadblock (its chest) stands (local): the reward island's middle.</summary>
        public Vector2 GoalSpot => HasPuzzle ? islands[RewardIsland].center : Vector2.zero;

        public IEnumerable<int> Joins(int region, bool gapCrosser)
        {
            if (HasPuzzle && region == GoalRegion)
            {
                if (Solvable) yield return RewardIsland;
                yield break;
            }
            foreach (IslandBridge b in bridges)
                if (b.Joins(region) && (!b.Broken || gapCrosser)) yield return b.Other(region);
            if (HasPuzzle && region == RewardIsland && Solvable) yield return GoalRegion;
        }

        /// <summary>The doorway side of an island, or null for an inner island.</summary>
        public Facing? DoorOf(int island)
        {
            for (int f = 0; f < 4; f++)
                if (doorIslands[f] == island) return (Facing)f;
            return null;
        }

        // ------------------------------------------------------------------ Geometry

        /// <summary>The lattice spot each doorway's island sits on.</summary>
        public static Vector2Int DoorSpot(Facing side)
        {
            switch (side)
            {
                case Facing.North: return new Vector2Int(2, 2);
                case Facing.East: return new Vector2Int(4, 1);
                case Facing.South: return new Vector2Int(2, 0);
                default: return new Vector2Int(0, 1);
            }
        }

        public static Facing? DoorAt(Vector2Int spot)
        {
            for (int f = 0; f < 4; f++)
                if (DoorSpot((Facing)f) == spot) return (Facing)f;
            return null;
        }

        public static bool OnLattice(Vector2Int spot) => spot.x >= 0 && spot.y >= 0 && spot.x < Columns.Length && spot.y < Rows.Length;

        public static Vector2 SpotCentre(Vector2Int spot) => new Vector2(Columns[spot.x], Rows[spot.y]);

        /// <summary>The land of a spot: a doorway's island reaches from the wall's inner face into the screen; others are small.</summary>
        public static Rect IslandRect(Vector2Int spot)
        {
            Rect water = WaterArea;
            Facing? door = DoorAt(spot);
            if (door.HasValue)
            {
                float w = DoorIslandHalfWidth;
                switch (door.Value)
                {
                    case Facing.North: return Rect.MinMaxRect(-w, water.yMax - DoorIslandDepth(Facing.North), w, water.yMax);
                    case Facing.South: return Rect.MinMaxRect(-w, water.yMin, w, water.yMin + DoorIslandDepth(Facing.South));
                    case Facing.East: return Rect.MinMaxRect(water.xMax - DoorIslandDepth(Facing.East), -w, water.xMax, w);
                    default: return Rect.MinMaxRect(water.xMin, -w, water.xMin + DoorIslandDepth(Facing.West), w);
                }
            }
            Vector2 c = SpotCentre(spot);
            return new Rect(c.x - InnerWidth / 2f, c.y - InnerHeight / 2f, InnerWidth, InnerHeight);
        }

        /// <summary>
        /// The plank between two neighbouring spots' islands (local rect): PlankWidth across, centred on the lattice line
        /// between them, from one shore to the other.
        /// </summary>
        public static Rect PlankRect(Vector2Int a, Vector2Int b)
        {
            Rect ra = IslandRect(a), rb = IslandRect(b);
            float half = PlankWidth / 2f;
            if (a.y == b.y)
            {
                Rect left = a.x < b.x ? ra : rb, right = a.x < b.x ? rb : ra;
                float y = Rows[a.y];
                return Rect.MinMaxRect(left.xMax, y - half, right.xMin, y + half);
            }
            Rect low = a.y < b.y ? ra : rb, high = a.y < b.y ? rb : ra;
            float x = Columns[a.x];
            return Rect.MinMaxRect(x - half, low.yMax, x + half, high.yMin);
        }

        /// <summary>True when a link between two neighbouring spots is long enough to break (its gap and a little plank both sides fit).</summary>
        public static bool CanBreak(Vector2Int a, Vector2Int b)
        {
            Rect p = PlankRect(a, b);
            float span = a.x == b.x ? p.height : p.width;
            return span >= GapLength + 2f * MinPlankEnd - 1e-4f && (a.x == b.x || a.y == b.y);
        }

        /// <summary>The island a point (local) stands on, else -1 (water, or a plank).</summary>
        public int IslandAt(Vector2 local)
        {
            for (int i = 0; i < islands.Length; i++)
                if (islands[i].Contains(local)) return i;
            return -1;
        }

        /// <summary>
        /// Where a doorway's arriving player stands (local): on the doorway's line, CubeNavigator.EntryInset inside the edge,
        /// exactly where a face crossing puts the player.
        /// </summary>
        public static Vector2 Arrival(Facing side) =>
            Vector2.Scale(side.ToVector(), ScreenEdges.Half) - (Vector2)side.ToVector() * CubeNavigator.EntryInset;

        /// <summary>The middle of the trial island (local), or the screen centre without one.</summary>
        public Vector2 TrialSpot => HasTrial ? islands[TrialIsland].center : Vector2.zero;

        // ------------------------------------------------------------------ Graph

        /// <summary>
        /// The component of every island over the planks (whole planks only, or broken spans too when gapCrosser): islands
        /// with the same number are joined.
        /// </summary>
        public int[] Components(bool gapCrosser)
        {
            var parent = new int[islands.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }
            foreach (IslandBridge b in bridges)
                if (!b.Broken || gapCrosser) parent[Find(b.A)] = Find(b.B);
            var result = new int[islands.Length];
            for (int i = 0; i < result.Length; i++) result[i] = Find(i);
            return result;
        }

        /// <summary>The doorways whose islands join the island (with or without the gap-crosser*).</summary>
        public List<Facing> DoorsReaching(int island, bool gapCrosser)
        {
            int[] c = Components(gapCrosser);
            var doors = new List<Facing>();
            for (int f = 0; f < 4; f++)
                if (doorIslands[f] >= 0 && c[doorIslands[f]] == c[island]) doors.Add((Facing)f);
            return doors;
        }

        /// <summary>
        /// True for an island of a lone doorway (1.26): with two or more doorways on the screen, its islands join no other
        /// doorway even with the gap-crosser* and hold no trial, so walking in by that doorway is a dead end and the player
        /// must go round by another screen.
        /// </summary>
        public bool IsLone(int island)
        {
            if (island < 0 || island >= islands.Length) return false;
            int doors = 0;
            foreach (int d in doorIslands)
                if (d >= 0) doors++;
            if (doors < 2 || DoorsReaching(island, true).Count != 1) return false;
            if (HasTrial && Components(true)[TrialIsland] == Components(true)[island]) return false;
            return true;
        }

        // ------------------------------------------------------------------ The lit network (4.12)

        /// <summary>The layout as the pure solver's design (spots as lattice ids).</summary>
        public IslandDesign Design()
        {
            var d = new IslandDesign { Trial = TrialIsland, Reward = RewardIsland };
            foreach (Vector2Int s in spots) d.Spots.Add(IslandLattice.Spot(s.x, s.y));
            foreach (IslandBridge b in bridges) d.Links.Add((b.A, b.B, b.Broken));
            for (int f = 0; f < 4; f++) d.DoorIslands[f] = doorIslands[f];
            return d;
        }

        /// <summary>The solver over this layout (island middles for its walking distances, the player's base walking speed).</summary>
        public IslandSolver Solver
        {
            get
            {
                if (solver != null) return solver;
                Rect[] land = islands;
                solver = new IslandSolver(Design(), i => (land[i].center.x, land[i].center.y), WalkSpeed);
                return solver;
            }
        }

        /// <summary>The player's base walking speed (PlayerMover's default) the retry cost is measured at.</summary>
        public const float WalkSpeed = 6f * ScreenMath.ContentScale;

        /// <summary>True for a plank of the lit network (a whole plank of the reward island's component): only these light.</summary>
        public bool IsNetworkPlank(int plank) => HasPuzzle && Solver.Bit(plank) >= 0;

        /// <summary>The lit network's planks, in plank order.</summary>
        public IReadOnlyList<int> NetworkPlanks => HasPuzzle ? Solver.Network : (IReadOnlyList<int>)Array.Empty<int>();

        /// <summary>True for an island of the lit network.</summary>
        public bool OnNetwork(int island) => HasPuzzle && Solver.OnNetwork(island);

        /// <summary>What a doorway's player meets (the solver's measure against the tier's targets).</summary>
        public IslandMetrics Measure(Facing side) => Solver.Measure((int)side, f => doorIslands[f] >= 0);

        /// <summary>The tier's targets.</summary>
        public IslandTargets Targets => IslandTargets.For(Tier);

        /// <summary>True when a doorway's island is not on the network (it reaches it over a broken span): the gap doorway.</summary>
        public bool HasGapDoor
        {
            get
            {
                for (int f = 0; f < 4; f++)
                    if (doorIslands[f] >= 0 && !OnNetwork(doorIslands[f])) return true;
                return false;
            }
        }

        /// <summary>
        /// True when every doorway that reaches the network can light all of it (a clean start it can make, the network one
        /// trail): the reward roadblock's region joins the reward island then.
        /// </summary>
        public bool Solvable => solvable ?? (solvable = ComputeSolvable()).Value;

        private bool? solvable;

        private bool ComputeSolvable()
        {
            if (!HasPuzzle || Solver.Network.Count == 0) return false;
            bool any = false;
            for (int f = 0; f < 4; f++)
            {
                if (doorIslands[f] < 0) continue;
                IslandMetrics m = Measure((Facing)f);
                if (!m.Connected) continue;
                if (!m.Solvable) return false;
                any = true;
            }
            return any;
        }

        // ------------------------------------------------------------------ Validation

        private static bool Overlaps(Rect a, Rect b, float eps = 1e-4f) =>
            a.xMin < b.xMax - eps && b.xMin < a.xMax - eps && a.yMin < b.yMax - eps && b.yMin < a.yMax - eps;

        private static float Gap(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Problems with the layout for a screen whose doorways are hasDoorway (empty when all is well): every doorway lands
        /// on its own island, which reaches the doorway (so the arriving player stands on land and can always walk back
        /// out); islands are separate land in the water; every plank joins two spots in line (neighbours, or two apart over
        /// an empty spot) shore to shore, over water only, never crossing another, and a broken one is long enough for its
        /// gap; every island joins some doorway (with the gap-crosser*); with three or more doorways a pair joins over whole
        /// planks and one joins only over a broken span (4.12: no doorway is left alone). A trial island is reached only over
        /// a broken span. With a puzzle, its rules too (PuzzleProblems).
        /// </summary>
        public List<string> Problems(string what, Func<Facing, bool> hasDoorway)
        {
            var problems = new List<string>();
            Rect water = WaterArea;
            int doorways = 0;
            for (int f = 0; f < 4; f++)
            {
                var side = (Facing)f;
                bool door = hasDoorway(side);
                int island = doorIslands[f];
                if (!door)
                {
                    if (island >= 0) problems.Add($"{what}: an island for the closed {side} side");
                    continue;
                }
                doorways++;
                if (island < 0 || island >= islands.Length)
                {
                    problems.Add($"{what}: the {side} doorway lands in the water");
                    continue;
                }
                if (spots[island] != DoorSpot(side)) problems.Add($"{what}: the {side} doorway's island is not on its spot");
                // The island reaches the doorway: it covers the doorway's whole width where it meets the wall.
                Rect r = islands[island];
                Rect doorway = ScreenEdges.DoorwayRect(side);
                bool reaches = side.IsHorizontal()
                    ? (side == Facing.East ? Mathf.Abs(r.xMax - water.xMax) < 1e-4f : Mathf.Abs(r.xMin - water.xMin) < 1e-4f) &&
                      r.yMin <= doorway.yMin + 1e-4f && r.yMax >= doorway.yMax - 1e-4f
                    : (side == Facing.North ? Mathf.Abs(r.yMax - water.yMax) < 1e-4f : Mathf.Abs(r.yMin - water.yMin) < 1e-4f) &&
                      r.xMin <= doorway.xMin + 1e-4f && r.xMax >= doorway.xMax - 1e-4f;
                if (!reaches) problems.Add($"{what}: the {side} doorway's island does not reach the doorway");
                if (!r.Contains(Arrival(side))) problems.Add($"{what}: a player arriving by the {side} doorway is not on its island");
            }
            for (int i = 0; i < islands.Length; i++)
            {
                if (!OnLattice(spots[i])) problems.Add($"{what}: island {i} is off the lattice");
                Rect r = islands[i];
                if (r.xMin < water.xMin - 1e-4f || r.xMax > water.xMax + 1e-4f || r.yMin < water.yMin - 1e-4f || r.yMax > water.yMax + 1e-4f)
                    problems.Add($"{what}: island {i} reaches into a wall");
                if (Mathf.Min(r.width, r.height) < 2f * TimeField.PlayerRadius + 0.2f * ScreenMath.ContentScale) problems.Add($"{what}: island {i} is too small to stand on");
                for (int j = i + 1; j < islands.Length; j++)
                {
                    if (spots[i] == spots[j]) problems.Add($"{what}: islands {i} and {j} share a spot");
                    else if (Gap(r, islands[j]) < MinWater - 1e-4f) problems.Add($"{what}: islands {i} and {j} run together");
                }
            }
            for (int k = 0; k < bridges.Length; k++)
            {
                IslandBridge b = bridges[k];
                if (b.A < 0 || b.B >= islands.Length || b.A == b.B)
                {
                    problems.Add($"{what}: plank {k} is malformed");
                    continue;
                }
                if (!IslandLattice.InLine(IslandLattice.Spot(spots[b.A].x, spots[b.A].y), IslandLattice.Spot(spots[b.B].x, spots[b.B].y), out int middle))
                    problems.Add($"{what}: plank {k} joins islands that are not in line, one or two spots apart");
                else if (middle >= 0 && Array.IndexOf(spots, new Vector2Int(IslandLattice.X(middle), IslandLattice.Y(middle))) >= 0)
                    problems.Add($"{what}: long plank {k} passes over an island");
                if (b.Plank != PlankRect(spots[b.A], spots[b.B])) problems.Add($"{what}: plank {k} does not run shore to shore");
                if (b.Across < 2f * TimeField.PlayerRadius + 0.2f * ScreenMath.ContentScale) problems.Add($"{what}: plank {k} is too narrow");
                if (b.Span < 0.5f * ScreenMath.ContentScale) problems.Add($"{what}: plank {k} spans no real water");
                for (int i = 0; i < islands.Length; i++)
                    if (i != b.A && i != b.B && Overlaps(b.Plank, islands[i])) problems.Add($"{what}: plank {k} runs over island {i}");
                for (int j = k + 1; j < bridges.Length; j++)
                {
                    if (bridges[j].A == b.A && bridges[j].B == b.B) problems.Add($"{what}: planks {k} and {j} are doubled");
                    else if (Overlaps(b.Plank, bridges[j].Plank)) problems.Add($"{what}: planks {k} and {j} cross");
                }
                if (b.Broken && b.Span < GapLength + 2f * MinPlankEnd - 1e-4f) problems.Add($"{what}: broken plank {k} is too short for its gap");
            }
            if (problems.Count > 0) return problems;

            int[] all = Components(true), whole = Components(false);
            for (int i = 0; i < islands.Length; i++)
                if (DoorsReaching(i, true).Count == 0) problems.Add($"{what}: island {i} joins no doorway, even with the gap-crosser*");
            if (doorways >= 3)
            {
                // 4.12: every doorway joins the others (no lone doorway), but one only over a broken span (the gap doorway).
                var doorList = new List<int>();
                for (int f = 0; f < 4; f++)
                    if (doorIslands[f] >= 0) doorList.Add(doorIslands[f]);
                bool needsGap = false, wholePair = false;
                foreach (int a in doorList)
                    foreach (int b in doorList)
                    {
                        if (a == b) continue;
                        if (all[a] == all[b] && whole[a] != whole[b]) needsGap = true;
                        if (whole[a] == whole[b]) wholePair = true;
                    }
                if (!wholePair) problems.Add($"{what}: no two doorways join over whole planks");
                if (!needsGap) problems.Add($"{what}: no doorways join only over a broken span");
            }
            if (doorways >= 2)
                for (int f = 0; f < 4; f++)
                {
                    int a = doorIslands[f];
                    if (a < 0) continue;
                    bool joins = false;
                    for (int g = 0; g < 4; g++)
                        if (g != f && doorIslands[g] >= 0 && all[doorIslands[g]] == all[a]) joins = true;
                    if (!joins) problems.Add($"{what}: the {(Facing)f} doorway joins no other (a lone doorway)");
                }
            if (HasTrial)
            {
                if (TrialIsland >= islands.Length || DoorOf(TrialIsland).HasValue) problems.Add($"{what}: the trial is not on an inner island");
                else if (DoorsReaching(TrialIsland, false).Count > 0) problems.Add($"{what}: the trial island is reached without the gap-crosser*");
            }
            if (HasPuzzle) problems.AddRange(PuzzleProblems(what));
            return problems;
        }

        /// <summary>
        /// The puzzle's rules (4.12): the reward roadblock stands on an inner island of the lit network, the trial island is
        /// off it, and from every doorway the network meets every target of the tier (IslandTargets.Misses: lit from a clean
        /// start the doorway can reach, the tier's planks, parity and P_rand, the crossing cheaper than the reward route).
        /// </summary>
        public List<string> PuzzleProblems(string what)
        {
            var problems = new List<string>();
            if (RewardIsland >= islands.Length || DoorOf(RewardIsland).HasValue) problems.Add($"{what}: the reward roadblock is not on an inner island");
            else if (Solver.Network.Count == 0) problems.Add($"{what}: the reward island has no lit plank");
            if (HasTrial && OnNetwork(TrialIsland)) problems.Add($"{what}: the trial island is on the lit network");
            // Every doorway but one (the gap doorway, with three or more) stands on the lit network (4.12 review).
            int doorways = 0, off = 0;
            for (int f = 0; f < 4; f++)
            {
                if (doorIslands[f] < 0) continue;
                doorways++;
                if (!OnNetwork(doorIslands[f])) off++;
            }
            int wantOff = doorways >= 3 ? 1 : 0;
            if (off != wantOff) problems.Add($"{what}: {off} doorways off the lit network (want {wantOff}: every doorway but the gap one on it)");
            if (problems.Count > 0) return problems;
            bool gap = HasGapDoor;
            for (int f = 0; f < 4; f++)
            {
                if (doorIslands[f] < 0) continue;
                foreach (string miss in Targets.Misses(Measure((Facing)f), gap))
                    problems.Add($"{what}: from the {(Facing)f} doorway ({Tier}), {miss}");
            }
            return problems;
        }

        // ------------------------------------------------------------------ Generation

        /// <summary>The geometry the pure generator works with: a broken span fits between two spots (lattice ids).</summary>
        public static bool CanBreakSpots(int a, int b) =>
            CanBreak(new Vector2Int(IslandLattice.X(a), IslandLattice.Y(a)), new Vector2Int(IslandLattice.X(b), IslandLattice.Y(b)));

        /// <summary>The middle of a spot's island (lattice id).</summary>
        public static (double X, double Y) SpotCentreOf(int spot)
        {
            Vector2 c = IslandRect(new Vector2Int(IslandLattice.X(spot), IslandLattice.Y(spot))).center;
            return (c.x, c.y);
        }

        /// <summary>A layout from the pure design, for a tier.</summary>
        public static IslandLayout FromDesign(IslandDesign d, PuzzleTier tier)
        {
            var list = new List<Vector2Int>();
            foreach (int s in d.Spots) list.Add(new Vector2Int(IslandLattice.X(s), IslandLattice.Y(s)));
            return new IslandLayout(list, d.Links, d.DoorIslands, d.Trial, d.Reward, tier);
        }

        /// <summary>
        /// A seeded island screen of a tier (4.12) for a screen whose doorways are hasDoorway: IslandGenerator's search,
        /// kept only with no Problems. Past the search's budget the tier's hand-checked template stands in (a screen with all
        /// four doorways; FromTemplate, the sweep reports it), so it never returns an unsolved layout. Null when neither fits
        /// (a screen with fewer doorways the search found no design for; MathPlan.Problems reports it).
        /// </summary>
        public static IslandLayout Generate(SeededRng rng, PuzzleTier tier, Func<Facing, bool> hasDoorway, bool trial)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var doors = new bool[4];
            for (int f = 0; f < 4; f++) doors[f] = hasDoorway((Facing)f);
            IslandLayout kept = null;
            IslandGenerator.Generate(rng, tier, doors, trial, CanBreakSpots, SpotCentreOf, d =>
            {
                IslandLayout layout = FromDesign(d, tier);
                if (layout.Problems("generated", hasDoorway).Count > 0) return false;
                kept = layout;
                return true;
            }, out int steps);
            if (kept != null)
            {
                kept.SearchSteps = steps;
                return kept;
            }
            IslandLayout template = Template(tier, hasDoorway, trial);
            if (template == null) return null;
            template.SearchSteps = steps;
            template.FromTemplate = true;
            return template;
        }

        /// <summary>
        /// One hand-checked design per tier (the gap doorway on the east, a trial island behind a broken span), found by the
        /// generator and checked by IslandTests against every target. Format: spots "x,y" | links "a-b" (x: broken) | the
        /// doorway islands N,E,S,W, the trial island and the reward island.
        /// </summary>
        public static readonly string[] Templates =
        {
            "0,0 2,0 0,1 2,1 0,2 1,2 2,2 4,1 1,1 | 0-1 0-2 1-3 2-4 3-6 4-5 5-6 7-3x 3-8x | doors 6,7,1,2 trial 8 reward 3",
            "2,0 3,0 0,1 1,1 2,1 3,1 1,2 2,2 4,1 4,0 1,0 | 0-1 0-4 1-5 2-3 3-4 3-6 4-5 4-7 6-7 8-5x 8-9 3-10x | doors 7,8,0,2 trial 10 reward 3",
            "0,0 1,0 2,0 3,0 0,1 1,1 2,1 3,1 1,2 2,2 3,2 4,1 4,2 4,0 | 0-1 0-4 1-5 2-3 2-6 3-7 4-5 5-6 5-8 6-7 6-9 7-10 8-9 11-7x 11-12 3-13x | doors 9,11,2,4 trial 13 reward 10",
        };

        /// <summary>A design from its Templates string.</summary>
        public static IslandDesign ParseDesign(string text)
        {
            string[] parts = text.Split('|');
            var d = new IslandDesign();
            foreach (string spot in parts[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] xy = spot.Split(',');
                d.Spots.Add(IslandLattice.Spot(int.Parse(xy[0]), int.Parse(xy[1])));
            }
            foreach (string link in parts[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                bool broken = link.EndsWith("x");
                string[] ab = link.TrimEnd('x').Split('-');
                d.Links.Add((int.Parse(ab[0]), int.Parse(ab[1]), broken));
            }
            string[] words = parts[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string[] doors = words[1].Split(',');
            for (int f = 0; f < 4; f++) d.DoorIslands[f] = int.Parse(doors[f]);
            d.Trial = int.Parse(words[3]);
            d.Reward = int.Parse(words[5]);
            return d;
        }

        /// <summary>The design without its trial island (and the span to it).</summary>
        public static IslandDesign WithoutTrial(IslandDesign d)
        {
            if (d.Trial < 0) return d;
            var e = new IslandDesign();
            int Map(int i) => i < d.Trial ? i : i - 1;
            for (int i = 0; i < d.Spots.Count; i++)
                if (i != d.Trial) e.Spots.Add(d.Spots[i]);
            foreach ((int a, int b, bool broken) in d.Links)
                if (a != d.Trial && b != d.Trial) e.Links.Add((Map(a), Map(b), broken));
            for (int f = 0; f < 4; f++) e.DoorIslands[f] = d.DoorIslands[f] < 0 ? -1 : Map(d.DoorIslands[f]);
            e.Reward = Map(d.Reward);
            return e;
        }

        /// <summary>The tier's template for a screen with all four doorways (with the trial when trial), else null.</summary>
        public static IslandLayout Template(PuzzleTier tier, Func<Facing, bool> hasDoorway, bool trial)
        {
            for (int f = 0; f < 4; f++)
                if (!hasDoorway((Facing)f)) return null;
            IslandDesign d = ParseDesign(Templates[(int)tier]);
            return FromDesign(trial ? d : WithoutTrial(d), tier);
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("islands");
            foreach (Vector2Int s in spots) sb.Append(' ').Append(s.x).Append(',').Append(s.y);
            sb.Append(" doors");
            foreach (int d in doorIslands) sb.Append(' ').Append(d);
            sb.Append(" planks");
            foreach (IslandBridge b in bridges) sb.Append(' ').Append(b.A).Append('-').Append(b.B).Append(b.Broken ? "x" : "");
            if (HasTrial) sb.Append(" trial ").Append(TrialIsland);
            if (HasPuzzle) sb.Append(" reward ").Append(RewardIsland).Append(' ').Append(Tier).Append(FromTemplate ? " template" : "");
            return sb.ToString();
        }

        public override string ToString() =>
            $"{islands.Length} islands, {bridges.Length} planks{(HasBroken ? " (some broken)" : "")}{(HasTrial ? ", a trial island" : "")}" +
            (HasPuzzle ? $", {NetworkPlanks.Count} to light ({Tier})" : "");
    }
}
