using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class PositionComponent : IActorComponent
    {
        public PositionComponent(SimulationVector2 position)
        {
            Position = position;
        }

        public SimulationVector2 Position { get; set; }
    }
}
