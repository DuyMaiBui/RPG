using AuraEngine.Core;

namespace AuraEngine.Physics
{
    /* Runtime body mutation. Every member returns InvalidHandle for stale ids,
       InvalidDefinition for non-finite arguments or operations that do not apply
       to the body's motion type (for example a force on a static body),
       BodyDisabled while the body is disabled, and UnsupportedOperation when the
       backend lacks AuraPhysicsCapabilities.BodyControl. 2D worlds ignore the z
       of linear vectors and the x/y of angular vectors. Forces and torques act
       on the next step only. */
    public interface IPhysicsBodyControl
    {
        AuraResult SetLinearVelocity(PhysicsBodyId body, AuraVector3 velocity);

        AuraResult SetAngularVelocity(PhysicsBodyId body, AuraVector3 velocity);

        AuraResult AddForce(PhysicsBodyId body, AuraVector3 force);

        AuraResult AddImpulse(PhysicsBodyId body, AuraVector3 impulse);

        AuraResult AddTorque(PhysicsBodyId body, AuraVector3 torque);

        AuraResult AddAngularImpulse(PhysicsBodyId body, AuraVector3 impulse);

        /* Teleports the body. zeroVelocity also clears linear and angular velocity. */
        AuraResult SetPose(PhysicsBodyId body, in AuraPose pose, bool zeroVelocity);

        AuraResult SetGravityScale(PhysicsBodyId body, float gravityScale);

        AuraResult SetFriction(PhysicsBodyId body, float friction);

        AuraResult SetRestitution(PhysicsBodyId body, float restitution);

        /* Bodies made of triangle mesh, height field or plane shapes stay static (UnsupportedOperation). */
        AuraResult SetBodyType(PhysicsBodyId body, AuraBodyType type);

        /* collisionMask is applied on top of the world collision matrix. */
        AuraResult SetLayer(PhysicsBodyId body, AuraPhysicsLayer layer, AuraPhysicsLayerMask collisionMask);

        /* Removes the body from (or returns it to) the simulation without destroying its handle. */
        AuraResult SetEnabled(PhysicsBodyId body, bool enabled);

        AuraResult IsEnabled(PhysicsBodyId body, out bool enabled);

        /* Switches continuous collision detection for fast bodies (Jolt linear cast, Box2D bullet).
           Static bodies are InvalidDefinition. */
        AuraResult SetCollisionDetection(PhysicsBodyId body, AuraBodyCollisionDetection mode);
    }
}
