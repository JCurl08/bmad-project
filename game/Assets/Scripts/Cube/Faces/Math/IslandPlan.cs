using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Math's island screens of a run as pure data (4.12): the screens CubeLayout gives Math's island module (row by row),
    /// each with an IslandLayout generated on its own stream (RngStreams.IslandPuzzles, the screen in the seed) at the tier
    /// of Math's die number (4.8: 2-3 early, 4-5 mid, 6 late), the trial on the screen MathPlan's own stream picks (its
    /// first draw, as before). Needs only the model and the layout, so the game, the lock check and the sweep all make the
    /// same plan; layouts are cached by seed and screen, so a run generates each once. MathPlan reads the same layouts.
    /// Goals are the screens' reward roadblocks (4.7 PuzzleGoals, on their reward islands).
    /// </summary>
    public sealed class IslandPlan
    {
        /// <summary>PCG32 stream for the island screens' layouts (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.IslandPuzzles;

        private static readonly BoundedCache<(int, int, FaceId, Vector2Int, PuzzleTier, int, bool), IslandLayout> Cache =
            new BoundedCache<(int, int, FaceId, Vector2Int, PuzzleTier, int, bool), IslandLayout>();

        /// <summary>Forgets every cached layout (timing tests measure a cold generation).</summary>
        public static void ClearCache() => Cache.Clear();

        /// <summary>Layouts generated so far (4.21: a warmed reveal generates none).</summary>
        public static int Generated => Cache.Added;

        private readonly List<IslandScreenSpec> screens;

        public int Seed { get; }
        public FaceId Face { get; }
        public int FaceSize { get; }

        /// <summary>Math's die number this run, and the tier it gives every island screen.</summary>
        public int DieNumber { get; }
        public PuzzleTier Tier { get; }

        public IReadOnlyList<IslandScreenSpec> Screens => screens;

        private IslandPlan(int seed, FaceId face, int faceSize, int dieNumber, List<IslandScreenSpec> screens)
        {
            Seed = seed;
            Face = face;
            FaceSize = faceSize;
            DieNumber = dieNumber;
            Tier = PuzzleTiers.OfDieNumber(dieNumber);
            this.screens = screens;
        }

        /// <summary>Math's tier this run (its die number's): dice defaults to the run's own.</summary>
        public static PuzzleTier TierOf(CubeModel model, DieNumbers dice = null) =>
            PuzzleTiers.OfDieNumber((dice ?? DieNumbers.Roll(model)).Of(model.FaceOf(Theme.Math)));

        /// <summary>
        /// The island screen holding the trial, of count (MathPlan's stream, its first draw, so the trial stays where 1.25
        /// put it), or -1 with none.
        /// </summary>
        public static int TrialIndex(int seed, int count) =>
            count > 0 ? new SeededRng(unchecked((ulong)(uint)seed), RngStreams.MathPlan).NextInt(count) : -1;

        /// <summary>
        /// The run's island screens: null when Math is not laid out; empty when the layout gives it no island module.
        /// dice defaults to the run's own (DieNumbers.Roll).
        /// </summary>
        public static IslandPlan Create(CubeModel model, CubeLayout layout, DieNumbers dice = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (layout == null) return null;
            FaceId face = model.FaceOf(Theme.Math);
            if (!layout.TryGetFace(face, out FaceLayout fl)) return null;
            dice = dice ?? DieNumbers.Roll(model);
            int number = dice.Of(face);
            PuzzleTier tier = PuzzleTiers.OfDieNumber(number);
            List<Vector2Int> cells = fl.IslandCells.OrderBy(c => c.y).ThenBy(c => c.x).ToList();
            int trial = TrialIndex(model.Seed, cells.Count);
            var list = new List<IslandScreenSpec>();
            for (int i = 0; i < cells.Count; i++)
            {
                var screen = new ScreenAddress(face, cells[i]);
                list.Add(new IslandScreenSpec(screen, LayoutFor(model, screen, tier, i == trial)));
            }
            return new IslandPlan(model.Seed, face, model.FaceSize, number, list);
        }

        /// <summary>A screen's layout (generated once per seed, screen, tier and trial, then cached).</summary>
        public static IslandLayout LayoutFor(CubeModel model, ScreenAddress screen, PuzzleTier tier, bool trial)
        {
            int doors = PuzzleScreenLayouts.DoorMask(model, screen);
            var key = (model.Seed, model.FaceSize, screen.Face, screen.Cell, tier, doors, trial);
            if (Cache.TryGetValue(key, out IslandLayout cached)) return cached;
            var rng = new SeededRng(PuzzleScreenLayouts.ScreenSeed(model.Seed, screen), RngStream);
            IslandLayout layout = IslandLayout.Generate(rng, tier, PuzzleScreenLayouts.HasDoor(doors), trial);
            Cache.Add(key, layout);
            return layout;
        }

        /// <summary>The island layout of a screen, or null for any other screen.</summary>
        public IslandLayout At(ScreenAddress screen)
        {
            foreach (IslandScreenSpec s in screens)
                if (s.Screen == screen) return s.Layout;
            return null;
        }

        /// <summary>The screens' reward roadblocks (4.7 PuzzleGoals): each reward island's middle, in screen order.</summary>
        public List<PuzzleGoal> Goals()
        {
            var goals = new List<PuzzleGoal>();
            foreach (IslandScreenSpec s in screens)
                if (s.Layout != null && s.Layout.HasPuzzle) goals.Add(new PuzzleGoal(s.Screen, s.Layout.GoalSpot));
            return goals;
        }

        /// <summary>
        /// Problems with the plan (empty when all is well): Math has IslandScreensPerFace island screens (when its library has
        /// an island module), each with a layout that has a puzzle, whose doorway islands are exactly the screen's doorways and
        /// which every doorway can solve inside every target of its tier (IslandLayout.Problems), and one holds the trial.
        /// </summary>
        public List<string> Problems(CubeModel model, bool expectScreens = true)
        {
            var problems = new List<string>();
            int wanted = CubeLayout.IslandScreensPerFace(FaceSize);
            if (expectScreens && screens.Count != wanted) problems.Add($"Math has {screens.Count} island screens (want {wanted})");
            int trials = 0;
            foreach (IslandScreenSpec s in screens)
            {
                if (s.Screen.Face != Face) problems.Add($"{s} is not on Math");
                if (s.Layout == null)
                {
                    problems.Add($"{s}: no island layout fits");
                    continue;
                }
                if (s.HasTrial) trials++;
                if (!s.Layout.HasPuzzle) problems.Add($"{s}: no lit network");
                if (s.Layout.Tier != Tier) problems.Add($"{s} is {s.Layout.Tier}, not {Tier}");
                ScreenAddress screen = s.Screen;
                problems.AddRange(s.Layout.Problems(s.ToString(), side => model.TryStep(screen, side, out _, out _)));
            }
            if (screens.Count > 0 && trials != 1) problems.Add($"{trials} island screens hold the trial (want one)");
            return problems;
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("die ").Append(DieNumber).Append(' ').Append(Tier).Append("; ");
            foreach (IslandScreenSpec s in screens)
            {
                sb.Append(s).Append(" [").Append(s.Layout != null ? s.Layout.Signature() : "none").Append("]");
                if (s.Layout != null && s.Layout.HasPuzzle)
                {
                    IslandMetrics m = s.Layout.Measure(Facing.North);
                    sb.Append(" P_rand ").Append(m.RandomSolveRate.ToString("0.####", CultureInfo.InvariantCulture)).Append(" trails ").Append(m.Trails);
                }
                sb.Append("; ");
            }
            return sb.ToString();
        }
    }

    /// <summary>The difficulty the sweep reports per tier (4.12): each metric's least, mean and most over the island doorways it saw.</summary>
    public sealed class IslandStats
    {
        private readonly Dictionary<PuzzleTier, List<IslandMetrics>> byTier = new Dictionary<PuzzleTier, List<IslandMetrics>>();
        private readonly Dictionary<PuzzleTier, int> outside = new Dictionary<PuzzleTier, int>();

        public int Screens { get; private set; }
        public int Missing { get; private set; }
        public int Templates { get; private set; }
        public long Steps { get; private set; }

        public void Add(IslandScreenSpec spec)
        {
            Screens++;
            if (spec.Layout == null || !spec.Layout.HasPuzzle)
            {
                Missing++;
                return;
            }
            IslandLayout layout = spec.Layout;
            if (layout.FromTemplate) Templates++;
            Steps += layout.SearchSteps;
            if (!byTier.TryGetValue(layout.Tier, out List<IslandMetrics> list)) byTier[layout.Tier] = list = new List<IslandMetrics>();
            bool gap = layout.HasGapDoor;
            for (int f = 0; f < 4; f++)
            {
                if (layout.DoorIsland((Facing)f) < 0) continue;
                IslandMetrics m = layout.Measure((Facing)f);
                list.Add(m);
                if (layout.Targets.Misses(m, gap).Count > 0) outside[layout.Tier] = (outside.TryGetValue(layout.Tier, out int c) ? c : 0) + 1;
            }
        }

        /// <summary>Doorway checks per tier, for the report.</summary>
        public int Checks(PuzzleTier tier) => byTier.TryGetValue(tier, out List<IslandMetrics> l) ? l.Count : 0;

        public int Outside => outside.Values.Sum();

        public string Describe()
        {
            var sb = new StringBuilder();
            string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            string Range(List<IslandMetrics> l, Func<IslandMetrics, double> f) => $"{F(l.Min(f))}/{F(l.Average(f))}/{F(l.Max(f))}";
            foreach (PuzzleTier tier in PuzzleTiers.All)
            {
                if (!byTier.TryGetValue(tier, out List<IslandMetrics> l) || l.Count == 0) continue;
                outside.TryGetValue(tier, out int miss);
                sb.Append($"  {tier} ({l.Count} doorway checks, {miss} outside a target; least/mean/most): planks {Range(l, m => m.Planks)}, " +
                          $"islands {Range(l, m => m.Islands)}, cycles {Range(l, m => m.Cycles)}, odd {Range(l, m => m.OddIslands)} " +
                          $"(at a doorway {Range(l, m => m.OddDoorIslands)}), P_rand {Range(l, m => 100.0 * m.RandomSolveRate)}%, " +
                          $"trails {Range(l, m => m.Trails)}, red herrings {Range(l, m => m.RedHerrings)}, spans inside {Range(l, m => m.BrokenInNetwork)}, " +
                          $"spread {Range(l, m => 100.0 * m.Spread)}%, start walk {Range(l, m => m.StartWalk)}, crossing {Range(l, m => m.Crossing)}, " +
                          $"reward route {Range(l, m => m.RewardRoute)}, retry {Range(l, m => m.RetrySeconds)} s, solvable {l.Count(m => m.Solvable)}/{l.Count}.\n");
            }
            return sb.ToString();
        }
    }
}
