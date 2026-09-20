using System;

namespace RPG.Simulation.Contracts
{
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
}
