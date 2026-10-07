using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// One island-and-bridge layout as pure data (positions local to its screen centre): round islands joined by plank
    /// bridges, with an entry island (the flag) where every walk starts. The puzzle is to walk every bridge exactly once,
    /// starting on the entry; Problems checks a layout can be solved that way (an Eulerian path from the entry: the
    /// bridges are connected and either no island or exactly two islands, one of them the entry, have an odd number of
    /// bridges) and that it is well formed (no repeated or crossing bridges, islands apart, no island without a bridge).
    /// Generate makes one on a lattice by walking a seeded trail of unused lattice links from a random start, so it is
    /// solvable by construction; the validator still checks it independently.
    /// Since 1.24 the islands are real, separate land pads with water between them and the bridges are long enough to read
    /// as bridges (Spacing). A bridge can be broken (Broken): a gap of BrokenGap (one floor square) in the middle of its
    /// plank, which only counts once the gap-crosser* has spanned it (BridgeWalk.Mend).
    /// </summary>
    public sealed class BridgeLayout
    {
        /// <summary>Distance between neighbouring lattice islands (so 1.3 of water between two islands' shores).</summary>
        public const float Spacing = 2.4f;

        /// <summary>The tighter lattice a layout falls back to on a crowded screen.</summary>
        public const float CompactSpacing = 2.0f;

        /// <summary>Drawn island radius.</summary>
        public const float IslandRadius = 0.55f;

        /// <summary>The player's centre within this distance of an island's centre stands on it.</summary>
        public const float OnIslandRadius = 0.6f;

        /// <summary>The gap in a broken bridge: one floor square, what the gap-crosser* spans.</summary>
        public const float BrokenGap = 0.9f;

        /// <summary>Plank left on each side of a broken bridge's gap, at least.</summary>
        public const float MinPlankEnd = 0.2f;

        /// <summary>Drawn plank width.</summary>
        public const float BridgeWidth = 0.42f;

        /// <summary>The player's centre within this distance of a bridge's centre line stands on it.</summary>
        public const float OnBridgeHalfWidth = 0.35f;

        /// <summary>Trail attempts per generation before giving up.</summary>
        private const int Attempts = 96;

        private readonly Vector2[] islands;
        private readonly Vector2Int[] bridges;
        private readonly int[] broken;

        public IReadOnlyList<Vector2> Islands => islands;

        /// <summary>Bridges as island index pairs (x &lt; y), in a canonical order that gives no walking order away.</summary>
        public IReadOnlyList<Vector2Int> Bridges => bridges;

        /// <summary>The island every walk starts on (marked with a flag).</summary>
        public int Entry { get; }

        public BridgeLayout(IEnumerable<Vector2> islandPositions, IEnumerable<Vector2Int> bridgeList, int entry)
        {
            islands = new List<Vector2>(islandPositions ?? Array.Empty<Vector2>()).ToArray();
            var list = new List<Vector2Int>();
            foreach (Vector2Int b in bridgeList ?? Array.Empty<Vector2Int>())
                list.Add(b.x <= b.y ? b : new Vector2Int(b.y, b.x));
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            bridges = list.ToArray();
            broken = Array.Empty<int>();
            Entry = entry;
        }

        private BridgeLayout(Vector2[] islands, Vector2Int[] bridges, int entry, int[] broken)
        {
            this.islands = islands;
            this.bridges = bridges;
            this.broken = broken;
            Entry = entry;
        }

        /// <summary>The broken bridges (indices into Bridges), in order.</summary>
        public IReadOnlyList<int> Broken => broken;

        public bool HasBroken => broken.Length > 0;

        public bool IsBroken(int bridge) => Array.IndexOf(broken, bridge) >= 0;

        /// <summary>The same layout with one more bridge broken (a gap the gap-crosser* spans in the middle of its plank).</summary>
        public BridgeLayout WithBroken(int bridge)
        {
            if (IsBroken(bridge)) return this;
            var list = new List<int>(broken) { bridge };
            list.Sort();
            return new BridgeLayout(islands, bridges, Entry, list.ToArray());
        }

        /// <summary>The open water a bridge spans: its length between the two islands' shores.</summary>
        public float Span(int bridge)
        {
            Ends(bridge, out Vector2 a, out Vector2 b);
            return Vector2.Distance(a, b) - 2f * IslandRadius;
        }

        /// <summary>True when a bridge is long enough to break: its gap and a little plank on both sides fit over its water.</summary>
        public bool CanBreak(int bridge) => Span(bridge) >= BrokenGap + 2f * MinPlankEnd - 1e-4f;

        public int BridgeCount => bridges.Length;

        /// <summary>The same layout moved by offset.</summary>
        public BridgeLayout Translated(Vector2 offset)
        {
            var moved = new Vector2[islands.Length];
            for (int i = 0; i < islands.Length; i++) moved[i] = islands[i] + offset;
            return new BridgeLayout(moved, bridges, Entry, broken);
        }

        /// <summary>The bridge's ends.</summary>
        public void Ends(int bridge, out Vector2 a, out Vector2 b)
        {
            a = islands[bridges[bridge].x];
            b = islands[bridges[bridge].y];
        }

        /// <summary>True when the bridge joins islands a and b (either way round).</summary>
        public bool Joins(int bridge, int a, int b)
        {
            Vector2Int e = bridges[bridge];
            return (e.x == a && e.y == b) || (e.x == b && e.y == a);
        }

        /// <summary>The bridge joining islands a and b, else -1.</summary>
        public int BridgeBetween(int a, int b)
        {
            for (int i = 0; i < bridges.Length; i++)
                if (Joins(i, a, b)) return i;
            return -1;
        }

        public bool Touches(int bridge, int island) => bridges[bridge].x == island || bridges[bridge].y == island;

        /// <summary>The island at the other end of a bridge from island.</summary>
        public int Other(int bridge, int island) => bridges[bridge].x == island ? bridges[bridge].y : bridges[bridge].x;

        public int Degree(int island)
        {
            int d = 0;
            foreach (Vector2Int b in bridges)
                if (b.x == island || b.y == island) d++;
            return d;
        }

        /// <summary>Every island and bridge, as a bounding rect (islands' full discs included).</summary>
        public Rect Bounds
        {
            get
            {
                if (islands.Length == 0) return new Rect();
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                foreach (Vector2 p in islands)
                {
                    x0 = Mathf.Min(x0, p.x);
                    y0 = Mathf.Min(y0, p.y);
                    x1 = Mathf.Max(x1, p.x);
                    y1 = Mathf.Max(y1, p.y);
                }
                return Rect.MinMaxRect(x0 - IslandRadius, y0 - IslandRadius, x1 + IslandRadius, y1 + IslandRadius);
            }
        }

        public Vector2 Centre => Bounds.center;

        /// <summary>The bounding rect of the island centres.</summary>
        public Rect IslandCentreBounds
        {
            get
            {
                Rect b = Bounds;
                return Rect.MinMaxRect(b.xMin + IslandRadius, b.yMin + IslandRadius, b.xMax - IslandRadius, b.yMax - IslandRadius);
            }
        }

        /// <summary>The island a point stands on (within OnIslandRadius), else -1.</summary>
        public int IslandAt(Vector2 p)
        {
            int best = -1;
            float bestD = OnIslandRadius;
            for (int i = 0; i < islands.Length; i++)
            {
                float d = Vector2.Distance(p, islands[i]);
                if (d <= bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The bridges a point stands on (within OnBridgeHalfWidth of the centre line).</summary>
        public List<int> BridgesAt(Vector2 p)
        {
            var list = new List<int>();
            for (int i = 0; i < bridges.Length; i++)
            {
                Ends(i, out Vector2 a, out Vector2 b);
                if (DistanceToSegment(p, a, b) <= OnBridgeHalfWidth) list.Add(i);
            }
            return list;
        }

        /// <summary>Points along every bridge (every step), for clearance checks and marks.</summary>
        public List<Vector2> BridgeSamples(float step)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < bridges.Length; i++)
            {
                Ends(i, out Vector2 a, out Vector2 b);
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                for (int k = 0; k <= n; k++) points.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            return points;
        }

        // ------------------------------------------------------------------ Validation

        /// <summary>
        /// True when every bridge can be walked exactly once in one walk starting on start: the bridges are connected
        /// (all reachable from start) and either no island or exactly two islands, start among them, have odd degree.
        /// </summary>
        public bool HasWalkFrom(int start)
        {
            if (start < 0 || start >= islands.Length || bridges.Length == 0) return false;
            if (Degree(start) == 0) return false;
            // Connected: every bridge reachable from start.
            var seen = new bool[islands.Length];
            var queue = new Queue<int>();
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                foreach (Vector2Int b in bridges)
                {
                    int next = b.x == i ? b.y : b.y == i ? b.x : -1;
                    if (next < 0 || seen[next]) continue;
                    seen[next] = true;
                    queue.Enqueue(next);
                }
            }
            foreach (Vector2Int b in bridges)
                if (!seen[b.x] || !seen[b.y]) return false;
            int odd = 0;
            for (int i = 0; i < islands.Length; i++)
                if (Degree(i) % 2 == 1) odd++;
            if (odd == 0) return true;
            return odd == 2 && Degree(start) % 2 == 1;
        }

        /// <summary>
        /// One walk crossing every bridge exactly once from the entry, as the islands visited in order (Hierholzer's
        /// construction); null when there is none (HasWalkFrom is false). For hints, tools and tests.
        /// </summary>
        public List<int> Solve()
        {
            if (!HasWalkFrom(Entry)) return null;
            var used = new bool[bridges.Length];
            var stack = new Stack<int>();
            var path = new List<int>();
            stack.Push(Entry);
            while (stack.Count > 0)
            {
                int v = stack.Peek();
                int next = -1;
                for (int b = 0; b < bridges.Length; b++)
                    if (!used[b] && Touches(b, v))
                    {
                        next = b;
                        break;
                    }
                if (next < 0)
                {
                    path.Add(stack.Pop());
                    continue;
                }
                used[next] = true;
                stack.Push(Other(next, v));
            }
            path.Reverse();
            return path;
        }

        /// <summary>Problems with the layout (empty when it is a well-formed, solvable layout).</summary>
        public List<string> Problems(string what)
        {
            var problems = new List<string>();
            if (islands.Length < 2) problems.Add($"{what}: fewer than two islands");
            if (Entry < 0 || Entry >= islands.Length)
            {
                problems.Add($"{what}: no entry island");
                return problems;
            }
            for (int i = 0; i < bridges.Length; i++)
            {
                Vector2Int b = bridges[i];
                if (b.x < 0 || b.y >= islands.Length || b.x == b.y) problems.Add($"{what}: bridge {i} is malformed");
                for (int j = i + 1; j < bridges.Length; j++)
                    if (bridges[j] == b) problems.Add($"{what}: bridge {i} is doubled");
            }
            if (problems.Count > 0) return problems;
            for (int i = 0; i < islands.Length; i++)
            {
                if (Degree(i) == 0) problems.Add($"{what}: island {i} has no bridge");
                for (int j = i + 1; j < islands.Length; j++)
                    if (Vector2.Distance(islands[i], islands[j]) < 2f * OnIslandRadius + 0.1f)
                        problems.Add($"{what}: islands {i} and {j} overlap");
            }
            for (int i = 0; i < bridges.Length; i++)
            {
                Ends(i, out Vector2 a, out Vector2 b);
                for (int k = 0; k < islands.Length; k++)
                {
                    if (Touches(i, k)) continue;
                    if (DistanceToSegment(islands[k], a, b) < OnIslandRadius + OnBridgeHalfWidth)
                        problems.Add($"{what}: bridge {i} runs over island {k}");
                }
                for (int j = i + 1; j < bridges.Length; j++)
                {
                    Vector2Int bi = bridges[i], bj = bridges[j];
                    if (bi.x == bj.x || bi.x == bj.y || bi.y == bj.x || bi.y == bj.y) continue;
                    Ends(j, out Vector2 c, out Vector2 d);
                    if (SegmentsCross(a, b, c, d)) problems.Add($"{what}: bridges {i} and {j} cross");
                }
            }
            foreach (int b in broken)
            {
                if (b < 0 || b >= bridges.Length) problems.Add($"{what}: broken bridge {b} does not exist");
                else if (!CanBreak(b)) problems.Add($"{what}: broken bridge {b} is too short for its gap");
            }
            if (!HasWalkFrom(Entry)) problems.Add($"{what}: no walk from the flag crosses every bridge exactly once");
            return problems;
        }

        // ------------------------------------------------------------------ Generation

        /// <summary>
        /// A layout on a cols x rows lattice (spacing apart, centred on the origin) with exactly bridgeCount bridges: a
        /// seeded trail of unused lattice links from a random start (see GenerateOn). Null when no trail of that length was
        /// found.
        /// </summary>
        public static BridgeLayout Generate(int cols, int rows, int bridgeCount, SeededRng rng, float spacing = Spacing)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var points = new List<Vector2>();
            var links = new List<Vector2Int>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int v = r * cols + c;
                    points.Add(new Vector2((c - (cols - 1) / 2f) * spacing, (r - (rows - 1) / 2f) * spacing));
                    if (c + 1 < cols) links.Add(new Vector2Int(v, v + 1));
                    if (r + 1 < rows) links.Add(new Vector2Int(v, v + cols));
                }
            return GenerateOn(points, links, bridgeCount, rng);
        }

        /// <summary>
        /// A layout over given island spots and the links allowed between them (links must not cross): a seeded trail of
        /// exactly bridgeCount unused links from a random start (so it is solvable from that start, the entry). With five
        /// or more bridges it prefers a layout with a junction (an island of three or more bridges), so it is a real
        /// choice and not a single path. Only the spots the trail uses become islands. Null when no trail of that length
        /// was found.
        /// </summary>
        public static BridgeLayout GenerateOn(IReadOnlyList<Vector2> points, IReadOnlyList<Vector2Int> links, int bridgeCount,
            SeededRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            int vertices = points.Count;
            if (vertices == 0 || bridgeCount < 1 || bridgeCount > links.Count) return null;

            List<int> fallback = null;
            int fallbackStart = -1;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                int start = rng.NextInt(vertices);
                var used = new bool[links.Count];
                var trail = new List<int>();
                int at = start;
                while (trail.Count < bridgeCount)
                {
                    var options = new List<int>();
                    for (int k = 0; k < links.Count; k++)
                        if (!used[k] && (links[k].x == at || links[k].y == at)) options.Add(k);
                    if (options.Count == 0) break;
                    int pick = options[rng.NextInt(options.Count)];
                    used[pick] = true;
                    trail.Add(pick);
                    at = links[pick].x == at ? links[pick].y : links[pick].x;
                }
                if (trail.Count < bridgeCount) continue;
                if (bridgeCount < 5 || HasJunction(trail, links, vertices)) return Build(points, links, trail, start);
                if (fallback == null)
                {
                    fallback = trail;
                    fallbackStart = start;
                }
            }
            return fallback != null ? Build(points, links, fallback, fallbackStart) : null;
        }

        private static bool HasJunction(List<int> trail, IReadOnlyList<Vector2Int> links, int vertices)
        {
            var degree = new int[vertices];
            foreach (int k in trail)
            {
                degree[links[k].x]++;
                degree[links[k].y]++;
            }
            foreach (int d in degree)
                if (d >= 3) return true;
            return false;
        }

        private static BridgeLayout Build(IReadOnlyList<Vector2> points, IReadOnlyList<Vector2Int> links, List<int> trail, int start)
        {
            var index = new Dictionary<int, int>();
            var verts = new List<int>();
            foreach (int k in trail)
            {
                verts.Add(links[k].x);
                verts.Add(links[k].y);
            }
            verts.Sort();
            var positions = new List<Vector2>();
            foreach (int v in verts)
            {
                if (index.ContainsKey(v)) continue;
                index[v] = positions.Count;
                positions.Add(points[v]);
            }
            var bridgeList = new List<Vector2Int>();
            foreach (int k in trail) bridgeList.Add(new Vector2Int(index[links[k].x], index[links[k].y]));
            return new BridgeLayout(positions, bridgeList, index[start]);
        }

        // ------------------------------------------------------------------ Geometry

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>True when segments ab and cd intersect (touching counts).</summary>
        public static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(d - c, a - c), d2 = Cross(d - c, b - c), d3 = Cross(b - a, c - a), d4 = Cross(b - a, d - a);
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true;
            const float eps = 1e-5f;
            return (Mathf.Abs(d1) < eps && DistanceToSegment(a, c, d) < eps) || (Mathf.Abs(d2) < eps && DistanceToSegment(b, c, d) < eps) ||
                   (Mathf.Abs(d3) < eps && DistanceToSegment(c, a, b) < eps) || (Mathf.Abs(d4) < eps && DistanceToSegment(d, a, b) < eps);
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("entry ").Append(Entry).Append(" islands");
            foreach (Vector2 p in islands) sb.Append(' ').Append(p.ToString("0.00"));
            sb.Append(" bridges");
            foreach (Vector2Int b in bridges) sb.Append(' ').Append(b.x).Append('-').Append(b.y);
            if (broken.Length > 0)
            {
                sb.Append(" broken");
                foreach (int b in broken) sb.Append(' ').Append(b);
            }
            return sb.ToString();
        }

        public override string ToString() =>
            $"{islands.Length} islands, {bridges.Length} bridges{(broken.Length > 0 ? $" ({broken.Length} broken)" : "")}, entry {Entry}";
    }

    /// <summary>
    /// The walk over a BridgeLayout, as pure state fed with the player's position (local to the layout's screen centre).
    /// Nothing counts until the player steps onto the entry island (Started). From then on, reaching another island along
    /// a bridge from the current island crosses that bridge (Crossed); crossing every bridge solves it (Solved, for good).
    /// Reaching an island along a bridge already crossed resets every bridge (CrossedTwice), and reaching another island
    /// without walking a bridge to it (cutting across the water: no sample on a joining bridge's span) resets them too
    /// (OffTrail; silent while no bridge has been crossed yet, since there is nothing to reset). After
    /// a reset the walk starts again from the entry (at once, if the player is standing on it). Stepping into the water
    /// on its own does nothing. A broken bridge (BridgeLayout.Broken) does not count until it is mended (Mend, when the
    /// gap-crosser* spans its gap): reaching the far island over it resets every bridge (Broken), like cutting across.
    /// </summary>
    public sealed class BridgeWalk
    {
        public enum Result { None, Started, Crossed, Solved, CrossedTwice, OffTrail, Broken }

        private readonly BridgeLayout layout;
        private readonly bool[] crossed;
        private readonly bool[] mended;
        private int lastIsland = -1;
        private int via = -1;
        private Vector2? previous;

        /// <summary>
        /// A bridge between the current island and the next counts without a sample on its plank only when the sample
        /// before the arrival lay this close to its centre line (a short plank crossed between two samples).
        /// </summary>
        public const float NearBridge = BridgeLayout.OnBridgeHalfWidth + 0.15f;

        public BridgeWalk(BridgeLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            crossed = new bool[layout.BridgeCount];
            mended = new bool[layout.BridgeCount];
        }

        /// <summary>True while a bridge is broken and not yet mended.</summary>
        public bool IsGap(int bridge) => layout.IsBroken(bridge) && !mended[bridge];

        /// <summary>Mends a broken bridge (the gap-crosser* spans its gap); it counts from now on. False if it was no gap.</summary>
        public bool Mend(int bridge)
        {
            if (!IsGap(bridge)) return false;
            mended[bridge] = true;
            return true;
        }

        public BridgeLayout Layout => layout;

        /// <summary>The island the walk is on (-1 before it starts or after a reset).</summary>
        public int Current { get; private set; } = -1;

        public bool IsActive => Current >= 0;
        public bool IsSolved { get; private set; }

        public bool Crossed(int bridge) => crossed[bridge];

        public int CrossedCount
        {
            get
            {
                int n = 0;
                foreach (bool c in crossed)
                    if (c) n++;
                return n;
            }
        }

        /// <summary>Resets the walk so far (every bridge uncrossed, not started).</summary>
        public void Reset()
        {
            for (int i = 0; i < crossed.Length; i++) crossed[i] = false;
            Current = -1;
            via = -1;
        }

        public Result Observe(Vector2 local)
        {
            Result result = Step(local);
            previous = local;
            return result;
        }

        private Result Step(Vector2 local)
        {
            if (IsSolved) return Result.None;
            int island = layout.IslandAt(local);
            if (island < 0)
            {
                lastIsland = -1;
                if (Current >= 0)
                {
                    List<int> on = layout.BridgesAt(local);
                    if (via >= 0 && on.Contains(via)) return Result.None;
                    foreach (int b in on)
                    {
                        if (!layout.Touches(b, Current)) continue;
                        via = b;
                        break;
                    }
                }
                return Result.None;
            }

            bool arrival = island != lastIsland;
            lastIsland = island;
            if (!arrival) return Result.None;

            if (Current < 0)
            {
                if (island != layout.Entry) return Result.None;
                Start();
                return Result.Started;
            }
            if (island == Current)
            {
                via = -1;
                return Result.None;
            }
            // The bridge walked: the one stepped onto from the current island, else the bridge joining the two islands when
            // the sample just before lay on its span (a short plank crossed between two samples). Otherwise the water was
            // cut across.
            int bridge = via >= 0 && layout.Joins(via, Current, island) ? via : -1;
            if (bridge < 0)
            {
                int between = layout.BridgeBetween(Current, island);
                if (between >= 0 && previous.HasValue)
                {
                    layout.Ends(between, out Vector2 a, out Vector2 b);
                    if (BridgeLayout.DistanceToSegment(previous.Value, a, b) <= NearBridge) bridge = between;
                }
            }
            if (bridge >= 0)
            {
                if (IsGap(bridge))
                {
                    Restart(island);
                    return Result.Broken;
                }
                if (crossed[bridge])
                {
                    Restart(island);
                    return Result.CrossedTwice;
                }
                crossed[bridge] = true;
                Current = island;
                via = -1;
                if (CrossedCount == crossed.Length)
                {
                    IsSolved = true;
                    return Result.Solved;
                }
                return Result.Crossed;
            }
            // Off the trail. Before any bridge is crossed nothing is lost, so it stays silent (no reset to show).
            bool silent = CrossedCount == 0;
            Restart(island);
            if (silent) return Current >= 0 ? Result.Started : Result.None;
            return Result.OffTrail;
        }

        private void Start()
        {
            Reset();
            Current = layout.Entry;
        }

        private void Restart(int island)
        {
            Reset();
            if (island == layout.Entry) Current = layout.Entry;
        }
    }
}
