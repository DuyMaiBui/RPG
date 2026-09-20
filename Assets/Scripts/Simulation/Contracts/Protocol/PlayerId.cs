using System;

namespace RPG.Simulation.Contracts
{
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public string Value { get; }
        bool IEquatable<PlayerId>.Equals(PlayerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }
}
