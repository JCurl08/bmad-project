using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Pure cube world model, fully determined by (seed, faceSize). Town sits on the start face (Front);
    /// the five science themes are shuffled onto the other faces; unbuilt themes are sealed.
    /// Edge adjacency is derived from each face's 3D basis, not from a hand-typed table.
    /// </summary>
    public sealed class CubeModel
    {
        public const FaceId StartFace = FaceId.Front;

        /// <summary>Science themes in a fixed order; the seed shuffles this list onto the non-start faces.</summary>
        public static readonly IReadOnlyList<Theme> ScienceThemes = new[]
        {
            Theme.Biology, Theme.Chemistry, Theme.Physics, Theme.Math, Theme.EarthAtmosphere,
        };

        /// <summary>Faces that receive a science theme, in assignment order.</summary>
        private static readonly FaceId[] ScienceFaces =
        {
            FaceId.Right, FaceId.Back, FaceId.Left, FaceId.Top, FaceId.Bottom,
        };

        // Coordinate frame: x right, y up, z toward the viewer. Each basis satisfies Right x Up = Normal.
        private static readonly FaceBasis[] Bases =
        {
            new FaceBasis(new Int3(0, 0, 1), new Int3(1, 0, 0), new Int3(0, 1, 0)),    // Front
            new FaceBasis(new Int3(1, 0, 0), new Int3(0, 0, -1), new Int3(0, 1, 0)),   // Right
            new FaceBasis(new Int3(0, 0, -1), new Int3(-1, 0, 0), new Int3(0, 1, 0)),  // Back
            new FaceBasis(new Int3(-1, 0, 0), new Int3(0, 0, 1), new Int3(0, 1, 0)),   // Left
            new FaceBasis(new Int3(0, 1, 0), new Int3(1, 0, 0), new Int3(0, 0, -1)),   // Top
            new FaceBasis(new Int3(0, -1, 0), new Int3(1, 0, 0), new Int3(0, 0, 1)),   // Bottom
        };

        private readonly Theme[] themeByFace = new Theme[CubeSettings.FaceCount];

        public int Seed { get; }
        public int FaceSize { get; }

        public CubeModel(int seed, int faceSize = CubeSettings.DefaultFaceSize)
        {
            if (faceSize < 1) throw new ArgumentOutOfRangeException(nameof(faceSize), "Face size must be at least 1");
            Seed = seed;
            FaceSize = faceSize;

            var rng = new SeededRng(seed);
            var sciences = new List<Theme>(ScienceThemes);
            rng.Shuffle(sciences);

            themeByFace[(int)StartFace] = Theme.Town;
            for (int i = 0; i < ScienceFaces.Length; i++)
                themeByFace[(int)ScienceFaces[i]] = sciences[i];
        }

        /// <summary>The start screen: cell (0,0) on the Town face.</summary>
        public ScreenAddress StartScreen => new ScreenAddress(StartFace, Vector2Int.zero);

        public Theme ThemeOf(FaceId face) => themeByFace[(int)face];

        public FaceId FaceOf(Theme theme)
        {
            for (int i = 0; i < themeByFace.Length; i++)
                if (themeByFace[i] == theme) return (FaceId)i;
            throw new ArgumentOutOfRangeException(nameof(theme));
        }

        /// <summary>Themes built this epic. Math and Earth and Atmosphere are sealed.</summary>
        public static bool IsBuilt(Theme theme) =>
            theme == Theme.Town || theme == Theme.Biology || theme == Theme.Chemistry || theme == Theme.Physics;

        public bool IsSealed(FaceId face) => !IsBuilt(ThemeOf(face));

        public bool IsInside(Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < FaceSize && cell.y < FaceSize;

        /// <summary>Every screen on the cube, faces in enum order, cells row by row.</summary>
        public IEnumerable<ScreenAddress> AllScreens()
        {
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                for (int y = 0; y < FaceSize; y++)
                    for (int x = 0; x < FaceSize; x++)
                        yield return new ScreenAddress((FaceId)f, x, y);
        }

        /// <summary>
        /// Steps one screen. Returns false (and leaves next = from, facing = direction) when the
        /// target screen is on a sealed face.
        /// </summary>
        public bool TryStep(ScreenAddress from, Facing direction, out ScreenAddress next, out Facing facing)
        {
            ScreenAddress target = Step(FaceSize, from, direction, out Facing newFacing);
            if (IsSealed(target.Face))
            {
                next = from;
                facing = direction;
                return false;
            }
            next = target;
            facing = newFacing;
            return true;
        }

        // ---- Pure geometry (no seed involved) ----

        public static FaceBasis Basis(FaceId face) => Bases[(int)face];

        public static FaceId FaceWithNormal(Int3 normal)
        {
            for (int i = 0; i < Bases.Length; i++)
                if (Bases[i].Normal == normal) return (FaceId)i;
            throw new ArgumentException($"No face has normal {normal}", nameof(normal));
        }

        /// <summary>The face across the given edge of a face.</summary>
        public static FaceId NeighborFace(FaceId face, Facing edge) => FaceWithNormal(Basis(face).Direction(edge));

        /// <summary>
        /// Geometric step, ignoring sealing. A cell (i, j) on face F with N cells per side sits at the
        /// 3D point N*n + X*r + Y*u with X = 2i+1-N, Y = 2j+1-N (all integers). Stepping off an edge in
        /// 3D direction d moves the point by d - n onto the face whose normal is d; the new cell and
        /// facing are read back from that face's basis.
        /// </summary>
        public static ScreenAddress Step(int faceSize, ScreenAddress from, Facing direction, out Facing facing)
        {
            int n = faceSize;
            Vector2Int inner = from.Cell + direction.ToVector();
            if (inner.x >= 0 && inner.y >= 0 && inner.x < n && inner.y < n)
            {
                facing = direction;
                return new ScreenAddress(from.Face, inner);
            }

            FaceBasis a = Basis(from.Face);
            Int3 d = a.Direction(direction);
            Int3 p = a.Normal * n + a.Right * (2 * from.Cell.x + 1 - n) + a.Up * (2 * from.Cell.y + 1 - n);
            Int3 q = p + d - a.Normal;

            FaceId toFace = FaceWithNormal(d);
            FaceBasis b = Basis(toFace);
            int x = Int3.Dot(q, b.Right);
            int y = Int3.Dot(q, b.Up);
            facing = FacingAlong(b, -a.Normal);
            return new ScreenAddress(toFace, new Vector2Int((x + n - 1) / 2, (y + n - 1) / 2));
        }

        /// <summary>
        /// Continuous version of an edge crossing, for runtime movement. t is the position along the
        /// exited edge as a fraction of the whole face side (0..1, measured along +y for East/West edges
        /// and +x for North/South edges). Returns the matching fraction on the entry edge of the
        /// neighbour face, measured the same way in that face's frame.
        /// </summary>
        public static float MapAlongEdge(FaceId face, Facing direction, float t, out FaceId toFace, out Facing facing)
        {
            FaceBasis a = Basis(face);
            Int3 d = a.Direction(direction);
            toFace = FaceWithNormal(d);
            FaceBasis b = Basis(toFace);
            facing = FacingAlong(b, -a.Normal);

            Int3 tangentFrom = direction.IsHorizontal() ? a.Up : a.Right;
            Int3 tangentTo = facing.IsHorizontal() ? b.Up : b.Right;
            return Int3.Dot(tangentFrom, tangentTo) > 0 ? t : 1f - t;
        }

        private static Facing FacingAlong(FaceBasis basis, Int3 direction)
        {
            for (int f = 0; f < 4; f++)
                if (basis.Direction((Facing)f) == direction) return (Facing)f;
            throw new InvalidOperationException($"Direction {direction} is not tangent to the face");
        }
    }
}
