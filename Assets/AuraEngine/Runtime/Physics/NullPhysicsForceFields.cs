using AuraEngine.Core;

namespace AuraEngine.Physics
{
    /* Force fields for backends without AuraPhysicsCapabilities.ForceFields. */
    public sealed class NullPhysicsForceFields : IPhysicsForceFields
    {
        public static readonly NullPhysicsForceFields Instance = new NullPhysicsForceFields();

        private NullPhysicsForceFields()
        {
        }

        AuraResult IPhysicsForceFields.SetGravity(AuraVector3 gravity) => AuraResult.UnsupportedOperation;

        AuraResult IPhysicsForceFields.GetGravity(out AuraVector3 gravity)
        {
            gravity = default;
            return AuraResult.UnsupportedOperation;
        }

        AuraForceFieldId IPhysicsForceFields.CreateField(in AuraForceFieldDefinition definition) => AuraForceFieldId.Invalid;

        AuraResult IPhysicsForceFields.UpdateField(AuraForceFieldId field, in AuraForceFieldDefinition definition) => AuraResult.UnsupportedOperation;

        AuraResult IPhysicsForceFields.DestroyField(AuraForceFieldId field) => AuraResult.UnsupportedOperation;
    }
}
