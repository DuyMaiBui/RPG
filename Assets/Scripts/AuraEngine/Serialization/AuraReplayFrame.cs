using System;
using AuraEngine.Core;

namespace AuraEngine.Serialization
{
    public readonly struct AuraReplayFrame : IEquatable<AuraReplayFrame>
    {
        public AuraReplayFrame(SimulationTick tick, ulong stateHash)
        {
            Tick = tick;
            StateHash = stateHash;
        }

        public SimulationTick Tick { get; }

        public ulong StateHash { get; }

        bool IEquatable<AuraReplayFrame>.Equals(AuraReplayFrame other) =>
            Tick == other.Tick && StateHash == other.StateHash;

        public override bool Equals(object obj) => obj is AuraReplayFrame other && ((IEquatable<AuraReplayFrame>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(Tick, StateHash);
        public override string ToString() => $"tick={Tick} hash={StateHash:X16}";

        public static bool operator ==(AuraReplayFrame left, AuraReplayFrame right) => ((IEquatable<AuraReplayFrame>)left).Equals(right);
        public static bool operator !=(AuraReplayFrame left, AuraReplayFrame right) => !((IEquatable<AuraReplayFrame>)left).Equals(right);
    }
}
