using AuraEngine.Core;
using AuraEngine.Simulation;

namespace AuraEngine.Networking
{
    /* Applies a replayed input command to a world. Both the live client and the
       re-simulation path use the same implementation so prediction matches the
       server's application of the command. */
    public sealed class AuraPredictionReplay
    {
        public AuraPredictionReplay(SimulationEntityId entity, float moveSpeed)
        {
            Entity = entity;
            MoveSpeed = moveSpeed;
        }

        public SimulationEntityId Entity { get; }

        public float MoveSpeed { get; }

        public void Apply(AuraSimulationWorld world, in AuraInputCommand command)
        {
            var position = new AuraVector3(command.MoveX * MoveSpeed, 0f, command.MoveY * MoveSpeed);
            world.SetKinematicTarget(Entity, new AuraPose(position, AuraQuaternion.Identity));
        }
    }
}
