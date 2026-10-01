using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPose2D : IEquatable<AuraPose2D>
    {
        public AuraPose2D(AuraVector2 position, float rotationRadians)
        {
            Position = position;
            RotationRadians = rotationRadians;
        }

        public static AuraPose2D Identity => new AuraPose2D(AuraVector2.Zero, 0f);

        public AuraVector2 Position { get; }
        public float RotationRadians { get; }

        public AuraPose ToPose() =>
            new AuraPose(
                new AuraVector3(Position.X, Position.Y, 0f),
                AuraQuaternion.FromAxisAngle(AuraVector3.UnitZ, RotationRadians));

        bool IEquatable<AuraPose2D>.Equals(AuraPose2D other) =>
            Position.Equals(other.Position) && RotationRadians.Equals(other.RotationRadians);

        public override bool Equals(object obj) => obj is AuraPose2D other && ((IEquatable<AuraPose2D>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(Position, RotationRadians);
        public override string ToString() => $"{Position} rot={RotationRadians}";

        public static bool operator ==(AuraPose2D left, AuraPose2D right) => ((IEquatable<AuraPose2D>)left).Equals(right);
        public static bool operator !=(AuraPose2D left, AuraPose2D right) => !((IEquatable<AuraPose2D>)left).Equals(right);
    }
}
