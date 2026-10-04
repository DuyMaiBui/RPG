using System;

namespace AuraEngine.Core
{
    public readonly struct AuraVector3 : IEquatable<AuraVector3>
    {
        public AuraVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static AuraVector3 Zero => new AuraVector3(0f, 0f, 0f);
        public static AuraVector3 One => new AuraVector3(1f, 1f, 1f);
        public static AuraVector3 UnitX => new AuraVector3(1f, 0f, 0f);
        public static AuraVector3 UnitY => new AuraVector3(0f, 1f, 0f);
        public static AuraVector3 UnitZ => new AuraVector3(0f, 0f, 1f);

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public float LengthSquared => X * X + Y * Y + Z * Z;
        public float Length => MathF.Sqrt(LengthSquared);

        public AuraVector2 ToVector2() => new AuraVector2(X, Y);

        public AuraVector3 Normalized()
        {
            var length = Length;
            return length <= 1e-6f ? Zero : this / length;
        }

        public static float Dot(AuraVector3 left, AuraVector3 right) =>
            left.X * right.X + left.Y * right.Y + left.Z * right.Z;

        public static AuraVector3 Cross(AuraVector3 left, AuraVector3 right) =>
            new AuraVector3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);

        public static float Distance(AuraVector3 left, AuraVector3 right) => (left - right).Length;

        public static AuraVector3 Lerp(AuraVector3 from, AuraVector3 to, float t) =>
            new AuraVector3(
                from.X + (to.X - from.X) * t,
                from.Y + (to.Y - from.Y) * t,
                from.Z + (to.Z - from.Z) * t);

        bool IEquatable<AuraVector3>.Equals(AuraVector3 other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is AuraVector3 other && ((IEquatable<AuraVector3>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";

        public static AuraVector3 operator +(AuraVector3 left, AuraVector3 right) =>
            new AuraVector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        public static AuraVector3 operator -(AuraVector3 left, AuraVector3 right) =>
            new AuraVector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static AuraVector3 operator -(AuraVector3 value) => new AuraVector3(-value.X, -value.Y, -value.Z);

        public static AuraVector3 operator *(AuraVector3 value, float scalar) =>
            new AuraVector3(value.X * scalar, value.Y * scalar, value.Z * scalar);

        public static AuraVector3 operator *(float scalar, AuraVector3 value) => value * scalar;

        public static AuraVector3 operator /(AuraVector3 value, float scalar) =>
            new AuraVector3(value.X / scalar, value.Y / scalar, value.Z / scalar);

        public static bool operator ==(AuraVector3 left, AuraVector3 right) => ((IEquatable<AuraVector3>)left).Equals(right);
        public static bool operator !=(AuraVector3 left, AuraVector3 right) => !((IEquatable<AuraVector3>)left).Equals(right);
    }
}
