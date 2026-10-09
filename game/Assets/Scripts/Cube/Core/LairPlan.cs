using System;
using System.Collections.Generic;
using System.Text;
using Game.Tracer;

namespace Game.Cube
{
    /// <summary>What a lair prisoner's hint is about (4.13).</summary>
    public enum PrisonerHint
    {
        /// <summary>How Primsworth the Demon's fight works (LairText.FightFacts, by Topic).</summary>
        Fight = 0,
        /// <summary>Which mended face gives which help in the fight (LairText.BenefitFact of BenefitTheme).</summary>
        Benefit = 1,
        /// <summary>Which region holds the one real way down (LairText.PortalFact of the real face's theme).</summary>
        RealPortal = 2,
    }

    /// <summary>One prisoner of a lair room: who they are (race and NPC index) and what their hint is about.</summary>
    public readonly struct PrisonerPlan
    {
        public readonly Race Race;

        /// <summary>The NPC index their parts and name come from (LairPlan.PrisonerIndex).</summary>
        public readonly int Index;

        public readonly PrisonerHint Hint;

        /// <summary>A Fight hint's fact (LairText.FightFacts); 0 otherwise.</summary>
        public readonly int Topic;

        /// <summary>A Benefit hint's face theme (the face whose mending it tells of); Town otherwise.</summary>
        public readonly Theme BenefitTheme;

        public PrisonerPlan(Race race, int index, PrisonerHint hint, int topic, Theme benefitTheme)
        {
            Race = race;
            Index = index;
            Hint = hint;
            Topic = topic;
            BenefitTheme = benefitTheme;
        }

        public override string ToString() =>
            $"{Race}#{Index}:{Hint}{(Hint == PrisonerHint.Fight ? Topic.ToString() : Hint == PrisonerHint.Benefit ? BenefitTheme.ToString() : "")}";
    }

    /// <summary>
    /// The boss lair of a run (4.13), pure and seeded on its own stream (RngStreams.Lair), from the model alone: every
    /// laid-out science face has one core portal (CubeLayout), and one of them, the real portal, leads to the core arena;
    /// every other leads down into a lair room (LairRooms) where PrisonersPerRoom people of the races are held behind bars.
    /// Each room's prisoners give true hints in their own voices (LairText): one on how the fight works (a different fact in
    /// each room), two on which mended face gives which help (the rooms between them cover every face's), and one naming the
    /// region that holds the real portal (design call: every lair room tells it, so one wrong turn is enough to learn it).
    /// The draws: the real portal first, then the fight-fact and benefit offsets, then a race offset per lair room.
    /// </summary>
    public sealed class LairPlan
    {
        public const ulong RngStream = RngStreams.Lair;

        /// <summary>Prisoners in every lair room (one per cell).</summary>
        public const int PrisonersPerRoom = 4;

        /// <summary>NPC index base for prisoners (town interiors use 8000): PrisonerIndex.</summary>
        public const int IndexBase = 9000;

        /// <summary>The benefit themes in the order the rooms hand them out (BossBenefits.All's order).</summary>
        public static readonly Theme[] BenefitThemes =
            { Theme.Town, Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere };

        private readonly List<FaceId> portalFaces;
        private readonly Dictionary<FaceId, List<PrisonerPlan>> prisoners;

        public int Seed { get; }

        /// <summary>The face whose core portal leads to the arena.</summary>
        public FaceId RealFace { get; }

        /// <summary>The faces with a core portal (every laid-out science face), in face order.</summary>
        public IReadOnlyList<FaceId> PortalFaces => portalFaces;

        /// <summary>The faces whose portal leads into a lair room, in face order.</summary>
        public IReadOnlyList<FaceId> LairFaces { get; }

        private LairPlan(int seed, FaceId real, List<FaceId> portals, List<FaceId> lairs, Dictionary<FaceId, List<PrisonerPlan>> held)
        {
            Seed = seed;
            RealFace = real;
            portalFaces = portals;
            LairFaces = lairs;
            prisoners = held;
        }

