using AuraEngine.Core;

namespace AuraEngine.Physics
{
    /* Body control for backends without runtime mutation: every call reports
       UnsupportedOperation rather than silently doing nothing. */
    public sealed class NullPhysicsBodyControl : IPhysicsBodyControl
    {
        public static readonly NullPhysicsBodyControl Instance = new NullPhysicsBodyControl();

        private NullPhysicsBodyControl()
        {
        }

        AuraResult IPhysicsBodyControl.SetLinearVelocity(PhysicsBodyId body, AuraVector3 velocity) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.SetAngularVelocity(PhysicsBodyId body, AuraVector3 velocity) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.AddForce(PhysicsBodyId body, AuraVector3 force) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.AddImpulse(PhysicsBodyId body, AuraVector3 impulse) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.AddTorque(PhysicsBodyId body, AuraVector3 torque) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.AddAngularImpulse(PhysicsBodyId body, AuraVector3 impulse) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.SetPose(PhysicsBodyId body, in AuraPose pose, bool zeroVelocity) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.SetGravityScale(PhysicsBodyId body, float gravityScale) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.SetFriction(PhysicsBodyId body, float friction) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.SetRestitution(PhysicsBodyId body, float restitution) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.SetBodyType(PhysicsBodyId body, AuraBodyType type) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.SetLayer(PhysicsBodyId body, AuraPhysicsLayer layer, AuraPhysicsLayerMask collisionMask) => AuraResult.UnsupportedOperation;
        AuraResult IPhysicsBodyControl.SetEnabled(PhysicsBodyId body, bool enabled) => AuraResult.UnsupportedOperation;

        AuraResult IPhysicsBodyControl.SetCollisionDetection(PhysicsBodyId body, AuraBodyCollisionDetection mode) => AuraResult.UnsupportedOperation;

        AuraResult IPhysicsBodyControl.IsEnabled(PhysicsBodyId body, out bool enabled)
        {
            enabled = false;
            return AuraResult.UnsupportedOperation;
        }
    }
}
