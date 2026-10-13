using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>One wind-stream screen of the Earth face (4.11): where it is, its tier and its layout.</summary>
    public sealed class WindScreenSpec
    {
        public ScreenAddress Screen { get; }
        public PuzzleTier Tier { get; }
        public WindLayout Layout { get; }

        public WindScreenSpec(ScreenAddress screen, PuzzleTier tier, WindLayout layout)
        {
            Screen = screen;
            Tier = tier;
            Layout = layout;
        }

        public override string ToString() => $"wind screen at {Screen}";
    }

    /// <summary>
    /// The Earth face's wind-stream screens of a run as pure data (4.11): the screens CubeLayout gives Earth's wind module
    /// (CubeLayout.WindScreensPerFace of them, one at 3x3, beside neither ice screen, never the town or the core screen), each
    /// with a WindLayout generated on its own stream (RngStreams.WindScreens, the screen in the seed) at the tier of Earth's die
    /// number (4.8: 2-3 early, 4-5 mid, 6 late). Needs only the model and the layout, so the game, the lock check and the sweep
    /// all make the same plan; layouts are cached by seed and screen. Goals are the screens' reward roadblocks (4.7 PuzzleGoals,
    /// on the goal pads, after the ice screens' in PuzzleScreens.Goals), At their regions for DoorwayReach.
    /// </summary>
    public sealed class WindPlan
    {
        /// <summary>PCG32 stream for the wind screens (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.WindScreens;

        private static readonly BoundedCache<(int, int, FaceId, Vector2Int, PuzzleTier, int), WindLayout> Cache =
            new BoundedCache<(int, int, FaceId, Vector2Int, PuzzleTier, int), WindLayout>();

        /// <summary>Forgets every cached layout (timing tests measure a cold generation).</summary>
        public static void ClearCache() => Cache.Clear();

        /// <summary>Layouts generated so far (4.21: a warmed reveal generates none).</summary>
        public static int Generated => Cache.Added;

        private readonly List<WindScreenSpec> screens;
        private readonly FaceLayout faceLayout;

        public int Seed { get; }
        public FaceId Face { get; }
        public int FaceSize { get; }
        public int DieNumber { get; }
        public PuzzleTier Tier { get; }

        public IReadOnlyList<WindScreenSpec> Screens => screens;

        private WindPlan(int seed, FaceId face, int faceSize, int dieNumber, FaceLayout faceLayout, List<WindScreenSpec> screens)
        {
            Seed = seed;
            Face = face;
            FaceSize = faceSize;
            DieNumber = dieNumber;
            Tier = PuzzleTiers.OfDieNumber(dieNumber);
            this.faceLayout = faceLayout;
            this.screens = screens;
        }

        /// <summary>The run's wind screens: null when Earth is not laid out; empty without a wind module. dice defaults to the run's.</summary>
        public static WindPlan Create(CubeModel model, CubeLayout layout, DieNumbers dice = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (layout == null) return null;
            FaceId face = model.FaceOf(Theme.EarthAtmosphere);
            if (!layout.TryGetFace(face, out FaceLayout fl)) return null;
            dice = dice ?? DieNumbers.Roll(model);
            int number = dice.Of(face);
            PuzzleTier tier = PuzzleTiers.OfDieNumber(number);
            var list = new List<WindScreenSpec>();
            foreach (Vector2Int cell in fl.WindCells)
            {
                var screen = new ScreenAddress(face, cell);
                list.Add(new WindScreenSpec(screen, tier, LayoutFor(model, screen, tier)));
            }
            return new WindPlan(model.Seed, face, model.FaceSize, number, fl, list);
        }

        /// <summary>A screen's layout (generated once per seed, screen and tier, then cached).</summary>
        public static WindLayout LayoutFor(CubeModel model, ScreenAddress screen, PuzzleTier tier)
        {
            int doors = PuzzleScreenLayouts.DoorMask(model, screen);
            var key = (model.Seed, model.FaceSize, screen.Face, screen.Cell, tier, doors);
            if (Cache.TryGetValue(key, out WindLayout cached)) return cached;
            var rng = new SeededRng(PuzzleScreenLayouts.ScreenSeed(model.Seed, screen), RngStream);
            WindLayout layout = WindLayout.Generate(rng, tier, PuzzleScreenLayouts.HasDoor(doors));
            Cache.Add(key, layout);
            return layout;
        }

        /// <summary>The wind layout of a screen, or null for any other screen.</summary>
        public WindLayout At(ScreenAddress screen)
        {
            foreach (WindScreenSpec s in screens)
                if (s.Screen == screen) return s.Layout;
            return null;
        }

        public bool IsWindScreen(ScreenAddress screen) => At(screen) != null;

        /// <summary>The screens' reward roadblocks (4.7 PuzzleGoals): each goal pad, in screen order.</summary>
        public List<PuzzleGoal> Goals()
        {
            var goals = new List<PuzzleGoal>();
            foreach (WindScreenSpec s in screens)
                if (s.Layout != null && s.Layout.Goal >= 0) goals.Add(new PuzzleGoal(s.Screen, WindLayout.CellLocal(s.Layout.Goal)));
            return goals;
        }

        /// <summary>
        /// Problems with the plan (empty when all is well): Earth has WindScreensPerFace wind screens (when its library has a wind
        /// module), none the town or the core screen, none beside another puzzle screen, each with a layout whose doorways match
        /// the screen's and which is solvable from each doorway inside every target of its tier, with no hard traps.
        /// </summary>
        public List<string> Problems(CubeModel model, bool expectScreens = true)
        {
            var problems = new List<string>();
            int wanted = CubeLayout.WindScreensPerFace(FaceSize);
            if (expectScreens && screens.Count != wanted) problems.Add($"Earth has {screens.Count} wind screens (want {wanted})");
            foreach (WindScreenSpec s in screens)
            {
                if (s.Screen.Face != Face) problems.Add($"{s} is not on Earth");
                if (TownSquare.IsTown(s.Screen, FaceSize)) problems.Add($"{s} is the town");
                if (faceLayout != null)
                {
                    if (faceLayout.CoreCell == s.Screen.Cell) problems.Add($"{s} is the core screen");
                    foreach (Vector2Int other in faceLayout.IslandCells.Concat(faceLayout.WindCells))
                        if (Mathf.Abs(other.x - s.Screen.Cell.x) + Mathf.Abs(other.y - s.Screen.Cell.y) == 1)
                            problems.Add($"{s} is beside the puzzle screen at {other}");
                }
                if (s.Tier != Tier) problems.Add($"{s} is {s.Tier}, not {Tier}");
                if (s.Layout == null)
                {
                    problems.Add($"{s}: no wind layout");
                    continue;
                }
                for (int f = 0; f < 4; f++)
                {
                    bool door = model.TryStep(s.Screen, (Facing)f, out _, out _);
                    if (door != s.Layout.HasDoor((Facing)f)) problems.Add($"{s}: the {(Facing)f} doorway does not match");
                }
                problems.AddRange(s.Layout.Problems(s.ToString()));
            }
            return problems;
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("die ").Append(DieNumber).Append(' ').Append(Tier).Append("; ");
            foreach (WindScreenSpec s in screens)
                sb.Append(s).Append(" [").Append(s.Layout != null ? s.Layout.Signature() : "none").Append("]; ");
            return sb.ToString();
        }
    }

    /// <summary>The difficulty the sweep reports per tier (4.11): each metric's least, mean and most over the wind screens it saw.</summary>
    public sealed class WindStats
    {
        private readonly Dictionary<PuzzleTier, List<WindMetrics>> byTier = new Dictionary<PuzzleTier, List<WindMetrics>>();
        private readonly Dictionary<PuzzleTier, int> outside = new Dictionary<PuzzleTier, int>();

        public int Screens { get; private set; }
        public int Missing { get; private set; }

        public void Add(WindScreenSpec spec)
        {
            Screens++;
            if (spec.Layout == null)
            {
                Missing++;
                return;
            }
            if (!byTier.TryGetValue(spec.Tier, out List<WindMetrics> list)) byTier[spec.Tier] = list = new List<WindMetrics>();
            foreach (Facing side in spec.Layout.Doors())
            {
                WindMetrics m = spec.Layout.Measure(side);
                list.Add(m);
                if (spec.Layout.Targets.Misses(m).Count > 0) outside[spec.Tier] = (outside.TryGetValue(spec.Tier, out int c) ? c : 0) + 1;
            }
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            string Range(List<WindMetrics> l, Func<WindMetrics, double> f) => $"{F(l.Min(f))}/{F(l.Average(f))}/{F(l.Max(f))}";
            foreach (PuzzleTier tier in PuzzleTiers.All)
            {
                if (!byTier.TryGetValue(tier, out List<WindMetrics> l) || l.Count == 0) continue;
                outside.TryGetValue(tier, out int miss);
                sb.Append($"  {tier} ({l.Count} doorway checks, {miss} outside a target; least/mean/most): regions {Range(l, m => m.Regions)}, " +
                          $"streams {Range(l, m => m.Streams)}, min rides {Range(l, m => m.MinRides)}, decoys {Range(l, m => m.Decoys)}, " +
                          $"return {Range(l, m => m.ReturnCost)}, traps {Range(l, m => m.Traps)}, P_rand {Range(l, m => 100.0 * m.RandomSolveRate)}%, " +
                          $"longest ride {Range(l, m => m.LongestRide)}, anchors {Range(l, m => m.Anchors)}, walls {Range(l, m => m.Walls)}, " +
                          $"crossing {Range(l, m => m.Crossing)}, fans 0 (not built).\n");
            }
            return sb.ToString();
        }
    }
}
