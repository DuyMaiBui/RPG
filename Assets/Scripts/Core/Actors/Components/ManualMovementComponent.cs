using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class ManualMovementComponent : IActorComponent
    {
        public SimulationVector2 Direction { get; set; }
        public bool IsActive => Direction.LengthSquared > 0.0001f;
    }
}
