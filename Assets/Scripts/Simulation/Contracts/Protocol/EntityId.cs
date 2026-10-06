using System;

namespace RPG.Simulation.Contracts
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public static readonly EntityId None = new EntityId(-1, -1);

        public EntityId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }

        public int Generation { get; }

        public bool IsNone => Index < 0;

        // IEquatable<T> is implemented explicitly by project convention, so Equals(object) and the operators must call
        // the same comparison directly — calling Equals(other) here would resolve back to Equals(object) and recurse
        // until the stack overflows.
        bool IEquatable<EntityId>.Equals(EntityId other) => EqualsCore(this, other);

        public override bool Equals(object obj) => obj is EntityId other && EqualsCore(this, other);

        public override int GetHashCode() => (Index * 397) ^ Generation;

        public static bool operator ==(EntityId left, EntityId right) => EqualsCore(left, right);

        public static bool operator !=(EntityId left, EntityId right) => !EqualsCore(left, right);

        private static bool EqualsCore(EntityId left, EntityId right) =>
            left.Index == right.Index && left.Generation == right.Generation;
    }
}
