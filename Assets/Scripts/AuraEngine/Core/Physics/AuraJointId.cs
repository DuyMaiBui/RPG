using System;

namespace AuraEngine.Core
{
    public readonly struct AuraJointId : IEquatable<AuraJointId>
    {
        public static readonly AuraJointId Invalid = new AuraJointId(-1, -1);

        public AuraJointId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0;

        bool IEquatable<AuraJointId>.Equals(AuraJointId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AuraJointId other && ((IEquatable<AuraJointId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;

        public static bool operator ==(AuraJointId left, AuraJointId right) => left.Index == right.Index && left.Generation == right.Generation;
        public static bool operator !=(AuraJointId left, AuraJointId right) => !(left == right);
    }
}
