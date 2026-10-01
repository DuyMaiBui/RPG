using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPhysicsEvent : IEquatable<AuraPhysicsEvent>
    {
        public AuraPhysicsEvent(
            AuraPhysicsEventType type,
            SimulationEntityId entityA,
            SimulationEntityId entityB,
            PhysicsBodyId bodyA,
            PhysicsBodyId bodyB,
            PhysicsShapeId shapeA,
            PhysicsShapeId shapeB,
            AuraVector3 point,
            AuraVector3 normal,
            float impulse)
        {
            Type = type;
            EntityA = entityA;
            EntityB = entityB;
            BodyA = bodyA;
            BodyB = bodyB;
            ShapeA = shapeA;
            ShapeB = shapeB;
            Point = point;
            Normal = normal;
            Impulse = impulse;
        }

        public AuraPhysicsEventType Type { get; }
        public SimulationEntityId EntityA { get; }
        public SimulationEntityId EntityB { get; }
        public PhysicsBodyId BodyA { get; }
        public PhysicsBodyId BodyB { get; }
        public PhysicsShapeId ShapeA { get; }
        public PhysicsShapeId ShapeB { get; }
        public AuraVector3 Point { get; }
        public AuraVector3 Normal { get; }
        public float Impulse { get; }

        bool IEquatable<AuraPhysicsEvent>.Equals(AuraPhysicsEvent other) =>
            Type == other.Type &&
            EntityA == other.EntityA &&
            EntityB == other.EntityB &&
            BodyA == other.BodyA &&
            BodyB == other.BodyB &&
            ShapeA == other.ShapeA &&
            ShapeB == other.ShapeB &&
            Point.Equals(other.Point) &&
            Normal.Equals(other.Normal) &&
            Impulse.Equals(other.Impulse);

        public override bool Equals(object obj) => obj is AuraPhysicsEvent other && ((IEquatable<AuraPhysicsEvent>)this).Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)Type, EntityA, EntityB, BodyA, BodyB, ShapeA, ShapeB, Point);

        public override string ToString() => $"{Type} {EntityA}<->{EntityB}";
    }
}
