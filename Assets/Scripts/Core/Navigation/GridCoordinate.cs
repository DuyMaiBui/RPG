using System;

namespace RPG.Core.Navigation
{
    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public GridCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
        public bool Equals(GridCoordinate other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }
}
