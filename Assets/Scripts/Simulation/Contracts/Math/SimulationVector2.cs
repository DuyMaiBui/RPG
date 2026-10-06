using System;

namespace RPG.Simulation.Contracts
{
    public readonly struct SimulationVector2 : IEquatable<SimulationVector2>
    {
        public SimulationVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static SimulationVector2 Zero => new(0f, 0f);
        public float X { get; }
        public float Y { get; }
        public float LengthSquared => X * X + Y * Y;

        public SimulationVector2 Normalized()
        {
            var length = SimulationMath.Sqrt(LengthSquared);
            return length <= 0.0001f ? Zero : this / length;
        }

        public bool Equals(SimulationVector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is SimulationVector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";

        public static SimulationVector2 operator +(SimulationVector2 left, SimulationVector2 right) =>
            new(left.X + right.X, left.Y + right.Y);

        public static SimulationVector2 operator -(SimulationVector2 left, SimulationVector2 right) =>
            new(left.X - right.X, left.Y - right.Y);

        public static SimulationVector2 operator *(SimulationVector2 value, float scalar) =>
            new(value.X * scalar, value.Y * scalar);

        public static SimulationVector2 operator /(SimulationVector2 value, float scalar) =>
            new(value.X / scalar, value.Y / scalar);
    }
}
