using System;

namespace AuraEngine.Core
{
    public readonly struct AuraRay : IEquatable<AuraRay>
    {
        public AuraRay(AuraVector3 origin, AuraVector3 direction)
        {
            Origin = origin;
            Direction = direction.Normalized();
        }

        public AuraVector3 Origin { get; }
        public AuraVector3 Direction { get; }

        public AuraVector3 GetPoint(float distance) => Origin + Direction * distance;

        bool IEquatable<AuraRay>.Equals(AuraRay other) =>
            Origin.Equals(other.Origin) && Direction.Equals(other.Direction);

        public override bool Equals(object obj) => obj is AuraRay other && ((IEquatable<AuraRay>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(Origin, Direction);
        public override string ToString() => $"origin={Origin} dir={Direction}";

        public static bool operator ==(AuraRay left, AuraRay right) => ((IEquatable<AuraRay>)left).Equals(right);
        public static bool operator !=(AuraRay left, AuraRay right) => !((IEquatable<AuraRay>)left).Equals(right);
    }
}
