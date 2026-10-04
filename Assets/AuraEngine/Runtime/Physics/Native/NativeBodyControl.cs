using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    /* IPhysicsBodyControl over the v10 C ABI. Owned by NativePhysicsWorld, which
       invalidates it on dispose so a stale control never reaches a freed world. */
    internal sealed class NativeBodyControl : IPhysicsBodyControl
    {
        private readonly NativeWorldHandle _world;
        private bool _invalid;

        internal NativeBodyControl(NativeWorldHandle world)
        {
            _world = world;
        }

        internal void Invalidate() => _invalid = true;

        AuraResult IPhysicsBodyControl.SetLinearVelocity(PhysicsBodyId body, AuraVector3 velocity) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetLinearVelocity(_world, NativeBodyHandle.From(body), NativeVector3.From(velocity));

        AuraResult IPhysicsBodyControl.SetAngularVelocity(PhysicsBodyId body, AuraVector3 velocity) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetAngularVelocity(_world, NativeBodyHandle.From(body), NativeVector3.From(velocity));

        AuraResult IPhysicsBodyControl.AddForce(PhysicsBodyId body, AuraVector3 force) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_AddForce(_world, NativeBodyHandle.From(body), NativeVector3.From(force));

        AuraResult IPhysicsBodyControl.AddImpulse(PhysicsBodyId body, AuraVector3 impulse) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_AddImpulse(_world, NativeBodyHandle.From(body), NativeVector3.From(impulse));

        AuraResult IPhysicsBodyControl.AddTorque(PhysicsBodyId body, AuraVector3 torque) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_AddTorque(_world, NativeBodyHandle.From(body), NativeVector3.From(torque));

        AuraResult IPhysicsBodyControl.AddAngularImpulse(PhysicsBodyId body, AuraVector3 impulse) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_AddAngularImpulse(_world, NativeBodyHandle.From(body), NativeVector3.From(impulse));

        AuraResult IPhysicsBodyControl.SetPose(PhysicsBodyId body, in AuraPose pose, bool zeroVelocity)
        {
            if (_invalid)
                return AuraResult.InvalidWorld;

            var native = NativePose.From(pose);
            return (AuraResult)NativeMethods.Aura_SetBodyPose(_world, NativeBodyHandle.From(body), ref native, (byte)(zeroVelocity ? 1 : 0));
        }

        AuraResult IPhysicsBodyControl.SetGravityScale(PhysicsBodyId body, float gravityScale) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetGravityScale(_world, NativeBodyHandle.From(body), gravityScale);

        AuraResult IPhysicsBodyControl.SetFriction(PhysicsBodyId body, float friction) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetFriction(_world, NativeBodyHandle.From(body), friction);

        AuraResult IPhysicsBodyControl.SetRestitution(PhysicsBodyId body, float restitution) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetRestitution(_world, NativeBodyHandle.From(body), restitution);

        AuraResult IPhysicsBodyControl.SetBodyType(PhysicsBodyId body, AuraBodyType type) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetMotionType(_world, NativeBodyHandle.From(body), (int)type);

        AuraResult IPhysicsBodyControl.SetLayer(PhysicsBodyId body, AuraPhysicsLayer layer, AuraPhysicsLayerMask collisionMask) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetBodyLayer(_world, NativeBodyHandle.From(body), (uint)layer.Value, collisionMask.Bits);

        AuraResult IPhysicsBodyControl.SetEnabled(PhysicsBodyId body, bool enabled) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetBodyEnabled(_world, NativeBodyHandle.From(body), (byte)(enabled ? 1 : 0));

        AuraResult IPhysicsBodyControl.SetCollisionDetection(PhysicsBodyId body, AuraBodyCollisionDetection mode) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetBodyCollisionDetection(_world, NativeBodyHandle.From(body), (int)mode);

        AuraResult IPhysicsBodyControl.IsEnabled(PhysicsBodyId body, out bool enabled)
        {
            enabled = false;
            if (_invalid)
                return AuraResult.InvalidWorld;

            var result = (AuraResult)NativeMethods.Aura_IsBodyEnabled(_world, NativeBodyHandle.From(body), out var native);
            enabled = result == AuraResult.Success && native != 0;
            return result;
        }
    }
}
