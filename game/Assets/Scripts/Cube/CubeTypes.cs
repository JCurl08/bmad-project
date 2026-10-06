using System;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>The six cube faces. Front is the start face and always holds Town.</summary>
    public enum FaceId { Front = 0, Right = 1, Back = 2, Left = 3, Top = 4, Bottom = 5 }

    /// <summary>What a face is themed as. Town is fixed; the sciences are placed by seed.</summary>
    public enum Theme { Town = 0, Biology = 1, Chemistry = 2, Physics = 3, Math = 4, EarthAtmosphere = 5 }

    /// <summary>A step direction or facing in a face's own 2D frame (East = +x, North = +y).</summary>
    public enum Facing { North = 0, East = 1, South = 2, West = 3 }

    /// <summary>Shared cube settings. The face size N is read from here everywhere.</summary>
    public static class CubeSettings
    {
        /// <summary>Screens per face side (N). 2 at Must tier, 3 at Should tier.</summary>
        public const int DefaultFaceSize = 2;

        public const int FaceCount = 6;
    }

    public static class FacingExtensions
    {
        public static Facing Opposite(this Facing facing) => (Facing)(((int)facing + 2) & 3);

        public static Vector2Int ToVector(this Facing facing)
        {
            switch (facing)
            {
                case Facing.North: return Vector2Int.up;
                case Facing.East: return Vector2Int.right;
                case Facing.South: return Vector2Int.down;
                default: return Vector2Int.left;
            }
        }

        /// <summary>True for East/West, whose edges run along the face's y axis.</summary>
        public static bool IsHorizontal(this Facing facing) => facing == Facing.East || facing == Facing.West;
    }

    /// <summary>One screen on the cube: a face plus the (x, y) cell on it, 0..N-1 each.</summary>
    [Serializable]
    public struct ScreenAddress : IEquatable<ScreenAddress>
    {
        public FaceId Face;
        public Vector2Int Cell;

        public ScreenAddress(FaceId face, Vector2Int cell)
        {
            Face = face;
            Cell = cell;
        }

        public ScreenAddress(FaceId face, int x, int y) : this(face, new Vector2Int(x, y)) { }

        public bool Equals(ScreenAddress other) => Face == other.Face && Cell == other.Cell;
        public override bool Equals(object obj) => obj is ScreenAddress other && Equals(other);
        public override int GetHashCode() => ((int)Face * 397) ^ Cell.GetHashCode();
        public static bool operator ==(ScreenAddress a, ScreenAddress b) => a.Equals(b);
        public static bool operator !=(ScreenAddress a, ScreenAddress b) => !a.Equals(b);
        public override string ToString() => $"{Face} ({Cell.x},{Cell.y})";
    }

    /// <summary>Small integer 3D vector for exact cube geometry.</summary>
    public readonly struct Int3 : IEquatable<Int3>
    {
        public readonly int X, Y, Z;

        public Int3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Int3 operator +(Int3 a, Int3 b) => new Int3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Int3 operator -(Int3 a, Int3 b) => new Int3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Int3 operator -(Int3 a) => new Int3(-a.X, -a.Y, -a.Z);
        public static Int3 operator *(Int3 a, int k) => new Int3(a.X * k, a.Y * k, a.Z * k);
        public static Int3 operator *(int k, Int3 a) => a * k;
        public static int Dot(Int3 a, Int3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Int3 Cross(Int3 a, Int3 b) =>
            new Int3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public bool Equals(Int3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Int3 other && Equals(other);
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791);
        public static bool operator ==(Int3 a, Int3 b) => a.Equals(b);
        public static bool operator !=(Int3 a, Int3 b) => !a.Equals(b);
        public override string ToString() => $"({X},{Y},{Z})";
    }

    /// <summary>A face's 3D frame: outward normal, right (+x on the face) and up (+y). Right x Up = Normal.</summary>
    public readonly struct FaceBasis
    {
        public readonly Int3 Normal, Right, Up;

        public FaceBasis(Int3 normal, Int3 right, Int3 up)
        {
            Normal = normal;
            Right = right;
            Up = up;
        }

        /// <summary>3D direction of a 2D step on this face.</summary>
        public Int3 Direction(Facing facing)
        {
            switch (facing)
            {
                case Facing.North: return Up;
                case Facing.East: return Right;
                case Facing.South: return -Up;
                default: return -Right;
            }
        }
    }
}
