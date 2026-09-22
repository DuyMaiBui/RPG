using RPG.Simulation.Contracts;

namespace RPG.Simulation.Contracts
{
    public sealed class MoveIntentCommand : ISimulationCommand
    {
        public MoveIntentCommand(SimulationVector2 direction) => Direction = direction;

        public SimulationVector2 Direction { get; }
        ushort ISimulationCommand.TypeId => 2;
    }
}
