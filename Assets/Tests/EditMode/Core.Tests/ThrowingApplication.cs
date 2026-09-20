using System;
using System.Collections.Generic;
using RPG.Simulation.Contracts;
using RPG.Simulation.Runtime;

namespace RPG.Core.Tests
{
    internal sealed class ThrowingApplication : ISimulationApplication<object>
    {
        public void BeginTick(SimulationContext<object> context, SimulationTick tick)
            => throw new InvalidOperationException("Expected test fault.");

        public void HandleCommand(SimulationContext<object> context, SessionContext session, in ClientCommandEnvelope command) { }
        public void Tick(SimulationContext<object> context, SimulationTick tick) { }
        public void HandleEvents(SimulationContext<object> context, IReadOnlyList<ISimulationEvent> events) { }
        public ISimulationUpdate CreateUpdate(SimulationContext<object> context, SimulationTick tick)
            => throw new NotSupportedException();
    }
}
