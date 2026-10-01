using System;

namespace AuraEngine.Core
{
    public readonly struct SimulationTick : IEquatable<SimulationTick>, IComparable<SimulationTick>
    {
        public SimulationTick(uint value) => Value = value;

        public uint Value { get; }

        public SimulationTick Next() => new SimulationTick(unchecked(Value + 1));

        bool IEquatable<SimulationTick>.Equals(SimulationTick other) => Value == other.Value;
        int IComparable<SimulationTick>.CompareTo(SimulationTick other) => Value.CompareTo(other.Value);

        public override bool Equals(object obj) => obj is SimulationTick other && ((IEquatable<SimulationTick>)this).Equals(other);
        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(SimulationTick left, SimulationTick right) => left.Value == right.Value;
        public static bool operator !=(SimulationTick left, SimulationTick right) => left.Value != right.Value;
        public static bool operator <(SimulationTick left, SimulationTick right) => left.Value < right.Value;
        public static bool operator >(SimulationTick left, SimulationTick right) => left.Value > right.Value;
        public static bool operator <=(SimulationTick left, SimulationTick right) => left.Value <= right.Value;
        public static bool operator >=(SimulationTick left, SimulationTick right) => left.Value >= right.Value;

        public override string ToString() => $"Tick({Value})";
    }
}
