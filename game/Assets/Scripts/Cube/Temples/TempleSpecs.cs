using System;
using System.Collections.Generic;
using System.Text;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Physics' temple (4.14; 4.25 redesign) as pure data: two booths against the side walls, one high on one side and one low
    /// on the other, each behind a timed door facing across the room, needing Required (3, the research note's late count)
    /// boulders in its time field, with exactly Required boulders, shared (PhysicsTrialSpec, built by PhysicsTrial as the
    /// face's own trial is). Since 4.25 a temple door times out like the face's doors instead of latching: the booth counts
    /// once the player runs into its doorway while it is open, the door holds while the player is inside, and it shuts again
    /// on its timer once they leave (TimedDoorGate.Latches off). Each door's switch is a strip across a short stall (two
    /// posts) on the far side of the room, on the door's lane, so the dash is one straight run along the lane from the stall
    /// into the doorway, the same length from anywhere on the switch (the stall keeps the player on the lane): DashLength.
    /// The temple's field is stronger than the face's (FieldK per boulder, TimedDoorGate.DilationPerBoulder), so the door can
    /// stay open DashSlack (1.25) times the dash with three boulders and still shut at least 10% early with two
    /// (assumption: the face's 0.6 cannot give both, at most 1.27 times with any margin). Deal picks among a few seeded
    /// variants; Problems proves each one: the timing, the corridors clear, the fields apart, the boulders starting outside
    /// every field and off every corridor, and room for Required boulders in each field off the corridor.
    /// </summary>
    public static class TempleBooths
    {
        /// <summary>Boulders each booth door needs, and how many there are (shared).</summary>
        public const int Required = 3;

        public const int BoothCount = 2;

        /// <summary>The temple field's slowing per boulder (the face's is TimeField.K, 0.6).</summary>
        public const float FieldK = 1.8f;

        /// <summary>With Required boulders a door stays open this many times the dash (4.25: at least 1.25).</summary>
        public const float DashSlack = 1.25f;

        /// <summary>The share of the dash one boulder short may stay open for, at most (it shuts at least 10% early).</summary>
        public const float ShortShare = 0.9f;

        /// <summary>A stall's length along the lane, its inner width (the booth door's), its posts' thickness and the switch strip's depth.</summary>
        public const float StallLength = 1.8f, StallPost = 0.15f, StripDepth = 0.4f;

        /// <summary>The stall's inner width: the booth's doorway, so every run from the switch goes straight in.</summary>
        public static float StallInner => PhysicsPlan.BoothInner;

        /// <summary>How far a booth stands from its side wall (in from it, so the boulders' drag between the fields stays short).</summary>
        public const float WallGap = 1.5f;

        private static readonly float[] HighLanes = { 2.6f, 3.0f };
        private static readonly float[] LowLanes = { -1.5f, -2.0f };
        private static readonly float[] StallXs = { 7.2f, 7.6f };

        private static readonly Vector2[][] BoulderSets =
        {
            new[] { new Vector2(-1.6f, 0.8f), new Vector2(0f, 0.8f), new Vector2(1.6f, 0.8f) },
            new[] { new Vector2(-3.2f, -4.8f), new Vector2(0f, 0.8f), new Vector2(3.2f, -4.8f) },
            new[] { new Vector2(-2.6f, 5.2f), new Vector2(0f, 0.8f), new Vector2(2.6f, 5.2f) },
        };

        /// <summary>A booth's footprint (local) against a side wall (side -1 west, +1 east), centred on its lane.</summary>
        public static Rect Footprint(int side, float lane)
        {
            float outer = TempleRoom.Floor.xMax - WallGap, inner = outer - PhysicsPlan.BoothOuter;
            float h = PhysicsPlan.BoothOuter / 2f;
            return side < 0 ? Rect.MinMaxRect(-outer, lane - h, -inner, lane + h) : Rect.MinMaxRect(inner, lane - h, outer, lane + h);
        }

        /// <summary>A booth's door (local): in the wall facing across the room, the player coming from the far side; its switch strip's centre.</summary>
        public static DoorLayout Door(int side, float lane, float stallX)
        {
            Rect f = Footprint(side, lane);
            float x = side < 0 ? f.xMax - PhysicsPlan.BoothWall / 2f : f.xMin + PhysicsPlan.BoothWall / 2f;
            var opening = new Vector2(-side, 0f);
            var door = new DoorLayout(new Vector2(x, lane), opening, PhysicsPlan.BoothInner, PhysicsPlan.BoothWall, PhysicsPlan.BoothInner, Required);
            return door.WithSwitch(new Vector2(-side * (stallX + StripDepth / 2f), lane));
        }

        /// <summary>Deals a variant: which side is high, the lanes, the stalls' distance, the boulders' start and which booth is first, each seeded.</summary>
        public static PhysicsTrialSpec Deal(SeededRng rng)
        {
            int highSide = rng.NextInt(2) == 0 ? -1 : 1;
            float high = HighLanes[rng.NextInt(HighLanes.Length)];
            float low = LowLanes[rng.NextInt(LowLanes.Length)];
            float stall = StallXs[rng.NextInt(StallXs.Length)];
            Vector2[] boulders = BoulderSets[rng.NextInt(BoulderSets.Length)];
            bool highFirst = rng.NextInt(2) == 0;
            var booths = new List<TrialBoothSpec>();
            foreach (bool isHigh in highFirst ? new[] { true, false } : new[] { false, true })
            {
                int side = isHigh ? highSide : -highSide;
                float lane = isHigh ? high : low;
                booths.Add(new TrialBoothSpec(Footprint(side, lane), Door(side, lane, stall)));
            }
            var stones = new List<Vector2>();
            foreach (Vector2 b in boulders) stones.Add(new Vector2(b.x * highSide * -1f, b.y)); // mirrored with the booths
            return new PhysicsTrialSpec(default, booths, stones, Required);
        }

        /// <summary>The unit vector a door's dash runs along: from its switch toward the door.</summary>
        public static Vector2 DashWay(DoorLayout door) => -door.Opening;

        /// <summary>The door's face toward the room (local x): where its doorway starts.</summary>
        private static float FaceX(DoorLayout door) => door.DoorLocal.x + door.Opening.x * door.Thickness / 2f;

        /// <summary>The switch strip (local): across the stall at its end nearest the booth.</summary>
        public static Rect Strip(DoorLayout door)
        {
            float half = StallInner / 2f;
            return Rect.MinMaxRect(door.SwitchLocal.x - StripDepth / 2f, door.SwitchLocal.y - half, door.SwitchLocal.x + StripDepth / 2f,
                door.SwitchLocal.y + half);
        }

        /// <summary>The stall (local): its inside, from the strip's near edge back along the lane, StallInner wide.</summary>
        public static Rect Stall(DoorLayout door)
        {
            float near = door.SwitchLocal.x + DashWay(door).x * StripDepth / 2f;
            float far = near - DashWay(door).x * StallLength;
            float half = StallInner / 2f;
            return Rect.MinMaxRect(Mathf.Min(near, far), door.SwitchLocal.y - half, Mathf.Max(near, far), door.SwitchLocal.y + half);
        }

        /// <summary>The stall's two posts (local, solid): along the lane, either side of its inside.</summary>
        public static List<Rect> StallPosts(DoorLayout door)
        {
            Rect s = Stall(door);
            return new List<Rect>
            {
                Rect.MinMaxRect(s.xMin, s.yMax, s.xMax, s.yMax + StallPost),
                Rect.MinMaxRect(s.xMin, s.yMin - StallPost, s.xMax, s.yMin),
            };
        }

        /// <summary>The dash's corridor (local): what the player's body sweeps from the stall's far end into the doorway.</summary>
        public static Rect Corridor(DoorLayout door)
        {
            Rect s = Stall(door);
            float face = FaceX(door);
            float half = StallInner / 2f;
            float far = DashWay(door).x > 0f ? s.xMin : s.xMax;
            return Rect.MinMaxRect(Mathf.Min(face, far), door.DoorLocal.y - half, Mathf.Max(face, far), door.DoorLocal.y + half);
        }

        /// <summary>
        /// The dash's length (local units): from where the player's body leaves the switch strip (wherever it stood on it, the
        /// stall keeps it on the lane) to where it first touches the doorway, running straight along the lane.
        /// </summary>
        public static float DashLength(DoorLayout door)
        {
            Rect strip = Strip(door);
            float leave = DashWay(door).x > 0f ? strip.xMax : strip.xMin; // the strip's edge nearest the booth
            return Mathf.Abs(FaceX(door) - leave) - 2f * TimeField.PlayerRadius;
        }

        /// <summary>The dash in seconds at base speed (the same from anywhere on the switch).</summary>
        public static float DashSeconds(DoorLayout door) => DashLength(door) / TimeField.BasePlayerSpeed;

        /// <summary>The temple field's slowing of a mass (TimeField.Dilation at FieldK).</summary>
        public static float Dilation(float mass) => TimeField.Dilation(mass, FieldK);

        /// <summary>How long a temple door stays open with a mass in its field.</summary>
        public static float OpenSeconds(DoorLayout door, float mass) => BaseSeconds(door) * Dilation(mass);

        /// <summary>The door's base time: with Required boulders it stays open DashSlack times the dash.</summary>
        public static float BaseSeconds(DoorLayout door) => DashSlack * DashSeconds(door) / Dilation(Required * TimeField.BoulderMass);

        /// <summary>A circle against a rect: true when they overlap.</summary>
        private static bool Touches(Rect r, Vector2 c, float radius)
        {
            float dx = Mathf.Max(r.xMin - c.x, 0f, c.x - r.xMax), dy = Mathf.Max(r.yMin - c.y, 0f, c.y - r.yMax);
            return dx * dx + dy * dy < radius * radius;
        }

        /// <summary>
        /// How many boulders fit in a door's field (centres within FieldRadius less NearMargin of its threshold), on the floor,
        /// clear of the booths, the stalls and every dash's corridor: a greedy count over a fine grid.
        /// </summary>
        public static int FieldRoom(PhysicsTrialSpec spec, DoorLayout door) => FieldSpots(spec, door).Count;

        /// <summary>The boulder spots FieldRoom counts (local): in the door's field, off the booths, the stalls and the corridors.</summary>
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
                    if (!ClearOfBooths(spec, p, 0.1f)) continue;
                    bool clear = true;
                    foreach (Vector2 q in placed)
                        if (Vector2.Distance(p, q) < 2f * Boulder.Radius + 0.1f) clear = false;
                    if (clear) placed.Add(p);
                }
            return placed;
        }

        /// <summary>True when a boulder at p stays off every booth, stall and corridor (by margin).</summary>
        private static bool ClearOfBooths(PhysicsTrialSpec spec, Vector2 p, float margin)
        {
            foreach (TrialBoothSpec b in spec.Booths)
            {
                if (Touches(TempleRoom.Grow(b.Footprint, margin), p, Boulder.Radius)) return false;
                if (!b.Layout.HasSwitch) continue;
                if (Touches(TempleRoom.Grow(Corridor(b.Layout), margin / 2f), p, Boulder.Radius)) return false;
                foreach (Rect post in StallPosts(b.Layout))
                    if (Touches(TempleRoom.Grow(post, margin), p, Boulder.Radius)) return false;
            }
            return true;
        }

        /// <summary>
        /// Problems (empty when the temple is solvable as dealt): two booths and exactly Required shared boulders; each booth on
        /// the floor against a side wall, clear of the altar, the lane in and the other booth; each door facing across the
        /// room with its switch strip in its stall, a dash of at least PhysicsPlan's least walk, open DashSlack times the dash
        /// with Required boulders and shut at least 10% early with one short; its corridor (stall to doorway) clear of the other
        /// booth, the other stall, the altar and the lane in; the fields more than twice their radius apart; the boulders
        /// starting on the floor, outside every field, off every booth, stall and corridor, off the altar and the lane in, and
        /// apart; and room in each field for Required boulders off the corridors.
        /// </summary>
        public static List<string> Problems(string what, PhysicsTrialSpec spec)
        {
            var problems = new List<string>();
            if (spec.Booths.Count != BoothCount) problems.Add($"{what}: {spec.Booths.Count} booths (want {BoothCount})");
            if (spec.Required != Required || spec.Boulders.Count != Required)
                problems.Add($"{what}: {spec.Boulders.Count} boulders for {spec.Required} a door (want exactly {Required}, shared)");
            Rect floor = TempleRoom.Floor, altar = TempleRoom.AltarRect, lane = TempleRoom.EntryLane;
            Rect bounds = Boulder.ScreenBounds(Vector2.zero);
            for (int i = 0; i < spec.Booths.Count; i++)
            {
                TrialBoothSpec booth = spec.Booths[i];
                DoorLayout d = booth.Layout;
                string w = $"{what} booth {i}";
                if (!floor.Contains(booth.Footprint.min) || !floor.Contains(booth.Footprint.max)) problems.Add($"{w}: off the floor");
                if (booth.Footprint.Overlaps(TempleRoom.Grow(altar, 0.5f)) || booth.Footprint.Overlaps(lane)) problems.Add($"{w}: on the altar or the lane in");
                if (d.Required != Required) problems.Add($"{w}: its door needs {d.Required}");
                if (Mathf.Abs(d.Opening.y) > 1e-4f) problems.Add($"{w}: its door does not face across the room");
                if (!d.HasSwitch)
                {
                    problems.Add($"{w}: no switch");
                    continue;
                }
                Rect stall = Stall(d), corridor = Corridor(d);
                if (!TempleRoom.Grow(floor, -0.2f).Contains(stall.min) || !TempleRoom.Grow(floor, -0.2f).Contains(stall.max))
                    problems.Add($"{w}: its stall is off the floor");
                if (!stall.Contains(d.SwitchLocal)) problems.Add($"{w}: its switch is not in its stall");
                if (DashSeconds(d) < PhysicsPlan.MinWalkSeconds) problems.Add($"{w}: the dash is only {DashSeconds(d):0.00} s");
                float open = OpenSeconds(d, Required * TimeField.BoulderMass);
                float shortOpen = OpenSeconds(d, (Required - 1) * TimeField.BoulderMass);
                if (open < DashSlack * DashSeconds(d) - 1e-4f) problems.Add($"{w}: open {open:0.00} s with {Required} boulders, under {DashSlack} times the dash");
                if (shortOpen > ShortShare * DashSeconds(d)) problems.Add($"{w}: one boulder short still nearly makes the dash ({shortOpen:0.00} of {DashSeconds(d):0.00} s)");
                if (corridor.Overlaps(altar) || corridor.Overlaps(lane)) problems.Add($"{w}: its dash crosses the altar or the lane in");
                for (int j = 0; j < spec.Booths.Count; j++)
                {
                    if (j == i) continue;
                    TrialBoothSpec other = spec.Booths[j];
                    if (corridor.Overlaps(TempleRoom.Grow(other.Footprint, 0.05f))) problems.Add($"{w}: its dash can hit booth {j}");
                    if (other.Layout.HasSwitch)
                        foreach (Rect post in StallPosts(other.Layout))
                            if (corridor.Overlaps(TempleRoom.Grow(post, 0.05f))) problems.Add($"{w}: its dash can hit booth {j}'s stall");
                    if (j < i) continue;
                    if (Vector2.Distance(d.FieldLocal, other.Layout.FieldLocal) <= 2f * TimeField.FieldRadius)
                        problems.Add($"{w}: its field overlaps booth {j}'s");
                    if (TempleRoom.Grow(booth.Footprint, 0.5f).Overlaps(other.Footprint)) problems.Add($"{w}: touches booth {j}");
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
                    if (Vector2.Distance(b, booth.Layout.FieldLocal) <= TimeField.FieldRadius + PhysicsPlan.FieldClearance)
                        problems.Add($"{w}: starts in a field");
                if (!ClearOfBooths(spec, b, 0.3f)) problems.Add($"{w}: starts on a booth, a stall or a dash");
                if (Touches(altar, b, Boulder.Radius) || Touches(lane, b, Boulder.Radius)) problems.Add($"{w}: on the altar or the lane in");
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
            var dashes = new List<string>();
            float open = float.MaxValue, early = float.MaxValue;
            foreach (TrialBoothSpec b in spec.Booths)
            {
                DoorLayout d = b.Layout;
                dashes.Add($"{DashSeconds(d):0.00}");
                open = Mathf.Min(open, OpenSeconds(d, Required));
                early = Mathf.Min(early, 1f - OpenSeconds(d, Required - 1) / DashSeconds(d));
            }
            return $"{spec.Booths.Count} booths sharing {spec.Boulders.Count} boulders (each door needs {Required}), box lines {spec.Booths.Count * Required}, " +
                   $"dashes {string.Join(" / ", dashes)} s, open {open:0.00} s with {Required} ({DashSlack:0.##}x), " +
                   $"one short shuts {early * 100f:0.#}% early, least drag {DragTiles(spec):0.#} tiles";
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
