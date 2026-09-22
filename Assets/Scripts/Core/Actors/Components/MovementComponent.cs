using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Actors
{
    public sealed class MovementComponent : IActorComponent
    {
        public MovementComponent(float speed)
        {
            if (speed < 0f) throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
        }

        public float Speed { get; }
        public SimulationVector2 DesiredDirection { get; set; }
    }
}
