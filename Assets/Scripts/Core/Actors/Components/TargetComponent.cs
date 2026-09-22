using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class TargetComponent : IActorComponent
    {
        public EntityId CurrentTarget { get; set; } = EntityId.None;
    }
}
