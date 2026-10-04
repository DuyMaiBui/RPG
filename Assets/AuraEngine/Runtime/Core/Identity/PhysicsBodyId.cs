using System;

namespace AuraEngine.Core
{
    public readonly struct PhysicsBodyId : IEquatable<PhysicsBodyId>
    {
        public static readonly PhysicsBodyId Invalid = new PhysicsBodyId(-1, -1);

        public PhysicsBodyId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0;

        bool IEquatable<PhysicsBodyId>.Equals(PhysicsBodyId other) =>
            Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj) => obj is PhysicsBodyId other && ((IEquatable<PhysicsBodyId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;

        public static bool operator ==(PhysicsBodyId left, PhysicsBodyId right) =>
            left.Index == right.Index && left.Generation == right.Generation;

        public static bool operator !=(PhysicsBodyId left, PhysicsBodyId right) => !(left == right);

        public override string ToString() => $"Body({Index}:{Generation})";
    }
}
