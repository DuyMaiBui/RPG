using System;

namespace RPG.Simulation.Contracts
{
    public interface ISimulationClient : IDisposable
    {
        bool TrySend(in ClientCommandEnvelope command);
        bool TryRead(out ServerUpdateEnvelope update);
    }
}
