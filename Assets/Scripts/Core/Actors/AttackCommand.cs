using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class AttackCommand : ISimulationCommand
    {
        public AttackCommand(EntityId target) => Target = target;
        public EntityId Target { get; }
        public ushort TypeId => 1;
    }
}
