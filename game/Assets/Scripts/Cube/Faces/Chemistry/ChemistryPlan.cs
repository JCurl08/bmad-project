using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>One Chemistry stage gate of the run: where it is and which isotope stage opens it.</summary>
    public readonly struct IsotopeGateSpec : IEquatable<IsotopeGateSpec>
    {
        public readonly ScreenAddress Screen;
        public readonly int Slot;
        public readonly IsotopeStage Stage;

        /// <summary>True when the gate is on the Chemistry face (its home).</summary>
        public readonly bool OnHome;

        public IsotopeGateSpec(ScreenAddress screen, int slot, IsotopeStage stage, bool onHome)
        {
            Screen = screen;
            Slot = slot;
            Stage = stage;
            OnHome = onHome;
        }

        public bool Equals(IsotopeGateSpec other) =>
            Screen == other.Screen && Slot == other.Slot && Stage == other.Stage && OnHome == other.OnHome;

        public override bool Equals(object obj) => obj is IsotopeGateSpec other && Equals(other);
        public override int GetHashCode() => (Screen.GetHashCode() * 31 + Slot) * 31 + (int)Stage;
        public override string ToString() => $"{Stage} gate at {Screen} #{Slot}{(OnHome ? "" : " (off-home)")}";
    }

    /// <summary>
    /// The Chemistry face of a run as pure data, derived from the run seed and the item placement: the decay stage
    /// of every Chemistry gate, the dispenser (the Chemistry pickup's spot) and the trial screen. The isotope only
    /// glows and is unstable for a short while after the dispenser hands it out, so the timed stages (glow,
    /// unstable) go on the Chemistry face near the dispenser and every gate on another face is a lead plate (lead
    /// keeps). On the home face the timed stages are dealt first (glow and unstable in a seeded order), then lead
    /// when no off-home gate has it, then seeded stages, so the coverage rule (RequiredStages) always holds: glow
    /// and unstable once there are two home gates (one of them with a single home gate), lead whenever there is an
    /// off-home gate or a third home gate. Uses its own SeededRng stream.
    /// </summary>
    public sealed class ChemistryPlan
    {
        /// <summary>PCG32 stream for the Chemistry face plan (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.ChemistryPlan;

        /// <summary>Currency the Chemistry trial pays out (fired with TrialRoom.Completed; spent in 1.12).</summary>
        public const int TrialCurrency = 25;

        /// <summary>The three stages, in decay order.</summary>
        public static readonly IsotopeStage[] Stages = { IsotopeStage.Glow, IsotopeStage.Unstable, IsotopeStage.Lead };

        private readonly List<IsotopeGateSpec> gates;

        public int Seed { get; }
        public FaceId Face { get; }
        public int FaceSize { get; }
        public IReadOnlyList<IsotopeGateSpec> Gates => gates;

        /// <summary>The isotope dispenser's placement (the Chemistry pickup's spot).</summary>
        public PickupPlacement Dispenser { get; }

        /// <summary>The Chemistry screen that holds the trial.</summary>
        public ScreenAddress TrialScreen { get; }

        private ChemistryPlan(int seed, FaceId face, int faceSize, List<IsotopeGateSpec> gates, PickupPlacement dispenser,
            ScreenAddress trial)
        {
            Seed = seed;
            Face = face;
            FaceSize = faceSize;
            this.gates = gates;
            Dispenser = dispenser;
            TrialScreen = trial;
        }

        /// <summary>The plan of a run; null when Chemistry is not an item of the placement (no Chemistry pickup).</summary>
        public static ChemistryPlan Create(CubeModel model, ItemPlacement placement)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (placement == null) return null;
            if (!placement.TryGetPickup(Theme.Chemistry, out PickupPlacement dispenser)) return null;

            FaceId face = model.FaceOf(Theme.Chemistry);
            int n = model.FaceSize;
            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);

            var required = new List<GatePlacement>();
            foreach (GatePlacement g in placement.Gates)
                if (g.Item == Theme.Chemistry) required.Add(g);

            var stages = new IsotopeStage[required.Count];
            var home = new List<int>();
            bool offHomeLead = false;
            for (int i = 0; i < required.Count; i++)
            {
                if (required[i].Screen.Face == face) home.Add(i);
                else
                {
                    stages[i] = IsotopeStage.Lead; // far from the dispenser: only lead keeps long enough
                    offHomeLead = true;
                }
            }

            // Home gates: deal every stage not yet used (a seeded order), then seeded stages for the rest.
            var needed = new List<IsotopeStage> { IsotopeStage.Glow, IsotopeStage.Unstable };
            rng.Shuffle(needed); // the timed stages first: they can only go on home gates
            if (!offHomeLead) needed.Add(IsotopeStage.Lead);
            rng.Shuffle(home);
            for (int k = 0; k < home.Count; k++)
                stages[home[k]] = k < needed.Count ? needed[k] : Stages[rng.NextInt(Stages.Length)];

            var specs = new List<IsotopeGateSpec>(required.Count);
            for (int i = 0; i < required.Count; i++)
                specs.Add(new IsotopeGateSpec(required[i].Screen, required[i].Slot, stages[i], required[i].Screen.Face == face));

            int trialCell = rng.NextInt(n * n);
            var trial = new ScreenAddress(face, trialCell % n, trialCell / n);
            return new ChemistryPlan(model.Seed, face, n, specs, dispenser, trial);
        }

        public bool TryGetGate(ScreenAddress screen, int slot, out IsotopeGateSpec spec)
        {
            foreach (IsotopeGateSpec s in gates)
            {
                if (s.Screen != screen || s.Slot != slot) continue;
                spec = s;
                return true;
            }
            spec = default;
            return false;
        }

        /// <summary>
        /// The coverage rule ("each stage at least once when there are enough gates"): the stages a run with this many
        /// home and off-home Chemistry gates must use. Off-home gates are always lead, so glow and unstable need home
        /// gates: both with two or more (a single home gate gets one of them, checked separately). Lead is required
        /// with an off-home gate, or with three or more home gates.
        /// </summary>
        public static List<IsotopeStage> RequiredStages(int homeGates, int offHomeGates)
        {
            var stages = new List<IsotopeStage>();
            if (homeGates >= 2)
            {
                stages.Add(IsotopeStage.Glow);
                stages.Add(IsotopeStage.Unstable);
            }
            if (offHomeGates > 0 || homeGates >= 3) stages.Add(IsotopeStage.Lead);
            return stages;
        }

        /// <summary>
        /// The trial's piece spots on its module (local to the screen centre): the first draws of the population
        /// stream, as ChemistryFace.EndReveal takes them. Fewer than ChemistryTrial.PieceCount when they do not fit.
        /// </summary>
        public static List<Vector2> TrialSpots(int seed, ScreenModule module)
        {
            var rng = new SeededRng(unchecked((ulong)(uint)seed), ChemistryFace.PopulationRngStream);
            return ChemistryFace.TrialSpots(module, rng);
        }

        /// <summary>Number of gates of a stage.</summary>
        public int CountOf(IsotopeStage stage)
        {
            int count = 0;
            foreach (IsotopeGateSpec s in gates)
                if (s.Stage == stage) count++;
            return count;
        }

        /// <summary>Compact text of the plan, for comparisons and logs.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("dispenser@").Append(Dispenser).Append(" trial@").Append(TrialScreen).Append(' ');
            foreach (IsotopeGateSpec s in gates) sb.Append(s).Append("; ");
            return sb.ToString();
        }

        /// <summary>
        /// Problems with a run's Chemistry plan: every Chemistry gate has a stage; the timed stages (glow, unstable)
        /// sit on the Chemistry face; the isotope opens a gate on another face (when another face is built); with
        /// the stages RequiredStages asks for are used (and a single home gate has a timed stage); the trial is on
        /// Chemistry and, given a moduleAt lookup (the module prefab of a screen), fits on its screen; and the
        /// dispenser comes first (DispenserProblems). Empty when all is well (or when Chemistry is not an item).
        /// </summary>
        public static List<string> FindProblems(CubeModel model, ItemPlacement placement,
            Func<ScreenAddress, ScreenModule> moduleAt = null)
        {
            var problems = new List<string>();
            ChemistryPlan plan = Create(model, placement);
            if (plan == null) return problems;

            int builtFaces = 0;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(model, (FaceId)f)) builtFaces++;

            int required = 0;
            foreach (GatePlacement g in placement.Gates)
            {
                if (g.Item != Theme.Chemistry) continue;
                required++;
                if (!plan.TryGetGate(g.Screen, g.Slot, out _)) problems.Add($"{g} has no isotope stage");
            }
            if (required != plan.Gates.Count) problems.Add($"{plan.Gates.Count} stage gates for {required} Chemistry gates");

            int offHome = 0;
            foreach (IsotopeGateSpec s in plan.Gates)
            {
                if (s.Stage == IsotopeStage.None) problems.Add($"{s} has no stage");
                if (s.OnHome != (s.Screen.Face == plan.Face)) problems.Add($"{s}: wrong home flag");
                if (s.Screen.Face != plan.Face)
                {
                    offHome++;
                    if (s.Stage != IsotopeStage.Lead) problems.Add($"{s}: a timed stage far from the dispenser");
                }
            }
            if (offHome < 1 && builtFaces > 1) problems.Add("the isotope opens no gate on another face");
            int home = plan.Gates.Count - offHome;
            foreach (IsotopeStage stage in RequiredStages(home, offHome))
                if (plan.CountOf(stage) == 0) problems.Add($"no {stage} gate ({home} home, {offHome} off-home gates)");
            if (home == 1 && plan.CountOf(IsotopeStage.Glow) + plan.CountOf(IsotopeStage.Unstable) == 0)
                problems.Add("the only home gate has no timed stage");
            if (plan.TrialScreen.Face != plan.Face) problems.Add($"the trial is on {plan.TrialScreen}, not on Chemistry");
            if (moduleAt != null)
            {
                ScreenModule module = moduleAt(plan.TrialScreen);
                if (module == null) problems.Add($"no module on the trial screen {plan.TrialScreen}");
                else if (TrialSpots(model.Seed, module).Count < ChemistryTrial.PieceCount)
                    problems.Add($"the trial does not fit on {plan.TrialScreen}");
            }
            problems.AddRange(DispenserProblems(model, placement));
            return problems;
        }

        /// <summary>
        /// The dispenser-first rule: the isotope dispenser (the Chemistry pickup) is on the Chemistry face, on a
        /// reachable screen, and reachable without passing any Chemistry-stage gate: following its guard chain (the
        /// gate in front of it, the pickup of that gate's item, the gate in front of that, ...) never meets a
        /// Chemistry gate or loops. So a player can always fetch a fresh isotope before any stage gate.
        /// </summary>
        public static List<string> DispenserProblems(CubeModel model, ItemPlacement placement)
        {
            var problems = new List<string>();
            if (!placement.TryGetPickup(Theme.Chemistry, out PickupPlacement dispenser))
            {
                problems.Add("no isotope dispenser");
                return problems;
            }
            if (dispenser.Screen.Face != model.FaceOf(Theme.Chemistry))
                problems.Add($"the dispenser is on {dispenser.Screen.Face}, not on Chemistry");

            HashSet<ScreenAddress> reachable = SeedSweep.FindReachable(model);
            var seen = new HashSet<Theme> { Theme.Chemistry };
            PickupPlacement current = dispenser;
            for (int step = 0; step <= placement.Pickups.Count; step++)
            {
                if (!reachable.Contains(current.Screen))
                {
                    problems.Add($"dispenser-first: {current} is on an unreachable screen");
                    break;
                }
                if (!current.IsGuarded) break;
                if (!placement.TryGetGate(current.Screen, current.GuardSlot, out GatePlacement guard))
                {
                    problems.Add($"dispenser-first: {current} has no gate in its slot");
                    break;
                }
                if (guard.Item == Theme.Chemistry)
                {
                    problems.Add($"dispenser-first: the dispenser needs a Chemistry-stage gate first ({guard})");
                    break;
                }
                if (!seen.Add(guard.Item))
                {
                    problems.Add($"dispenser-first: the dispenser's guard chain loops at {guard}");
                    break;
                }
                if (!placement.TryGetPickup(guard.Item, out current))
                {
                    problems.Add($"dispenser-first: {guard} guards the dispenser but its item has no pickup");
                    break;
                }
            }
            return problems;
        }
    }
}
