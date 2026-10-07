using System;
using System.Collections.Generic;
using System.Text;
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

        public IslandBridge AsBroken(bool broken) => new IslandBridge(A, B, Plank, broken, Vertical);

        /// <summary>The plank's width across (what the player walks on).</summary>
        public float Across => Vertical ? Plank.width : Plank.height;
    }

    /// <summary>
    /// A Math island screen as pure data (1.25), local to the screen centre: the whole screen inside its edge walls is
    /// solid water dotted with islands (axis-aligned land on a 5 x 3 lattice of spots), joined here and there by planks.
    /// Every doorway lands on its own island (DoorIsland: the island reaching from the doorway into the screen), so the
    /// player can always walk back out the way it came. Planks only join lattice neighbours, so they never cross and are
    /// spread over the whole screen. Some planks are broken spans: a one-square gap only the gap-crosser* spans.
    /// The graph is deliberately not fully connected (Generate): two doorways are joined by whole planks; a third reaches
    /// them only over a broken span (it needs the gap-crosser*); the fourth joins nothing else on the screen (the player
    /// must come round through another screen). The trial screen's trial island hangs off the joined islands by a broken
    /// span. Problems checks all of it; Components gives what joins with or without the gap-crosser*.
    /// </summary>
    public sealed class IslandLayout
    {
        /// <summary>The lattice: island spots' x (five columns) and y (three rows), local to the screen centre.</summary>
        public static readonly float[] Columns = { -5.6f, -2.8f, 0f, 2.8f, 5.6f };
        public static readonly float[] Rows = { -2.7f, 0f, 2.7f };

        /// <summary>An inner island's size (a doorway's island is bigger: it reaches the doorway).</summary>
        public const float InnerWidth = 1.4f, InnerHeight = 1.3f;

        /// <summary>A doorway island's half-width across its doorway (a little over the doorway's) and its depth into the screen.</summary>
        public const float DoorIslandHalfWidth = 1.3f, DoorIslandDepth = 1.9f, SideDoorIslandDepth = 2f;

        /// <summary>A plank's width (the player is 0.8 across).</summary>
        public const float PlankWidth = 1.2f;

        /// <summary>A broken span's gap: one floor square, what the gap-crosser* spans.</summary>
        public const float GapLength = 1f;

        /// <summary>Plank left on each side of a gap, at least.</summary>
        public const float MinPlankEnd = 0.2f;

        /// <summary>Open water kept between two islands that no plank joins (so they read as separate land).</summary>
        public const float MinWater = 0.6f;

        /// <summary>Attempts at a layout before giving up.</summary>
        private const int Attempts = 64;

        private readonly Vector2Int[] spots;
        private readonly Rect[] islands;
        private readonly IslandBridge[] bridges;
        private readonly int[] doorIslands;

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

        public IslandLayout(IEnumerable<Vector2Int> islandSpots, IEnumerable<(int a, int b, bool broken)> links, int[] doorIslandBySide,
            int trialIsland = -1)
        {
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
                    case Facing.North: return Rect.MinMaxRect(-w, water.yMax - DoorIslandDepth, w, water.yMax);
                    case Facing.South: return Rect.MinMaxRect(-w, water.yMin, w, water.yMin + DoorIslandDepth);
                    case Facing.East: return Rect.MinMaxRect(water.xMax - SideDoorIslandDepth, -w, water.xMax, w);
                    default: return Rect.MinMaxRect(water.xMin, -w, water.xMin + SideDoorIslandDepth, w);
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

        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        /// <summary>The island a point (local) stands on, else -1 (water, or a plank).</summary>
        public int IslandAt(Vector2 local)
        {
            for (int i = 0; i < islands.Length; i++)
                if (islands[i].Contains(local)) return i;
            return -1;
        }

        /// <summary>Where a doorway's arriving player stands (local): just inside the wall, on the doorway's line.</summary>
        public static Vector2 Arrival(Facing side) =>
            Vector2.Scale(side.ToVector(), ScreenEdges.Half) - (Vector2)side.ToVector() * (ScreenEdges.EdgeWallThickness + 0.3f);

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
        /// out); islands are separate land in the water; every plank joins two lattice neighbours shore to shore, over water
        /// only, and a broken one is long enough for its gap; every island joins some doorway (with the gap-crosser*); and the
        /// graph is not fully connected: with three or more doorways, a pair of doorways joins only over a broken span and
        /// a doorway joins no other even with the gap-crosser*. A trial island is reached only over a broken span.
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
                if (Mathf.Min(r.width, r.height) < 2f * TimeField.PlayerRadius + 0.2f) problems.Add($"{what}: island {i} is too small to stand on");
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
                Vector2Int d = spots[b.B] - spots[b.A];
                if (Mathf.Abs(d.x) + Mathf.Abs(d.y) != 1) problems.Add($"{what}: plank {k} joins islands that are not neighbours");
                if (b.Plank != PlankRect(spots[b.A], spots[b.B])) problems.Add($"{what}: plank {k} does not run shore to shore");
                if (b.Across < 2f * TimeField.PlayerRadius + 0.2f) problems.Add($"{what}: plank {k} is too narrow");
                if (b.Span < 0.5f) problems.Add($"{what}: plank {k} spans no real water");
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
                var doorList = new List<int>();
                for (int f = 0; f < 4; f++)
                    if (doorIslands[f] >= 0) doorList.Add(doorIslands[f]);
                bool needsGap = false, alone = false, wholePair = false;
                foreach (int a in doorList)
                {
                    bool joinsAny = false;
                    foreach (int b in doorList)
                    {
                        if (a == b) continue;
                        if (all[a] == all[b]) joinsAny = true;
                        if (all[a] == all[b] && whole[a] != whole[b]) needsGap = true;
                        if (whole[a] == whole[b]) wholePair = true;
                    }
                    if (!joinsAny) alone = true;
                }
                if (!wholePair) problems.Add($"{what}: no two doorways join over whole planks");
                if (!needsGap) problems.Add($"{what}: no doorways join only over a broken span");
                if (!alone) problems.Add($"{what}: every doorway joins another (the screen is fully connected)");
            }
            if (HasTrial)
            {
                if (TrialIsland >= islands.Length || DoorOf(TrialIsland).HasValue) problems.Add($"{what}: the trial is not on an inner island");
                else if (DoorsReaching(TrialIsland, false).Count > 0) problems.Add($"{what}: the trial island is reached without the gap-crosser*");
            }
            return problems;
        }

        // ------------------------------------------------------------------ Generation

        /// <summary>
        /// A seeded island screen for a screen whose doorways are hasDoorway, or null when none was found. The doorways are
        /// shuffled into roles: the first two are joined over whole planks (a route of islands between them, mostly by way of
        /// a seeded inner spot so routes wander over the whole screen); the third reaches that network only over a broken
        /// span (its own route, one long link of it broken); the fourth stands alone, sometimes with a lookout island or two.
        /// With trial, a trial island hangs off the joined network by a broken span. With fewer doorways the roles are dealt
        /// in the same order.
        /// </summary>
        public static IslandLayout Generate(SeededRng rng, Func<Facing, bool> hasDoorway, bool trial)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                IslandLayout layout = TryGenerate(rng, hasDoorway, trial);
                if (layout != null && layout.Problems("generated", hasDoorway).Count == 0) return layout;
            }
            return null;
        }

        private static IslandLayout TryGenerate(SeededRng rng, Func<Facing, bool> hasDoorway, bool trial)
        {
            var sides = new List<Facing>();
            for (int f = 0; f < 4; f++)
                if (hasDoorway((Facing)f)) sides.Add((Facing)f);
            if (sides.Count == 0) return null;
            rng.Shuffle(sides);

            var spotList = new List<Vector2Int>();
            var index = new Dictionary<Vector2Int, int>();
            var links = new List<(int a, int b, bool broken)>();
            var doorIslands = new[] { -1, -1, -1, -1 };
            int Add(Vector2Int s)
            {
                if (index.TryGetValue(s, out int i)) return i;
                index[s] = spotList.Count;
                spotList.Add(s);
                return spotList.Count - 1;
            }
            foreach (Facing side in sides) doorIslands[(int)side] = Add(DoorSpot(side));

            // Spots a path may not pass: other doorways' islands (except its own ends).
            var doorSpots = new HashSet<Vector2Int>();
            foreach (Facing side in sides) doorSpots.Add(DoorSpot(side));

            var joined = new HashSet<Vector2Int> { DoorSpot(sides[0]) };
            if (sides.Count >= 2)
            {
                // 1. Two doorways joined over whole planks.
                Vector2Int goal = DoorSpot(sides[1]);
                List<Vector2Int> path = Route(rng, DoorSpot(sides[0]), s => s == goal, s => !doorSpots.Contains(s) || s == goal, index);
                if (path == null) return null;
                for (int k = 0; k + 1 < path.Count; k++) links.Add((Add(path[k]), Add(path[k + 1]), false));
                foreach (Vector2Int s in path) joined.Add(s);
            }
            if (sides.Count >= 3)
            {
                // 2. A third doorway reaches them only over a broken span.
                Vector2Int start = DoorSpot(sides[2]);
                List<Vector2Int> path = Route(rng, start, joined.Contains, s => joined.Contains(s) || (!doorSpots.Contains(s) && !index.ContainsKey(s)), index);
                if (path == null) return null;
                var breakable = new List<int>();
                for (int k = 0; k + 1 < path.Count; k++)
                    if (CanBreak(path[k], path[k + 1])) breakable.Add(k);
                if (breakable.Count == 0) return null;
                int broken = breakable[rng.NextInt(breakable.Count)];
                for (int k = 0; k + 1 < path.Count; k++) links.Add((Add(path[k]), Add(path[k + 1]), k == broken));
            }
            if (sides.Count >= 4 && rng.NextInt(3) != 0)
            {
                // 3. The lone doorway, sometimes with a lookout island of its own.
                Vector2Int lone = DoorSpot(sides[3]);
                var free = new List<Vector2Int>();
                foreach (Vector2Int step in Steps)
                {
                    Vector2Int s = lone + step;
                    if (OnLattice(s) && !index.ContainsKey(s) && !doorSpots.Contains(s) && Clear(s, index)) free.Add(s);
                }
                if (free.Count > 0)
                {
                    Vector2Int lookout = free[rng.NextInt(free.Count)];
                    links.Add((doorIslands[(int)sides[3]], Add(lookout), false));
                    // Sometimes one more island out along the lookout's row or column.
                    Vector2Int further = lookout + (lookout - lone);
                    if (rng.NextInt(2) == 0 && OnLattice(further) && !index.ContainsKey(further) && !doorSpots.Contains(further) && Clear(further, index))
                        links.Add((index[lookout], Add(further), false));
                }
            }
            int trialIsland = -1;
            if (trial)
            {
                // 4. The trial island: off the joined network, over a broken span.
                var options = new List<(Vector2Int from, Vector2Int to)>();
                foreach (Vector2Int j in joined)
                    foreach (Vector2Int step in Steps)
                    {
                        Vector2Int s = j + step;
                        if (OnLattice(s) && !index.ContainsKey(s) && !doorSpots.Contains(s) && CanBreak(j, s) && Clear(s, index))
                            options.Add((j, s));
                    }
                if (options.Count == 0) return null;
                (Vector2Int from, Vector2Int to) = options[rng.NextInt(options.Count)];
                trialIsland = Add(to);
                links.Add((index[from], trialIsland, true));
            }
            return new IslandLayout(spotList, links, doorIslands, trialIsland);
        }

        /// <summary>
        /// A route from start to a goal spot: two times in three by way of a free inner spot (the first, in a seeded order,
        /// that two legs sharing no spot can reach), else, or when none can, a shortest one.
        /// </summary>
        private static List<Vector2Int> Route(SeededRng rng, Vector2Int start, Func<Vector2Int, bool> goal, Func<Vector2Int, bool> passable,
            Dictionary<Vector2Int, int> placed)
        {
            if (rng.NextInt(3) != 0)
            {
                var waypoints = new List<Vector2Int>();
                for (int x = 0; x < Columns.Length; x++)
                    for (int y = 0; y < Rows.Length; y++)
                    {
                        var s = new Vector2Int(x, y);
                        if (s != start && !goal(s) && !placed.ContainsKey(s) && DoorAt(s) == null && passable(s)) waypoints.Add(s);
                    }
                rng.Shuffle(waypoints);
                foreach (Vector2Int via in waypoints)
                {
                    List<Vector2Int> first = ShortestPath(rng, start, s => s == via, s => passable(s) && !goal(s), placed);
                    if (first != null)
                    {
                        var used = new HashSet<Vector2Int>(first);
                        used.Remove(via);
                        List<Vector2Int> second = ShortestPath(rng, via, goal, s => passable(s) && !used.Contains(s), placed);
                        if (second != null)
                        {
                            first.AddRange(second.GetRange(1, second.Count - 1));
                            return first;
                        }
                    }
                }
            }
            return ShortestPath(rng, start, goal, passable, placed);
        }

        /// <summary>True when a new island on the spot keeps MinWater from every island already placed.</summary>
        private static bool Clear(Vector2Int spot, Dictionary<Vector2Int, int> placed)
        {
            Rect r = IslandRect(spot);
            foreach (Vector2Int s in placed.Keys)
                if (Gap(r, IslandRect(s)) < MinWater - 1e-4f) return false;
            return true;
        }

        /// <summary>
        /// A shortest lattice path from start to a goal spot through passable spots (new spots must keep clear of placed
        /// islands), random among the shortest (neighbours tried in a seeded order); null when there is none.
        /// </summary>
        private static List<Vector2Int> ShortestPath(SeededRng rng, Vector2Int start, Func<Vector2Int, bool> goal,
            Func<Vector2Int, bool> passable, Dictionary<Vector2Int, int> placed)
        {
            var previous = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int at = queue.Dequeue();
                if (at != start && goal(at))
                {
                    var path = new List<Vector2Int> { at };
                    while (at != start)
                    {
                        at = previous[at];
                        path.Add(at);
                    }
                    path.Reverse();
                    return path;
                }
                var order = new List<Vector2Int>(Steps);
                rng.Shuffle(order);
                foreach (Vector2Int step in order)
                {
                    Vector2Int next = at + step;
                    if (!OnLattice(next) || previous.ContainsKey(next) || !passable(next)) continue;
                    if (!placed.ContainsKey(next) && !goal(next) && !Clear(next, placed)) continue;
                    previous[next] = at;
                    queue.Enqueue(next);
                }
            }
            return null;
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
            return sb.ToString();
        }

        public override string ToString() =>
            $"{islands.Length} islands, {bridges.Length} planks{(HasBroken ? " (some broken)" : "")}{(HasTrial ? ", a trial island" : "")}";
    }
}
