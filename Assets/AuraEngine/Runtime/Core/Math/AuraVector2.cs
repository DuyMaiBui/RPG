using System;

namespace AuraEngine.Core
{
    public readonly struct AuraVector2 : IEquatable<AuraVector2>
    {
        public AuraVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static AuraVector2 Zero => new AuraVector2(0f, 0f);
        public static AuraVector2 One => new AuraVector2(1f, 1f);

        public float X { get; }
        public float Y { get; }

        public float LengthSquared => X * X + Y * Y;
        public float Length => MathF.Sqrt(LengthSquared);

        public AuraVector2 Normalized()
        {
            var length = Length;
            return length <= 1e-6f ? Zero : this / length;
        }

        public static float Dot(AuraVector2 left, AuraVector2 right) => left.X * right.X + left.Y * right.Y;

        public static float Distance(AuraVector2 left, AuraVector2 right) => (left - right).Length;

        public static AuraVector2 Lerp(AuraVector2 from, AuraVector2 to, float t) =>
            new AuraVector2(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);

        bool IEquatable<AuraVector2>.Equals(AuraVector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is AuraVector2 other && ((IEquatable<AuraVector2>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";

        public static AuraVector2 operator +(AuraVector2 left, AuraVector2 right) =>
            new AuraVector2(left.X + right.X, left.Y + right.Y);

        public static AuraVector2 operator -(AuraVector2 left, AuraVector2 right) =>
            new AuraVector2(left.X - right.X, left.Y - right.Y);

        public static AuraVector2 operator -(AuraVector2 value) => new AuraVector2(-value.X, -value.Y);

        public static AuraVector2 operator *(AuraVector2 value, float scalar) =>
            new AuraVector2(value.X * scalar, value.Y * scalar);

        public static AuraVector2 operator *(float scalar, AuraVector2 value) => value * scalar;

        public static AuraVector2 operator /(AuraVector2 value, float scalar) =>
            new AuraVector2(value.X / scalar, value.Y / scalar);

        public static bool operator ==(AuraVector2 left, AuraVector2 right) => ((IEquatable<AuraVector2>)left).Equals(right);
        public static bool operator !=(AuraVector2 left, AuraVector2 right) => !((IEquatable<AuraVector2>)left).Equals(right);
    }
}
