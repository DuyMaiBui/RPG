using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Tests
{
    public sealed class EnqueueCommandSystem : IAuraSimulationSystem
    {
        private readonly AuraSimulationWorld _world;
        private bool _enqueued;

        public EnqueueCommandSystem(AuraSimulationWorld world) => _world = world;

        public int TickCount { get; private set; }

        void IAuraSimulationSystem.Tick(IAuraSimulationContext context, in SimulationStep step)
        {
            TickCount++;
            if (_enqueued)
                return;

            _enqueued = true;
            _world.EnqueueCommand(new TestCommand(9));
        }
    }
}
