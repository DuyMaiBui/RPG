using System;

namespace RPG.Simulation.Contracts
{
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));

        public string Value { get; }

        // IEquatable<T> is implemented explicitly by project convention, so Equals(object) and the operators must call
        // the same comparison directly — calling Equals(other) here would resolve back to Equals(object) and recurse
        // until the stack overflows.
        bool IEquatable<PlayerId>.Equals(PlayerId other) => EqualsCore(this, other);

        public override bool Equals(object obj) => obj is PlayerId other && EqualsCore(this, other);

        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(PlayerId left, PlayerId right) => EqualsCore(left, right);

        public static bool operator !=(PlayerId left, PlayerId right) => !EqualsCore(left, right);

        private static bool EqualsCore(PlayerId left, PlayerId right) =>
            string.Equals(left.Value, right.Value, StringComparison.Ordinal);
    }
}
