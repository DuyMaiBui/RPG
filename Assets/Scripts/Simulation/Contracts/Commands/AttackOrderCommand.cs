namespace RPG.Simulation.Contracts
{
    /// <summary>Order one owned actor to attack one entity, chasing it as far as its leash allows.</summary>
    public sealed class AttackOrderCommand : ISimulationCommand
    {
        public AttackOrderCommand(EntityId actor, EntityId target, bool queued = false)
        {
            Actor = actor;
            Target = target;
            Queued = queued;
        }

        public EntityId Actor { get; }

        public EntityId Target { get; }

        public bool Queued { get; }

        ushort ISimulationCommand.TypeId => 5;
    }
}
