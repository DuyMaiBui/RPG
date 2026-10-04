using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Simulation
{
    public interface IAuraSimulationContext
    {
        SimulationTick CurrentTick { get; }

        IPhysicsQuery Queries { get; }

        SimulationEntityId CreateEntity();

        bool DestroyEntity(SimulationEntityId entity);

        bool IsAlive(SimulationEntityId entity);

        PhysicsBodyId AttachBody(SimulationEntityId entity, in AuraPhysicsBodyDefinition definition);

        bool TryGetBody(SimulationEntityId entity, out PhysicsBodyId body);

        bool TryGetBodyState(SimulationEntityId entity, out AuraBodyState state);

        bool TryResolveEntity(PhysicsBodyId body, out SimulationEntityId entity);

        AuraResult SetKinematicTarget(SimulationEntityId entity, in AuraPose pose);
    }
}
