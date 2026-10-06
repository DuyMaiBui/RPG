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

        /// <summary>Status-effect speed scale in [0, 1]; 1 when unaffected. Written by the status effect system.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public float EffectiveSpeed => Speed * SpeedMultiplier;
        public SimulationVector2 DesiredDirection { get; set; }
    }
}
