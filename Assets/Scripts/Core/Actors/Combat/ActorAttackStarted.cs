using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class ActorAttackStarted : ISimulationEvent
    {
        public ActorAttackStarted(EntityId source, EntityId target)
        {
            Source = source;
            Target = target;
        }

        public EntityId Source { get; }
        public EntityId Target { get; }
    }
}
