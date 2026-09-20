using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class ActorDied : ISimulationEvent
    {
        public ActorDied(EntityId source, EntityId target)
        {
            Source = source;
            Target = target;
        }

        public EntityId Source { get; }
        public EntityId Target { get; }
    }
}
