using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>One move of a puzzle (4.10): where it ends (a node, or PuzzleGraph.Fail for a fall) and how many tiles it covers.</summary>
    public readonly struct PuzzleMove
    {
        public readonly int To;
        public readonly int Length;

        public PuzzleMove(int to, int length)
        {
            To = to;
            Length = length;
        }

        public bool Fails => To == PuzzleGraph.Fail;
    }

    /// <summary>
    /// A whole-screen puzzle as a graph (4.10, shared with 4.11's wind streams): its nodes are the places the player can come
    /// to rest (an ice slide's stops, a wind ride's floor regions), its moves the slides or rides between them, each ending on
    /// a node or failing (a pit: the player falls and starts again at its doorway). Pure and allocation-light, so a generator
    /// can measure thousands of candidates: Explore discovers every node reachable from the starts (the moves come from a
    /// MoveSource), and the measures below read the stored moves. Nodes are ints below the capacity (a grid cell, or a
    /// cell and a mask packed together). Every measure is the research note's (puzzle-difficulty-research.md, 1b and 1c).
    /// </summary>
    public sealed class PuzzleGraph
    {
        /// <summary>A move's To when it fails (a fall into a pit).</summary>
        public const int Fail = -1;

        /// <summary>Fills into with every legal move from a node (one per way the player can set off; duplicates allowed).</summary>
        public delegate void MoveSource(int node, List<PuzzleMove> into);

        private readonly int capacity;
        private readonly bool[] reached;
        private readonly int[] first, count;
        private readonly List<PuzzleMove> moves = new List<PuzzleMove>();
        private readonly List<int> order = new List<int>();
        private readonly List<PuzzleMove> scratch = new List<PuzzleMove>();
        private readonly int[] queue;
        private readonly int[] marks;
        private int mark;

        // Reverse moves (built on demand after an Explore): for each node, the nodes with a move onto it.
        private bool reverseBuilt;
        private readonly int[] revFirst, revCount;
        private int[] revFrom = new int[64];

        // Scratch for the measures (reused; read only inside one call).
        private readonly long[] ways;
        private readonly int[] tiles, prev, prevLength, bfsOrder;
        private readonly double[] chance, chanceNext;
        private int bfsCount, bfsStart = -1;

        public PuzzleGraph(int capacity)
        {
            this.capacity = capacity;
            reached = new bool[capacity];
            first = new int[capacity];
            count = new int[capacity];
            queue = new int[capacity];
            marks = new int[capacity];
            revFirst = new int[capacity];
            revCount = new int[capacity];
            ways = new long[capacity];
            tiles = new int[capacity];
            prev = new int[capacity];
            prevLength = new int[capacity];
            bfsOrder = new int[capacity];
            chance = new double[capacity];
            chanceNext = new double[capacity];
        }

        public int Capacity => capacity;

        /// <summary>Every node reached by the last Explore, in discovery order (the starts first).</summary>
        public IReadOnlyList<int> Reached => order;

        public bool IsReached(int node) => node >= 0 && node < capacity && reached[node];

        /// <summary>The number of moves stored for a reached node.</summary>
        public int MoveCount(int node) => reached[node] ? count[node] : 0;

        /// <summary>A reached node's i-th move.</summary>
        public PuzzleMove Move(int node, int i) => moves[first[node] + i];

        /// <summary>Discovers every node reachable from the starts, storing each one's moves (breadth first, so deterministic).</summary>
        public void Explore(IEnumerable<int> starts, MoveSource source)
        {
            foreach (int n in order) reached[n] = false;
            order.Clear();
            moves.Clear();
            reverseBuilt = false;
            bfsStart = -1;
            int head = 0, tail = 0;
            foreach (int s in starts)
            {
                if (s < 0 || reached[s]) continue;
                reached[s] = true;
                order.Add(s);
                queue[tail++] = s;
            }
            while (head < tail)
            {
                int u = queue[head++];
                scratch.Clear();
                source(u, scratch);
                first[u] = moves.Count;
                count[u] = scratch.Count;
                foreach (PuzzleMove m in scratch)
                {
                    moves.Add(m);
                    if (m.To < 0 || reached[m.To]) continue;
                    reached[m.To] = true;
                    order.Add(m.To);
                    queue[tail++] = m.To;
                }
            }
        }

        /// <summary>True when a reached node has a move that fails (a pit beside one of its ways out).</summary>
        public bool HasFailingMove(int node)
        {
            for (int i = 0; i < MoveCount(node); i++)
                if (Move(node, i).Fails) return true;
            return false;
        }

        /// <summary>True when every move of a reached node fails (it has at least one): a pit-reset state.</summary>
        public bool OnlyFails(int node)
        {
            int n = MoveCount(node);
            if (n == 0) return false;
            for (int i = 0; i < n; i++)
                if (!Move(node, i).Fails) return false;
            return true;
        }

        /// <summary>The distinct nodes a reached node's moves end on, itself and falls left out.</summary>
        public int OutDegree(int node)
        {
            mark++;
            int d = 0;
            for (int i = 0; i < MoveCount(node); i++)
            {
                int to = Move(node, i).To;
                if (to < 0 || to == node || marks[to] == mark) continue;
                marks[to] = mark;
                d++;
            }
            return d;
        }

        /// <summary>Fewest moves from a node to every reached node (-1 where none), over the stored moves (falls lead nowhere).</summary>
        public int[] Distances(int from, int[] into = null)
        {
            int[] dist = into ?? new int[capacity];
            for (int i = 0; i < capacity; i++) dist[i] = -1;
            bfsCount = 0;
            bfsStart = from;
            if (!IsReached(from)) return dist;
            dist[from] = 0;
            bfsOrder[bfsCount++] = from;
            for (int head = 0; head < bfsCount; head++)
            {
                int u = bfsOrder[head];
                for (int i = 0; i < count[u]; i++)
                {
                    int to = moves[first[u] + i].To;
                    if (to < 0 || dist[to] >= 0) continue;
                    dist[to] = dist[u] + 1;
                    bfsOrder[bfsCount++] = to;
                }
            }
            return dist;
        }

        private void BuildReverse()
        {
            if (reverseBuilt) return;
            reverseBuilt = true;
            foreach (int n in order) revCount[n] = 0;
            foreach (int u in order)
                for (int i = 0; i < count[u]; i++)
                {
                    int to = moves[first[u] + i].To;
                    if (to >= 0) revCount[to]++;
                }
            int total = 0;
            foreach (int n in order)
            {
                revFirst[n] = total;
                total += revCount[n];
                revCount[n] = 0;
            }
            if (revFrom.Length < total) revFrom = new int[Math.Max(total, revFrom.Length * 2)];
            foreach (int u in order)
                for (int i = 0; i < count[u]; i++)
                {
                    int to = moves[first[u] + i].To;
                    if (to >= 0) revFrom[revFirst[to] + revCount[to]++] = u;
                }
        }

        /// <summary>Fewest moves from every reached node to the targets (-1 where it never gets there), over the stored moves.</summary>
        public int[] DistancesTo(Func<int, bool> target, int[] into = null)
        {
            BuildReverse();
            int[] dist = into ?? new int[capacity];
            for (int i = 0; i < capacity; i++) dist[i] = -1;
            int tail = 0;
            foreach (int n in order)
                if (target(n))
                {
                    dist[n] = 0;
                    queue[tail++] = n;
                }
            for (int head = 0; head < tail; head++)
            {
                int v = queue[head];
                for (int i = 0; i < revCount[v]; i++)
                {
                    int u = revFrom[revFirst[v] + i];
                    if (dist[u] >= 0) continue;
                    dist[u] = dist[v] + 1;
                    queue[tail++] = u;
                }
            }
            return dist;
        }

        /// <summary>
        /// The reached nodes that are hard traps: nodes from which no move sequence reaches a safe node (safe(n)) or a fall
        /// (a fall puts the player back at its doorway, a reset). The research note's "no hard traps" rule counts these.
        /// </summary>
        public List<int> Traps(Func<int, bool> safe)
        {
            int[] d = DistancesTo(n => safe(n) || HasFailingMove(n));
            var traps = new List<int>();
            foreach (int n in order)
                if (d[n] < 0) traps.Add(n);
            return traps;
        }

        /// <summary>Makes sure the BFS order (nodes by distance) is the one from start.</summary>
        private void EnsureBfs(int start)
        {
            if (bfsStart != start) Distances(start, new int[capacity]);
        }

        /// <summary>
        /// The number of distinct shortest move sequences (as sequences of nodes) from start to goal, capped at cap; 0 when
        /// the goal is never reached. dist is Distances(start).
        /// </summary>
        public long ShortestPathCount(int start, int goal, int[] dist, long cap = 1000000)
        {
            if (dist[goal] < 0) return 0;
            EnsureBfs(start);
            foreach (int n in order) ways[n] = 0;
            ways[start] = 1;
            for (int k = 0; k < bfsCount; k++)
            {
                int u = bfsOrder[k];
                if (ways[u] == 0) continue;
                mark++;
                for (int i = 0; i < count[u]; i++)
                {
                    int to = moves[first[u] + i].To;
                    if (to < 0 || dist[to] != dist[u] + 1 || marks[to] == mark) continue;
                    marks[to] = mark;
                    ways[to] = Math.Min(cap, ways[to] + ways[u]);
                }
            }
            return ways[goal];
        }

        /// <summary>
        /// A best route from start to goal: fewest moves, then fewest tiles; the nodes visited (start first) and the tiles
        /// of each move. Null when the goal is never reached. dist is Distances(start).
        /// </summary>
        public List<int> BestRoute(int start, int goal, int[] dist, out List<int> lengths)
        {
            lengths = null;
            if (dist[goal] < 0) return null;
            EnsureBfs(start);
            foreach (int n in order) tiles[n] = int.MaxValue;
            tiles[start] = 0;
            for (int k = 0; k < bfsCount; k++)
            {
                int u = bfsOrder[k];
                if (tiles[u] == int.MaxValue) continue;
                for (int i = 0; i < count[u]; i++)
                {
                    PuzzleMove m = moves[first[u] + i];
                    if (m.To < 0 || dist[m.To] != dist[u] + 1) continue;
                    int t = tiles[u] + m.Length;
                    if (t >= tiles[m.To]) continue;
                    tiles[m.To] = t;
                    prev[m.To] = u;
                    prevLength[m.To] = m.Length;
                }
            }
            if (tiles[goal] == int.MaxValue) return null;
            var route = new List<int> { goal };
            lengths = new List<int>();
            int at = goal;
            while (at != start)
            {
                lengths.Add(prevLength[at]);
                at = prev[at];
                route.Add(at);
            }
            route.Reverse();
            lengths.Reverse();
            return route;
        }

        /// <summary>
        /// The random player's solve rate (P_rand), exactly: the chance that a player who picks each move uniformly among the
        /// legal ones (every way of setting off counts once), stopping at the first fall, reaches the goal within cap moves.
        /// The Monte Carlo of the research note converges to this; computing it exactly keeps the sweep deterministic.
        /// </summary>
        public double RandomSolveRate(int start, int goal, int cap)
        {
            if (!IsReached(start) || !IsReached(goal)) return 0;
            double[] p = chance, next = chanceNext;
            foreach (int u in order) p[u] = 0;
            p[goal] = 1;
            for (int t = 1; t <= cap; t++)
            {
                foreach (int u in order)
                {
                    if (u == goal)
                    {
                        next[u] = 1;
                        continue;
                    }
                    int n = count[u];
                    if (n == 0)
                    {
                        next[u] = 0;
                        continue;
                    }
                    double sum = 0;
                    int f = first[u];
                    for (int i = 0; i < n; i++)
                    {
                        int to = moves[f + i].To;
                        if (to >= 0) sum += p[to];
                    }
                    next[u] = sum / n;
                }
                double[] swap = p;
                p = next;
                next = swap;
            }
            return p[start];
        }
    }
}
