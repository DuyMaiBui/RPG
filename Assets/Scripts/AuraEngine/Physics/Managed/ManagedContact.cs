using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal sealed class ManagedContact
    {
        public int BodyA;
        public int BodyB;
        public PhysicsBodyId BodyAId;
        public PhysicsBodyId BodyBId;
        public PhysicsShapeId ShapeAId;
        public PhysicsShapeId ShapeBId;
        public AuraVector3 Point;
        public AuraVector3 Normal;
        public float Depth;
        public float Impulse;
        public bool IsTrigger;
    }
}
