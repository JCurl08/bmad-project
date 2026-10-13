using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// 4.21: the work the science reveal (the first step off Town) and the temples' unsealing would do, cut into small steps
    /// that CubeWorld runs a few at a time over frames while the player is still in Town: the run's CubeLayout, every ice,
    /// wind and island screen's layout (IcePlan/WindPlan/IslandPlan.LayoutFor, the same keys and tiers their Create uses) and
    /// every temple's puzzle (TemplePlan.For). The layouts and puzzles land in the plans' caches, so the reveal frame only
    /// builds GameObjects. Determinism is untouched: each generator makes its own seeded stream from the seed and the
    /// screen or face, so nothing drawn here shifts any other draw, and the reveal's plans are the same warmed or cold.
    /// Pure: no scene needed (tests run Steps to the end).
    /// </summary>
    public sealed class GenerationPrewarm
    {
        private readonly IModuleCatalog library;
        private readonly DieNumbers dice;

        public CubeModel Model { get; }

        /// <summary>The run's science layout once its step ran (null before, or without a library).</summary>
        public CubeLayout Layout { get; private set; }

        /// <summary>True once every step ran.</summary>
        public bool Done { get; private set; }

        /// <summary>Steps run so far.</summary>
        public int StepsRun { get; private set; }

        public GenerationPrewarm(CubeModel model, IModuleCatalog library, DieNumbers dice = null)
        {
            Model = model;
            this.library = library;
            this.dice = dice ?? (model != null ? DieNumbers.Roll(model) : null);
        }

        /// <summary>The warmed layout for a model: only this run's (the same model), else null (make it afresh).</summary>
        public CubeLayout LayoutFor(CubeModel model) => model != null && ReferenceEquals(model, Model) ? Layout : null;

        /// <summary>The steps, run one per MoveNext; each yields what it made (for logs and tests).</summary>
        public IEnumerable<string> Steps()
        {
            if (Model == null) yield break;
            if (library != null)
            {
                Layout = CubeLayout.Generate(Model, library);
                StepsRun++;
                yield return "layout";
            }
            if (Layout != null)
            {
                FaceId earth = Model.FaceOf(Theme.EarthAtmosphere);
                if (Layout.TryGetFace(earth, out FaceLayout earthLayout))
                {
                    PuzzleTier tier = PuzzleTiers.OfDieNumber(dice.Of(earth));
                    foreach (Vector2Int cell in earthLayout.IslandCells)
                    {
                        IcePlan.LayoutFor(Model, new ScreenAddress(earth, cell), tier);
                        StepsRun++;
                        yield return $"ice {cell}";
                    }
                    foreach (Vector2Int cell in earthLayout.WindCells)
                    {
                        WindPlan.LayoutFor(Model, new ScreenAddress(earth, cell), tier);
                        StepsRun++;
                        yield return $"wind {cell}";
                    }
                }
                FaceId math = Model.FaceOf(Theme.Math);
                if (Layout.TryGetFace(math, out FaceLayout mathLayout))
                {
                    PuzzleTier tier = PuzzleTiers.OfDieNumber(dice.Of(math));
                    // As IslandPlan.Create: the cells in row order, one of them the trial.
                    List<Vector2Int> cells = mathLayout.IslandCells.OrderBy(c => c.y).ThenBy(c => c.x).ToList();
                    int trial = IslandPlan.TrialIndex(Model.Seed, cells.Count);
                    for (int i = 0; i < cells.Count; i++)
                    {
                        IslandPlan.LayoutFor(Model, new ScreenAddress(math, cells[i]), tier, i == trial);
                        StepsRun++;
                        yield return $"islands {cells[i]}";
                    }
                }
            }
            foreach (FaceId face in TemplePlan.TempleFaces(Model))
            {
                TemplePlan.For(Model, face);
                StepsRun++;
                yield return $"temple {face}";
            }
            Done = true;
        }

        /// <summary>Runs every step now (tests and tools).</summary>
        public GenerationPrewarm RunAll()
        {
            foreach (string _ in Steps())
            {
            }
            return this;
        }
    }
}
