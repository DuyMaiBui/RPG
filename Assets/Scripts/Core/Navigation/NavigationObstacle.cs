using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public readonly struct NavigationObstacle : IEquatable<NavigationObstacle>
    {
        public NavigationObstacle(int id, SimulationVector2 center, SimulationVector2 halfExtents)
        {
            if (halfExtents.X < 0f || halfExtents.Y < 0f)
                throw new ArgumentOutOfRangeException(nameof(halfExtents));

            Id = id;
            Center = center;
            HalfExtents = halfExtents;
        }

        public int Id { get; }
        public SimulationVector2 Center { get; }
        public SimulationVector2 HalfExtents { get; }

        public bool Equals(NavigationObstacle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is NavigationObstacle other && Equals(other);
        public override int GetHashCode() => Id;
    }
}
