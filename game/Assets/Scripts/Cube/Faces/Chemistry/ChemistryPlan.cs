using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>One Chemistry stage gate of the run: where it is, which stone opens it, and its difficulty.</summary>
    public readonly struct IsotopeGateSpec : IEquatable<IsotopeGateSpec>
    {
        public readonly ScreenAddress Screen;
        public readonly int Slot;
        public readonly IsotopeStage Stage;

        /// <summary>True when the gate is on the Chemistry face (its home).</summary>
        public readonly bool OnHome;

        /// <summary>True for the other stones' optional gates (guard nothing required).</summary>
        public readonly bool Optional;

        /// <summary>The number of stage gates on this gate's face (sets how many swings it takes).</summary>
        public readonly int Difficulty;

        public IsotopeGateSpec(ScreenAddress screen, int slot, IsotopeStage stage, bool onHome, bool optional = false,
            int difficulty = 1)
        {
            Screen = screen;
            Slot = slot;
            Stage = stage;
            OnHome = onHome;
            Optional = optional;
            Difficulty = difficulty;
        }

        public IsotopeGateSpec WithDifficulty(int difficulty) =>
            new IsotopeGateSpec(Screen, Slot, Stage, OnHome, Optional, difficulty);

        public bool Equals(IsotopeGateSpec other) =>
            Screen == other.Screen && Slot == other.Slot && Stage == other.Stage && OnHome == other.OnHome &&
            Optional == other.Optional && Difficulty == other.Difficulty;

        public override bool Equals(object obj) => obj is IsotopeGateSpec other && Equals(other);
        public override int GetHashCode() => ((Screen.GetHashCode() * 31 + Slot) * 31 + (int)Stage) * 31 + (Optional ? 1 : 0);

        public override string ToString() =>
            $"{(Optional ? "optional " : "")}{Stage} gate at {Screen} #{Slot}{(OnHome ? "" : " (off-home)")}, difficulty {Difficulty}";
    }

    /// <summary>
    /// The Chemistry face of a run as pure data, on the beak pattern (BiologyPlan), derived from the run seed and the item
    /// placement: the rolled stone (the placement's rolled Chemistry variant, its stage read from the item ids), the stage
    /// of every Chemistry gate and the trial screen. Every required Chemistry gate (home or off-home) takes the rolled
    /// stone's stage: a dark room (Glowstone*), a cracked wall (Wildstone*) or a plate door (Leadstone*). The other
    /// stones' optional gates (ItemPlacement.OptionalGates) take their own stage and guard nothing required. Each gate's
    /// difficulty is the number of stage gates on its face. Uses its own SeededRng stream (the trial screen).
    /// </summary>
    public sealed class ChemistryPlan
    {
        /// <summary>PCG32 stream for the Chemistry face plan (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.ChemistryPlan;

        /// <summary>Currency the Chemistry trial pays out (fired with TrialRoom.Completed; spent in 1.12).</summary>
        public const int TrialCurrency = 25;

        /// <summary>The three stages (ChemistryIsotope.VariantOrder, the builder's canonical order; a stage is never read from it).</summary>
        public static readonly IsotopeStage[] Stages = ChemistryIsotope.VariantOrder;

        /// <summary>Stage gates per extra swing on a face of CubeSettings.TunedScreensPerFace screens (scaled per screen).</summary>
        public const int TunedGatesPerExtraHit = 3;

        /// <summary>Stage gates per extra swing on a face of faceSize x faceSize screens.</summary>
        public static int GatesPerExtraHit(int faceSize) => CubeSettings.PerScreen(TunedGatesPerExtraHit, faceSize);

        /// <summary>
        /// Swings a stage gate needs on a face of faceSize x faceSize screens: one, plus one for every GatesPerExtraHit
        /// stage gates beyond the first on its face (the beak gates' rule), so the toughness follows the gate density.
        /// </summary>
        public static int HitsFor(int difficulty, int faceSize) =>
            1 + (Mathf.Max(1, difficulty) - 1) / GatesPerExtraHit(faceSize);

        private readonly List<IsotopeGateSpec> gates;

        public int Seed { get; }

        /// <summary>The run's rolled stone.</summary>
        public IsotopeStage Stage { get; }

        public FaceId Face { get; }
        public int FaceSize { get; }
        public IReadOnlyList<IsotopeGateSpec> Gates => gates;

        /// <summary>The Chemistry screen that holds the trial.</summary>
        public ScreenAddress TrialScreen { get; }

        private ChemistryPlan(int seed, IsotopeStage stage, FaceId face, int faceSize, List<IsotopeGateSpec> gates,
            ScreenAddress trial)
        {
            Seed = seed;
            Stage = stage;
            Face = face;
            FaceSize = faceSize;
            this.gates = gates;
            TrialScreen = trial;
        }

        /// <summary>
        /// The plan of a run, with the stone the placement rolled for Chemistry. variantKinds maps each catalog variant
        /// index to its stage, read from the items' ids (ChemistryIsotope.VariantKinds); there is no order-based fallback.
        /// Null when Chemistry is not a laid-out item of the placement, has no variants, or variantKinds does not cover
        /// the rolled variant.
        /// </summary>
        public static ChemistryPlan Create(CubeModel model, ItemPlacement placement, IReadOnlyList<IsotopeStage> variantKinds)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (placement == null) return null;
            IsotopeStage stage = KindOfVariant(variantKinds, placement.RolledVariant(Theme.Chemistry));
            if (stage == IsotopeStage.None) return null;
            return Create(model, placement, stage, variantKinds);
        }

        /// <summary>The stage of a variant index from the id-derived kinds; None when unknown.</summary>
        private static IsotopeStage KindOfVariant(IReadOnlyList<IsotopeStage> variantKinds, int variant) =>
            variantKinds != null && variant >= 0 && variant < variantKinds.Count ? variantKinds[variant] : IsotopeStage.None;

        /// <summary>The plan for a known rolled stone; optional gates take their stage from variantKinds (see above).</summary>
        public static ChemistryPlan Create(CubeModel model, ItemPlacement placement, IsotopeStage stage,
            IReadOnlyList<IsotopeStage> variantKinds)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            if (stage == IsotopeStage.None) throw new ArgumentException("A run's stone has a stage", nameof(stage));
            FaceId face = model.FaceOf(Theme.Chemistry);
            int n = model.FaceSize;
            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);

            var specs = new List<IsotopeGateSpec>();
            foreach (GatePlacement g in placement.Gates)
                if (g.Item == Theme.Chemistry)
                    specs.Add(new IsotopeGateSpec(g.Screen, g.Slot, stage, g.Screen.Face == face));
            foreach (OptionalGatePlacement g in placement.OptionalGates)
            {
                if (g.Item != Theme.Chemistry) continue;
                IsotopeStage gateStage = KindOfVariant(variantKinds, g.Variant);
                // Unknown or malformed data: never let an optional gate use the rolled stone, or no stone at all.
                if (gateStage == stage || gateStage == IsotopeStage.None) continue;
                specs.Add(new IsotopeGateSpec(g.Screen, g.Slot, gateStage, g.Screen.Face == face, true));
            }

            // Difficulty: the number of stage gates on each gate's face.
            for (int i = 0; i < specs.Count; i++)
            {
                int count = 0;
                foreach (IsotopeGateSpec s in specs)
                    if (s.Screen.Face == specs[i].Screen.Face) count++;
                specs[i] = specs[i].WithDifficulty(count);
            }

            int trialCell = TownSquare.PickCellIndex(rng, n); // never the town (4.5)
            var trial = new ScreenAddress(face, trialCell % n, trialCell / n);
            return new ChemistryPlan(model.Seed, stage, face, n, specs, trial);
        }

        public bool TryGetGate(ScreenAddress screen, int slot, bool optional, out IsotopeGateSpec spec)
        {
            foreach (IsotopeGateSpec s in gates)
            {
                if (s.Screen != screen || s.Slot != slot || s.Optional != optional) continue;
                spec = s;
                return true;
            }
            spec = default;
            return false;
        }

        /// <summary>
        /// The run's stage plan as ChemistryFace makes it at the reveal: only when the catalog's Chemistry variants are
        /// stones, the rolled item is one and the placement rolled a variant; otherwise null (plain gates).
        /// </summary>
        public static ChemistryPlan ForRun(CubeModel model, ItemPlacement placement, ItemDefinition stoneItem,
            IReadOnlyList<IsotopeStage> variantKinds)
        {
            IsotopeStage? rolled = ChemistryIsotope.KindOf(stoneItem);
            if (variantKinds == null || rolled == null || placement == null || placement.RolledVariant(Theme.Chemistry) < 0) return null;
            return Create(model, placement, rolled.Value, variantKinds);
        }

        /// <summary>Whether ChemistryFace builds a planned stage gate for an item: only the stone of that stage opens it.</summary>
        public static bool Builds(IsotopeGateSpec spec, ItemDefinition item) => ChemistryIsotope.KindOf(item) == spec.Stage;

        /// <summary>The look (GateLooks) of the gate ChemistryFace builds at a slot for an item, or null where it builds none.</summary>
        public string GateLook(ScreenAddress screen, int slot, bool optional, ItemDefinition item) =>
            TryGetGate(screen, slot, optional, out IsotopeGateSpec spec) && Builds(spec, item) ? GateLooks.Of(spec.Stage) : null;

        /// <summary>
        /// The trial's piece spots on its module (local to the screen centre): the first draws of the population
        /// stream, as ChemistryFace.EndReveal takes them. Fewer than ChemistryTrial.PieceCount when they do not fit.
        /// </summary>
        public static List<Vector2> TrialSpots(int seed, ScreenModule module)
        {
            var rng = new SeededRng(unchecked((ulong)(uint)seed), ChemistryFace.PopulationRngStream);
            return ChemistryFace.TrialSpots(module, rng);
        }

        private static bool HasOptional(ChemistryPlan plan, IsotopeStage stage)
        {
            foreach (IsotopeGateSpec s in plan.gates)
                if (s.Optional && s.Stage == stage) return true;
            return false;
        }

        /// <summary>Number of stage gates on a face (the difficulty of each gate there).</summary>
        public int DifficultyOn(FaceId face)
        {
            int count = 0;
            foreach (IsotopeGateSpec s in gates)
                if (s.Screen.Face == face) count++;
            return count;
        }

        /// <summary>Compact text of the plan, for comparisons and logs.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(Stage).Append(" trial@").Append(TrialScreen).Append(' ');
            foreach (IsotopeGateSpec s in gates) sb.Append(s).Append("; ");
            return sb.ToString();
        }

        /// <summary>
        /// Problems with a run's Chemistry plan: every required Chemistry gate has a spec with the rolled stone's stage;
        /// every optional gate uses another stone and sits on the Chemistry face; the rolled stone opens a gate on another
        /// face (when another face is built); with at least one optional gate per other stone, every other stone has one; each gate's difficulty is the stage gates on its face; the trial is on
        /// Chemistry and, given a moduleAt lookup (the module prefab of a screen), fits on its screen. Empty when all is
        /// well (or when the run has no Chemistry stones). That optional gates guard nothing required is the sweep's
        /// generic optional-gate rule (SeedSweep.FindOptionalGateProblems).
        /// </summary>
        public static List<string> FindProblems(CubeModel model, ItemPlacement placement,
            IReadOnlyList<IsotopeStage> variantKinds, Func<ScreenAddress, ScreenModule> moduleAt = null)
        {
            var problems = new List<string>();
            ChemistryPlan plan = Create(model, placement, variantKinds);
            if (plan == null) return problems;

            int builtFaces = 0;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(model, (FaceId)f)) builtFaces++;

            int required = 0;
            foreach (GatePlacement g in placement.Gates)
            {
                if (g.Item != Theme.Chemistry) continue;
                required++;
                if (!plan.TryGetGate(g.Screen, g.Slot, false, out _)) problems.Add($"{g} has no stage gate");
            }
            int optional = 0;
            foreach (OptionalGatePlacement g in placement.OptionalGates)
            {
                if (g.Item != Theme.Chemistry) continue;
                optional++;
                if (!plan.TryGetGate(g.Screen, g.Slot, true, out _)) problems.Add($"{g} has no stage gate");
            }
            if (required + optional != plan.Gates.Count)
                problems.Add($"{plan.Gates.Count} stage gates for {required} required and {optional} optional Chemistry gates");

            int offHome = 0;
            foreach (IsotopeGateSpec s in plan.Gates)
            {
                if (s.Stage == IsotopeStage.None) problems.Add($"{s} has no stage");
                if (s.OnHome != (s.Screen.Face == plan.Face)) problems.Add($"{s}: wrong home flag");
                if (!s.Optional && s.Stage != plan.Stage) problems.Add($"{s}: a required gate needs another stone");
                if (s.Optional && s.Stage == plan.Stage) problems.Add($"{s}: an optional gate uses the rolled stone");
                if (s.Optional && !s.OnHome) problems.Add($"{s}: optional stage gates belong on Chemistry");
                if (!s.Optional && !s.OnHome) offHome++;
                if (s.Difficulty != plan.DifficultyOn(s.Screen.Face)) problems.Add($"{s}: wrong difficulty");
            }
            if (offHome < 1 && builtFaces > 1) problems.Add($"the {plan.Stage} stone opens no gate on another face");
            // Optional gates are dealt one per other stone per round, so once there are enough of them, each other
            // stone has one (a small face may only have room for fewer).
            if (optional >= Stages.Length - 1)
                foreach (IsotopeStage other in Stages)
                    if (other != plan.Stage && !HasOptional(plan, other)) problems.Add($"no optional {other} gate (the other stones' gates are extras)");
            if (plan.TrialScreen.Face != plan.Face) problems.Add($"the trial is on {plan.TrialScreen}, not on Chemistry");
            if (TownSquare.IsTown(plan.TrialScreen, plan.FaceSize)) problems.Add($"the trial is on the town {plan.TrialScreen}");
            if (moduleAt != null)
            {
                ScreenModule module = moduleAt(plan.TrialScreen);
                if (module == null) problems.Add($"no module on the trial screen {plan.TrialScreen}");
                else if (TrialSpots(model.Seed, module).Count < ChemistryTrial.PieceCount)
                    problems.Add($"the trial does not fit on {plan.TrialScreen}");
            }
            return problems;
        }
    }
}
