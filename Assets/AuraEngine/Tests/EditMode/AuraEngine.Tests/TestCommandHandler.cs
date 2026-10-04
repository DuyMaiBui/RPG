using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Tests
{
    public sealed class TestCommandHandler : IAuraCommandHandler
    {
        public int HandleCount { get; private set; }

        public int Sum { get; private set; }

        public SimulationEntityId CreatedEntity { get; private set; } = SimulationEntityId.None;

        ushort IAuraCommandHandler.TypeId => TestCommand.Id;

        AuraResult IAuraCommandHandler.Handle(IAuraSimulationContext context, IAuraCommand command)
        {
            if (command is not TestCommand testCommand)
                return AuraResult.InvalidDefinition;

            HandleCount++;
            Sum += testCommand.Value;
            if (CreatedEntity.IsNone)
                CreatedEntity = context.CreateEntity();

            return AuraResult.Success;
        }
    }
}
