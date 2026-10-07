using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>Where one face sits in a cube net: its slot (in faces, y up) and how far it is turned clockwise (quarter turns).</summary>
    public readonly struct NetPlacement
    {
        public readonly FaceId Face;

        /// <summary>The face's slot in the net, in whole faces from the net's lower-left (x right, y up).</summary>
        public readonly Vector2Int Slot;

        /// <summary>Clockwise quarter turns (0..3) from the face's own frame to the net's.</summary>
        public readonly int Turns;

        public NetPlacement(FaceId face, Vector2Int slot, int turns)
        {
            Face = face;
            Slot = slot;
            Turns = ((turns % 4) + 4) % 4;
        }

        public override string ToString() => $"{Face} at {Slot} turned {Turns * 90}";
    }

    /// <summary>
    /// Pure cross-shaped net of the cube for the pause map (CubeMap). The centre face (Town) sits in the middle of the
    /// cross, its four neighbours around it (each on the side of the centre it touches, in the centre's net frame) and the
    /// opposite face at the end of the east arm:
    /// <code>
    ///      . N . .
    ///      W C E O
    ///      . S . .
    /// </code>
    /// The placement is derived from CubeModel's face bases (through CubeModel.Step), never typed by hand: crossing a net
    /// edge keeps the net direction of travel, so each neighbour is turned so the facing it is entered with points the same
    /// way in the net. Every face is a right-handed frame on the outside of the cube, so unfolding only turns faces (never
    /// mirrors them) and the cells on each side of every drawn edge are real cube neighbours. Any centre face and turn work.
    /// Net cells count in screens from the net's lower-left (x right, y up), 4N wide and 3N tall.
    /// </summary>
    public sealed class CubeNet
    {
        /// <summary>The net is this many faces wide and tall.</summary>
        public const int Columns = 4, Rows = 3;

        /// <summary>The centre face's slot (the middle of the cross).</summary>
        public static readonly Vector2Int CentreSlot = new Vector2Int(1, 1);

        private readonly NetPlacement[] placements = new NetPlacement[CubeSettings.FaceCount];
        private readonly Dictionary<Vector2Int, FaceId> faceAtSlot = new Dictionary<Vector2Int, FaceId>();

        public int FaceSize { get; }
        public FaceId Centre { get; }

        /// <summary>The face across the cube from the centre: at the end of the east arm.</summary>
        public FaceId Opposite { get; }

        /// <summary>Net width and height in screens (4N x 3N).</summary>
        public Vector2Int SizeInCells => new Vector2Int(Columns * FaceSize, Rows * FaceSize);

        public IReadOnlyList<NetPlacement> Placements => placements;

        private CubeNet(int faceSize, FaceId centre, int centreTurns)
        {
            if (faceSize < 1) throw new ArgumentOutOfRangeException(nameof(faceSize));
            FaceSize = faceSize;
            Centre = centre;

            Place(new NetPlacement(centre, CentreSlot, centreTurns));
            // The centre's four neighbours, each across the centre's edge that points that way in the net.
            for (int d = 0; d < 4; d++) PlaceAcross(centre, (Facing)d);
            // The opposite face at the end of the east arm: across the east neighbour's net-east edge.
            FaceId east = faceAtSlot[CentreSlot + Vector2Int.right];
            Opposite = PlaceAcross(east, Facing.East);

            if (faceAtSlot.Count != CubeSettings.FaceCount)
                throw new InvalidOperationException("The cross net did not place every face once");
        }

        /// <summary>The cross net with the given face in the middle (Town in play), optionally turned clockwise.</summary>
        public static CubeNet Cross(FaceId centre, int faceSize = CubeSettings.DefaultFaceSize, int centreTurns = 0) =>
            new CubeNet(faceSize, centre, centreTurns);

        /// <summary>The net for a model: its start face (Town) in the middle, unturned.</summary>
        public static CubeNet For(CubeModel model) => Cross(CubeModel.StartFace, model.FaceSize);

        public NetPlacement PlacementOf(FaceId face) => placements[(int)face];

        /// <summary>The face in a net slot, if any.</summary>
        public bool TryGetFace(Vector2Int slot, out FaceId face) => faceAtSlot.TryGetValue(slot, out face);

        /// <summary>The net direction a facing on a face points (the face turned into the net).</summary>
        public Facing ToNet(FaceId face, Facing facing) => Turn(facing, PlacementOf(face).Turns);

        /// <summary>The facing on a face that points in a net direction.</summary>
        public Facing FromNet(FaceId face, Facing netDirection) => Turn(netDirection, -PlacementOf(face).Turns);

        /// <summary>The net cell (in screens, y up) a screen is drawn on.</summary>
        public Vector2Int NetCell(ScreenAddress screen)
        {
            NetPlacement p = PlacementOf(screen.Face);
            return p.Slot * FaceSize + RotateCell(screen.Cell, FaceSize, p.Turns);
        }

        /// <summary>The screen drawn on a net cell; false for the net's empty corners and cells outside it.</summary>
        public bool TryGetScreen(Vector2Int netCell, out ScreenAddress screen)
        {
            screen = default;
            if (netCell.x < 0 || netCell.y < 0) return false;
            var slot = new Vector2Int(netCell.x / FaceSize, netCell.y / FaceSize);
            if (!faceAtSlot.TryGetValue(slot, out FaceId face)) return false;
            Vector2Int inSlot = netCell - slot * FaceSize;
            NetPlacement p = PlacementOf(face);
            screen = new ScreenAddress(face, RotateCell(inSlot, FaceSize, -p.Turns));
            return true;
        }

        /// <summary>
        /// A point inside a screen (local: 0..1 across the screen in the face's frame, x right, y up) as a point inside its
        /// net cell (0..1, net frame, y up).
        /// </summary>
        public Vector2 ToNetLocal(FaceId face, Vector2 local) => RotateUnit(local, PlacementOf(face).Turns);

        /// <summary>A direction in a face's frame (e.g. the player's facing) turned into the net's frame.</summary>
        public Vector2 ToNetDirection(FaceId face, Vector2 direction)
        {
            Vector2 v = direction;
            for (int i = 0; i < PlacementOf(face).Turns; i++) v = new Vector2(v.y, -v.x);
            return v;
        }

        /// <summary>A facing turned clockwise by turns quarter turns (negative: anticlockwise).</summary>
        public static Facing Turn(Facing facing, int turns) => (Facing)((((int)facing + turns) % 4 + 4) % 4);

        /// <summary>A cell of an n x n face turned clockwise by turns quarter turns (negative: anticlockwise).</summary>
        public static Vector2Int RotateCell(Vector2Int cell, int n, int turns)
        {
            int k = ((turns % 4) + 4) % 4;
            for (int i = 0; i < k; i++) cell = new Vector2Int(cell.y, n - 1 - cell.x);
            return cell;
        }

        /// <summary>A point of the unit square turned clockwise about its centre by turns quarter turns.</summary>
        public static Vector2 RotateUnit(Vector2 p, int turns)
        {
            int k = ((turns % 4) + 4) % 4;
            for (int i = 0; i < k; i++) p = new Vector2(p.y, 1f - p.x);
            return p;
        }

        private void Place(NetPlacement placement)
        {
            if (faceAtSlot.ContainsKey(placement.Slot))
                throw new InvalidOperationException($"Net slot {placement.Slot} is taken");
            if (faceAtSlot.ContainsValue(placement.Face))
                throw new InvalidOperationException($"{placement.Face} is already in the net");
            placements[(int)placement.Face] = placement;
            faceAtSlot[placement.Slot] = placement.Face;
        }

        /// <summary>
        /// Places the face across a placed face's edge that points netDirection in the net, one slot that way, turned so
        /// that the facing it is entered with (CubeModel.Step) also points netDirection. Returns the placed face.
        /// </summary>
        private FaceId PlaceAcross(FaceId from, Facing netDirection)
        {
            Facing edge = FromNet(from, netDirection);
            ScreenAddress exit = EdgeCell(from, edge);
            ScreenAddress entered = CubeModel.Step(FaceSize, exit, edge, out Facing entryFacing);
            int turns = (int)netDirection - (int)entryFacing;
            Place(new NetPlacement(entered.Face, PlacementOf(from).Slot + netDirection.ToVector(), turns));
            return entered.Face;
        }

        /// <summary>A screen on a face's edge (stepping across the edge from it leaves the face).</summary>
        private ScreenAddress EdgeCell(FaceId face, Facing edge)
        {
            int last = FaceSize - 1;
            switch (edge)
            {
                case Facing.North: return new ScreenAddress(face, 0, last);
                case Facing.East: return new ScreenAddress(face, last, 0);
                default: return new ScreenAddress(face, 0, 0);
            }
        }
    }
}
