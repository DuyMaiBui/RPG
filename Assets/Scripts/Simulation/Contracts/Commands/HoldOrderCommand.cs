namespace RPG.Simulation.Contracts
{
    /// <summary>Order one owned actor to hold its current position: attack what comes into reach, never chase.</summary>
    public sealed class HoldOrderCommand : ISimulationCommand
    {
        public HoldOrderCommand(EntityId actor, bool queued = false)
        {
            Actor = actor;
            Queued = queued;
        }

        public EntityId Actor { get; }

        public bool Queued { get; }

        ushort ISimulationCommand.TypeId => 7;
    }
}
