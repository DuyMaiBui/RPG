using System.Collections.Generic;
using RPG.Simulation.Contracts;

namespace RPG.Simulation.Runtime
{
    public interface ISimulationApplication<TState>
    {
        void BeginTick(SimulationContext<TState> context, SimulationTick tick);
        void HandleCommand(SimulationContext<TState> context, SessionContext session, in ClientCommandEnvelope command);
        void Tick(SimulationContext<TState> context, SimulationTick tick);
        void HandleEvents(SimulationContext<TState> context, IReadOnlyList<ISimulationEvent> events);
        ISimulationUpdate CreateUpdate(SimulationContext<TState> context, SimulationTick tick);
    }
}
