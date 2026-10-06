using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    /// <summary>Raised when an actor casts an ability. Presentation only; it carries no simulation state.</summary>
    public sealed class ActorAbilityCast : ISimulationEvent
    {
        public ActorAbilityCast(EntityId source, EntityId target, int abilityId)
        {
            Source = source;
            Target = target;
            AbilityId = abilityId;
        }

        public EntityId Source { get; }

        public EntityId Target { get; }

        public int AbilityId { get; }
    }
}
