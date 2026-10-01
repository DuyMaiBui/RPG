using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Tests
{
    public sealed class TestMoveSystem : IAuraSimulationSystem
    {
        private AuraVector3 _position;

        public TestMoveSystem(SimulationEntityId target, AuraVector3 step)
        {
            Target = target;
            Step = step;
        }

        public SimulationEntityId Target { get; set; }

        public AuraVector3 Step { get; set; }

        public int TickCount { get; private set; }

        void IAuraSimulationSystem.Tick(IAuraSimulationContext context, in SimulationStep step)
        {
            TickCount++;
            if (!context.TryGetBody(Target, out _))
                return;

            _position += Step;
            context.SetKinematicTarget(Target, new AuraPose(_position, AuraQuaternion.Identity));
        }
    }
}