        /// <summary>True when the face's portal leads into a lair room (any laid-out face but the real one).</summary>
        public bool IsLair(FaceId face) => face != RealFace && portalFaces.Contains(face);

        /// <summary>True when the face's portal leads to the arena.</summary>
        public bool IsReal(FaceId face) => face == RealFace;

        /// <summary>A lair room's prisoners (empty for a face with no lair).</summary>
        public IReadOnlyList<PrisonerPlan> PrisonersOf(FaceId face) =>
            prisoners.TryGetValue(face, out List<PrisonerPlan> list) ? list : (IReadOnlyList<PrisonerPlan>)Array.Empty<PrisonerPlan>();

        /// <summary>
        /// The NPC index of prisoner i of the k-th lair room: one population of at most four rooms of PrisonersPerRoom, so
        /// every name stays inside its voice's pool (no numerals: NpcNames.OrdinalOf stays under 16).
        /// </summary>
        public static int PrisonerIndex(int lairIndex, int i) => IndexBase + PrisonersPerRoom * lairIndex + i;

        /// <summary>The run's lair (null without a model, or with no portal at all).</summary>
        public static LairPlan ForRun(CubeModel model)
        {
            if (model == null) return null;
            var portals = new List<FaceId>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(model, (FaceId)f)) portals.Add((FaceId)f);
            if (portals.Count == 0) return null;

            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);
            FaceId real = portals[rng.NextInt(portals.Count)];
            int fightOffset = rng.NextInt(LairText.FightFacts.Length);
            int benefitOffset = rng.NextInt(BenefitThemes.Length);
            var lairs = new List<FaceId>();
            foreach (FaceId face in portals)
                if (face != real) lairs.Add(face);

