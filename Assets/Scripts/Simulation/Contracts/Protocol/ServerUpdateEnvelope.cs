using System;

namespace RPG.Simulation.Contracts
{
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
}
