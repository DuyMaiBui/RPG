using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Tests
{
    public sealed class SelfRemovingSystem : IAuraSimulationSystem
    {
        private readonly AuraSimulationWorld _world;

        public SelfRemovingSystem(AuraSimulationWorld world) => _world = world;

        public int TickCount { get; private set; }

        void IAuraSimulationSystem.Tick(IAuraSimulationContext context, in SimulationStep step)
        {
            TickCount++;
            _world.RemoveSystem(this);
        }
    }
}
