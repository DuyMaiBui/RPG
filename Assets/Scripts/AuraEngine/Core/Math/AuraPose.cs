using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPose : IEquatable<AuraPose>
    {
        public AuraPose(AuraVector3 position, AuraQuaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public static AuraPose Identity => new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity);

        public AuraVector3 Position { get; }
        public AuraQuaternion Rotation { get; }

        public AuraVector3 TransformPoint(AuraVector3 localPoint) => Position + Rotation.Rotate(localPoint);

        public static AuraPose Lerp(AuraPose from, AuraPose to, float t) =>
            new AuraPose(AuraVector3.Lerp(from.Position, to.Position, t), AuraQuaternion.Slerp(from.Rotation, to.Rotation, t));

        bool IEquatable<AuraPose>.Equals(AuraPose other) =>
            Position.Equals(other.Position) && Rotation.Equals(other.Rotation);

        public override bool Equals(object obj) => obj is AuraPose other && ((IEquatable<AuraPose>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(Position, Rotation);
        public override string ToString() => $"{Position} {Rotation}";

        public static bool operator ==(AuraPose left, AuraPose right) => ((IEquatable<AuraPose>)left).Equals(right);
        public static bool operator !=(AuraPose left, AuraPose right) => !((IEquatable<AuraPose>)left).Equals(right);
    }
}
