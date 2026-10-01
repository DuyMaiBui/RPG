using AuraEngine.Core;

namespace AuraEngine.Tests
{
    internal sealed class FakePhysicsBody
    {
        public int Generation;
        public bool Occupied;
        public AuraBodyType Type;
        public AuraPose Pose;
        public AuraVector3 LinearVelocity;
        public AuraVector3 AngularVelocity;
        public float Mass = 1f;
        public bool IsAwake = true;
        public AuraShapeType ShapeType;
        public float Radius;
        public AuraVector3 HalfExtents;
        public bool IsTrigger;
    }
}
