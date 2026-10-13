using System;
using System.Collections.Generic;
using System.Text;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The kind of puzzle a face's temple holds (4.14): a hard version of the face's own puzzle.</summary>
    public enum TemplePuzzleKind
    {
        /// <summary>Math: a late "light every plank" island network (IslandLayout).</summary>
        Islands = 0,

        /// <summary>Earth: a late sliding-ice screen (IceLayout).</summary>
        Ice = 1,

        /// <summary>Earth: a late wind-stream screen (WindLayout).</summary>
        Wind = 2,

        /// <summary>Physics: two booths behind timed doors sharing three boulders (PhysicsTrial, TempleBooths).</summary>
        Booths = 3,

        /// <summary>Chemistry (4.19): Lights Out on a 5x3 bank of crystals (TempleLights); a strike flips a crystal and its neighbours.</summary>
        Lights = 4,

        /// <summary>Biology (4.19): birdsong Simon with four finch statues (TempleSong); a wrong note replays the round.</summary>
        Song = 5,

        /// <summary>Town (4.19): a 3x3 sliding-tile picture of the town crest (TempleSliding); strike a tile beside the gap.</summary>
        Sliding = 6,
    }

    /// <summary>
    /// One face's temple puzzle (4.14) as pure data: what kind it is, the tier it is made at (late, always: at or above the
    /// face's hardest tier in the research note) and its layout or spec, all from the run seed and the face on their own
    /// stream (RngStreams.Temples), so the game, the sweep and the tests make the same puzzle. Every kind is either searched
    /// and kept only once its solver finds no problem (islands, ice, wind: their generators' own checks, with a hand-checked
    /// temple template past the budget), dealt so it is solvable by construction (booths: Problems re-proves the timing and
    /// reach), or (4.19) made by legal moves from the solved state and measured by a breadth-first search (the sliding
    /// picture, the crystals) or dealt and checked (the song). The room it stands in is a whole screen off the net
    /// (TempleRooms) with one doorway, south.
    /// </summary>
    public sealed class TemplePuzzle
    {
        /// <summary>The tier every temple is made at: late (the research note's hardest).</summary>
        public const PuzzleTier Tier = PuzzleTier.Late;

        public int Seed { get; }
        public FaceId Face { get; }
        public Theme Theme { get; }
        public TemplePuzzleKind Kind { get; }

        public IslandLayout Islands { get; }
        public IceLayout Ice { get; }
        public WindLayout Wind { get; }
        public PhysicsTrialSpec Booths { get; }
        public TempleSliding Sliding { get; }
        public TempleLights Lights { get; }
        public TempleSong Song { get; }

        /// <summary>True when a hand-checked temple template stood in for the search (the sweep reports it).</summary>
        public bool FromTemplate { get; }

        public TemplePuzzle(int seed, FaceId face, Theme theme, TemplePuzzleKind kind, IslandLayout islands = null, IceLayout ice = null,
            WindLayout wind = null, PhysicsTrialSpec booths = null, TempleSliding sliding = null, TempleLights lights = null,
            TempleSong song = null, bool fromTemplate = false)
        {
            Seed = seed;
            Face = face;
            Theme = theme;
            Kind = kind;
            Islands = islands;
            Ice = ice;
            Wind = wind;
            Booths = booths;
            Sliding = sliding;
            Lights = lights;
            Song = song;
            FromTemplate = fromTemplate;
        }

        /// <summary>Where the gem rises once the puzzle is solved (local to the room's centre): the goal, or the altar.</summary>
        public Vector2 GemSpot
        {
            get
            {
                switch (Kind)
                {
                    case TemplePuzzleKind.Islands: return Islands != null ? Islands.GoalSpot : TempleRoom.AltarSpot;
                    case TemplePuzzleKind.Ice: return Ice != null && Ice.Goal >= 0 ? IceLayout.CellLocal(Ice.Goal) : TempleRoom.AltarSpot;
                    case TemplePuzzleKind.Wind: return Wind != null && Wind.Goal >= 0 ? WindLayout.CellLocal(Wind.Goal) : TempleRoom.AltarSpot;
                    default: return TempleRoom.AltarSpot;
                }
            }
        }

        /// <summary>True for the kinds that fill the whole room with a grid or water (the altar stands on their goal).</summary>
        public bool FillsRoom => Kind == TemplePuzzleKind.Islands || Kind == TemplePuzzleKind.Ice || Kind == TemplePuzzleKind.Wind;

        /// <summary>
        /// Problems with the puzzle (empty when it is solvable inside its tier's targets from the temple's one doorway): the
        /// islands' own checks (geometry, every target, solvable), the ice's and wind's (every target from the south doorway,
        /// no hard traps), the booths' timing and reach (TempleBooths.Problems), or 4.19's tiles, crystals and song (their
        /// searched optimal lengths and placement: TempleSliding, TempleLights, TempleSong); and the kind matches the theme.
        /// </summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            string what = $"the {InWorldText.Region(Theme)} temple ({Kind})";
            if (!TemplePlan.KindsFor(Theme).Contains(Kind)) problems.Add($"{what}: not a {Theme} puzzle");
            switch (Kind)
            {
                case TemplePuzzleKind.Islands:
                    if (Islands == null) problems.Add($"{what}: no layout");
                    else
                    {
                        problems.AddRange(Islands.Problems(what, TemplePlan.IslandDoor));
                        if (Islands.Tier != Tier) problems.Add($"{what}: {Islands.Tier}, not {Tier}");
                        if (!Islands.Solvable) problems.Add($"{what}: not solvable from its doorway");
                    }
                    break;
                case TemplePuzzleKind.Ice:
                    if (Ice == null) problems.Add($"{what}: no layout");
                    else
                    {
                        problems.AddRange(Ice.Problems(what));
                        if (Ice.Tier != Tier) problems.Add($"{what}: {Ice.Tier}, not {Tier}");
                        for (int f = 0; f < 4; f++)
                            if (Ice.HasDoor((Facing)f) != TemplePlan.SouthOnly((Facing)f)) problems.Add($"{what}: the {(Facing)f} landing is wrong");
                    }
                    break;
                case TemplePuzzleKind.Wind:
                    if (Wind == null) problems.Add($"{what}: no layout");
                    else
                    {
                        problems.AddRange(Wind.Problems(what));
                        if (Wind.Tier != Tier) problems.Add($"{what}: {Wind.Tier}, not {Tier}");
                        for (int f = 0; f < 4; f++)
                            if (Wind.HasDoor((Facing)f) != TemplePlan.SouthOnly((Facing)f)) problems.Add($"{what}: the {(Facing)f} doorway is wrong");
                    }
                    break;
                case TemplePuzzleKind.Booths:
                    if (Booths == null) problems.Add($"{what}: no booths");
                    else problems.AddRange(TempleBooths.Problems(what, Booths));
                    break;
                case TemplePuzzleKind.Sliding:
                    if (Sliding == null) problems.Add($"{what}: no tiles");
                    else problems.AddRange(Sliding.Problems(what));
                    break;
                case TemplePuzzleKind.Lights:
                    if (Lights == null) problems.Add($"{what}: no crystals");
                    else problems.AddRange(Lights.Problems(what));
                    break;
                default:
                    if (Song == null) problems.Add($"{what}: no song");
                    else problems.AddRange(Song.Problems(what));
                    break;
            }
            return problems;
        }

        /// <summary>The puzzle's measured difficulty in a line (the sweep and the plan's notes).</summary>
        public string Stats()
        {
            switch (Kind)
            {
                case TemplePuzzleKind.Islands: return Islands?.Measure(Facing.South).ToString() ?? "-";
                case TemplePuzzleKind.Ice: return Ice?.Measure(Facing.South).ToString() ?? "-";
                case TemplePuzzleKind.Wind: return Wind?.Measure(Facing.South).ToString() ?? "-";
                case TemplePuzzleKind.Booths: return Booths != null ? TempleBooths.Stats(Booths) : "-";
                case TemplePuzzleKind.Sliding: return Sliding?.Stats() ?? "-";
                case TemplePuzzleKind.Lights: return Lights?.Stats() ?? "-";
                default: return Song?.Stats() ?? "-";
            }
        }

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(Face).Append('=').Append(Theme).Append(' ').Append(Kind).Append(FromTemplate ? " template" : "").Append(" [");
            switch (Kind)
            {
                case TemplePuzzleKind.Islands: sb.Append(Islands?.Signature()); break;
                case TemplePuzzleKind.Ice: sb.Append(Ice?.Signature()); break;
                case TemplePuzzleKind.Wind: sb.Append(Wind?.Signature()); break;
                case TemplePuzzleKind.Booths: sb.Append(Booths != null ? TempleBooths.Signature(Booths) : "-"); break;
                case TemplePuzzleKind.Sliding: sb.Append(Sliding?.Signature()); break;
                case TemplePuzzleKind.Lights: sb.Append(Lights?.Signature()); break;
                default: sb.Append(Song?.Signature()); break;
            }
            return sb.Append(']').ToString();
        }

        public override string ToString() => $"the {InWorldText.Region(Theme)} temple ({Kind}, {Tier})";
    }

    /// <summary>
    /// The run's temples (4.14), pure: one per face with a town (every face at 3x3), each a TemplePuzzle made from the seed and
    /// the face (RngStreams.Temples) and cached, so a run makes each once. Earth draws ice or wind (one draw, first on its
    /// stream); every other theme has one kind. FindProblems is the sweep's check that every temple is solvable.
    /// </summary>
    public static class TemplePlan
    {
        public const ulong RngStream = RngStreams.Temples;

        private static readonly BoundedCache<(int, FaceId, Theme), TemplePuzzle> Cache = new BoundedCache<(int, FaceId, Theme), TemplePuzzle>(128);

        /// <summary>Forgets every cached puzzle (timing tests measure a cold generation).</summary>
        public static void ClearCache() => Cache.Clear();

        /// <summary>Temple puzzles made so far (4.21: a warmed run makes none on unsealing).</summary>
        public static int Generated => Cache.Added;

        /// <summary>The temple's one doorway: south (the way in and out).</summary>
        public static bool SouthOnly(Facing side) => side == Facing.South;

        /// <summary>
        /// The doorways the temple's island network is made for: north and south. The north doorway's island stands against the
        /// temple's solid north wall (a dead end on the network); a network made for the south doorway alone found no design on
        /// the lattice (assumption: two doorways, so both lie on the network and no gap doorway is dealt).
        /// </summary>
        public static bool IslandDoor(Facing side) => side == Facing.South || side == Facing.North;

        /// <summary>The kinds a theme's temple can hold.</summary>
        public static List<TemplePuzzleKind> KindsFor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Math: return new List<TemplePuzzleKind> { TemplePuzzleKind.Islands };
                case Theme.EarthAtmosphere: return new List<TemplePuzzleKind> { TemplePuzzleKind.Ice, TemplePuzzleKind.Wind };
                case Theme.Physics: return new List<TemplePuzzleKind> { TemplePuzzleKind.Booths };
                case Theme.Chemistry: return new List<TemplePuzzleKind> { TemplePuzzleKind.Lights };
                case Theme.Biology: return new List<TemplePuzzleKind> { TemplePuzzleKind.Song };
                default: return new List<TemplePuzzleKind> { TemplePuzzleKind.Sliding };
            }
        }

        /// <summary>The face's own seed on the temple stream.</summary>
        public static SeededRng RngFor(int seed, FaceId face) =>
            new SeededRng(((ulong)(uint)seed << 32) | (uint)(0x7E00 + (int)face), RngStream);

        /// <summary>The temple of a face in a run (made once per seed, face and theme, then cached).</summary>
        public static TemplePuzzle For(int seed, FaceId face, Theme theme)
        {
            var key = (seed, face, theme);
            if (Cache.TryGetValue(key, out TemplePuzzle cached)) return cached;
            TemplePuzzle made = Make(seed, face, theme);
            Cache.Add(key, made);
            return made;
        }

        /// <summary>The temple of a face of a model.</summary>
        public static TemplePuzzle For(CubeModel model, FaceId face) => For(model.Seed, face, model.ThemeOf(face));

        /// <summary>Makes a temple's puzzle (no cache).</summary>
        public static TemplePuzzle Make(int seed, FaceId face, Theme theme)
        {
            SeededRng rng = RngFor(seed, face);
            List<TemplePuzzleKind> kinds = KindsFor(theme);
            TemplePuzzleKind kind = kinds.Count > 1 ? kinds[rng.NextInt(kinds.Count)] : kinds[0];
            switch (kind)
            {
                case TemplePuzzleKind.Islands:
                {
                    IslandLayout layout = IslandLayout.Generate(rng, TemplePuzzle.Tier, IslandDoor, false);
                    bool template = layout == null || layout.FromTemplate || layout.Problems("temple", IslandDoor).Count > 0;
                    if (template) layout = TempleTemplates.Islands();
                    return new TemplePuzzle(seed, face, theme, kind, islands: layout, fromTemplate: template);
                }
                case TemplePuzzleKind.Ice:
                {
                    IceLayout layout = IceLayout.Generate(rng, TemplePuzzle.Tier, SouthOnly);
                    bool template = layout.FromTemplate || layout.Problems("temple").Count > 0;
                    if (template) layout = TempleTemplates.Ice();
                    return new TemplePuzzle(seed, face, theme, kind, ice: layout, fromTemplate: template);
                }
                case TemplePuzzleKind.Wind:
                {
                    WindLayout layout = WindLayout.Generate(rng, TemplePuzzle.Tier, SouthOnly);
                    bool template = layout.FromTemplate || layout.Problems("temple").Count > 0;
                    if (template) layout = WindLayout.Template(TemplePuzzle.Tier, SouthOnly);
                    return new TemplePuzzle(seed, face, theme, kind, wind: layout, fromTemplate: template);
                }
                case TemplePuzzleKind.Booths:
                    return new TemplePuzzle(seed, face, theme, kind, booths: TempleBooths.Deal(rng));
                case TemplePuzzleKind.Sliding:
                {
                    TempleSliding tiles = TempleSliding.Deal(rng);
                    return new TemplePuzzle(seed, face, theme, kind, sliding: tiles, fromTemplate: tiles.FromTemplate);
                }
                case TemplePuzzleKind.Lights:
                {
                    TempleLights lights = TempleLights.Deal(rng);
                    return new TemplePuzzle(seed, face, theme, kind, lights: lights, fromTemplate: lights.FromTemplate);
                }
                default:
                {
                    TempleSong song = TempleSong.Deal(rng);
                    return new TemplePuzzle(seed, face, theme, kind, song: song, fromTemplate: song.FromTemplate);
                }
            }
        }

        /// <summary>Every face of a run with a temple: every unsealed face (each has a town at 3x3), Town first.</summary>
        public static List<FaceId> TempleFaces(CubeModel model)
        {
            var faces = new List<FaceId>();
            if (model == null || !TownSquare.HasTown(model.FaceSize)) return faces;
            faces.Add(CubeModel.StartFace);
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (face != CubeModel.StartFace && !model.IsSealed(face)) faces.Add(face);
            }
            return faces;
        }

        /// <summary>
        /// The sweep's check (empty when all is well): every temple of the run is solvable inside its tier's targets from its
        /// doorway (TemplePuzzle.Problems), of its face's kind.
        /// </summary>
        public static List<string> FindProblems(CubeModel model)
        {
            var problems = new List<string>();
            foreach (FaceId face in TempleFaces(model))
            {
                TemplePuzzle temple = For(model, face);
                if (temple == null) problems.Add($"face {face}: no temple");
                else problems.AddRange(temple.Problems());
            }
            return problems;
        }

        /// <summary>The run's temples in one line each (the determinism fixture).</summary>
        public static string Signature(CubeModel model)
        {
            var parts = new List<string>();
            foreach (FaceId face in TempleFaces(model)) parts.Add(For(model, face).Signature());
            return string.Join("; ", parts);
        }
    }

    /// <summary>
    /// The temple room's fixed geometry (4.14), local to its screen's centre: a whole screen (ScreenMath.DefaultScreenSize)
    /// inside the screen-edge walls (ScreenEdges), with one doorway, south. The player arrives just inside it (as through any
    /// south doorway: IslandLayout.Arrival), and walking out past the screen's south edge leaves. The altar (where the gem
    /// rises in the rooms with no grid) stands by the north wall.
    /// </summary>
    public static class TempleRoom
    {
        /// <summary>The temple rooms' row of screens below the faces (arena -3, town rooms -6, lair rooms -9).</summary>
        public const int RoomRow = -12;

        /// <summary>Where the gem rises in a booth, tile, crystal or finch temple (local): by the north wall, on the altar.</summary>
        public static readonly Vector2 AltarSpot = new Vector2(0f, 5f);

        /// <summary>The altar's footprint (local): art only, nothing solid.</summary>
        public static Rect AltarRect => new Rect(AltarSpot - new Vector2(1.1f, 0.9f), new Vector2(2.2f, 1.8f));

        /// <summary>A face's temple room screen: a row below the lair rooms, every other column.</summary>
        public static Vector2Int RoomScreen(FaceId face) => SubScreens.RoomScreen(RoomRow, face);

        /// <summary>A face's temple room centre (world).</summary>
        public static Vector2 RoomCentre(FaceId face) => SubScreens.RoomCentre(RoomRow, face);

        /// <summary>Where the player arrives (local): just inside the south doorway, as a walk in through it.</summary>
        public static Vector2 EntryPoint => IslandLayout.Arrival(Facing.South);

        /// <summary>The line past which the player has walked out (local y): the screen's south edge.</summary>
        public static float ExitLine => -ScreenEdges.Half.y;

        /// <summary>The walkable floor (local): the screen inside its edge walls.</summary>
        public static Rect Floor
        {
            get
            {
                Vector2 h = ScreenEdges.Half;
                float t = ScreenEdges.EdgeWallThickness;
                return Rect.MinMaxRect(-h.x + t, -h.y + t, h.x - t, h.y - t);
            }
        }

        /// <summary>The room's solid walls (local): every edge whole but the south one, which has its doorway.</summary>
        public static List<Rect> Walls => ScreenEdges.SolidRects(3, new Vector2Int(1, 1), TemplePlan.SouthOnly);

        /// <summary>The lane in from the doorway that a booth, tile, crystal or finch temple keeps clear (local).</summary>
        public static Rect EntryLane => Rect.MinMaxRect(-2f, Floor.yMin, 2f, -3f);

        /// <summary>A rect grown by a margin on every side (shrunk when negative).</summary>
        public static Rect Grow(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);
    }
}
