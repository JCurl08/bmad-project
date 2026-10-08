using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>How a beak gate opens.</summary>
    public enum BeakGateKind
    {
        /// <summary>Thick beak: a rock or pot in the alcove opening, broken by attacking it.</summary>
        Breakable = 0,

        /// <summary>Thin beak: a door opened by pecking its distant button.</summary>
        Button = 1,

        /// <summary>Thin beak: a bramble that becomes a vine bridge when a flower on an adjacent screen is pollinated.</summary>
        FlowerVine = 2,
    }

    /// <summary>What a breakable gate looks like.</summary>
    public enum BreakableLook { Rock = 0, Pot = 1 }

    /// <summary>One beak gate of the run: where it is, which beak and mechanic open it, and its difficulty.</summary>
    public readonly struct BeakGateSpec
    {
        public readonly ScreenAddress Screen;
        public readonly int Slot;
        public readonly BeakKind Beak;
        public readonly BeakGateKind Kind;

        /// <summary>True for the non-rolled beak's optional gates.</summary>
        public readonly bool Optional;

        /// <summary>For FlowerVine: the adjacent screen (same face) whose flower opens it.</summary>
        public readonly ScreenAddress FlowerScreen;

        public readonly BreakableLook Look;

        /// <summary>The number of beak gates on this gate's face (sets how many beak hits it takes).</summary>
        public readonly int Difficulty;

        public BeakGateSpec(ScreenAddress screen, int slot, BeakKind beak, BeakGateKind kind, bool optional,
            ScreenAddress flowerScreen, BreakableLook look, int difficulty)
        {
            Screen = screen;
            Slot = slot;
            Beak = beak;
            Kind = kind;
            Optional = optional;
            FlowerScreen = flowerScreen;
            Look = look;
            Difficulty = difficulty;
        }

        public BeakGateSpec WithDifficulty(int difficulty) =>
            new BeakGateSpec(Screen, Slot, Beak, Kind, Optional, FlowerScreen, Look, difficulty);

        public override string ToString()
        {
            string extra = Kind == BeakGateKind.FlowerVine ? $" (flower on {FlowerScreen})"
                : Kind == BeakGateKind.Breakable ? $" ({Look})" : "";
            return $"{(Optional ? "optional " : "")}{Beak} {Kind}{extra} at {Screen} #{Slot}, difficulty {Difficulty}";
        }
    }

    /// <summary>
    /// The Biology face of a run as pure data, derived from the run seed and the item placement: the rolled
    /// beak, how each beak gate opens, and the trial screen. For the thick beak every Biology gate is a rock or
    /// pot. For the thin beak the first home-face Biology gate is a flower-vine gate whose flower sits on an
    /// adjacent Biology screen, and every other one is a distant-button door. The other beak's optional gates
    /// (ItemPlacement.OptionalGates) get that beak's mechanic (rock/pot, or button). Each gate's difficulty is
    /// the number of beak gates on its face. Uses its own SeededRng stream. Neither the trial nor a flower is ever on a town
    /// screen (4.5).
    /// </summary>
    public sealed class BiologyPlan
    {
        /// <summary>PCG32 stream for the Biology face plan (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.BiologyPlan;

        /// <summary>Currency the Biology trial pays out (fired with TrialRoom.Completed; spent in 1.12).</summary>
        public const int TrialCurrency = 25;

        /// <summary>Beak gates per extra hit on a face of CubeSettings.TunedScreensPerFace screens (scaled per screen by HitsFor).</summary>
        public const int TunedGatesPerExtraHit = 3;

        /// <summary>
        /// Beak hits a gate needs on a face of faceSize x faceSize screens: one, plus one for every GatesPerExtraHit beak
        /// gates beyond the first on its face (3 on a 2x2 face, 7 on a 3x3 one), so the toughness follows the gate density
        /// per screen rather than the face size.
        /// </summary>
        public static int HitsFor(int difficulty, int faceSize) =>
            1 + (Mathf.Max(1, difficulty) - 1) / GatesPerExtraHit(faceSize);

        /// <summary>Beak gates per extra hit on a face of faceSize x faceSize screens.</summary>
        public static int GatesPerExtraHit(int faceSize) => CubeSettings.PerScreen(TunedGatesPerExtraHit, faceSize);

        /// <summary>Flower-vine gates of a thin-beak run on a face of CubeSettings.TunedScreensPerFace screens.</summary>
        public const int TunedFlowerVineCount = 1;

        /// <summary>Flower-vine gates of a thin-beak run on a face of faceSize x faceSize screens (1 at 2x2, 2 at 3x3).</summary>
        public static int FlowerVineCount(int faceSize) => CubeSettings.PerScreen(TunedFlowerVineCount, faceSize);

        private readonly List<BeakGateSpec> gates;

        public int Seed { get; }
        public BeakKind Beak { get; }
        public FaceId Face { get; }
        public int FaceSize { get; }
        public IReadOnlyList<BeakGateSpec> Gates => gates;

        /// <summary>The Biology screen that holds the trial.</summary>
        public ScreenAddress TrialScreen { get; }

        private BiologyPlan(int seed, BeakKind beak, FaceId face, int faceSize, List<BeakGateSpec> gates, ScreenAddress trial)
        {
            Seed = seed;
            Beak = beak;
            Face = face;
            FaceSize = faceSize;
            this.gates = gates;
            TrialScreen = trial;
        }

        /// <summary>
        /// The plan of a run, with the beak the placement rolled for Biology. variantKinds maps each catalog
        /// variant index to its beak (BiologyBeaks.VariantKinds reads it from the items' ids); null assumes
        /// BiologyBeaks.VariantOrder. Returns null when Biology is not a laid-out item of the placement or has no
        /// beak variants.
        /// </summary>
        public static BiologyPlan Create(CubeModel model, ItemPlacement placement, IReadOnlyList<BeakKind> variantKinds = null)
        {
            if (placement == null) return null;
            int variant = placement.RolledVariant(Theme.Biology);
            if (variant < 0) return null;
            return Create(model, placement, KindOfVariant(variantKinds, variant), variantKinds);
        }

        private static BeakKind KindOfVariant(IReadOnlyList<BeakKind> variantKinds, int variant) =>
            variantKinds != null && variant >= 0 && variant < variantKinds.Count
                ? variantKinds[variant]
                : BiologyBeaks.FromVariant(variant);

        /// <summary>The plan for a known rolled beak; optional gates take their beak from variantKinds (see above).</summary>
        public static BiologyPlan Create(CubeModel model, ItemPlacement placement, BeakKind beak,
            IReadOnlyList<BeakKind> variantKinds = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            FaceId face = model.FaceOf(Theme.Biology);
            int n = model.FaceSize;
            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);
            var specs = new List<BeakGateSpec>();

            int flowerCount = n > 1 ? FlowerVineCount(n) : 0;
            var flowerScreens = new List<ScreenAddress>();
            foreach (GatePlacement g in placement.Gates)
            {
                if (g.Item != Theme.Biology) continue;
                BeakGateKind kind = BeakGateKind.Button;
                var flower = default(ScreenAddress);
                if (beak == BeakKind.Thick)
                {
                    kind = BeakGateKind.Breakable;
                }
                else if (flowerScreens.Count < flowerCount && g.Screen.Face == face)
                {
                    // Each flower on its own adjacent screen (a flower sits at the fixed FlowerLocal spot).
                    List<ScreenAddress> adjacent = Adjacent(g.Screen, n);
                    adjacent.RemoveAll(flowerScreens.Contains);
                    TownSquare.WithoutTown(adjacent, n); // a flower never grows in a town (4.5)
                    if (adjacent.Count > 0)
                    {
                        kind = BeakGateKind.FlowerVine;
                        flower = adjacent[rng.NextInt(adjacent.Count)];
                        flowerScreens.Add(flower);
                    }
                }
                var look = (BreakableLook)rng.NextInt(2);
                specs.Add(new BeakGateSpec(g.Screen, g.Slot, beak, kind, false, flower, look, 1));
            }

            BeakKind other = BiologyBeaks.Other(beak);
            foreach (OptionalGatePlacement g in placement.OptionalGates)
            {
                if (g.Item != Theme.Biology) continue;
                BeakKind gateBeak = KindOfVariant(variantKinds, g.Variant);
                if (gateBeak == beak) gateBeak = other; // malformed data: never let an optional gate use the rolled beak
                BeakGateKind kind = gateBeak == BeakKind.Thick ? BeakGateKind.Breakable : BeakGateKind.Button;
                var look = (BreakableLook)rng.NextInt(2);
                specs.Add(new BeakGateSpec(g.Screen, g.Slot, gateBeak, kind, true, default, look, 1));
            }

            // Difficulty: the number of beak gates on each gate's face.
            for (int i = 0; i < specs.Count; i++)
            {
                int count = 0;
                foreach (BeakGateSpec s in specs)
                    if (s.Screen.Face == specs[i].Screen.Face) count++;
                specs[i] = specs[i].WithDifficulty(count);
            }

            int trialCell = TownSquare.PickCellIndex(rng, n); // never the town (4.5)
            var trial = new ScreenAddress(face, trialCell % n, trialCell / n);
            return new BiologyPlan(model.Seed, beak, face, n, specs, trial);
        }

        /// <summary>The screens next to a screen on the same face (up to four), in N, E, S, W order.</summary>
        public static List<ScreenAddress> Adjacent(ScreenAddress screen, int faceSize)
        {
            var list = new List<ScreenAddress>();
            for (int f = 0; f < 4; f++)
            {
                Vector2Int cell = screen.Cell + ((Facing)f).ToVector();
                if (cell.x < 0 || cell.y < 0 || cell.x >= faceSize || cell.y >= faceSize) continue;
                list.Add(new ScreenAddress(screen.Face, cell));
            }
            return list;
        }

        public bool TryGetGate(ScreenAddress screen, int slot, bool optional, out BeakGateSpec spec)
        {
            foreach (BeakGateSpec s in gates)
            {
                if (s.Screen != screen || s.Slot != slot || s.Optional != optional) continue;
                spec = s;
                return true;
            }
            spec = default;
            return false;
        }

        /// <summary>
        /// The run's beak plan as BiologyFace makes it at the reveal: only when the catalog's Biology variants are beaks,
        /// the rolled item is one and the placement rolled a variant; otherwise null (every Biology gate is a plain gate).
        /// </summary>
        public static BiologyPlan ForRun(CubeModel model, ItemPlacement placement, ItemDefinition beakItem,
            IReadOnlyList<BeakKind> variantKinds)
        {
            BeakKind? rolled = BiologyBeaks.KindOf(beakItem);
            if (variantKinds == null || rolled == null || placement == null || placement.RolledVariant(Theme.Biology) < 0) return null;
            return Create(model, placement, rolled.Value, variantKinds);
        }

        /// <summary>The look (GateLooks) of the gate BiologyFace builds at a slot, or null where it builds none (a plain gate).</summary>
        public string GateLook(ScreenAddress screen, int slot, bool optional) =>
            TryGetGate(screen, slot, optional, out BeakGateSpec spec) ? LookOf(spec) : null;

        /// <summary>The look of a beak gate: the gate component BiologyFace.CreateGate builds for its kind.</summary>
        public static string LookOf(BeakGateSpec spec)
        {
            switch (spec.Kind)
            {
                case BeakGateKind.Breakable: return spec.Look == BreakableLook.Pot ? GateLooks.Pot : GateLooks.Rock;
                case BeakGateKind.FlowerVine: return GateLooks.Bramble;
                default: return GateLooks.ButtonDoor;
            }
        }

        /// <summary>Number of beak gates on a face (the difficulty of each gate there).</summary>
        public int DifficultyOn(FaceId face)
        {
            int count = 0;
            foreach (BeakGateSpec s in gates)
                if (s.Screen.Face == face) count++;
            return count;
        }

        /// <summary>Compact text of the plan, for comparisons and logs.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(Beak).Append(" trial@").Append(TrialScreen).Append(' ');
            foreach (BeakGateSpec s in gates) sb.Append(s).Append("; ");
            return sb.ToString();
        }

        /// <summary>
        /// Problems with a run's Biology plan: required gates use the rolled beak and optional ones the other;
        /// the rolled beak has a gate off its home face (when another face is built); a thin-beak run has
        /// FlowerVineCount flower-vine gates (fewer only with fewer home gates), each flower on its own adjacent screen of the same face; the trial is on
        /// the Biology face. Empty when all is well (or when the run has no beak variants).
        /// </summary>
        public static List<string> FindProblems(CubeModel model, ItemPlacement placement,
            IReadOnlyList<BeakKind> variantKinds = null)
        {
            var problems = new List<string>();
            BiologyPlan plan = Create(model, placement, variantKinds);
            if (plan == null) return problems;

            int offHome = 0, flowers = 0, homeRequired = 0;
            var flowerScreens = new HashSet<ScreenAddress>();
            int builtFaces = 0;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(model, (FaceId)f)) builtFaces++;

            foreach (BeakGateSpec s in plan.Gates)
            {
                if (!s.Optional && s.Beak != plan.Beak) problems.Add($"{s}: a required gate needs the other beak");
                if (s.Optional && s.Beak == plan.Beak) problems.Add($"{s}: an optional gate uses the rolled beak");
                if (s.Optional && s.Screen.Face != plan.Face) problems.Add($"{s}: optional beak gates belong on Biology");
                if (!s.Optional && s.Screen.Face != plan.Face) offHome++;
                if (!s.Optional && s.Screen.Face == plan.Face) homeRequired++;
                if (s.Kind == BeakGateKind.FlowerVine)
                {
                    flowers++;
                    if (!flowerScreens.Add(s.FlowerScreen)) problems.Add($"{s}: shares its flower screen with another vine");
                    if (s.FlowerScreen.Face != s.Screen.Face || !Adjacent(s.Screen, plan.FaceSize).Contains(s.FlowerScreen))
                        problems.Add($"{s}: the flower is not on an adjacent screen of the same face");
                    if (TownSquare.IsTown(s.FlowerScreen, plan.FaceSize)) problems.Add($"{s}: the flower grows in the town");
                }
                if (s.Difficulty != plan.DifficultyOn(s.Screen.Face)) problems.Add($"{s}: wrong difficulty");
            }
            if (offHome < 1 && builtFaces > 1) problems.Add($"the {plan.Beak} beak opens no gate on another face");
            int wantFlowers = plan.FaceSize > 1 ? Mathf.Min(FlowerVineCount(plan.FaceSize), homeRequired) : 0;
            if (plan.Beak == BeakKind.Thin && flowers != wantFlowers)
                problems.Add($"thin-beak run has {flowers} flower-vine gates (want {wantFlowers})");
            if (plan.Beak == BeakKind.Thick && flowers != 0) problems.Add("thick-beak run has a flower-vine gate");
            if (plan.TrialScreen.Face != plan.Face) problems.Add($"the trial is on {plan.TrialScreen}, not on Biology");
            if (TownSquare.IsTown(plan.TrialScreen, plan.FaceSize)) problems.Add($"the trial is on the town {plan.TrialScreen}");
            return problems;
        }
    }
}
