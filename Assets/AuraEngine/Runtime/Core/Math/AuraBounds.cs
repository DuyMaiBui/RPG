using System;

namespace AuraEngine.Core
{
    public readonly struct AuraBounds : IEquatable<AuraBounds>
    {
        public AuraBounds(AuraVector3 center, AuraVector3 extents)
        {
            Center = center;
            Extents = extents;
        }

        public AuraVector3 Center { get; }
        public AuraVector3 Extents { get; }

        public AuraVector3 Min => Center - Extents;
        public AuraVector3 Max => Center + Extents;

        public bool Contains(AuraVector3 point) =>
            point.X >= Min.X && point.X <= Max.X &&
            point.Y >= Min.Y && point.Y <= Max.Y &&
            point.Z >= Min.Z && point.Z <= Max.Z;

        public static AuraBounds FromCenterRadius(AuraVector3 center, float radius) =>
            new AuraBounds(center, new AuraVector3(radius, radius, radius));

        bool IEquatable<AuraBounds>.Equals(AuraBounds other) =>
            Center.Equals(other.Center) && Extents.Equals(other.Extents);

        public override bool Equals(object obj) => obj is AuraBounds other && ((IEquatable<AuraBounds>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(Center, Extents);
        public override string ToString() => $"center={Center} extents={Extents}";

        public static bool operator ==(AuraBounds left, AuraBounds right) => ((IEquatable<AuraBounds>)left).Equals(right);
        public static bool operator !=(AuraBounds left, AuraBounds right) => !((IEquatable<AuraBounds>)left).Equals(right);
    }
}
