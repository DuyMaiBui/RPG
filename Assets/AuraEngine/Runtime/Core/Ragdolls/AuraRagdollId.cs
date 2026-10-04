using System;
namespace AuraEngine.Core
{
    public readonly struct AuraRagdollId : IEquatable<AuraRagdollId>
    {
        public static readonly AuraRagdollId Invalid = new AuraRagdollId(-1, -1);
        public AuraRagdollId(int index, int generation) { Index = index; Generation = generation; }
        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0;
        bool IEquatable<AuraRagdollId>.Equals(AuraRagdollId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AuraRagdollId other && ((IEquatable<AuraRagdollId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;
        public static bool operator ==(AuraRagdollId a, AuraRagdollId b) => a.Index == b.Index && a.Generation == b.Generation;
        public static bool operator !=(AuraRagdollId a, AuraRagdollId b) => !(a == b);
    }
}
