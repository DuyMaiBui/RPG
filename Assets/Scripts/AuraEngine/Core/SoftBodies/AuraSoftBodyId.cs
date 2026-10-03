using System;

namespace AuraEngine.Core
{
    public readonly struct AuraSoftBodyId : IEquatable<AuraSoftBodyId>
    {
        public static readonly AuraSoftBodyId Invalid = new AuraSoftBodyId(-1, -1);
        public AuraSoftBodyId(int index, int generation) { Index = index; Generation = generation; }
        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0;
        bool IEquatable<AuraSoftBodyId>.Equals(AuraSoftBodyId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AuraSoftBodyId other && ((IEquatable<AuraSoftBodyId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;
        public static bool operator ==(AuraSoftBodyId left, AuraSoftBodyId right) => left.Index == right.Index && left.Generation == right.Generation;
        public static bool operator !=(AuraSoftBodyId left, AuraSoftBodyId right) => !(left == right);
    }
}
