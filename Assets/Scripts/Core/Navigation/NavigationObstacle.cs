using System;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;

namespace RPG.Core.Navigation
{
    public readonly struct NavigationObstacle : IEquatable<NavigationObstacle>
    {
        public NavigationObstacle(int id, SimulationVector2 center, SimulationVector2 halfExtents)
            : this(id, center, CollisionShape.Box(halfExtents))
        {
        }

        public NavigationObstacle(int id, SimulationVector2 center, CollisionShape shape)
        {
            Id = id;
            Center = center;
            Shape = shape;
        }

        public int Id { get; }
        public SimulationVector2 Center { get; }
        public CollisionShape Shape { get; }
        public SimulationVector2 HalfExtents => Shape.HalfExtents;

        public bool Equals(NavigationObstacle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is NavigationObstacle other && Equals(other);
        public override int GetHashCode() => Id;
    }
}
