using AuraEngine.Core;

namespace AuraEngine.Simulation
{
    public interface IAuraSimulationSystem
    {
        void Tick(IAuraSimulationContext context, in SimulationStep step);
    }
}
