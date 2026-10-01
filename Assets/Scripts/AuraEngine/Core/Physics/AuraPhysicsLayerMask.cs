using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPhysicsLayerMask : IEquatable<AuraPhysicsLayerMask>
    {
        public AuraPhysicsLayerMask(ulong bits) => Bits = bits;

        public static AuraPhysicsLayerMask None => new AuraPhysicsLayerMask(0UL);
        public static AuraPhysicsLayerMask All => new AuraPhysicsLayerMask(~0UL);

        public ulong Bits { get; }

        public static AuraPhysicsLayerMask FromLayer(AuraPhysicsLayer layer) => new AuraPhysicsLayerMask(1UL << layer.Value);

        public static AuraPhysicsLayerMask FromLayers(params AuraPhysicsLayer[] layers)
        {
            if (layers == null)
                return None;

            var bits = 0UL;
            for (var index = 0; index < layers.Length; index++)
                bits |= 1UL << layers[index].Value;

            return new AuraPhysicsLayerMask(bits);
        }

        public bool Includes(AuraPhysicsLayer layer) => (Bits & (1UL << layer.Value)) != 0UL;

        public bool Overlaps(AuraPhysicsLayerMask other) => (Bits & other.Bits) != 0UL;

        public AuraPhysicsLayerMask With(AuraPhysicsLayer layer) => new AuraPhysicsLayerMask(Bits | (1UL << layer.Value));

        public AuraPhysicsLayerMask Without(AuraPhysicsLayer layer) => new AuraPhysicsLayerMask(Bits & ~(1UL << layer.Value));

        bool IEquatable<AuraPhysicsLayerMask>.Equals(AuraPhysicsLayerMask other) => Bits == other.Bits;
        public override bool Equals(object obj) => obj is AuraPhysicsLayerMask other && ((IEquatable<AuraPhysicsLayerMask>)this).Equals(other);
        public override int GetHashCode() => Bits.GetHashCode();
        public override string ToString() => $"Mask(0x{Bits:X16})";

        public static AuraPhysicsLayerMask operator |(AuraPhysicsLayerMask left, AuraPhysicsLayerMask right) =>
            new AuraPhysicsLayerMask(left.Bits | right.Bits);

        public static AuraPhysicsLayerMask operator &(AuraPhysicsLayerMask left, AuraPhysicsLayerMask right) =>
            new AuraPhysicsLayerMask(left.Bits & right.Bits);

        public static AuraPhysicsLayerMask operator ~(AuraPhysicsLayerMask value) => new AuraPhysicsLayerMask(~value.Bits);

        public static bool operator ==(AuraPhysicsLayerMask left, AuraPhysicsLayerMask right) => left.Bits == right.Bits;
        public static bool operator !=(AuraPhysicsLayerMask left, AuraPhysicsLayerMask right) => left.Bits != right.Bits;
    }
}
