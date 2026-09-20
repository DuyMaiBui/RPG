using System;
using System.Collections.Generic;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    internal sealed class ThrowingApplication : ISimulationApplication<object>
    {
        void ISimulationApplication<object>.BeginTick(SimulationContext<object> context, SimulationTick tick)
            => throw new InvalidOperationException("Expected test fault.");

        void ISimulationApplication<object>.HandleCommand(SimulationContext<object> context, SessionContext session, in ClientCommandEnvelope command) { }
        void ISimulationApplication<object>.Tick(SimulationContext<object> context, SimulationTick tick) { }
        void ISimulationApplication<object>.HandleEvents(SimulationContext<object> context, IReadOnlyList<ISimulationEvent> events) { }
        ISimulationUpdate ISimulationApplication<object>.CreateUpdate(SimulationContext<object> context, SimulationTick tick)
            => throw new NotSupportedException();
    }
}
