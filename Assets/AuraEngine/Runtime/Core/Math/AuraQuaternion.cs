using System;

namespace AuraEngine.Core
{
    public readonly struct AuraQuaternion : IEquatable<AuraQuaternion>
    {
        public AuraQuaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static AuraQuaternion Identity => new AuraQuaternion(0f, 0f, 0f, 1f);

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float W { get; }

        public static AuraQuaternion FromAxisAngle(AuraVector3 axis, float radians)
        {
            var half = radians * 0.5f;
            var normalized = axis.Normalized();
            var sine = MathF.Sin(half);
            return new AuraQuaternion(normalized.X * sine, normalized.Y * sine, normalized.Z * sine, MathF.Cos(half));
        }

        public static AuraQuaternion FromEuler(float xRadians, float yRadians, float zRadians)
        {
            var halfX = xRadians * 0.5f;
            var halfY = yRadians * 0.5f;
            var halfZ = zRadians * 0.5f;

            var sinX = MathF.Sin(halfX);
            var cosX = MathF.Cos(halfX);
            var sinY = MathF.Sin(halfY);
            var cosY = MathF.Cos(halfY);
            var sinZ = MathF.Sin(halfZ);
            var cosZ = MathF.Cos(halfZ);

            return new AuraQuaternion(
                sinX * cosY * cosZ - cosX * sinY * sinZ,
                cosX * sinY * cosZ + sinX * cosY * sinZ,
                cosX * cosY * sinZ - sinX * sinY * cosZ,
                cosX * cosY * cosZ + sinX * sinY * sinZ);
        }

        public AuraQuaternion Normalized()
        {
            var lengthSquared = X * X + Y * Y + Z * Z + W * W;
            if (lengthSquared <= 1e-12f)
                return Identity;

            var inverse = 1f / MathF.Sqrt(lengthSquared);
            return new AuraQuaternion(X * inverse, Y * inverse, Z * inverse, W * inverse);
        }

        public AuraQuaternion Conjugate() => new AuraQuaternion(-X, -Y, -Z, W);

        public AuraVector3 InverseRotate(AuraVector3 value)
        {
            var conjugate = Conjugate();
            return conjugate.Rotate(value);
        }

        public AuraVector3 Rotate(AuraVector3 value)
        {
            var x2 = X + X;
            var y2 = Y + Y;
            var z2 = Z + Z;

            var xx = X * x2;
            var yy = Y * y2;
            var zz = Z * z2;
            var xy = X * y2;
            var xz = X * z2;
            var yz = Y * z2;
            var wx = W * x2;
            var wy = W * y2;
            var wz = W * z2;

            return new AuraVector3(
                (1f - (yy + zz)) * value.X + (xy - wz) * value.Y + (xz + wy) * value.Z,
                (xy + wz) * value.X + (1f - (xx + zz)) * value.Y + (yz - wx) * value.Z,
                (xz - wy) * value.X + (yz + wx) * value.Y + (1f - (xx + yy)) * value.Z);
        }

        public static AuraQuaternion Multiply(AuraQuaternion left, AuraQuaternion right) =>
            new AuraQuaternion(
                left.W * right.X + left.X * right.W + left.Y * right.Z - left.Z * right.Y,
                left.W * right.Y - left.X * right.Z + left.Y * right.W + left.Z * right.X,
                left.W * right.Z + left.X * right.Y - left.Y * right.X + left.Z * right.W,
                left.W * right.W - left.X * right.X - left.Y * right.Y - left.Z * right.Z);

        public static AuraQuaternion Slerp(AuraQuaternion from, AuraQuaternion to, float t)
        {
            var dot = from.X * to.X + from.Y * to.Y + from.Z * to.Z + from.W * to.W;
            if (dot < 0f)
            {
                to = new AuraQuaternion(-to.X, -to.Y, -to.Z, -to.W);
                dot = -dot;
            }

            if (dot > 0.9995f)
                return new AuraQuaternion(
                    from.X + (to.X - from.X) * t,
                    from.Y + (to.Y - from.Y) * t,
                    from.Z + (to.Z - from.Z) * t,
                    from.W + (to.W - from.W) * t).Normalized();

            var theta = MathF.Acos(dot);
            var sinTheta = MathF.Sin(theta);
            var scaleFrom = MathF.Sin((1f - t) * theta) / sinTheta;
            var scaleTo = MathF.Sin(t * theta) / sinTheta;
            return new AuraQuaternion(
                from.X * scaleFrom + to.X * scaleTo,
                from.Y * scaleFrom + to.Y * scaleTo,
                from.Z * scaleFrom + to.Z * scaleTo,
                from.W * scaleFrom + to.W * scaleTo);
        }

        bool IEquatable<AuraQuaternion>.Equals(AuraQuaternion other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

        public override bool Equals(object obj) => obj is AuraQuaternion other && ((IEquatable<AuraQuaternion>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        public override string ToString() => $"({X}, {Y}, {Z}, {W})";

        public static AuraQuaternion operator *(AuraQuaternion left, AuraQuaternion right) => Multiply(left, right);

        public static bool operator ==(AuraQuaternion left, AuraQuaternion right) => ((IEquatable<AuraQuaternion>)left).Equals(right);
        public static bool operator !=(AuraQuaternion left, AuraQuaternion right) => !((IEquatable<AuraQuaternion>)left).Equals(right);
    }
}
