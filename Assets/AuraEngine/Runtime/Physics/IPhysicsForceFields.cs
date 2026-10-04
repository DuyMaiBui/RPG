using AuraEngine.Core;

namespace AuraEngine.Physics
{
    /* World gravity and force field zones (gravity wells, wind, buoyancy, planet gravity). Members return
       UnsupportedOperation when the backend lacks AuraPhysicsCapabilities.ForceFields and InvalidHandle for
       stale field ids. Fields act on dynamic bodies inside the zone once per step before integration, in
       ascending body then field order; they are configuration and not part of snapshots or the state hash.
       SetGravity wakes sleeping bodies; 2D worlds ignore z. */
    public interface IPhysicsForceFields
    {
        AuraResult SetGravity(AuraVector3 gravity);

        AuraResult GetGravity(out AuraVector3 gravity);

        /* Returns AuraForceFieldId.Invalid when the backend rejects the definition. */
        AuraForceFieldId CreateField(in AuraForceFieldDefinition definition);

        AuraResult UpdateField(AuraForceFieldId field, in AuraForceFieldDefinition definition);

        AuraResult DestroyField(AuraForceFieldId field);
    }
}
