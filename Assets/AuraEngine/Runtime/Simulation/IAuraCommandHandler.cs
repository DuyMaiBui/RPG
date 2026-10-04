using AuraEngine.Core;

namespace AuraEngine.Simulation
{
    public interface IAuraCommandHandler
    {
        ushort TypeId { get; }

        AuraResult Handle(IAuraSimulationContext context, IAuraCommand command);
    }
}
