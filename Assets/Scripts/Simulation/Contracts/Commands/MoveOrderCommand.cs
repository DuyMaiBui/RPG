namespace RPG.Simulation.Contracts
{
    /// <summary>Order one owned actor to move to a point, ignoring enemies on the way. <see cref="Queued"/> appends
    /// the order instead of replacing the current one, which is what a shift-click does.</summary>
    public sealed class MoveOrderCommand : ISimulationCommand
    {
        public MoveOrderCommand(EntityId actor, SimulationVector2 destination, bool queued = false)
        {
            Actor = actor;
            Destination = destination;
            Queued = queued;
        }

        public EntityId Actor { get; }

        public SimulationVector2 Destination { get; }

        public bool Queued { get; }

        ushort ISimulationCommand.TypeId => 3;
    }
}
