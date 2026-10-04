using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    internal sealed partial class NativeJointControl : IPhysicsJointAxisControl
    {
        AuraResult IPhysicsJointAxisControl.SetAxisLimits(AuraJointId joint, int axis, in AuraJointAxisLimit limit)
        {
            if (_invalid)
                return AuraResult.InvalidWorld;
            if (!joint.IsValid)
                return AuraResult.InvalidHandle;
            if (axis < 0 || axis > 5)
                return AuraResult.InvalidDefinition;

            var native = NativeJointAxisLimit.From(limit);
            return (AuraResult)NativeMethods.Aura_SetJointAxisLimits(_world, ToHandle(joint), (uint)axis, ref native);
        }

        AuraResult IPhysicsJointAxisControl.SetAxisMotor(AuraJointId joint, int axis, in AuraJointMotorDefinition motor)
        {
            if (_invalid)
                return AuraResult.InvalidWorld;
            if (!joint.IsValid)
                return AuraResult.InvalidHandle;
            if (axis < 0 || axis > 5)
                return AuraResult.InvalidDefinition;

            var native = NativeJointMotorDesc.From(motor);
            return (AuraResult)NativeMethods.Aura_SetJointAxisMotor(_world, ToHandle(joint), (uint)axis, ref native);
        }
    }
}
