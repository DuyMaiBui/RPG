using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal struct ManagedContactPoint
    {
        public AuraVector3 Position;
        public float Penetration;
        public int FeatureId;
        public float NormalImpulse;
        public float TangentImpulse1;
        public float TangentImpulse2;
        public float NormalMass;
        public float TangentMass1;
        public float TangentMass2;
    }
}
