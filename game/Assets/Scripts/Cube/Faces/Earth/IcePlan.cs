using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>One ice screen of the Earth face (4.10): where it is, its tier and its layout (null when none was found).</summary>
    public sealed class IceScreenSpec
    {
        public ScreenAddress Screen { get; }
        public PuzzleTier Tier { get; }
        public IceLayout Layout { get; }

        public IceScreenSpec(ScreenAddress screen, PuzzleTier tier, IceLayout layout)
        {
            Screen = screen;
            Tier = tier;
            Layout = layout;
        }

        public override string ToString() => $"ice screen at {Screen}";
    }

    /// <summary>
    /// The Earth face's sliding ice screens of a run as pure data (4.10): the screens CubeLayout gives Earth's ice module (its
    /// whole-screen puzzle module, IslandScreensPerFace of them, never the town, the core screen or side by side), each with
    /// an IceLayout generated on its own stream (RngStreams.IceScreens, the screen in the seed) at the tier of Earth's die
    /// number (4.8: 2-3 early, 4-5 mid, 6 late). Needs only the model and the layout, so the game, the lock check and the
    /// sweep all make the same plan; layouts are cached by seed and screen, so a run generates each once. Goals are the
    /// screens' reward roadblocks (4.7 PuzzleGoals, on the goal pads), RegionsAt their regions for DoorwayReach.
    /// </summary>
    public sealed class IcePlan
    {
        /// <summary>PCG32 stream for the ice screens (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.IceScreens;

        private static readonly BoundedCache<(int, int, FaceId, Vector2Int, PuzzleTier, int), IceLayout> Cache =
            new BoundedCache<(int, int, FaceId, Vector2Int, PuzzleTier, int), IceLayout>();

        /// <summary>Forgets every cached layout (timing tests measure a cold generation).</summary>
        public static void ClearCache() => Cache.Clear();

        /// <summary>Layouts generated so far (4.21: a warmed reveal generates none).</summary>
        public static int Generated => Cache.Added;

        private readonly List<IceScreenSpec> screens;

        public int Seed { get; }
        public FaceId Face { get; }
        public int FaceSize { get; }

        /// <summary>Earth's die number this run, and the tier it gives every ice screen.</summary>
        public int DieNumber { get; }
        public PuzzleTier Tier { get; }

        public IReadOnlyList<IceScreenSpec> Screens => screens;

        private IcePlan(int seed, FaceId face, int faceSize, int dieNumber, List<IceScreenSpec> screens)
        {
            Seed = seed;
            Face = face;
            FaceSize = faceSize;
            DieNumber = dieNumber;
            Tier = PuzzleTiers.OfDieNumber(dieNumber);
            this.screens = screens;
        }

        /// <summary>
        /// The run's ice screens: null when Earth is not laid out; empty when the layout gives it no ice module (a library
        /// without one). dice defaults to the run's own (DieNumbers.Roll).
        /// </summary>
        public static IcePlan Create(CubeModel model, CubeLayout layout, DieNumbers dice = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (layout == null) return null;
            FaceId face = model.FaceOf(Theme.EarthAtmosphere);
            if (!layout.TryGetFace(face, out FaceLayout fl)) return null;
            dice = dice ?? DieNumbers.Roll(model);
            int number = dice.Of(face);
            PuzzleTier tier = PuzzleTiers.OfDieNumber(number);
            var list = new List<IceScreenSpec>();
            foreach (Vector2Int cell in fl.IslandCells)
            {
                var screen = new ScreenAddress(face, cell);
                list.Add(new IceScreenSpec(screen, tier, LayoutFor(model, screen, tier)));
            }
            return new IcePlan(model.Seed, face, model.FaceSize, number, list);
        }

        /// <summary>A screen's layout (generated once per seed, screen and tier, then cached).</summary>
        public static IceLayout LayoutFor(CubeModel model, ScreenAddress screen, PuzzleTier tier)
        {
            int doors = PuzzleScreenLayouts.DoorMask(model, screen);
            var key = (model.Seed, model.FaceSize, screen.Face, screen.Cell, tier, doors);
            if (Cache.TryGetValue(key, out IceLayout cached)) return cached;
            var rng = new SeededRng(PuzzleScreenLayouts.ScreenSeed(model.Seed, screen), RngStream);
            IceLayout layout = IceLayout.Generate(rng, tier, PuzzleScreenLayouts.HasDoor(doors));
            Cache.Add(key, layout);
            return layout;
        }

        /// <summary>The ice layout of a screen, or null for any other screen.</summary>
        public IceLayout At(ScreenAddress screen)
        {
            foreach (IceScreenSpec s in screens)
                if (s.Screen == screen) return s.Layout;
            return null;
        }

        public bool IsIceScreen(ScreenAddress screen)
        {
            foreach (IceScreenSpec s in screens)
                if (s.Screen == screen) return true;
            return false;
        }

        /// <summary>The screens' reward roadblocks (4.7 PuzzleGoals): each goal pad, in screen order.</summary>
        public List<PuzzleGoal> Goals()
        {
            var goals = new List<PuzzleGoal>();
            foreach (IceScreenSpec s in screens)
                if (s.Layout != null && s.Layout.Goal >= 0) goals.Add(new PuzzleGoal(s.Screen, IceLayout.CellLocal(s.Layout.Goal)));
            return goals;
        }

        /// <summary>
        /// Problems with the plan (empty when all is well): Earth has IslandScreensPerFace ice screens (when its library has an
        /// ice module), none in the town, each with a layout whose landings are exactly the screen's doorways and which is
        /// solvable from each doorway inside every target of its tier, with no hard traps (IceLayout.Problems).
        /// </summary>
        public List<string> Problems(CubeModel model, bool expectScreens = true)
        {
            var problems = new List<string>();
            int wanted = CubeLayout.IslandScreensPerFace(FaceSize);
            if (expectScreens && screens.Count != wanted) problems.Add($"Earth has {screens.Count} ice screens (want {wanted})");
            foreach (IceScreenSpec s in screens)
            {
                if (s.Screen.Face != Face) problems.Add($"{s} is not on Earth");
                if (TownSquare.IsTown(s.Screen, FaceSize)) problems.Add($"{s} is the town");
                if (s.Tier != Tier) problems.Add($"{s} is {s.Tier}, not {Tier}");
                if (s.Layout == null)
                {
                    problems.Add($"{s}: no ice layout");
                    continue;
                }
                for (int f = 0; f < 4; f++)
                {
                    bool door = model.TryStep(s.Screen, (Facing)f, out _, out _);
                    if (door != s.Layout.HasDoor((Facing)f)) problems.Add($"{s}: the {(Facing)f} landing does not match its doorway");
                }
                problems.AddRange(s.Layout.Problems(s.ToString()));
            }
            return problems;
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("die ").Append(DieNumber).Append(' ').Append(Tier).Append("; ");
            foreach (IceScreenSpec s in screens)
                sb.Append(s).Append(" [").Append(s.Layout != null ? s.Layout.Signature() : "none").Append("]; ");
            return sb.ToString();
        }
    }

    /// <summary>The whole-screen puzzles' regions and goals together (Math's islands, Earth's ice and 4.11 wind).</summary>
    public static class PuzzleScreens
    {
        /// <summary>The regions of every puzzle screen (null for an ordinary screen), for DoorwayReach.</summary>
        public static Func<ScreenAddress, IScreenRegions> RegionsAt(MathPlan math, IcePlan ice, WindPlan wind = null, IslandPlan islandPlan = null)
        {
            if (math == null && ice == null && wind == null && islandPlan == null) return null;
            return screen =>
            {
                IScreenRegions islands = (IScreenRegions)math?.IslandAt(screen) ?? islandPlan?.At(screen);
                if (islands != null) return islands;
                IScreenRegions slides = ice?.At(screen);
                if (slides != null) return slides;
                return wind?.At(screen);
            };
        }

        /// <summary>
        /// Every puzzle screen's reward roadblock (4.7 PuzzleGoals), for RewardPlacement: the ice goals, then the wind ones,
        /// then (4.12) Math's island ones, so Earth's goals keep their numbers.
        /// </summary>
        public static List<PuzzleGoal> Goals(IcePlan ice, WindPlan wind = null, IslandPlan islands = null)
        {
            var goals = ice != null ? ice.Goals() : new List<PuzzleGoal>();
            if (wind != null) goals.AddRange(wind.Goals());
            if (islands != null) goals.AddRange(islands.Goals());
            return goals;
        }
    }

    /// <summary>The difficulty the sweep reports per tier (4.10): each metric's least, mean and most over the ice screens it saw.</summary>
    public sealed class IceStats
    {
        private readonly Dictionary<PuzzleTier, List<IceMetrics>> byTier = new Dictionary<PuzzleTier, List<IceMetrics>>();
        private readonly Dictionary<PuzzleTier, int> outside = new Dictionary<PuzzleTier, int>();

        public int Screens { get; private set; }
        public int Missing { get; private set; }

        public void Add(IceScreenSpec spec)
        {
            Screens++;
            if (spec.Layout == null)
            {
                Missing++;
                return;
            }
            if (!byTier.TryGetValue(spec.Tier, out List<IceMetrics> list)) byTier[spec.Tier] = list = new List<IceMetrics>();
            foreach (Facing side in spec.Layout.Doors())
            {
                IceMetrics m = spec.Layout.Measure(side);
                list.Add(m);
                if (spec.Layout.Targets.Misses(m).Count > 0) outside[spec.Tier] = (outside.TryGetValue(spec.Tier, out int c) ? c : 0) + 1;
            }
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            string Range(List<IceMetrics> l, Func<IceMetrics, double> f) => $"{F(l.Min(f))}/{F(l.Average(f))}/{F(l.Max(f))}";
            foreach (PuzzleTier tier in PuzzleTiers.All)
            {
                if (!byTier.TryGetValue(tier, out List<IceMetrics> l) || l.Count == 0) continue;
                outside.TryGetValue(tier, out int miss);
                sb.Append($"  {tier} ({l.Count} doorway checks, {miss} outside a target; least/mean/most): min slides {Range(l, m => m.MinSlides)}, " +
                          $"stops {Range(l, m => m.Stops)}, out-degree {Range(l, m => m.OutDegree)}, detour {Range(l, m => m.Detour)}, " +
                          $"rocks {Range(l, m => 100.0 * m.RockDensity)}%, pits {Range(l, m => m.Pits)}, pit resets {Range(l, m => m.PitResets)}, " +
                          $"traps {Range(l, m => m.Traps)}, grip {Range(l, m => m.Grips)}, shortest solutions {Range(l, m => m.ShortestSolutions)}, " +
                          $"P_rand {Range(l, m => 100.0 * m.RandomSolveRate)}%, red herrings {Range(l, m => m.RedHerrings)}, " +
                          $"retry {Range(l, m => m.RetryCost)}, longest slide {Range(l, m => m.LongestSlide)}, crossing {Range(l, m => m.Crossing)}.\n");
            }
            return sb.ToString();
        }
    }
}
