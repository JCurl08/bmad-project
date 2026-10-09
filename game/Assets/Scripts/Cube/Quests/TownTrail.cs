using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Town's lost-and-found side quest (4.18), as pure data: the Mayor says Town's fragment went missing, and a seeded
    /// trail of MinSteps..MaxSteps townsfolk across Town's other screens (never the square) each sends the player to the
    /// next. One of them (FavourStep) first wants a favour: a word carried to another townsperson (Helper) and the answer
    /// brought back. The last one hands the fragment over; handing it to the Mayor mends Town (LeaderQuest).
    /// <para>The people are Town NPCs (TownPlan.Specs indices) past the square's stands, so they never collide with the
    /// greeter, the square's errand (Errands) or the Mayor. Each trail person prefers a different screen (TownPlan.Spots puts
    /// NPC i on screen (i - square) mod the other screens), and the sweep checks the real spots (FindProblems).
    /// Everything comes from the run seed on its own stream (RngStreams.TownTrail), so nothing else moves.</para>
    /// </summary>
    public sealed class TownTrail
    {
        public const ulong RngStream = RngStreams.TownTrail;
        public const int MinSteps = 2;
        public const int MaxSteps = 3;

        public int Seed { get; }
        public int FaceSize { get; }

        /// <summary>The trail, in order: TownPlan.Specs indices. The last hands the fragment over.</summary>
        public IReadOnlyList<int> Steps { get; }

        /// <summary>Which step (an index into Steps) wants the favour first.</summary>
        public int FavourStep { get; }

        /// <summary>The townsperson the favour's word is carried to (a TownPlan.Specs index, not on the trail).</summary>
        public int Helper { get; }

        private TownTrail(int seed, int faceSize, List<int> steps, int favourStep, int helper)
        {
            Seed = seed;
            FaceSize = faceSize;
            Steps = steps;
            FavourStep = favourStep;
            Helper = helper;
        }

        /// <summary>The Town NPC index of the person who asks the favour.</summary>
        public int FavourAsker => Steps[FavourStep];

        /// <summary>True when a Town NPC index takes part (on the trail, or the helper).</summary>
        public bool Involves(int index) => index == Helper || IndexOfStep(index) >= 0;

        /// <summary>Where a Town NPC index is on the trail (an index into Steps), or -1.</summary>
        public int IndexOfStep(int index)
        {
            for (int i = 0; i < Steps.Count; i++)
                if (Steps[i] == index) return i;
            return -1;
        }

        /// <summary>The first Town NPC index past the square's stands (the trail and the helper stand on Town's other screens).</summary>
        public static int FirstOffSquare(int faceSize) => TownPlan.StartScreenNpcsFor(faceSize);

        /// <summary>
        /// The run's trail on a Town face of faceSize screens a side, or null (no town on so small a face, or too few people
        /// off the square). Draws: the trail's length, a shuffle of the candidates, the favour step.
        /// </summary>
        public static TownTrail For(int seed, int faceSize)
        {
            if (!TownSquare.HasTown(faceSize)) return null;
            int count = TownPlan.Specs(seed, faceSize).Count;
            int first = FirstOffSquare(faceSize);
            int screens = faceSize * faceSize - 1;
            var candidates = new List<int>();
            for (int i = first; i < count; i++) candidates.Add(i);
            if (candidates.Count < MinSteps + 1) return null;

            var rng = new SeededRng(((ulong)(uint)seed << 32) | 0x5452u, RngStream); // "TR"
            int length = MinSteps + rng.NextInt(MaxSteps - MinSteps + 1);
            rng.Shuffle(candidates);
            // Each step on its own screen (the screen TownPlan.Spots tries first for that NPC), the helper anywhere else.
            var steps = new List<int>();
            var usedScreens = new HashSet<int>();
            foreach (int c in candidates)
            {
                if (steps.Count == length) break;
                if (usedScreens.Add((c - first) % screens)) steps.Add(c);
            }
            if (steps.Count < MinSteps) return null;
            int favourStep = rng.NextInt(steps.Count);
            int helper = -1;
            foreach (int c in candidates)
                if (!steps.Contains(c) && (c - first) % screens != (steps[favourStep] - first) % screens)
                {
                    helper = c;
                    break;
                }
            if (helper < 0)
                foreach (int c in candidates)
                    if (!steps.Contains(c))
                    {
                        helper = c;
                        break;
                    }
            if (helper < 0) return null;
            return new TownTrail(seed, faceSize, steps, favourStep, helper);
        }

        /// <summary>A compact form for the determinism fixture: "steps 7,12,9 favour 1 helper 15".</summary>
        public string Signature() => $"steps {string.Join(",", Steps)} favour {FavourStep} helper {Helper}";

        public override string ToString() => Signature();

        /// <summary>
        /// Problems with a run's trail (empty when it is completable): 2-3 distinct people past the square, each found a
        /// spot (spots: TownPlan.Spots for the run, same order as TownPlan.Specs) on a Town screen other than the square,
        /// the steps on different screens, a helper who is not on the trail, and every line it can say within the text
        /// rule and the box.
        /// </summary>
        public static List<string> FindProblems(int seed, int faceSize, TownTrail trail, IReadOnlyList<TownSpot> spots)
        {
            var problems = new List<string>();
            if (!TownSquare.HasTown(faceSize)) return problems;
            if (trail == null)
            {
                problems.Add("no trail");
                return problems;
            }
            List<NpcSpec> specs = TownPlan.Specs(seed, faceSize);
            if (trail.Steps.Count < MinSteps || trail.Steps.Count > MaxSteps) problems.Add($"{trail.Steps.Count} steps");
            var people = new List<int>(trail.Steps) { trail.Helper };
            var seen = new HashSet<int>();
            var square = new ScreenAddress(CubeModel.StartFace, CubeModel.StartCell(faceSize));
            var stepScreens = new HashSet<ScreenAddress>();
            foreach (int index in people)
            {
                if (!seen.Add(index)) problems.Add($"person {index} appears twice");
                if (index < FirstOffSquare(faceSize) || index >= specs.Count) { problems.Add($"person {index} is not off the square"); continue; }
                if (spots == null || index >= spots.Count || !spots[index].Found) { problems.Add($"person {index} has no spot"); continue; }
                TownSpot spot = spots[index];
                if (spot.Screen.Face != CubeModel.StartFace) problems.Add($"person {index} is off Town");
                if (spot.Screen == square) problems.Add($"person {index} stands in the square");
                if (index != trail.Helper && !stepScreens.Add(spot.Screen)) problems.Add($"two steps share {spot.Screen}");
            }
            if (trail.FavourStep < 0 || trail.FavourStep >= trail.Steps.Count) problems.Add($"favour step {trail.FavourStep}");
            if (problems.Count > 0) return problems;
            // Every line the trail can say, in its speakers' voices, keeps the text rule and fits the box.
            foreach (string line in Lines(trail, specs, spots))
            {
                if (line.Length > Dialogue.MaxLineLength) problems.Add($"too long ({line.Length}): {line}");
                foreach (string p in InWorldText.Problems(line)) problems.Add($"{p}: {line}");
            }
            return problems;
        }

        /// <summary>The trail's spoken lines for a run (the Mayor's, every step's, the favour's), filled with the real names and places.</summary>
        public static IEnumerable<string> Lines(TownTrail trail, IReadOnlyList<NpcSpec> specs, IReadOnlyList<TownSpot> spots)
        {
            string Where(int index) => QuestLines.Where(spots[index].Screen, trail.FaceSize);
            NpcSpec first = specs[trail.Steps[0]];
            yield return QuestLines.MayorLostLine(first.Name, Where(trail.Steps[0]));
            yield return QuestLines.MayorReminderLine(first.Name, Where(trail.Steps[0]));
            NpcSpec helper = specs[trail.Helper];
            for (int s = 0; s < trail.Steps.Count; s++)
            {
                NpcSpec person = specs[trail.Steps[s]];
                if (s == trail.FavourStep)
                {
                    yield return QuestLines.FavourAskLine(person.Voice, helper.Name, Where(trail.Helper));
                    yield return QuestLines.HelperReplyLine(helper.Voice, person.Name);
                    yield return QuestLines.FavourThanksLine(person.Voice);
                }
                if (s + 1 < trail.Steps.Count)
                {
                    NpcSpec next = specs[trail.Steps[s + 1]];
                    yield return QuestLines.PointerLine(person.Voice, next.Name, Where(trail.Steps[s + 1]));
                }
                else yield return QuestLines.GiveLine(person.Voice);
            }
        }
    }
}
