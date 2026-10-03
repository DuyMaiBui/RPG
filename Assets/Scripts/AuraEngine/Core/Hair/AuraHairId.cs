using System;

namespace AuraEngine.Core
{
    public readonly struct AuraHairId : IEquatable<AuraHairId>
    {
        public static readonly AuraHairId Invalid = new AuraHairId(-1, -1);

        public AuraHairId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0;

        bool IEquatable<AuraHairId>.Equals(AuraHairId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AuraHairId other && ((IEquatable<AuraHairId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;
        public static bool operator ==(AuraHairId left, AuraHairId right) => left.Index == right.Index && left.Generation == right.Generation;
        public static bool operator !=(AuraHairId left, AuraHairId right) => !(left == right);
    }
}
