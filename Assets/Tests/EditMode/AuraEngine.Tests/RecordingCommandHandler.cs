using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Tests
{
    public sealed class RecordingCommandHandler : IAuraCommandHandler
    {
        private readonly List<int> _order;

        public RecordingCommandHandler(List<int> order) => _order = order;

        ushort IAuraCommandHandler.TypeId => TestCommand.Id;

        AuraResult IAuraCommandHandler.Handle(IAuraSimulationContext context, IAuraCommand command)
        {
            if (command is TestCommand testCommand)
                _order.Add(testCommand.Value);

            return AuraResult.Success;
        }
    }
}
