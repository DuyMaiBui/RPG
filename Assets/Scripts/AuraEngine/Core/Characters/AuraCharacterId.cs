using System;

namespace AuraEngine.Core
{
    public readonly struct AuraCharacterId : IEquatable<AuraCharacterId>
    {
        public static readonly AuraCharacterId Invalid = new AuraCharacterId(-1, -1);

        public AuraCharacterId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0;

        bool IEquatable<AuraCharacterId>.Equals(AuraCharacterId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AuraCharacterId other && ((IEquatable<AuraCharacterId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;
        public static bool operator ==(AuraCharacterId left, AuraCharacterId right) => left.Index == right.Index && left.Generation == right.Generation;
        public static bool operator !=(AuraCharacterId left, AuraCharacterId right) => !(left == right);
        public override string ToString() => $"Character({Index}:{Generation})";
    }
}
