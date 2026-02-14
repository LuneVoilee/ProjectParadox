using System;
using UnityEngine;

namespace Map.Common
{
    public enum HexDirection
    {
        NE, E, SE, SW, W, NW
    }

    [Serializable]
    public struct HexCoordinates : IEquatable<HexCoordinates>
    {
        public int X;
        public int Y;
        public int Z;

        public HexCoordinates(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static HexCoordinates FromOffset(int col, int row)
        {
            int x = col - ((row - (row & 1)) / 2);
            int z = row;
            int y = -x - z;
            return new HexCoordinates(x, y, z);
        }

        public static HexCoordinates FromOffset(Vector2Int offset)
        {
            return FromOffset(offset.x, offset.y);
        }

        public Vector2Int ToOffset()
        {
            int col = X + ((Z - (Z & 1)) / 2);
            int row = Z;
            return new Vector2Int(col, row);
        }

        public HexCoordinates GetNeighbor(HexDirection direction)
        {
            return direction switch
            {
                HexDirection.NE => new HexCoordinates(X, Y - 1, Z + 1),
                HexDirection.E  => new HexCoordinates(X + 1, Y - 1, Z),
                HexDirection.SE => new HexCoordinates(X + 1, Y, Z - 1),
                HexDirection.SW => new HexCoordinates(X, Y + 1, Z - 1),
                HexDirection.W  => new HexCoordinates(X - 1, Y + 1, Z),
                HexDirection.NW => new HexCoordinates(X - 1, Y, Z + 1),
                _ => this
            };
        }

        public override string ToString()
        {
            return $"({X},{Y},{Z})";
        }

        public bool Equals(HexCoordinates other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoordinates other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }
}
