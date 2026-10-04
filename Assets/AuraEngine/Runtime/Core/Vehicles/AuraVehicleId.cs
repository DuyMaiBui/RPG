using System;

namespace AuraEngine.Core
{
    public readonly struct AuraVehicleId : IEquatable<AuraVehicleId>
    {
        public static readonly AuraVehicleId Invalid = new AuraVehicleId(-1, -1);

        public AuraVehicleId(int index, int generation) { Index = index; Generation = generation; }
        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index >= 0;
        bool IEquatable<AuraVehicleId>.Equals(AuraVehicleId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AuraVehicleId other && ((IEquatable<AuraVehicleId>)this).Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;
        public static bool operator ==(AuraVehicleId left, AuraVehicleId right) => left.Index == right.Index && left.Generation == right.Generation;
        public static bool operator !=(AuraVehicleId left, AuraVehicleId right) => !(left == right);
    }
}
