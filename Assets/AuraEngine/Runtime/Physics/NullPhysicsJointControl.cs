using AuraEngine.Core;

namespace AuraEngine.Physics
{
    /* Joint control for backends without runtime mutation: every call reports
       UnsupportedOperation rather than silently doing nothing. */
    public sealed class NullPhysicsJointControl : IPhysicsJointControl
    {
        public static readonly NullPhysicsJointControl Instance = new NullPhysicsJointControl();

        private NullPhysicsJointControl()
        {
        }

        AuraResult IPhysicsJointControl.SetMotor(AuraJointId joint, in AuraJointMotorDefinition motor) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsJointControl.SetLimits(AuraJointId joint, bool enabled, float minLimit, float maxLimit) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsJointControl.SetBreakThreshold(AuraJointId joint, float maxForce, float maxTorque) => AuraResult.UnsupportedOperation;

        AuraResult IPhysicsJointControl.IsBroken(AuraJointId joint, out bool broken)
        {
            broken = false;
            return AuraResult.UnsupportedOperation;
        }

        AuraResult IPhysicsJointControl.GetFeedback(AuraJointId joint, out AuraJointFeedback feedback)
        {
            feedback = default;
            return AuraResult.UnsupportedOperation;
        }
    }
}
