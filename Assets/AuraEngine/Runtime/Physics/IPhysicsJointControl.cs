using AuraEngine.Core;

namespace AuraEngine.Physics
{
    /* Runtime joint mutation. Support by joint type (other types return UnsupportedOperation):
         SetMotor / SetLimits: Hinge, Slider. Box2D (2D) has no position motor.
         SetBreakThreshold: Fixed, Point, Hinge, Slider, Distance, Spring (torque only for Fixed, Hinge, Slider).
         IsBroken / GetFeedback: every joint type.
       Stale ids return InvalidHandle; non-finite or out-of-range arguments return InvalidDefinition.
       Limits are relative to the creation pose: hinge -pi..pi, both ends around zero (min <= 0 <= max). */
    public interface IPhysicsJointControl
    {
        AuraResult SetMotor(AuraJointId joint, in AuraJointMotorDefinition motor);

        AuraResult SetLimits(AuraJointId joint, bool enabled, float minLimit, float maxLimit);

        /* A threshold of 0 means unbreakable. A joint exceeding either threshold after a step is removed from
           the simulation and reported by IsBroken; the id stays valid until DestroyJoint. */
        AuraResult SetBreakThreshold(AuraJointId joint, float maxForce, float maxTorque);

        AuraResult IsBroken(AuraJointId joint, out bool broken);

        AuraResult GetFeedback(AuraJointId joint, out AuraJointFeedback feedback);
    }
}
