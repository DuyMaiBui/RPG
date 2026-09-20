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
        public bool Equals(EntityId other) => Index == other.Index && Generation == other.Generation;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => (Index * 397) ^ Generation;
        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }

    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public string Value { get; }
        public bool Equals(PlayerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct SessionId
    {
        public SessionId(Guid value) => Value = value;
        public Guid Value { get; }
    }

    public readonly struct SimulationTick
    {
        public SimulationTick(long value) => Value = value;
        public long Value { get; }
    }

    public readonly struct ClientSequence
    {
        public ClientSequence(long value) => Value = value;
        public long Value { get; }
    }

    public readonly struct ProtocolVersion
    {
        public static readonly ProtocolVersion Current = new ProtocolVersion(1, 0);

        public ProtocolVersion(ushort major, ushort minor)
        {
            Major = major;
            Minor = minor;
        }

        public ushort Major { get; }
        public ushort Minor { get; }
        public bool IsCompatibleWith(ProtocolVersion server) => Major == server.Major && Minor <= server.Minor;
    }

    public interface ISimulationCommand { ushort TypeId { get; } }
    public interface ISimulationUpdate { ushort TypeId { get; } }
    public interface ISimulationEvent { }

    public readonly struct ClientCommandEnvelope
    {
        public ClientCommandEnvelope(ProtocolVersion protocol, ClientSequence sequence, SimulationTick clientTick, ISimulationCommand payload)
        {
            Protocol = protocol;
            Sequence = sequence;
            ClientTick = clientTick;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public ProtocolVersion Protocol { get; }
        public ClientSequence Sequence { get; }
        public SimulationTick ClientTick { get; }
        public ISimulationCommand Payload { get; }
    }

    public readonly struct ServerUpdateEnvelope
    {
        public ServerUpdateEnvelope(ProtocolVersion protocol, SimulationTick serverTick, ClientSequence lastProcessedClientSequence, ISimulationUpdate payload)
        {
            Protocol = protocol;
            ServerTick = serverTick;
            LastProcessedClientSequence = lastProcessedClientSequence;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public ProtocolVersion Protocol { get; }
        public SimulationTick ServerTick { get; }
        public ClientSequence LastProcessedClientSequence { get; }
        public ISimulationUpdate Payload { get; }
    }

    public interface ISimulationClient : IDisposable
    {
        bool TrySend(in ClientCommandEnvelope command);
        bool TryRead(out ServerUpdateEnvelope update);
    }
}
