using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal sealed class ManagedJoint
    {
        public int Generation;
        public bool Occupied;
        public AuraJointType Type;
        public int BodyA;
        public int BodyB;
        public AuraVector3 LocalAnchorA;
        public AuraVector3 LocalAnchorB;
        public AuraVector3 AxisLocalA = AuraVector3.UnitY;
        public AuraVector3 AxisLocalB = AuraVector3.UnitY;
        public AuraVector3 NormalLocalA = AuraVector3.UnitZ;
        public AuraVector3 NormalLocalB = AuraVector3.UnitZ;
        public AuraVector3 FixedPoint;
        public float Distance;
        public bool EnableLimit;
        public float MinLimit;
        public float MaxLimit;
        public float SwingLimit;
        public bool MotorEnabled;
        public float MotorTargetVelocity;
        public float MaxMotorForce;
        public float SpringFrequency;
        public float SpringDamping;
        public AuraVector3 Accumulated;
    }
}
