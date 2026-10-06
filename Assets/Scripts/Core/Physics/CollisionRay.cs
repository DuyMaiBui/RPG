using RPG.Simulation.Contracts;

namespace RPG.Core.Physics
{
    /// <summary>A world-space ray used by collision queries. Direction does not need to be normalised; the query
    /// normalises it internally.</summary>
    public readonly struct CollisionRay
    {
        public CollisionRay(SimulationVector2 origin, SimulationVector2 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        public SimulationVector2 Origin { get; }

        public SimulationVector2 Direction { get; }
    }
}
