using System;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public sealed class LocalSimulationClient<TState> : ISimulationClient
    {
        private readonly SimulationHost<TState> _host;
        private readonly SessionContext _session;

        public LocalSimulationClient(SimulationHost<TState> host, SessionContext session)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _session = session;
        }

        public bool TrySend(in ClientCommandEnvelope command) => _host.TryEnqueue(_session, command);
        public bool TryRead(out ServerUpdateEnvelope update) => _host.TryReadLatest(out update);
        public void Dispose() { }
    }
}
