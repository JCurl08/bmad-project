using System;
using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What one stop of the Mayor's tour (4.20) teaches, in the tour's order.</summary>
    public enum TourLesson
    {
        /// <summary>Walking between screens: the stop is next to the square, so getting there is the task.</summary>
        Move = 0,

        /// <summary>Talking: talk to the tour guide (Gus Guidewell) standing by the marker.</summary>
        Talk = 1,

        /// <summary>Swinging: dig up the tour's heap of dirt (a RewardMound) by the marker.</summary>
        Dig = 2,

        /// <summary>Chests: open the tour's chest by the marker.</summary>
        Chest = 3,

        /// <summary>The map: open it (M).</summary>
        Map = 4,

        /// <summary>The quest log: open it (Q).</summary>
        QuestLog = 5,

        /// <summary>Switching items (1/2): only when the player holds two or more items when the stop comes up.</summary>
        Items = 6,

        /// <summary>Locked arches, key against item: the marker stands by one of Town's arches. The last stop gives the fragment.</summary>
        Locks = 7,
    }

    /// <summary>One tour stop: its lesson, its Town screen, its marker and the thing it uses (mound, chest), local to the screen's centre.</summary>
    public readonly struct TourStop
    {
        public readonly TourLesson Lesson;
        public readonly ScreenAddress Screen;

        /// <summary>The glowing marker's centre (local to the screen's centre): the player arrives by walking onto it.</summary>
        public readonly Vector2 Marker;

        /// <summary>Where the stop's thing stands (the Dig stop's mound, the Chest stop's chest), local; the marker for the rest.</summary>
        public readonly Vector2 Prop;

        /// <summary>The Locks stop's arch: the side of its screen the arch is on (the face's rim). North for every other stop.</summary>
        public readonly Facing ArchSide;

        public TourStop(TourLesson lesson, ScreenAddress screen, Vector2 marker, Vector2 prop, Facing archSide)
        {
            Lesson = lesson;
            Screen = screen;
            Marker = marker;
            Prop = prop;
            ArchSide = archSide;
        }

        public override string ToString() => $"{Lesson} at {Screen}";
    }

    /// <summary>
    /// Mayor Muddleton's tour of Town (4.20, replacing 4.18's lost-and-found trail as the source of Town's fragment), as pure
    /// data. The Mayor asks on the first talk whether the player would like to be shown round (TownTourQuest, LeaderQuest);
    /// yes starts the tour, no hands the fragment over at once.
    /// <para>The tour visits the eight screens round Town's square in a ring, one stop a screen, each with a glowing marker,
    /// teaching one thing (Lessons, a fixed order) with a short line and a tiny task. The ring's start (one of the four screens
    /// beside the square, so the first stop is one doorway away) and its way round (clockwise or not) come from the run seed
    /// on its own stream (RngStreams.TownTour, the retired trail's 30), so each stop is the next screen of the ring: one
    /// doorway on (two when the item stop does not apply). The last stop (Locks) is a corner, on the face's rim, so its marker
    /// stands by one of Town's arches; it gives Town's fragment, and the Mayor's usual hand-in then opens Town's temple.</para>
    /// <para>Only a 3x3 Town has a ring that is all rim (the play size); on any other size For gives null and the Mayor
    /// falls back to handing the fragment over (4.8's gift).</para>
    /// </summary>
    public sealed class TownTour
    {
        public const ulong RngStream = RngStreams.TownTour;

        /// <summary>The only face size the tour runs on (CubeSettings.DefaultFaceSize): its ring of eight is the whole rim.</summary>
        public const int FaceSizeFor = 3;

        /// <summary>The lessons, in the tour's order (the ring gives each its screen).</summary>
        public static readonly IReadOnlyList<TourLesson> Lessons = new[]
        {
            TourLesson.Move, TourLesson.Talk, TourLesson.Dig, TourLesson.Chest, TourLesson.Map, TourLesson.QuestLog,
            TourLesson.Items, TourLesson.Locks,
        };

        /// <summary>The eight screens round the square, clockwise from north (offsets from the square's cell). Even: beside it.</summary>
        public static readonly IReadOnlyList<Vector2Int> Ring = new[]
        {
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1),
            new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
        };

        private const float K = ScreenMath.ContentScale;

        /// <summary>How near the marker's centre the player must come to arrive (world units; the glow's radius).</summary>
        public const float ArriveRadius = 1f * K;

        /// <summary>Where the Dig stop's mound and the Chest stop's chest stand: in the north-south lane, south of the marker.</summary>
        public static readonly Vector2 PropLocal = new Vector2(0f, -2.2f * K);

        /// <summary>Where the guide would like to stand: north-east of the Talk stop's marker, just off both lanes.</summary>
        public static readonly Vector2 GuidePreferred = new Vector2(2f * K, 1.6f * K);

        /// <summary>How far in from its screen's side the Locks stop's marker stands, in the lane leading to the arch.</summary>
        public static readonly float ArchMarkerInset = ScreenModule.EdgeClearance + 1.5f * K;

        /// <summary>The Jumbles* the tour's chest holds (a small taste of a chest's 5).</summary>
        public const int ChestJumbles = 3;

        public int Seed { get; }
        public int FaceSize { get; }

        /// <summary>The ring index of the first stop (0, 2, 4 or 6: a screen beside the square).</summary>
        public int Start { get; }

        /// <summary>+1 clockwise, -1 the other way.</summary>
        public int Direction { get; }

        /// <summary>The stops, in order (one per lesson).</summary>
        public IReadOnlyList<TourStop> Stops { get; }

        private TownTour(int seed, int faceSize, int start, int direction, List<TourStop> stops)
        {
            Seed = seed;
            FaceSize = faceSize;
            Start = start;
            Direction = direction;
            Stops = stops;
        }

        /// <summary>The stop with a lesson (every lesson has one).</summary>
        public TourStop StopOf(TourLesson lesson)
        {
            foreach (TourStop s in Stops)
                if (s.Lesson == lesson) return s;
            throw new ArgumentException($"no {lesson} stop");
        }

        /// <summary>The run's tour on a Town of faceSize screens a side, or null (only a 3x3 Town has one). Draws: the start, the way round.</summary>
        public static TownTour For(int seed, int faceSize)
        {
            if (faceSize != FaceSizeFor || !TownSquare.HasTown(faceSize)) return null;
            var rng = new SeededRng(((ulong)(uint)seed << 32) | 0x5455u, RngStream); // "TU": the tour
            int start = 2 * rng.NextInt(4);
            int direction = rng.NextInt(2) == 0 ? 1 : -1;
            Vector2Int square = CubeModel.StartCell(faceSize);
            var stops = new List<TourStop>(Lessons.Count);
            for (int k = 0; k < Lessons.Count; k++)
            {
                Vector2Int offset = Ring[((start + direction * k) % Ring.Count + Ring.Count) % Ring.Count];
                var screen = new ScreenAddress(CubeModel.StartFace, square + offset);
                TourLesson lesson = Lessons[k];
                Facing arch = offset.y > 0 ? Facing.North : offset.y < 0 ? Facing.South : offset.x > 0 ? Facing.East : Facing.West;
                Vector2 marker = lesson == TourLesson.Locks ? ArchMarker(arch) : Vector2.zero;
                Vector2 prop = lesson == TourLesson.Dig || lesson == TourLesson.Chest ? PropLocal : marker;
                stops.Add(new TourStop(lesson, screen, marker, prop, lesson == TourLesson.Locks ? arch : Facing.North));
            }
            return new TownTour(seed, faceSize, start, direction, stops);
        }

        /// <summary>A tour with the given stops, as they are (tests and tools: FindProblems must catch a bad one).</summary>
        public static TownTour Custom(int seed, int faceSize, int start, int direction, IEnumerable<TourStop> stops) =>
            new TownTour(seed, faceSize, start, direction, new List<TourStop>(stops));

        /// <summary>The Locks stop's marker: in the lane leading to the arch on a side, ArchMarkerInset in from that side.</summary>
        public static Vector2 ArchMarker(Facing side)
        {
            Vector2 half = ScreenMath.DefaultScreenSize / 2f;
            Vector2Int v = side.ToVector();
            return new Vector2(v.x * (half.x - ArchMarkerInset), v.y * (half.y - ArchMarkerInset));
        }

        /// <summary>
        /// Where the guide stands (pure, so the sweep and play agree): on the Talk stop's screen, the valid Town spot (TownPlan:
        /// off the lanes, clear of the module's solids) nearest GuidePreferred that keeps clear of every planned Town person's
        /// spot there (TownPlan.Spacing). Unfound when there is none.
        /// </summary>
        public static TownSpot GuideSpot(TownTour tour, IReadOnlyList<TownSpot> townSpots, IReadOnlyList<Rect> blocked)
        {
            if (tour == null) return default;
            ScreenAddress screen = tour.StopOf(TourLesson.Talk).Screen;
            var taken = new List<Rect>();
            if (townSpots != null)
                foreach (TownSpot s in townSpots)
                    if (s.Found && s.Screen == screen) taken.Add(TownPlan.Footprint(s.Local, TownPlan.Spacing / 2f));
            Vector2 best = default;
            float bestDistance = float.MaxValue;
            foreach (Vector2 c in TownPlan.Candidates(blocked))
            {
                Rect mine = TownPlan.Footprint(c, TownPlan.Spacing / 2f);
                bool clear = true;
                foreach (Rect t in taken)
                    if (t.Overlaps(mine)) { clear = false; break; }
                if (!clear) continue;
                float d = (c - GuidePreferred).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = c;
            }
            return bestDistance < float.MaxValue ? new TownSpot(screen, best) : default;
        }

        /// <summary>A compact form for the determinism fixture: "start (1,2) cw stops Move(1,2) Talk(2,2) ...".</summary>
        public string Signature()
        {
            var parts = new List<string>();
            foreach (TourStop s in Stops) parts.Add($"{s.Lesson}({s.Screen.Cell.x},{s.Screen.Cell.y})");
            return $"start {Start} {(Direction > 0 ? "cw" : "ccw")} stops {string.Join(" ", parts)}";
        }

        public override string ToString() => Signature();

        /// <summary>How far a marker or prop keeps inside its lane's sides and its screen's rim band.</summary>
        public const float LaneMargin = 0.4f * K;

        /// <summary>True when a local point is in one of a screen's two centre lanes and inside its rim band (always clear ground).</summary>
        public static bool InLane(Vector2 local)
        {
            Vector2 half = ScreenMath.DefaultScreenSize / 2f;
            float lane = ScreenModule.ExitLaneHalfWidth - LaneMargin;
            bool inside = Mathf.Abs(local.x) <= half.x - ScreenModule.EdgeClearance && Mathf.Abs(local.y) <= half.y - ScreenModule.EdgeClearance;
            return inside && (Mathf.Abs(local.x) <= lane || Mathf.Abs(local.y) <= lane);
        }

        /// <summary>
        /// Problems with a run's tour (empty when it can be walked start to end): the eight lessons in order, each on its own
        /// Town screen off the square, the first beside the square and each next one doorway on, every screen reachable
        /// (reachable, from the sweep), the Locks stop on the face's rim on its arch's side, every marker and thing in a lane
        /// and clear of the module's solids (blockedFor), the guide's spot found on the Talk screen (townSpots: the run's
        /// planned Town spots), and every line the tour can say within the text rule and the box.
        /// </summary>
        public static List<string> FindProblems(int seed, int faceSize, TownTour tour, IReadOnlyList<TownSpot> townSpots,
            Func<Vector2Int, IReadOnlyList<Rect>> blockedFor, ICollection<ScreenAddress> reachable = null)
        {
            var problems = new List<string>();
            if (!TownSquare.HasTown(faceSize) || faceSize != FaceSizeFor) return problems;
            if (tour == null)
            {
                problems.Add("no tour");
                return problems;
            }
            if (tour.Stops.Count != Lessons.Count) problems.Add($"{tour.Stops.Count} stops");
            var square = new ScreenAddress(CubeModel.StartFace, CubeModel.StartCell(faceSize));
            var seen = new HashSet<ScreenAddress>();
            for (int k = 0; k < tour.Stops.Count; k++)
            {
                TourStop s = tour.Stops[k];
                if (k < Lessons.Count && s.Lesson != Lessons[k]) problems.Add($"stop {k} teaches {s.Lesson}");
                if (s.Screen.Face != CubeModel.StartFace) problems.Add($"{s} is off Town");
                if (s.Screen == square) problems.Add($"{s} is on the square");
                if (!seen.Add(s.Screen)) problems.Add($"{s} shares its screen");
                Vector2Int prev = k == 0 ? square.Cell : tour.Stops[k - 1].Screen.Cell;
                Vector2Int step = s.Screen.Cell - prev;
                if (Mathf.Abs(step.x) + Mathf.Abs(step.y) != 1) problems.Add($"{s} is not one doorway on from {prev}");
                if (s.Screen.Cell.x < 0 || s.Screen.Cell.y < 0 || s.Screen.Cell.x >= faceSize || s.Screen.Cell.y >= faceSize)
                    problems.Add($"{s} is off the face");
                if (reachable != null && !reachable.Contains(s.Screen)) problems.Add($"{s} is unreachable");
                IReadOnlyList<Rect> blocked = blockedFor != null ? blockedFor(s.Screen.Cell) : null;
                foreach (Vector2 p in new[] { s.Marker, s.Prop })
                {
                    if (!InLane(p)) problems.Add($"{s}: {p} is off the lanes");
                    if (blocked != null)
                        foreach (Rect b in blocked)
                            if (b.Overlaps(new Rect(p - Vector2.one * ArriveRadius, Vector2.one * 2f * ArriveRadius)))
                                problems.Add($"{s}: {p} meets a solid {b}");
                }
                if (s.Lesson == TourLesson.Locks)
                {
                    Vector2Int v = s.ArchSide.ToVector();
                    Vector2Int beyond = s.Screen.Cell + v;
                    bool onRim = beyond.x < 0 || beyond.y < 0 || beyond.x >= faceSize || beyond.y >= faceSize;
                    if (!onRim) problems.Add($"{s}: its {s.ArchSide} side is not the face's rim (no arch)");
                }
            }
            TownSpot guide = GuideSpot(tour, townSpots, blockedFor != null ? blockedFor(tour.StopOf(TourLesson.Talk).Screen.Cell) : null);
            if (!guide.Found) problems.Add("the guide has no spot"); // GuideSpot only ever picks valid TownPlan candidates
            foreach (string line in TownTourText.Lines(tour))
            {
                if (line.Length > Dialogue.MaxLineLength) problems.Add($"too long ({line.Length}): {line}");
                foreach (string p in InWorldText.Problems(line)) problems.Add($"{p}: {line}");
            }
            return problems;
        }
    }

    /// <summary>
    /// The Mayor's tour's words (4.20): the Mayor's question and answers (in the Town voice), each stop's line (said by the
    /// stop's sign, under the Speaker label), the guide's lines, and the popups. Plain, short and under the text rule (no key
    /// letter mid-sentence but the allowed ones: Interact (E), Enter, Esc, M, Tab, Q).
    /// </summary>
    public static class TownTourText
    {
        /// <summary>The dialogue box's label for a stop's line.</summary>
        public const string Speaker = "Mayor Muddleton's tour";

        // ---------- the Mayor

        /// <summary>The first talk's question (after his greeting): a tour, or the fragment at once.</summary>
        public const string Offer =
            "Every face is coming apart, friend, and Town keeps one fragment of its own. New in town? Shall I show you round first? The fragment's yours at the end of the tour.";

        /// <summary>Yes: the tour starts ({where}: the first stop).</summary>
        public const string StartTemplate =
            "Splendid! Follow the glowing markers, one stop at a time. The first one's {where}. Change your mind? Ask me and I'll hand the fragment over.";

        /// <summary>No: the fragment at once.</summary>
        public const string Declined =
            "An old hand, eh? Then here's Town's fragment. Hold on to it, then hand it back to me and we'll open Town's temple!";

        /// <summary>Talking to the Mayor mid-tour ({where}: the next stop), before his question.</summary>
        public const string ReminderTemplate = "How's the tour going, friend? The next glowing marker is {where}.";

        /// <summary>Mid-tour: the question.</summary>
        public const string SkipAsk = "Or shall I skip the rest of the tour and hand you Town's fragment now?";

        /// <summary>Yes, skip: the fragment at once.</summary>
        public const string Skipped = "Right you are! Here's Town's fragment. Hand it back to me and we'll open Town's temple!";

        /// <summary>No, keep going ({where}: the next stop).</summary>
        public const string KeepGoingTemplate = "Good on you! On you go, then: the next marker glows {where}.";

        public static string Start(string where) => Fill(StartTemplate, where);

        public static string Reminder(string where) => Fill(ReminderTemplate, where);

        public static string KeepGoing(string where) => Fill(KeepGoingTemplate, where);

        // ---------- the stops

        /// <summary>Each stop's line, by lesson, said as the player arrives.</summary>
        public static string StopLine(TourLesson lesson)
        {
            switch (lesson)
            {
                case TourLesson.Move:
                    return "Stop one: getting about! You walked through a doorway to get here. Every screen has one in the middle of each side, so walk on through to reach the next screen.";
                case TourLesson.Talk:
                    return "Stop two: talking! Walk up to " + NpcNames.Guide + " here and press Interact (E) to talk. Interact pages through what folk say, and Esc skips to the end.";
                case TourLesson.Dig:
                    return "Stop three: digging! See that heap of dirt? Swing at it twice (Enter, or the left mouse button) to dig it up. Heaps like it hide treasure all over the cube.";
                case TourLesson.Chest:
                    return "Stop four: chests! Walk into this chest, or swing at it, to open it. Chests hide " + InWorldText.Currency + ", keys, boosts and fragments.";
                case TourLesson.Map:
                    return "Stop five: the map! Press M (or Tab) to open it. It shows the screens you've seen, and everything waits while it's open. Press M again to close it.";
                case TourLesson.QuestLog:
                    return "Stop six: the quest log! Press Q to open it. Every quest folk give you goes in there, with your next step. This tour's in there too!";
                case TourLesson.Items:
                    return "Stop seven: items! Press 1 or 2 to switch the item in your hands. Your swing uses whatever you hold, so pick the right one for the job.";
                default:
                    return "Last stop: locked arches! The arches between faces start locked. A keyhole takes a key, used up. A picture takes that face's item, kept. Here's Town's fragment!";
            }
        }

        /// <summary>The popup when a stop's task is done ({where}: the next stop).</summary>
        public static string NextPopup(string where) => $"Well done! Next stop: {where}";

        // ---------- the guide

        /// <summary>What the guide says (Town voice).</summary>
        public static readonly IReadOnlyList<string> GuideLines = new[]
        {
            "Hullo, I'm " + NpcNames.Guide + ", the tour guide! That's talking done: easy, eh? Folk all over the cube chat away in their own manner.",
            "Their hints are always true, so talk to everyone. And nobody in Town ever turns on you: it's the one place everyone still mixes.",
        };

        // ---------- scans

        /// <summary>The longest {where} a stop can have (the scans fill templates with it).</summary>
        public const string SampleWhere = QuestLines.SampleWhere;

        /// <summary>Every line the tour can say with a sample where (the voice, length and text scans).</summary>
        public static IEnumerable<string> AllLines()
        {
            yield return Offer;
            yield return Start(SampleWhere);
            yield return Declined;
            yield return Reminder(SampleWhere);
            yield return SkipAsk;
            yield return Skipped;
            yield return KeepGoing(SampleWhere);
            foreach (TourLesson lesson in TownTour.Lessons) yield return StopLine(lesson);
            foreach (string line in GuideLines) yield return line;
        }

        /// <summary>Every line a run's tour can say, with its real wheres (the sweep checks them).</summary>
        public static IEnumerable<string> Lines(TownTour tour)
        {
            foreach (string line in AllLines()) yield return line;
            foreach (TourStop s in tour.Stops)
            {
                string where = QuestLines.Where(s.Screen, tour.FaceSize);
                yield return Start(where);
                yield return Reminder(where);
                yield return KeepGoing(where);
                yield return NextPopup(where);
                yield return QuestText.TourStep(s.Lesson, where, false);
                yield return QuestText.TourStep(s.Lesson, where, true);
            }
        }

        private static string Fill(string template, string where) => template.Replace("{where}", where ?? "in the town square");
    }
}
