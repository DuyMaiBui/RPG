using System;

namespace AuraEngine.Core
{
    public readonly struct AuraClothId : IEquatable<AuraClothId>
    {
        public static readonly AuraClothId Invalid = new AuraClothId(-1, -1);
        public AuraClothId(int index, int generation) { Index = index; Generation = generation; }
        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0;
        bool IEquatable<AuraClothId>.Equals(AuraClothId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AuraClothId other && ((IEquatable<AuraClothId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;
        public static bool operator ==(AuraClothId left, AuraClothId right) => left.Index == right.Index && left.Generation == right.Generation;
        public static bool operator !=(AuraClothId left, AuraClothId right) => !(left == right);
    }
}
