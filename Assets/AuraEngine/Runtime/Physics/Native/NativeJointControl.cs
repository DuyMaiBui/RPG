using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    /* IPhysicsJointControl over the v10 C ABI. Owned by NativePhysicsWorld, which
       invalidates it on dispose so a stale control never reaches a freed world. */
    internal sealed partial class NativeJointControl : IPhysicsJointControl
    {
        private readonly NativeWorldHandle _world;
        private bool _invalid;

        internal NativeJointControl(NativeWorldHandle world)
        {
            _world = world;
        }

        internal void Invalidate() => _invalid = true;

        private static ulong ToHandle(AuraJointId joint) =>
            ((ulong)(uint)joint.Generation << 32) | (uint)joint.Index;

        AuraResult IPhysicsJointControl.SetMotor(AuraJointId joint, in AuraJointMotorDefinition motor)
        {
            if (_invalid)
                return AuraResult.InvalidWorld;
            if (!joint.IsValid)
                return AuraResult.InvalidHandle;

            var native = NativeJointMotorDesc.From(motor);
            return (AuraResult)NativeMethods.Aura_SetJointMotor(_world, ToHandle(joint), ref native);
        }

        AuraResult IPhysicsJointControl.SetLimits(AuraJointId joint, bool enabled, float minLimit, float maxLimit)
        {
            if (_invalid)
                return AuraResult.InvalidWorld;
            if (!joint.IsValid)
                return AuraResult.InvalidHandle;

            return (AuraResult)NativeMethods.Aura_SetJointLimits(_world, ToHandle(joint), (byte)(enabled ? 1 : 0), minLimit, maxLimit);
        }

        AuraResult IPhysicsJointControl.SetBreakThreshold(AuraJointId joint, float maxForce, float maxTorque)
        {
            if (_invalid)
                return AuraResult.InvalidWorld;
            if (!joint.IsValid)
                return AuraResult.InvalidHandle;

            return (AuraResult)NativeMethods.Aura_SetJointBreakThreshold(_world, ToHandle(joint), maxForce, maxTorque);
        }

        AuraResult IPhysicsJointControl.IsBroken(AuraJointId joint, out bool broken)
        {
            broken = false;
            if (_invalid)
                return AuraResult.InvalidWorld;
            if (!joint.IsValid)
                return AuraResult.InvalidHandle;

            var result = (AuraResult)NativeMethods.Aura_IsJointBroken(_world, ToHandle(joint), out var native);
            broken = result == AuraResult.Success && native != 0;
            return result;
        }

        AuraResult IPhysicsJointControl.GetFeedback(AuraJointId joint, out AuraJointFeedback feedback)
        {
            feedback = default;
            if (_invalid)
                return AuraResult.InvalidWorld;
            if (!joint.IsValid)
                return AuraResult.InvalidHandle;

            var result = (AuraResult)NativeMethods.Aura_GetJointFeedback(_world, ToHandle(joint), out var native);
            if (result == AuraResult.Success)
                feedback = native.ToManaged();
            return result;
        }
    }
}