            var held = new Dictionary<FaceId, List<PrisonerPlan>>();
            Race[] races = RaceExtensions.All;
            for (int k = 0; k < lairs.Count; k++)
            {
                FaceId face = lairs[k];
                int raceOffset = rng.NextInt(races.Length);
                var room = new List<PrisonerPlan>(PrisonersPerRoom);
                for (int i = 0; i < PrisonersPerRoom; i++)
                {
                    Race race = races[(raceOffset + i) % races.Length];
                    int index = PrisonerIndex(k, i);
                    switch (i)
                    {
                        case 0:
                            room.Add(new PrisonerPlan(race, index, PrisonerHint.Fight, (fightOffset + k) % LairText.FightFacts.Length, Theme.Town));
                            break;
                        case 1:
                        case 2:
                            room.Add(new PrisonerPlan(race, index, PrisonerHint.Benefit, 0,
                                BenefitThemes[(benefitOffset + 2 * k + i - 1) % BenefitThemes.Length]));
                            break;
                        default:
                            room.Add(new PrisonerPlan(race, index, PrisonerHint.RealPortal, 0, Theme.Town));
                            break;
                    }
                }
                held[face] = room;
            }
            return new LairPlan(model.Seed, real, portals, lairs, held);
        }

        /// <summary>A prisoner's NPC (parts from their own index, named from their voice's pool, labelled a captive).</summary>
        public NpcSpec SpecOf(PrisonerPlan prisoner)
        {
            NpcSpec spec = NpcFactory.ChooseParts(Seed, prisoner.Race, prisoner.Index);
            return spec.Named(spec.Name, $"{NpcSpec.RaceName(prisoner.Race, spec.Variant).ToLowerInvariant()} {LairText.CaptiveKind}");
        }

        /// <summary>What a prisoner says (two lines: their captive line, then their hint), true for this run.</summary>
        public List<string> LinesOf(PrisonerPlan prisoner, CubeModel model) => LairText.Conversation(SpecOf(prisoner), prisoner, model, RealFace);

        /// <summary>Compact text of the plan, for the determinism fixture.</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append("real=").Append(RealFace);
            foreach (FaceId face in LairFaces)
            {
                sb.Append(' ').Append(face).Append('[');
                List<PrisonerPlan> room = prisoners[face];
                for (int i = 0; i < room.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(room[i]);
                }
                sb.Append(']');
            }
            return sb.ToString();
        }

        public override string ToString() => Signature();

        /// <summary>
        /// Problems with a run's lair (empty when all is well): a plan exists; exactly one portal (one per laid-out face, by
        /// the layout) leads to the arena, on a laid-out face whose core screen is reachable; every other leads into a lair
        /// room with PrisonersPerRoom prisoners, each saying two lines that keep the text rule and fit the dialogue box,
        /// among them a fight hint, benefit hints and the real portal's region; and the rooms together tell every built
        /// face's benefit once there are enough of them (three or more).
        /// </summary>
        public static List<string> FindProblems(CubeModel model, CubeLayout layout, LairPlan plan, HashSet<ScreenAddress> reachable)
        {
            var problems = new List<string>();
            if (plan == null)
            {
                problems.Add("no lair plan");
                return problems;
            }
            int toArena = 0, toLair = 0;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                bool hasPortal = layout != null && layout.TryGetFace(face, out _);
                bool listed = false;
                foreach (FaceId p in plan.PortalFaces) listed |= p == face;
                if (hasPortal != listed) problems.Add($"{face}: a portal {(hasPortal ? "the plan misses" : "the layout lacks")}");
                if (!listed) continue;
                if (plan.IsReal(face)) toArena++;
                if (plan.IsLair(face)) toLair++;
                if (plan.IsReal(face) == plan.IsLair(face)) problems.Add($"{face}: its portal leads {(plan.IsReal(face) ? "both ways" : "nowhere")}");
            }
            if (toArena != 1) problems.Add($"{toArena} portals lead to the arena (want 1)");
            if (toLair != plan.PortalFaces.Count - 1) problems.Add($"{toLair} portals lead into the lair (want {plan.PortalFaces.Count - 1})");
            if (layout != null && layout.TryGetFace(plan.RealFace, out FaceLayout real))
            {
                var screen = new ScreenAddress(plan.RealFace, real.CoreCell);
                if (reachable != null && !reachable.Contains(screen)) problems.Add($"the real portal on {screen} is unreachable");
            }
            else problems.Add($"the real portal's face {plan.RealFace} is not laid out");

            Theme realTheme = model.ThemeOf(plan.RealFace);
            var benefitsTold = new HashSet<Theme>();
            foreach (FaceId face in plan.LairFaces)
            {
                IReadOnlyList<PrisonerPlan> room = plan.PrisonersOf(face);
                if (room.Count != PrisonersPerRoom) problems.Add($"{face}: {room.Count} prisoners (want {PrisonersPerRoom})");
                bool fight = false, benefit = false, portal = false;
                foreach (PrisonerPlan prisoner in room)
                {
                    List<string> lines = plan.LinesOf(prisoner, model);
                    if (lines.Count != Dialogue.MaxLines) problems.Add($"{face}: {prisoner} says {lines.Count} lines");
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line) || line.Length > Dialogue.MaxLineLength) problems.Add($"{face}: {prisoner} says \"{line}\"");
                        foreach (string problem in InWorldText.Problems(line)) problems.Add($"{face}: {prisoner} {problem}: \"{line}\"");
                    }
                    switch (prisoner.Hint)
                    {
                        case PrisonerHint.Fight: fight = true; break;
                        case PrisonerHint.Benefit:
                            benefit = true;
                            benefitsTold.Add(prisoner.BenefitTheme);
                            break;
                        default:
                            portal = true;
                            if (lines.Count < 2 || !lines[1].Contains(InWorldText.Region(realTheme)))
                                problems.Add($"{face}: {prisoner} does not name the real portal's region ({InWorldText.Region(realTheme)})");
                            break;
                    }
                }
                if (!fight || !benefit || !portal) problems.Add($"{face}: the room lacks a {(!fight ? "fight" : !benefit ? "benefit" : "real-portal")} hint");
            }
            if (plan.LairFaces.Count >= 3)
                foreach (Theme theme in BenefitThemes)
                    if (!benefitsTold.Contains(theme) && !model.IsSealed(model.FaceOf(theme)))
                        problems.Add($"no prisoner tells what mending {theme} gives");
            return problems;
        }
    }
}
