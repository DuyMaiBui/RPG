using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class ActorDamaged : ISimulationEvent
    {
        public ActorDamaged(EntityId source, EntityId target, int damage)
        {
            Source = source;
            Target = target;
            Damage = damage;
        }

        public EntityId Source { get; }
        public EntityId Target { get; }
        public int Damage { get; }
    }
}
