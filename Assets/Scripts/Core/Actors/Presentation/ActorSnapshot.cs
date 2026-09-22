using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public readonly struct ActorSnapshot
    {
        public ActorSnapshot(EntityId entity, ActorKind kind, FactionId faction, int currentHealth, int maximumHealth, ActorVisualState visualState)
        {
            Entity = entity;
            Kind = kind;
            Faction = faction;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            VisualState = visualState;
        }

        public EntityId Entity { get; }
        public ActorKind Kind { get; }
        public FactionId Faction { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public ActorVisualState VisualState { get; }
    }
}
