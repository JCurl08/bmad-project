using System.Collections.Generic;
using System.Linq;

namespace Game.Cube
{
    /// <summary>
    /// The reveal-time guard (4.15), pure: the problems of the plans a run generated, with the sweep's own checks (SeedSweep,
    /// for any seed, not only the swept ones). CubeWorld.RevealScience logs them once, as one error, when there are any; the
    /// EditMode robustness test runs the same checks over seeds 1-500 and wants none.
    /// <list type="bullet">
    /// <item>Always: the puzzle screens (ice, wind, islands; IcePlan/WindPlan/IslandPlan.Problems) and any layout that fell
    /// back to its template.</item>
    /// <item>For a whole cube (no face sealed on purpose, towns at the face size) with a module library: the core entrances,
    /// the lair (one real portal, the rest into lair rooms), the temples (solvable, none a template) and the face-edge locks,
    /// when made (no fallback draw).</item>
    /// <item>With the run's item placement too: every item collectable with no softlock (SeedSweep.FindItemProblems), and,
    /// with the rewards, every roadblock's reward (RewardPlacement.FindProblems).</item>
    /// </list>
    /// </summary>
    public static class RunPlanCheck
    {
        /// <summary>The puzzle screens' problems, and their template fallbacks (a library without a module expects no screens).</summary>
        public static List<string> PuzzleProblems(CubeModel model, IcePlan ice, WindPlan wind, IslandPlan islands, ModuleLibrary library)
        {
            var problems = new List<string>();
            if (islands != null)
            {
                foreach (string p in islands.Problems(model, library != null && library.IslandModuleIndex(Theme.Math) >= 0))
                    problems.Add("islands: " + p);
                foreach (IslandScreenSpec s in islands.Screens)
                    if (s.Layout != null && s.Layout.FromTemplate) problems.Add($"islands: {s} is its template");
            }
            if (wind != null)
            {
                foreach (string p in wind.Problems(model, library != null && library.WindModuleIndex(Theme.EarthAtmosphere) >= 0))
                    problems.Add("wind: " + p);
                foreach (WindScreenSpec s in wind.Screens)
                    if (s.Layout != null && s.Layout.FromTemplate) problems.Add($"wind: {s} is its template");
            }
            if (ice != null)
            {
                foreach (string p in ice.Problems(model, library != null && library.IslandModuleIndex(Theme.EarthAtmosphere) >= 0))
                    problems.Add("ice: " + p);
                foreach (IceScreenSpec s in ice.Screens)
                    if (s.Layout != null && s.Layout.FromTemplate) problems.Add($"ice: {s} is its template");
            }
            return problems;
        }

        /// <summary>The temples' problems (TemplePlan.FindProblems) and their template fallbacks.</summary>
        public static List<string> TempleProblems(CubeModel model)
        {
            var problems = TemplePlan.FindProblems(model).Select(p => "temples: " + p).ToList();
            foreach (FaceId face in TemplePlan.TempleFaces(model))
            {
                TemplePuzzle temple = TemplePlan.For(model, face);
                if (temple != null && temple.FromTemplate) problems.Add($"temples: {face}'s temple ({temple.Kind}) is its template");
            }
            return problems;
        }

        /// <summary>True for the cube the sweep checks: no face sealed on purpose, and towns (so temples and locks) at its size.</summary>
        public static bool IsWholeCube(CubeModel model) => model != null && model.SealedThemes.Count == 0 && TownSquare.HasTown(model.FaceSize);

        /// <summary>
        /// Every problem of a run's revealed plans (empty when all is well). layout and library may be null (nothing past the
        /// puzzle screens is checked then); placement null skips the items and rewards; rewards null skips the rewards. math
        /// (the run's MathPlan, as the sweep passes it) gives Math's island regions first; without it they come from islands
        /// (IslandPlan), which holds the same cached layouts (MathPlan reads IslandPlan.LayoutFor), so the regions agree.
        /// </summary>
        public static List<string> Problems(CubeModel model, CubeLayout layout, ModuleLibrary library, IEnumerable<Theme> itemThemes,
            ItemPlacement placement, HiddenCurrencyPlacement hidden, RewardPlacement rewards, IcePlan ice, WindPlan wind,
            IslandPlan islands, EdgeLocks locks, LairPlan lair, DieNumbers dice = null, MathPlan math = null)
        {
            List<string> problems = PuzzleProblems(model, ice, wind, islands, library);
            if (layout == null || library == null || !IsWholeCube(model)) return problems;
            foreach (string p in SeedSweep.FindCoreProblems(model, layout, library)) problems.Add("core: " + p);
            foreach (string p in LairPlan.FindProblems(model, layout, lair, SeedSweep.FindReachable(model))) problems.Add("lair: " + p);
            problems.AddRange(TempleProblems(model));
            if (locks != null && locks.FellBack) problems.Add("locks: the locks fell back to Town's edges only (no draw passed)");
            if (placement == null) return problems;
            List<PuzzleGoal> puzzles = PuzzleScreens.Goals(ice, wind, islands);
            var regionsAt = PuzzleScreens.RegionsAt(math, ice, wind, islands);
            foreach (string p in SeedSweep.FindItemProblems(model, placement, ItemPlacement.RunItems(layout, itemThemes), regionsAt))
                problems.Add("items: " + p);
            if (rewards != null && hidden != null)
                foreach (string p in RewardPlacement.FindProblems(model, layout, placement, hidden, rewards, library, regionsAt, puzzles, dice))
                    problems.Add("rewards: " + p);
            return problems;
        }
    }
}
