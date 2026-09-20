using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class AttackCommand : ISimulationCommand
    {
        public AttackCommand(EntityId target) => Target = target;
        public EntityId Target { get; }
        ushort ISimulationCommand.TypeId => 1;
    }
}
