using System;

namespace AuraEngine.Core
{
    public readonly struct AuraContact : IEquatable<AuraContact>
    {
        public AuraContact(
            SimulationEntityId entityA,
            SimulationEntityId entityB,
            PhysicsBodyId bodyA,
            PhysicsBodyId bodyB,
            AuraVector3 point,
            AuraVector3 normal,
            float impulse)
        {
            EntityA = entityA;
            EntityB = entityB;
            BodyA = bodyA;
            BodyB = bodyB;
            Point = point;
            Normal = normal;
            Impulse = impulse;
        }

        public SimulationEntityId EntityA { get; }
        public SimulationEntityId EntityB { get; }
        public PhysicsBodyId BodyA { get; }
        public PhysicsBodyId BodyB { get; }
        public AuraVector3 Point { get; }
        public AuraVector3 Normal { get; }
        public float Impulse { get; }

        bool IEquatable<AuraContact>.Equals(AuraContact other) =>
            EntityA == other.EntityA &&
            EntityB == other.EntityB &&
            BodyA == other.BodyA &&
            BodyB == other.BodyB &&
            Point.Equals(other.Point) &&
            Normal.Equals(other.Normal) &&
            Impulse.Equals(other.Impulse);

        public override bool Equals(object obj) => obj is AuraContact other && ((IEquatable<AuraContact>)this).Equals(other);

        public override int GetHashCode() => HashCode.Combine(EntityA, EntityB, BodyA, BodyB, Point, Normal, Impulse);
        public override string ToString() => $"{EntityA}<->{EntityB} impulse={Impulse}";
    }
}
