using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal sealed class ManagedShape
    {
        public AuraShapeType Type;
        public AuraPose LocalPose;
        public bool IsTrigger;
        public float Friction;
        public float Restitution;
        public AuraVector3 HalfExtents;
        public float Radius;
        public float Height;
    }
}
