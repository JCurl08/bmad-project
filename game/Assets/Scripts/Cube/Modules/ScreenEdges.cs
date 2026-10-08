using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The walls round every screen (1.25), as pure geometry local to the screen centre. Each of a screen's four edges is
    /// an uncrossable wall, EdgeWallThickness deep inside the screen, except one doorway centred on it: the gap where the
    /// exit lane meets the edge (DoorwayHalfWidth either side of the centre line, the lane's half-width, so 3.75 tiles wide).
    /// A doorway that leads onto another face is a gate arch (DoorArch art, ArchWidth across and ArchDepth deep: the edge band's
    /// depth, so the arch never reaches past the band into the screen's content, and its open middle is exactly the doorway); a
    /// doorway within a face is a plain opening. An edge that leads nowhere (a
    /// sealed face) has no doorway: CubeWorld walls it off whole. Everything here stays inside the edge band
    /// (ScreenModule.EdgeClearance) and off the lanes, so no content placed by the KeepsExitsOpen rules meets a wall.
    /// </summary>
    public static class ScreenEdges
    {
        /// <summary>Depth of the wall along every screen edge: half a tile (4.1; it was one tile on the 16x10 screens).</summary>
        public const float EdgeWallThickness = 0.5f;

        /// <summary>Half the width of every doorway: the exit lane's half-width (a 3.75-tile gap, so at least the 2.5 it was).</summary>
        public const float DoorwayHalfWidth = ScreenModule.ExitLaneHalfWidth;

        /// <summary>
        /// A face-edge gate arch: its width across the doorway (posts included: twice the doorway, as the art's open middle is
        /// half of it) and its depth into the screen (the edge band, so it stays clear of every module's content). The posts stand
        /// on the half-tile wall and the edge band's clear strip; the arch has no collider.
        /// </summary>
        public const float ArchWidth = 4f * DoorwayHalfWidth, ArchDepth = ScreenModule.EdgeClearance;

        /// <summary>Half the screen (12 x 7.5).</summary>
        public static Vector2 Half => Game.Tracer.ScreenMath.DefaultScreenSize / 2f;

        /// <summary>True when a side of a cell leaves its face (a face edge) rather than leading to the next screen of the face.</summary>
        public static bool IsFaceEdge(int faceSize, Vector2Int cell, Facing side)
        {
            Vector2Int next = cell + side.ToVector();
            return next.x < 0 || next.y < 0 || next.x >= faceSize || next.y >= faceSize;
        }

        /// <summary>
        /// The two wall pieces of an edge that has a doorway, either side of it (local rects). The north and south walls run
        /// the whole width; the east and west ones fit between them.
        /// </summary>
        public static Rect[] WallRects(Facing side)
        {
            Vector2 h = Half;
            float t = EdgeWallThickness, d = DoorwayHalfWidth;
            switch (side)
            {
                case Facing.North:
                    return new[] { Rect.MinMaxRect(-h.x, h.y - t, -d, h.y), Rect.MinMaxRect(d, h.y - t, h.x, h.y) };
                case Facing.South:
                    return new[] { Rect.MinMaxRect(-h.x, -h.y, -d, -h.y + t), Rect.MinMaxRect(d, -h.y, h.x, -h.y + t) };
                case Facing.East:
                    return new[] { Rect.MinMaxRect(h.x - t, -h.y + t, h.x, -d), Rect.MinMaxRect(h.x - t, d, h.x, h.y - t) };
                default:
                    return new[] { Rect.MinMaxRect(-h.x, -h.y + t, -h.x + t, -d), Rect.MinMaxRect(-h.x, d, -h.x + t, h.y - t) };
            }
        }

        /// <summary>The whole wall of an edge with no doorway (local rect).</summary>
        public static Rect SolidWallRect(Facing side)
        {
            Vector2 h = Half;
            float t = EdgeWallThickness;
            switch (side)
            {
                case Facing.North: return Rect.MinMaxRect(-h.x, h.y - t, h.x, h.y);
                case Facing.South: return Rect.MinMaxRect(-h.x, -h.y, h.x, -h.y + t);
                case Facing.East: return Rect.MinMaxRect(h.x - t, -h.y + t, h.x, h.y - t);
                default: return Rect.MinMaxRect(-h.x, -h.y + t, -h.x + t, h.y - t);
            }
        }

        /// <summary>The doorway of an edge: the gap through its wall (local rect, EdgeWallThickness deep).</summary>
        public static Rect DoorwayRect(Facing side)
        {
            Vector2 h = Half;
            float t = EdgeWallThickness, d = DoorwayHalfWidth;
            switch (side)
            {
                case Facing.North: return Rect.MinMaxRect(-d, h.y - t, d, h.y);
                case Facing.South: return Rect.MinMaxRect(-d, -h.y, d, -h.y + t);
                case Facing.East: return Rect.MinMaxRect(h.x - t, -d, h.x, d);
                default: return Rect.MinMaxRect(-h.x, -d, -h.x + t, d);
            }
        }

        /// <summary>The centre of a doorway on the screen's edge line (local).</summary>
        public static Vector2 DoorwayCentre(Facing side) => Vector2.Scale(side.ToVector(), Half);

        /// <summary>The middle of a gate arch (local): centred on the doorway, reaching ArchDepth in from the edge.</summary>
        public static Vector2 ArchCentre(Facing side) => DoorwayCentre(side) - (Vector2)side.ToVector() * (ArchDepth / 2f);

        /// <summary>The arch's turn (degrees): its top (the lintel) on the edge, its posts pointing into the screen.</summary>
        public static float ArchAngle(Facing side)
        {
            switch (side)
            {
                case Facing.North: return 0f;
                case Facing.South: return 180f;
                case Facing.East: return -90f;
                default: return 90f;
            }
        }

        /// <summary>
        /// The whole footprint of a gate arch's art (local rect): ArchWidth across, centred on the doorway, inside the wall
        /// band (its posts stand on the wall either side of the doorway, so they need no solid of their own).
        /// </summary>
        public static Rect ArchRect(Facing side)
        {
            Vector2 c = ArchCentre(side);
            Vector2 size = side.IsHorizontal() ? new Vector2(ArchDepth, ArchWidth) : new Vector2(ArchWidth, ArchDepth);
            return new Rect(c - size / 2f, size);
        }

        /// <summary>Every solid rect on a screen's edges (local): walls round doorways, whole walls where there is none.</summary>
        public static List<Rect> SolidRects(int faceSize, Vector2Int cell, System.Func<Facing, bool> hasDoorway)
        {
            var rects = new List<Rect>();
            for (int f = 0; f < 4; f++)
            {
                var side = (Facing)f;
                if (!hasDoorway(side))
                {
                    rects.Add(SolidWallRect(side));
                    continue;
                }
                rects.AddRange(WallRects(side));
            }
            return rects;
        }
    }
}
