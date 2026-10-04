using System;

namespace AuraEngine.Core
{
    public readonly struct SimulationEntityId : IEquatable<SimulationEntityId>
    {
        public static readonly SimulationEntityId None = new SimulationEntityId(-1, -1);

        public SimulationEntityId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsNone => Index < 0;

        bool IEquatable<SimulationEntityId>.Equals(SimulationEntityId other) =>
            Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj) => obj is SimulationEntityId other && ((IEquatable<SimulationEntityId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;

        public static bool operator ==(SimulationEntityId left, SimulationEntityId right) =>
            left.Index == right.Index && left.Generation == right.Generation;

        public static bool operator !=(SimulationEntityId left, SimulationEntityId right) => !(left == right);

        public override string ToString() => $"Entity({Index}:{Generation})";
    }
}
