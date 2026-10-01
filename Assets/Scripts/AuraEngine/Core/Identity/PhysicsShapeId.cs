using System;

namespace AuraEngine.Core
{
    public readonly struct PhysicsShapeId : IEquatable<PhysicsShapeId>
    {
        public static readonly PhysicsShapeId Invalid = new PhysicsShapeId(-1, -1);

        public PhysicsShapeId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0;

        bool IEquatable<PhysicsShapeId>.Equals(PhysicsShapeId other) =>
            Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj) => obj is PhysicsShapeId other && ((IEquatable<PhysicsShapeId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;

        public static bool operator ==(PhysicsShapeId left, PhysicsShapeId right) =>
            left.Index == right.Index && left.Generation == right.Generation;

        public static bool operator !=(PhysicsShapeId left, PhysicsShapeId right) => !(left == right);

        public override string ToString() => $"Shape({Index}:{Generation})";
    }
}
