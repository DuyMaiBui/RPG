using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPhysicsQueryHit : IEquatable<AuraPhysicsQueryHit>
    {
        public AuraPhysicsQueryHit(
            SimulationEntityId entity,
            PhysicsBodyId body,
            PhysicsShapeId shape,
            float distance,
            AuraVector3 point,
            AuraVector3 normal)
        {
            Entity = entity;
            Body = body;
            Shape = shape;
            Distance = distance;
            Point = point;
            Normal = normal;
        }

        public SimulationEntityId Entity { get; }
        public PhysicsBodyId Body { get; }
        public PhysicsShapeId Shape { get; }
        public float Distance { get; }
        public AuraVector3 Point { get; }
        public AuraVector3 Normal { get; }

        bool IEquatable<AuraPhysicsQueryHit>.Equals(AuraPhysicsQueryHit other) =>
            Entity == other.Entity &&
            Body == other.Body &&
            Shape == other.Shape &&
            Distance.Equals(other.Distance) &&
            Point.Equals(other.Point) &&
            Normal.Equals(other.Normal);

        public override bool Equals(object obj) => obj is AuraPhysicsQueryHit other && ((IEquatable<AuraPhysicsQueryHit>)this).Equals(other);

        public override int GetHashCode() => HashCode.Combine(Entity, Body, Shape, Distance, Point, Normal);

        public override string ToString() => $"entity={Entity} body={Body} distance={Distance}";
    }
}
