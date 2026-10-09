using System;
using System.Collections.Generic;
using System.Text;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// An ordering temple (4.14) as pure data: Biology's seeds, Chemistry's glowing stones or Town's mixed tokens, standing
    /// on a grid over the room's floor (clear of the altar and the lane in). Target i is struck i-th; each shows a mark the
    /// order follows by a rule (the research note's clue level 2): a seed's size (smallest first), a stone's glow (brightest
    /// first) or a token's pips (fewest first). A wrong strike puts every target back (no damage). Chemistry's also has a
    /// timer: once the first stone is lit, every stone must be lit within TimerSeconds, at least TimerSlack times the best
    /// route (late tier: T at least 1.5x), or they all go dark. Solvable by construction: the order exists and every target
    /// stands free on the open floor; Problems re-checks it all.
    /// </summary>
    public sealed class TempleOrderSpec
    {
        /// <summary>Targets per kind: six seeds, six stones (late: 5-6), four tokens (Town's short one).</summary>
        public const int SeedCount = 6, StoneCount = 6, TownCount = 4;

        /// <summary>The least distance between two targets' centres (the grid's step).</summary>
        public const float Spacing = 3f;

        /// <summary>A swing's cooldown (PlayerAttack's default), counted once per target in the best route.</summary>
        public const float SwingSeconds = 0.35f;

        /// <summary>The timer's slack over the best route (research note 1f, late: T at least 1.5x).</summary>
        public const float TimerSlack = 1.5f;

        /// <summary>The timer never runs shorter than this.</summary>
        public const float MinTimerSeconds = 6f;

        /// <summary>The target grid (local): seven columns, three rows.</summary>
        public static readonly float[] Columns = { -9f, -6f, -3f, 0f, 3f, 6f, 9f };
        public static readonly float[] Rows = { -3.5f, 0f, 3.5f };

        private readonly List<Vector2> spots;
        private readonly List<int> marks;
        private readonly List<Theme> looks;

        public TemplePuzzleKind Kind { get; }

        /// <summary>Each target's spot (local), in striking order.</summary>
        public IReadOnlyList<Vector2> Spots => spots;

        /// <summary>
        /// Each target's mark, in striking order: a seed's size step (0 smallest), a stone's glow step (0 brightest), a token's
        /// pips (fewest first). Always strictly rising, so the rule gives the order.
        /// </summary>
        public IReadOnlyList<int> Marks => marks;

        /// <summary>Town's tokens: whose face each one is drawn from (a mix); the region's theme of the kind otherwise.</summary>
        public IReadOnlyList<Theme> Looks => looks;

        /// <summary>Seconds every target must be struck within once the first is (0: no timer).</summary>
        public float TimerSeconds { get; }

        public int Count => spots.Count;

        public TempleOrderSpec(TemplePuzzleKind kind, List<Vector2> spots, List<int> marks, List<Theme> looks, float timerSeconds)
        {
            Kind = kind;
            this.spots = spots ?? new List<Vector2>();
            this.marks = marks ?? new List<int>();
            this.looks = looks ?? new List<Theme>();
            TimerSeconds = timerSeconds;
        }

        /// <summary>Targets a kind needs.</summary>
        public static int CountFor(TemplePuzzleKind kind) =>
            kind == TemplePuzzleKind.SeedOrder ? SeedCount : kind == TemplePuzzleKind.StoneOrder ? StoneCount : TownCount;

        /// <summary>Every spot a target may take (local): the grid, less the spots by the altar and in the lane in.</summary>
        public static List<Vector2> Candidates()
        {
            var list = new List<Vector2>();
            Rect altar = Grow(TempleRoom.AltarRect, 1f);
            foreach (float y in Rows)
                foreach (float x in Columns)
                {
                    var p = new Vector2(x, y);
                    if (altar.Contains(p) || Grow(TempleRoom.EntryLane, 0.5f).Contains(p)) continue;
                    list.Add(p);
                }
            return list;
        }

        /// <summary>The seconds of the best route from the first target to the last, at base speed, with a swing at each.</summary>
        public float TimedRouteSeconds
        {
            get
            {
                float d = 0f;
                for (int i = 1; i < spots.Count; i++) d += Vector2.Distance(spots[i - 1], spots[i]);
                return d / TimeField.BasePlayerSpeed + spots.Count * SwingSeconds;
            }
        }

        /// <summary>The timer a route needs: TimerSlack times it, never under MinTimerSeconds, rounded up to half a second.</summary>
        public static float TimerFor(float routeSeconds) =>
            Mathf.Max(MinTimerSeconds, Mathf.Ceil(routeSeconds * TimerSlack * 2f) / 2f);

        /// <summary>Deals a kind's targets: a seeded shuffle of the grid, the first spots taken in striking order.</summary>
        public static TempleOrderSpec Deal(SeededRng rng, TemplePuzzleKind kind)
        {
            int n = CountFor(kind);
            List<Vector2> grid = Candidates();
            rng.Shuffle(grid);
            List<Vector2> picked = grid.GetRange(0, Math.Min(n, grid.Count));
            var marks = new List<int>();
            var looks = new List<Theme>();
            if (kind == TemplePuzzleKind.TownOrder)
            {
                // Four distinct pip counts of 1-6, fewest first, each on a token from a different science face.
                var pips = new List<int> { 1, 2, 3, 4, 5, 6 };
                rng.Shuffle(pips);
                pips = pips.GetRange(0, n);
                pips.Sort();
                marks.AddRange(pips);
                var faces = new List<Theme>(CubeModel.ScienceThemes);
                rng.Shuffle(faces);
                for (int i = 0; i < n; i++) looks.Add(faces[i % faces.Count]);
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    marks.Add(i);
                    looks.Add(kind == TemplePuzzleKind.SeedOrder ? Theme.Biology : Theme.Chemistry);
                }
            }
            var spec = new TempleOrderSpec(kind, picked, marks, looks, 0f);
            if (kind != TemplePuzzleKind.StoneOrder) return spec;
            return new TempleOrderSpec(kind, picked, marks, looks, TimerFor(spec.TimedRouteSeconds));
        }

        /// <summary>
        /// Problems (empty when all is well): the kind's count; marks strictly rising (the rule gives one order); every target
        /// on the floor, Spacing apart, clear of the altar and the lane in; the timer (stones only) at least TimerSlack times
        /// the best route, and none on the others.
        /// </summary>
        public List<string> Problems(string what)
        {
            var problems = new List<string>();
            int want = CountFor(Kind);
            if (spots.Count != want) problems.Add($"{what}: {spots.Count} targets (want {want})");
            if (marks.Count != spots.Count || looks.Count != spots.Count) problems.Add($"{what}: marks or looks do not match the targets");
            for (int i = 1; i < marks.Count; i++)
                if (marks[i] <= marks[i - 1]) problems.Add($"{what}: target {i}'s mark does not follow the rule");
            if (Kind == TemplePuzzleKind.TownOrder)
                foreach (int m in marks)
                    if (m < 1 || m > 6) problems.Add($"{what}: a token with {m} pips");
            Rect floor = TempleRoom.Floor, altar = Grow(TempleRoom.AltarRect, 0.5f), lane = TempleRoom.EntryLane;
            for (int i = 0; i < spots.Count; i++)
            {
                Vector2 p = spots[i];
                if (!Grow(floor, -1.5f).Contains(p)) problems.Add($"{what}: target {i} at {p} is not on the open floor");
                if (altar.Contains(p)) problems.Add($"{what}: target {i} stands on the altar");
                if (lane.Contains(p)) problems.Add($"{what}: target {i} stands in the lane in");
                for (int j = i + 1; j < spots.Count; j++)
                    if (Vector2.Distance(p, spots[j]) < Spacing - 1e-3f) problems.Add($"{what}: targets {i} and {j} are too close");
            }
            if (Kind == TemplePuzzleKind.StoneOrder)
            {
                if (TimerSeconds < TimerSlack * TimedRouteSeconds - 1e-3f)
                    problems.Add($"{what}: the timer {TimerSeconds:0.0} s is under {TimerSlack}x the best route {TimedRouteSeconds:0.00} s");
            }
            else if (TimerSeconds > 0f) problems.Add($"{what}: a timer on a {Kind} temple");
            return problems;
        }

        /// <summary>The chance a player striking untouched targets at random gets the order right: 1 in Count!.</summary>
        public double RandomSolveRate
        {
            get
            {
                double ways = 1;
                for (int i = 2; i <= spots.Count; i++) ways *= i;
                return 1.0 / ways;
            }
        }

        public string Stats()
        {
            string rule = Kind == TemplePuzzleKind.SeedOrder ? "smallest seed first" : Kind == TemplePuzzleKind.StoneOrder ? "brightest glow first" : "fewest pips first";
            string timer = TimerSeconds > 0f ? $"timer {TimerSeconds:0.0} s ({TimerSeconds / TimedRouteSeconds:0.00}x the best route {TimedRouteSeconds:0.00} s)" : "no timer";
            return $"{spots.Count} targets, clue level 2 ({rule}), a wrong strike resets, {timer}, P_rand 1/{Math.Round(1.0 / RandomSolveRate)}";
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < spots.Count; i++)
                sb.Append(spots[i].x.ToString("0.#")).Append(',').Append(spots[i].y.ToString("0.#")).Append(':').Append(marks[i])
                    .Append(Kind == TemplePuzzleKind.TownOrder ? "/" + looks[i] : "").Append(' ');
            if (TimerSeconds > 0f) sb.Append("timer ").Append(TimerSeconds.ToString("0.0"));
            return sb.ToString().TrimEnd();
        }

        internal static Rect Grow(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);
    }

    /// <summary>
    /// Physics' temple (4.14) as pure data: two booths against the north half of the room, each behind a timed door that
    /// needs Required (3, the research note's late count) boulders in its time field, and exactly Required boulders, shared
    /// (PhysicsTrialSpec, built by PhysicsTrial as the face's own trial is). Each door's switch stands across the room on
    /// the dash line (DashY, below the booths), and the door latches when the player reaches its latch band (LatchBand: a
    /// tall strip on the line beside the booth, on the switch's side), so the dash is one straight run along the floor
    /// (drawn on it). Review patch: the dash is fair from anywhere on the switch. A player touching the switch at its very
    /// edge, up to SwitchTouch off the line, runs a parallel line that still meets the latch band and never a booth (the
    /// corridor is clear for the whole touch spread), and the door's time (BaseSeconds) is set from the dash: with Required
    /// boulders it is open for the longest dash (leaving from the switch's far side), and with one short it shuts at least
    /// 10% before the shortest dash (leaving on the line at its near edge) ends. The same three boulders must be dragged to
    /// one door, then over to the other: six box lines. Deal picks among a few seeded variants; Problems proves each one:
    /// that timing, the corridor, the fields apart, the boulders starting outside every field, and room for Required
    /// boulders in each field off the corridor.
    /// </summary>
    public static class TempleBooths
    {
        /// <summary>Boulders each booth door needs, and how many there are (shared).</summary>
        public const int Required = 3;

        public const int BoothCount = 2;

        /// <summary>The booths' near (south) wall line (local y): their doors face south onto the open floor.</summary>
        public const float NearY = 2.4f;

        /// <summary>A booth's footprint (local), centred on x, its door south.</summary>
        public static Rect Footprint(float x) =>
            Rect.MinMaxRect(x - PhysicsPlan.BoothOuter / 2f, NearY, x + PhysicsPlan.BoothOuter / 2f, NearY + PhysicsPlan.BoothOuter);

        /// <summary>A booth's door (local), facing south (the player comes from below), needing Required boulders.</summary>
        public static DoorLayout Door(float x) => new DoorLayout(new Vector2(x, NearY + PhysicsPlan.BoothWall / 2f), Vector2.down,
            PhysicsPlan.BoothInner, PhysicsPlan.BoothWall, PhysicsPlan.BoothInner, Required);

        /// <summary>The dash line (local y): below the booths, far enough that the whole switch-touch corridor clears them.</summary>
        public const float DashY = 0.3f;

        /// <summary>How far off the switch's centre a player still touches it (switch and player radii).</summary>
        public const float SwitchTouch = TimeField.SwitchRadius + TimeField.PlayerRadius;

        /// <summary>The latch band's width, and how far it stands from its booth's side (beyond a player's radius).</summary>
        public const float BandWidth = 0.3f, BandGap = 0.3f;

        /// <summary>The share of the shortest dash one boulder short may stay open for, at most (it shuts at least 10% early).</summary>
        public const float ShortShare = 0.9f;

        private static readonly float[] BoothXs = { 5.5f, 6.5f };
        private static readonly float[] SwitchXs = { 9f, 10f };

        private static readonly Vector2[][] BoulderSets =
        {
            new[] { new Vector2(-2.5f, -3.5f), new Vector2(0f, -1.5f), new Vector2(2.5f, -3.5f) },
            new[] { new Vector2(-2f, -1.5f), new Vector2(0f, -3f), new Vector2(2f, -1.5f) },
            new[] { new Vector2(-3f, -2.5f), new Vector2(0f, -0.5f), new Vector2(3f, -2.5f) },
        };

        /// <summary>Deals a variant: the booths' spread, the switches' distance, the boulders' start row and which booth is first, each seeded.</summary>
        public static PhysicsTrialSpec Deal(SeededRng rng)
        {
            float bx = BoothXs[rng.NextInt(BoothXs.Length)];
            float sx = SwitchXs[rng.NextInt(SwitchXs.Length)];
            Vector2[] boulders = BoulderSets[rng.NextInt(BoulderSets.Length)];
            bool westFirst = rng.NextInt(2) == 0;
            var booths = new List<TrialBoothSpec>();
            foreach (float side in westFirst ? new[] { -1f, 1f } : new[] { 1f, -1f })
            {
                float x = side * bx;
                // Each door's switch stands across the room on the dash line, so the run is one straight dash along the floor
                // (a keyboard's own direction: no diagonal detour eats the late tier's tight slack).
                DoorLayout door = Door(x).WithSwitch(new Vector2(-side * sx, DashY));
                booths.Add(new TrialBoothSpec(Footprint(x), door));
            }
            return new PhysicsTrialSpec(default, booths, new List<Vector2>(boulders), Required, true);
        }

        /// <summary>The side of its booth a door's switch is on (+1 east, -1 west).</summary>
        public static float SwitchSide(DoorLayout door) => door.SwitchLocal.x > door.DoorLocal.x ? 1f : -1f;

        /// <summary>
        /// A door's latch band (local): a strip across the dash line beside its booth, on the switch's side, tall enough for
        /// the whole switch-touch spread (SwitchTouch either side, plus a margin). Reaching it while the door is open latches it.
        /// </summary>
        public static Rect LatchBand(DoorLayout door)
        {
            float x = door.DoorLocal.x + SwitchSide(door) * (PhysicsPlan.BoothOuter / 2f + TimeField.PlayerRadius + BandGap + BandWidth / 2f);
            float h = SwitchTouch + 0.3f;
            return Rect.MinMaxRect(x - BandWidth / 2f, DashY - h, x + BandWidth / 2f, DashY + h);
        }

        /// <summary>The dash's corridor (local): from the switch to the latch band, as tall as a player touching the switch anywhere.</summary>
        public static Rect Corridor(DoorLayout door)
        {
            Rect band = LatchBand(door);
            float h = SwitchTouch + TimeField.PlayerRadius;
            float near = SwitchSide(door) > 0f ? band.xMax : band.xMin;
            return Rect.MinMaxRect(Mathf.Min(near, door.SwitchLocal.x), DashY - h, Mathf.Max(near, door.SwitchLocal.x), DashY + h);
        }

        /// <summary>From the switch's centre line to the band's near edge, less the player's radius (the run with no switch in it).</summary>
        private static float DashLength(DoorLayout door)
        {
            Rect band = LatchBand(door);
            float near = SwitchSide(door) > 0f ? band.xMax : band.xMin;
            return Mathf.Abs(door.SwitchLocal.x - near) - TimeField.PlayerRadius;
        }

        /// <summary>The longest dash (seconds at base speed): leaving the switch from its far side (touching it SwitchTouch off the line).</summary>
        public static float LongDash(DoorLayout door) => DashLength(door) / TimeField.BasePlayerSpeed;

        /// <summary>The shortest dash: leaving the switch on the line at its near edge.</summary>
        public static float ShortDash(DoorLayout door) => (DashLength(door) - SwitchTouch) / TimeField.BasePlayerSpeed;

        /// <summary>
        /// The door's base time for the dash: with Required boulders it stays open Slack(Required) times the longest dash, but
        /// never so long that one boulder short stays open more than ShortShare of the shortest dash.
        /// </summary>
        public static float BaseSeconds(DoorLayout door)
        {
            float ratio = TimeField.Dilation((Required - 1) * TimeField.BoulderMass) / TimeField.Dilation(Required * TimeField.BoulderMass);
            float open = Mathf.Min(TimeField.Slack(Required) * LongDash(door), ShortShare * ShortDash(door) / ratio * 0.998f);
            return open / TimeField.Dilation(Required * TimeField.BoulderMass);
        }

        /// <summary>A circle against a rect: true when they overlap.</summary>
        private static bool Touches(Rect r, Vector2 c, float radius)
        {
            float dx = Mathf.Max(r.xMin - c.x, 0f, c.x - r.xMax), dy = Mathf.Max(r.yMin - c.y, 0f, c.y - r.yMax);
            return dx * dx + dy * dy < radius * radius;
        }

        /// <summary>
        /// How many boulders fit in a door's field (centres within FieldRadius less NearMargin of its threshold), on the floor,
        /// clear of the booths and the dash's corridor (Corridor): a greedy count over a fine grid.
        /// </summary>
        public static int FieldRoom(PhysicsTrialSpec spec, DoorLayout door) => FieldSpots(spec, door).Count;

        /// <summary>The boulder spots FieldRoom counts (local): in the door's field, off the booths and the dash's corridor.</summary>
        public static List<Vector2> FieldSpots(PhysicsTrialSpec spec, DoorLayout door)
        {
            float inner = TimeField.FieldRadius - PhysicsPlan.NearMargin;
            Rect bounds = Boulder.ScreenBounds(Vector2.zero);
            var placed = new List<Vector2>();
            const float step = 0.25f;
            for (float y = door.FieldLocal.y - inner; y <= door.FieldLocal.y + inner; y += step)
                for (float x = door.FieldLocal.x - inner; x <= door.FieldLocal.x + inner; x += step)
                {
                    var p = new Vector2(x, y);
                    if (Vector2.Distance(p, door.FieldLocal) > inner || !bounds.Contains(p)) continue;
                    bool clear = true;
                    foreach (TrialBoothSpec b in spec.Booths)
                        if (Touches(TempleOrderSpec.Grow(b.Footprint, 0.1f), p, Boulder.Radius)) clear = false;
                    if (Touches(TempleOrderSpec.Grow(Corridor(door), 0.05f), p, Boulder.Radius)) clear = false;
                    foreach (Vector2 q in placed)
                        if (Vector2.Distance(p, q) < 2f * Boulder.Radius + 0.1f) clear = false;
                    if (clear) placed.Add(p);
                }
            return placed;
        }

        /// <summary>
        /// Problems (empty when the temple is solvable as dealt): two booths, exactly Required shared boulders weighing only in
        /// the booths (CountsOnlyOwnBoulders); each booth on the floor, clear of the altar, the lane in and the other booth;
        /// the fields more than twice their radius apart; each door with its switch, a walk of at least PhysicsPlan's least,
        /// open for the whole walk with Required boulders and shut at least 10% early with one short, its runway from the
        /// switch clear of both booths; the boulders starting on the floor, outside every field, clear of the booths, the
        /// switches and each other; and room in each field for Required boulders off the runway.
        /// </summary>
        public static List<string> Problems(string what, PhysicsTrialSpec spec)
        {
            var problems = new List<string>();
            if (spec.Booths.Count != BoothCount) problems.Add($"{what}: {spec.Booths.Count} booths (want {BoothCount})");
            if (spec.Required != Required || spec.Boulders.Count != Required)
                problems.Add($"{what}: {spec.Boulders.Count} boulders for {spec.Required} a door (want exactly {Required}, shared)");
            if (!spec.CountsOnlyOwnBoulders) problems.Add($"{what}: the booths weigh any boulder");
            Rect floor = TempleRoom.Floor, altar = TempleRoom.AltarRect, lane = TempleRoom.EntryLane;
            Rect bounds = Boulder.ScreenBounds(Vector2.zero);
            for (int i = 0; i < spec.Booths.Count; i++)
            {
                TrialBoothSpec booth = spec.Booths[i];
                DoorLayout d = booth.Layout;
                string w = $"{what} booth {i}";
                if (!TempleOrderSpec.Grow(floor, -0.3f).Contains(booth.Footprint.min) || !TempleOrderSpec.Grow(floor, -0.3f).Contains(booth.Footprint.max))
                    problems.Add($"{w}: off the floor");
                if (booth.Footprint.Overlaps(TempleOrderSpec.Grow(altar, 0.5f)) || booth.Footprint.Overlaps(lane)) problems.Add($"{w}: on the altar or the lane in");
                if (d.Required != Required) problems.Add($"{w}: its door needs {d.Required}");
                if (!d.HasSwitch)
                {
                    problems.Add($"{w}: no switch");
                    continue;
                }
                if (!TempleOrderSpec.Grow(floor, -1f).Contains(d.SwitchLocal)) problems.Add($"{w}: its switch is off the floor");
                if (ShortDash(d) < PhysicsPlan.MinWalkSeconds) problems.Add($"{w}: the dash from its switch is only {ShortDash(d):0.00} s");
                float open = TimeField.OpenSeconds(BaseSeconds(d), Required * TimeField.BoulderMass);
                float shortOpen = TimeField.OpenSeconds(BaseSeconds(d), (Required - 1) * TimeField.BoulderMass);
                if (open < LongDash(d)) problems.Add($"{w}: shut before the longest dash ends even with {Required} boulders");
                if (shortOpen > ShortShare * ShortDash(d)) problems.Add($"{w}: one boulder short still nearly makes the shortest dash ({shortOpen:0.00} of {ShortDash(d):0.00} s)");
                // The whole switch-touch corridor (every parallel dash) is clear of both booths, and meets the latch band.
                Rect corridor = Corridor(d);
                foreach (TrialBoothSpec other in spec.Booths)
                    if (corridor.Overlaps(TempleOrderSpec.Grow(other.Footprint, 0.05f))) problems.Add($"{w}: a dash from its switch can hit a booth");
                Rect band = LatchBand(d);
                if (band.yMin > DashY - SwitchTouch || band.yMax < DashY + SwitchTouch) problems.Add($"{w}: its latch band misses a dash off the line");
                if (band.Overlaps(TempleOrderSpec.Grow(booth.Footprint, TimeField.PlayerRadius))) problems.Add($"{w}: its latch band is behind the booth's wall");
                for (int j = i + 1; j < spec.Booths.Count; j++)
                {
                    if (Vector2.Distance(d.FieldLocal, spec.Booths[j].Layout.FieldLocal) <= 2f * TimeField.FieldRadius)
                        problems.Add($"{w}: its field overlaps booth {j}'s");
                    if (TempleOrderSpec.Grow(booth.Footprint, 0.5f).Overlaps(spec.Booths[j].Footprint)) problems.Add($"{w}: touches booth {j}");
                }
                int room = FieldRoom(spec, d);
                if (room < Required) problems.Add($"{w}: room for only {room} boulders in its field off the run");
            }
            for (int i = 0; i < spec.Boulders.Count; i++)
            {
                Vector2 b = spec.Boulders[i];
                string w = $"{what} boulder {i}";
                if (!bounds.Contains(b)) problems.Add($"{w}: off the floor");
                foreach (TrialBoothSpec booth in spec.Booths)
                {
                    if (Vector2.Distance(b, booth.Layout.FieldLocal) <= TimeField.FieldRadius + PhysicsPlan.FieldClearance)
                        problems.Add($"{w}: starts in a field");
                    if (Touches(TempleOrderSpec.Grow(booth.Footprint, 0.3f), b, Boulder.Radius)) problems.Add($"{w}: starts against a booth");
                    if (booth.Layout.HasSwitch && Vector2.Distance(b, booth.Layout.SwitchLocal) < PhysicsPlan.MinGap) problems.Add($"{w}: on a switch");
                }
                if (Touches(altar, b, Boulder.Radius)) problems.Add($"{w}: on the altar");
                for (int j = i + 1; j < spec.Boulders.Count; j++)
                    if (Vector2.Distance(b, spec.Boulders[j]) < PhysicsPlan.BoulderSpacing) problems.Add($"{w}: too near boulder {j}");
            }
            return problems;
        }

        /// <summary>
        /// The least drag (tiles of the first 16x10 screens): each boulder from its start into the first door's field, then
        /// every boulder over to the second's.
        /// </summary>
        public static float DragTiles(PhysicsTrialSpec spec)
        {
            if (spec.Booths.Count < 2) return 0f;
            float inner = TimeField.FieldRadius - PhysicsPlan.NearMargin;
            Vector2 a = spec.Booths[0].Layout.FieldLocal, b = spec.Booths[1].Layout.FieldLocal;
            float total = 0f;
            foreach (Vector2 p in spec.Boulders) total += Mathf.Max(0f, Vector2.Distance(p, a) - inner);
            total += spec.Boulders.Count * Mathf.Max(0f, Vector2.Distance(a, b) - 2f * inner);
            return total / ScreenMath.ContentScale;
        }

        public static string Stats(PhysicsTrialSpec spec)
        {
            var walks = new List<string>();
            float slack = float.MaxValue, early = float.MaxValue;
            foreach (TrialBoothSpec b in spec.Booths)
            {
                DoorLayout d = b.Layout;
                walks.Add($"{ShortDash(d):0.00}-{LongDash(d):0.00}");
                slack = Mathf.Min(slack, TimeField.OpenSeconds(BaseSeconds(d), Required) / LongDash(d));
                early = Mathf.Min(early, 1f - TimeField.OpenSeconds(BaseSeconds(d), Required - 1) / ShortDash(d));
            }
            return $"{spec.Booths.Count} booths sharing {spec.Boulders.Count} boulders (each door needs {Required}), box lines {spec.Booths.Count * Required}, " +
                   $"dashes {string.Join(" / ", walks)} s (on the line to the switch's far side), slack {slack:0.00} over the longest, " +
                   $"one short shuts {early * 100f:0.#}% before the shortest, least drag {DragTiles(spec):0.#} tiles";
        }

        public static string Signature(PhysicsTrialSpec spec)
        {
            var sb = new StringBuilder();
            foreach (TrialBoothSpec b in spec.Booths) sb.Append("booth ").Append(b.Layout).Append(' ');
            foreach (Vector2 p in spec.Boulders) sb.Append(p.ToString("0.0")).Append(' ');
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// The hand-checked temple layouts (4.14) that stand in when a search runs past its budget (never on seeds 1-500, TempleTests):
    /// a late island network for the north and south doorways, and a late ice screen for the south doorway alone (the
    /// generators' own templates are made for four doorways). Wind's own late template already holds for the south doorway.
    /// Each is one the search found, kept by hand; TempleTests checks every target.
    /// </summary>
    public static class TempleTemplates
    {
        public const string IslandText =
            "0,0 2,0 3,0 4,0 1,1 2,1 3,1 0,2 1,2 2,2 3,2 4,2 | 0-1 0-7 1-5 2-3 3-11 4-5 4-8 5-6 5-9 6-10 7-8 9-10 10-11 | doors 9,-1,1,-1 trial -1 reward 8";

        public static readonly string[] IceRows =
        {
            "...o....#......#.....", "#..............#..#..", "..#..o........#......", ".................#...",
            ".##.......+.#o#...#..", ".........+++.......#.", ".........#+.#.....#..", "##.....#......#...#..",
            "#........#....###..#.", "#......o....#.#......", "#........~L~o...G....",
        };

        public static IslandLayout Islands() => IslandLayout.FromDesign(IslandLayout.ParseDesign(IslandText), TemplePuzzle.Tier);

        public static IceLayout Ice() => IceLayout.FromRows(TemplePuzzle.Tier, TemplePlan.SouthOnly, IceRows);
    }
}
