using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public readonly struct ProjectileSnapshot
    {
        public ProjectileSnapshot(EntityId entity, EntityId source, EntityId target, SimulationVector2 position)
        {
            Entity = entity;
            Source = source;
            Target = target;
            Position = position;
        }

        public EntityId Entity { get; }
        public EntityId Source { get; }
        public EntityId Target { get; }
        public SimulationVector2 Position { get; }
    }
}
