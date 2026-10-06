using RPG.Simulation.Contracts;

namespace RPG.Core.Physics
{
    /// <summary>Result of a raycast or circle sweep. Point and Normal are world space; Normal is a unit vector pointing
    /// from the hit surface back toward the query origin. Distance is how far the query travelled before contact, in
    /// world units, and is zero when the query already starts overlapping the shape.</summary>
    public readonly struct CollisionHit
    {
        public CollisionHit(SimulationVector2 point, SimulationVector2 normal, float distance)
        {
            Point = point;
            Normal = normal;
            Distance = distance;
        }

        public SimulationVector2 Point { get; }

        public SimulationVector2 Normal { get; }

        public float Distance { get; }
    }
}
