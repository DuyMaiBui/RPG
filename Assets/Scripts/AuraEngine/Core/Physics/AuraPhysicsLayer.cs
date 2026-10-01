using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPhysicsLayer : IEquatable<AuraPhysicsLayer>
    {
        public const int MaxLayers = 64;

        public static readonly AuraPhysicsLayer Default = new AuraPhysicsLayer(0);

        public AuraPhysicsLayer(int value)
        {
            if (value < 0 || value >= MaxLayers)
                throw new ArgumentOutOfRangeException(nameof(value), value, "A physics layer must be in [0, 63].");

            Value = value;
        }

        public int Value { get; }

        public AuraPhysicsLayerMask ToMask() => new AuraPhysicsLayerMask(1UL << Value);

        bool IEquatable<AuraPhysicsLayer>.Equals(AuraPhysicsLayer other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AuraPhysicsLayer other && ((IEquatable<AuraPhysicsLayer>)this).Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Layer({Value})";

        public static bool operator ==(AuraPhysicsLayer left, AuraPhysicsLayer right) => left.Value == right.Value;
        public static bool operator !=(AuraPhysicsLayer left, AuraPhysicsLayer right) => left.Value != right.Value;
    }
}
