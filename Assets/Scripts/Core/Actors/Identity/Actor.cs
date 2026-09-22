using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class Actor
    {
        public Actor(EntityId id, ActorComponentSet components)
        {
            Id = id;
            Components = components ?? throw new System.ArgumentNullException(nameof(components));
        }

        public EntityId Id { get; }
        public ActorComponentSet Components { get; }
    }
}
