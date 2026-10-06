namespace RPG.Simulation.Contracts
{
    /// <summary>Order one owned actor to move to a point, engaging enemies met on the way.</summary>
    public sealed class AttackMoveOrderCommand : ISimulationCommand
    {
        public AttackMoveOrderCommand(EntityId actor, SimulationVector2 destination, bool queued = false)
        {
            Actor = actor;
            Destination = destination;
            Queued = queued;
        }

        public EntityId Actor { get; }

        public SimulationVector2 Destination { get; }

        public bool Queued { get; }

        ushort ISimulationCommand.TypeId => 4;
    }
}
