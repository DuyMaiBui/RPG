namespace RPG.Simulation.Contracts
{
    /// <summary>Order one owned actor to cast one of its abilities at one target. The order waits for the cooldown and
    /// walks into range, so only an ability the actor does not own is refused.</summary>
    public sealed class CastAbilityOrderCommand : ISimulationCommand
    {
        public CastAbilityOrderCommand(EntityId actor, int abilityId, EntityId target, bool queued = false)
        {
            Actor = actor;
            AbilityId = abilityId;
            Target = target;
            Queued = queued;
        }

        public EntityId Actor { get; }

        public int AbilityId { get; }

        public EntityId Target { get; }

        public bool Queued { get; }

        ushort ISimulationCommand.TypeId => 6;
    }
}
