namespace RPG.Simulation.Contracts
{
    /// <summary>Cancel one owned actor's orders. This is not an order: it clears the queue, so the actor falls back to
    /// its own behaviour on the next tick.</summary>
    public sealed class StopOrderCommand : ISimulationCommand
    {
        public StopOrderCommand(EntityId actor) => Actor = actor;

        public EntityId Actor { get; }

        ushort ISimulationCommand.TypeId => 8;
    }
}
