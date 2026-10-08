using System.Collections.Generic;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>What a town's enterable building is inside (4.6).</summary>
    public enum InteriorKind
    {
        /// <summary>A shop: shelves, a counter and a talkative keeper behind it (spending stays on the between-runs screen).</summary>
        Shop = 0,
        /// <summary>The leader's hall: a throne on a dais, banners, pillars and courtiers (the leader is out by the statue).</summary>
        LeadersHall = 1,
        /// <summary>A home: a bed, a hearth, a table and a family.</summary>
        Home = 2,
    }

    /// <summary>The furniture an interior is made of (InteriorArt draws each in its race's colours).</summary>
    public enum InteriorPiece
    {
        Counter = 0,
        Shelf = 1,
        Throne = 2,
        Banner = 3,
        Pillar = 4,
        Bed = 5,
        Table = 6,
        Stool = 7,
        Hearth = 8,
        Rug = 9,
        Carpet = 10,
        /// <summary>The race's own keepsake in a corner (a barrel, a perch, a bubbling pot, a telescope, a shape stack, a fire pit).</summary>
        Keepsake = 11,
    }

    /// <summary>One piece of furniture: what it is, where its art is drawn and its solid base (empty for floor decor).</summary>
    public readonly struct InteriorFurniture
    {
        public readonly InteriorPiece Piece;

        /// <summary>Where the art is drawn (local to the room's centre).</summary>
        public readonly Rect Art;

        /// <summary>The solid part (local), or an empty rect for decor that people walk over (rugs, carpets, banners, stools).</summary>
        public readonly Rect Solid;

        public bool IsSolid => Solid.width > 0f && Solid.height > 0f;

        public InteriorFurniture(InteriorPiece piece, Rect art, Rect solid)
        {
            Piece = piece;
            Art = art;
            Solid = solid;
        }

        public override string ToString() => $"{Piece} at {Art}";
    }

    /// <summary>One person's place in a room: their feet (local) and the box their feet wander in.</summary>
    public readonly struct InteriorStand
    {
        public readonly Vector2 Feet;
        public readonly Rect Bounds;

        /// <summary>True for the shop's keeper (behind the counter).</summary>
        public readonly bool Keeper;

        public InteriorStand(Vector2 feet, Rect bounds, bool keeper = false)
        {
            Feet = feet;
            Bounds = bounds;
            Keeper = keeper;
        }
    }

    /// <summary>
    /// The room behind a town's marked door (4.6), as pure geometry local to the room's centre, fixed per race: an
    /// 18 x 10 room (walls round it, a taller north wall) with one exit, a gap in the middle of the south wall opening
    /// into a short vestibule. The player arrives just inside the exit facing north and leaves by walking south through
    /// it (ExitLine). Each race's room is one kind (KindFor): a shop, the leader's hall or a home, furnished in the race's
    /// style (InteriorArt) and lived in by PeopleCount people of the race. The exit lane is always clear, and every
    /// person's whole wander box stays clear of the furniture, the walls and the exit lane (Problems).
    /// Rooms are not on the cube net: TownInteriors builds each on its own screen below the faces (RoomScreen).
    /// </summary>
    public sealed class TownInterior
    {
        /// <summary>Half the walkable room (inside the walls).</summary>
        public static readonly Vector2 RoomHalf = new Vector2(9f, 5f);

        /// <summary>The walls' thickness (solid), and how tall the north wall is drawn (the back wall seen from the front).</summary>
        public const float WallThickness = 0.75f, NorthWallHeight = 1.5f;

        /// <summary>Half the exit's width: wide enough for the player (radius TimeField.PlayerRadius) with room to spare.</summary>
        public const float ExitHalfWidth = 1.1f;

        /// <summary>How far the vestibule behind the exit runs below the south wall (its back wall closes it).</summary>
        public const float VestibuleDepth = 1.25f;

        /// <summary>The vestibule's back wall is thinner, so the room fits on its screen.</summary>
        public const float BackWallThickness = 0.5f;

        /// <summary>People in every room (the keeper among them in a shop).</summary>
        public const int PeopleCount = 3;

        /// <summary>NPC index base for interior people (town residents use 7000).</summary>
        public const int IndexBase = 8000;

        /// <summary>The walkable room (local).</summary>
        public static Rect Inner => Rect.MinMaxRect(-RoomHalf.x, -RoomHalf.y, RoomHalf.x, RoomHalf.y);

        /// <summary>Where the player arrives (local): just inside the exit, facing north into the room.</summary>
        public static Vector2 EntryPoint => new Vector2(0f, -RoomHalf.y + 1.3f);

        /// <summary>The player's centre below this line (local y, the south wall's outer face) has walked out through the exit.</summary>
        public static float ExitLine => -RoomHalf.y - WallThickness;

        /// <summary>The lane from the exit to just past the arrival point, kept free of furniture and people.</summary>
        public static Rect ExitLane => Rect.MinMaxRect(-ExitHalfWidth - TimeField.PlayerRadius, -RoomHalf.y, ExitHalfWidth + TimeField.PlayerRadius,
            EntryPoint.y + TimeField.PlayerRadius + 0.6f);

        /// <summary>
        /// The room's solid walls (local): north, east, west, the south wall's two halves either side of the exit, and the
        /// vestibule's sides and back.
        /// </summary>
        public static List<Rect> Walls
        {
            get
            {
                float x = RoomHalf.x, y = RoomHalf.y, w = WallThickness;
                float vestibuleBottom = -y - w - VestibuleDepth;
                return new List<Rect>
                {
                    Rect.MinMaxRect(-x - w, y, x + w, y + w),
                    Rect.MinMaxRect(x, -y - w, x + w, y + w),
                    Rect.MinMaxRect(-x - w, -y - w, -x, y + w),
                    Rect.MinMaxRect(-x, -y - w, -ExitHalfWidth, -y),
                    Rect.MinMaxRect(ExitHalfWidth, -y - w, x, -y),
                    Rect.MinMaxRect(-ExitHalfWidth - w, vestibuleBottom, -ExitHalfWidth, -y - w),
                    Rect.MinMaxRect(ExitHalfWidth, vestibuleBottom, ExitHalfWidth + w, -y - w),
                    Rect.MinMaxRect(-ExitHalfWidth - w, vestibuleBottom - BackWallThickness, ExitHalfWidth + w, vestibuleBottom),
                };
            }
        }

        /// <summary>The vestibule's floor (local): the exit's gap and the space behind it.</summary>
        public static Rect Vestibule => Rect.MinMaxRect(-ExitHalfWidth, -RoomHalf.y - WallThickness - VestibuleDepth, ExitHalfWidth, -RoomHalf.y);

        private static readonly Dictionary<Theme, TownInterior> Cache = new Dictionary<Theme, TownInterior>();

        private readonly List<InteriorFurniture> furniture;
        private readonly List<InteriorStand> stands;

        public Theme Theme { get; }
        public InteriorKind Kind { get; }
        public IReadOnlyList<InteriorFurniture> Furniture => furniture;
        public IReadOnlyList<InteriorStand> Stands => stands;

        /// <summary>What the map and the overlay call the room ("shop", "leader's hall", "home").</summary>
        public string Name => RoomName(Kind);

        private TownInterior(Theme theme, InteriorKind kind, List<InteriorFurniture> furniture, List<InteriorStand> stands)
        {
            Theme = theme;
            Kind = kind;
            this.furniture = furniture;
            this.stands = stands;
        }

        /// <summary>
        /// Which room each race's marked building holds (fixed; each kind twice): the Town's cottage and the mushroom hut are
        /// shops, the treehouse and the observatory are homes, the shape people's tower and the dinosaurs' lodge are halls.
        /// </summary>
        public static InteriorKind KindFor(Theme theme)
        {
            switch (theme)
            {
                case Theme.Town:
                case Theme.Chemistry: return InteriorKind.Shop;
                case Theme.Math:
                case Theme.EarthAtmosphere: return InteriorKind.LeadersHall;
                default: return InteriorKind.Home;
            }
        }

        public static string RoomName(InteriorKind kind)
        {
            switch (kind)
            {
                case InteriorKind.Shop: return "shop";
                case InteriorKind.LeadersHall: return "leader's hall";
                default: return "home";
            }
        }

        /// <summary>The room of a face theme's town (made once).</summary>
        public static TownInterior For(Theme theme)
        {
            if (Cache.TryGetValue(theme, out TownInterior cached)) return cached;
            InteriorKind kind = KindFor(theme);
            var pieces = new List<InteriorFurniture>();
            var people = new List<InteriorStand>();
            switch (kind)
            {
                case InteriorKind.Shop: Shop(pieces, people); break;
                case InteriorKind.LeadersHall: Hall(pieces, people); break;
                default: Home(pieces, people); break;
            }
            var room = new TownInterior(theme, kind, pieces, people);
            Cache[theme] = room;
            return room;
        }

        private static Rect R(float x, float y, float w, float h) => new Rect(x, y, w, h);

        private static void Solid(List<InteriorFurniture> list, InteriorPiece piece, Rect art) => list.Add(new InteriorFurniture(piece, art, art));

        private static void Decor(List<InteriorFurniture> list, InteriorPiece piece, Rect art) => list.Add(new InteriorFurniture(piece, art, Rect.zero));

        private static InteriorStand Stand(float x, float y, float halfX, float halfY, bool keeper = false) =>
            new InteriorStand(new Vector2(x, y), Rect.MinMaxRect(x - halfX, y - halfY, x + halfX, y + halfY), keeper);

        private static void Shop(List<InteriorFurniture> f, List<InteriorStand> s)
        {
            // Shelves of wares on the back wall, the counter across the room, a rug, the race's keepsake in a corner.
            Solid(f, InteriorPiece.Shelf, R(-8.6f, 3.2f, 4.6f, 1.8f));
            Solid(f, InteriorPiece.Shelf, R(4f, 3.2f, 4.6f, 1.8f));
            Solid(f, InteriorPiece.Counter, R(-3.2f, 0.6f, 6.4f, 1.4f));
            Decor(f, InteriorPiece.Rug, R(-7f, -3.4f, 4.2f, 2.4f));
            Solid(f, InteriorPiece.Keepsake, R(6.4f, -4.6f, 2.2f, 2.2f));
            // The keeper behind the counter, two customers in front of it.
            s.Add(Stand(0f, 2.15f, 2f, 0.1f, true));
            s.Add(Stand(-5f, -2.1f, 1.4f, 0.4f));
            s.Add(Stand(4.4f, -2.1f, 1f, 0.4f));
        }

        private static void Hall(List<InteriorFurniture> f, List<InteriorStand> s)
        {
            // A carpet from the exit to the throne on its dais, banners on the back wall, two pairs of pillars, the keepsake.
            Decor(f, InteriorPiece.Carpet, R(-1.2f, -5f, 2.4f, 7.8f));
            Solid(f, InteriorPiece.Throne, R(-1.8f, 2.4f, 3.6f, 2.6f));
            Decor(f, InteriorPiece.Banner, R(-5.6f, 4.4f, 1.4f, 2.1f));
            Decor(f, InteriorPiece.Banner, R(4.2f, 4.4f, 1.4f, 2.1f));
            foreach (float x in new[] { -6.8f, 6.8f })
                foreach (float y in new[] { 1.2f, -3.2f })
                    f.Add(new InteriorFurniture(InteriorPiece.Pillar, R(x - 0.6f, y, 1.2f, 2.8f), R(x - 0.5f, y, 1f, 0.8f)));
            Solid(f, InteriorPiece.Keepsake, R(-8.6f, 3f, 2f, 2f));
            // Courtiers: either side of the carpet, and one by the dais.
            s.Add(Stand(-4f, -1.9f, 1f, 0.5f));
            s.Add(Stand(4f, -1.9f, 1f, 0.5f));
            s.Add(Stand(3.6f, 1.2f, 0.6f, 0.2f));
        }

        private static void Home(List<InteriorFurniture> f, List<InteriorStand> s)
        {
            // A bed and a shelf on the back wall, the hearth, a table with two stools, a rug, the keepsake.
            Solid(f, InteriorPiece.Bed, R(-8.6f, 2f, 3f, 3f));
            Solid(f, InteriorPiece.Shelf, R(-4.8f, 3.4f, 3.4f, 1.6f));
            Solid(f, InteriorPiece.Hearth, R(4.6f, 3f, 3.6f, 2f));
            Solid(f, InteriorPiece.Table, R(2f, -1.2f, 3.2f, 1.6f));
            Decor(f, InteriorPiece.Stool, R(0.9f, -1.2f, 1f, 1f));
            Decor(f, InteriorPiece.Stool, R(5.3f, -1.2f, 1f, 1f));
            Decor(f, InteriorPiece.Rug, R(-6.4f, -3.4f, 4.2f, 2.6f));
            Solid(f, InteriorPiece.Keepsake, R(6.6f, -4.6f, 2f, 2f));
            s.Add(Stand(-4.5f, -2f, 1.5f, 0.5f));
            s.Add(Stand(-1.5f, 0.95f, 1f, 0.25f));
            s.Add(Stand(7f, 0.45f, 1f, 0.15f));
        }

        /// <summary>Every solid rect of the room's furniture (local).</summary>
        public List<Rect> Solids
        {
            get
            {
                var rects = new List<Rect>();
                foreach (InteriorFurniture piece in furniture)
                    if (piece.IsSolid) rects.Add(piece.Solid);
                return rects;
            }
        }

        /// <summary>The keeper's stand, or -1 (only a shop has one).</summary>
        public int KeeperIndex
        {
            get
            {
                for (int i = 0; i < stands.Count; i++)
                    if (stands[i].Keeper) return i;
                return -1;
            }
        }

        /// <summary>
        /// The space a person takes up anywhere in their wander box (local): the box widened by half the body's width and
        /// raised by its height, as NpcFactory builds the collider over the feet.
        /// </summary>
        public static Rect Reach(InteriorStand stand) => Rect.MinMaxRect(stand.Bounds.xMin - NpcFactory.BodyWidth / 2f, stand.Bounds.yMin,
            stand.Bounds.xMax + NpcFactory.BodyWidth / 2f, stand.Bounds.yMax + NpcFactory.MaxBodyHeight);

        /// <summary>
        /// Problems with the room (empty when all is well): PeopleCount people, a keeper only in a shop and then behind
        /// the counter; every piece inside the room (banners may hang on the back wall); no solid on the exit lane or the
        /// arrival point; every person's whole reach inside the room, clear of every solid, the exit lane and every other
        /// person's reach.
        /// </summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            if (stands.Count != PeopleCount) problems.Add($"{Theme}: {stands.Count} people (want {PeopleCount})");
            int keeper = KeeperIndex;
            if ((Kind == InteriorKind.Shop) != (keeper >= 0)) problems.Add($"{Theme}: a {Kind} {(keeper >= 0 ? "has" : "lacks")} a keeper");

            Rect inner = Inner;
            var withWall = Rect.MinMaxRect(inner.xMin, inner.yMin, inner.xMax, inner.yMax + NorthWallHeight);
            List<Rect> solids = Solids;
            foreach (InteriorFurniture piece in furniture)
            {
                Rect within = piece.Piece == InteriorPiece.Banner ? withWall : inner;
                if (!Contains(within, piece.Art)) problems.Add($"{Theme}: {piece} reaches outside the room");
                if (piece.IsSolid && !Contains(piece.Art, piece.Solid)) problems.Add($"{Theme}: {piece}'s art does not cover its solid");
                if (piece.IsSolid && piece.Solid.Overlaps(ExitLane)) problems.Add($"{Theme}: {piece} blocks the exit lane");
            }
            var arrival = new Rect(EntryPoint - Vector2.one * TimeField.PlayerRadius, Vector2.one * 2f * TimeField.PlayerRadius);
            foreach (Rect solid in solids)
                if (solid.Overlaps(arrival)) problems.Add($"{Theme}: {solid} covers the arrival point");
            for (int i = 0; i < solids.Count; i++)
                for (int j = i + 1; j < solids.Count; j++)
                    if (solids[i].Overlaps(solids[j])) problems.Add($"{Theme}: {solids[i]} and {solids[j]} overlap");

            for (int i = 0; i < stands.Count; i++)
            {
                Rect reach = Reach(stands[i]);
                if (!stands[i].Bounds.Contains(stands[i].Feet)) problems.Add($"{Theme}: person {i} stands outside their own box");
                if (!Contains(inner, reach)) problems.Add($"{Theme}: person {i} can wander out of the room");
                if (reach.Overlaps(ExitLane)) problems.Add($"{Theme}: person {i} can wander onto the exit lane");
                foreach (Rect solid in solids)
                    if (reach.Overlaps(solid)) problems.Add($"{Theme}: person {i} can wander into {solid}");
                for (int j = i + 1; j < stands.Count; j++)
                    if (reach.Overlaps(Reach(stands[j]))) problems.Add($"{Theme}: people {i} and {j} can bump");
            }
            if (keeper >= 0)
            {
                Rect counter = Rect.zero;
                foreach (InteriorFurniture piece in furniture)
                    if (piece.Piece == InteriorPiece.Counter) counter = piece.Solid;
                Rect box = stands[keeper].Bounds;
                if (counter.width <= 0f || box.yMin < counter.yMax || box.xMin < counter.xMin || box.xMax > counter.xMax)
                    problems.Add($"{Theme}: the keeper is not behind the counter");
            }
            return problems;
        }

        /// <summary>Compact text of the room, for comparisons and logs.</summary>
        public string Signature()
        {
            var parts = new List<string>();
            foreach (InteriorFurniture piece in furniture) parts.Add($"{piece.Piece}@{piece.Art.x:0.0},{piece.Art.y:0.0}");
            return $"{Theme} {Kind}: {string.Join(" ", parts)}";
        }

        private static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 1e-4f && inner.xMax <= outer.xMax + 1e-4f && inner.yMin >= outer.yMin - 1e-4f && inner.yMax <= outer.yMax + 1e-4f;
    }
}
