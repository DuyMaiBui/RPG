using AuraEngine.Core;

namespace AuraEngine.Physics.Native
{
    /* IPhysicsForceFields over the C ABI. Owned by NativePhysicsWorld, which invalidates it on dispose so a
       stale handle never reaches a freed world. */
    internal sealed class NativeForceFields : IPhysicsForceFields
    {
        private readonly NativeWorldHandle _world;
        private bool _invalid;

        internal NativeForceFields(NativeWorldHandle world)
        {
            _world = world;
        }

        internal void Invalidate() => _invalid = true;

        AuraResult IPhysicsForceFields.SetGravity(AuraVector3 gravity) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_SetWorldGravity(_world, NativeVector3.From(gravity));

        AuraResult IPhysicsForceFields.GetGravity(out AuraVector3 gravity)
        {
            gravity = default;
            if (_invalid)
                return AuraResult.InvalidWorld;

            var result = (AuraResult)NativeMethods.Aura_GetWorldGravity(_world, out var native);
            if (result == AuraResult.Success)
                gravity = native.ToManaged();
            return result;
        }

        AuraForceFieldId IPhysicsForceFields.CreateField(in AuraForceFieldDefinition definition)
        {
            if (_invalid)
                return AuraForceFieldId.Invalid;

            var desc = NativeForceFieldDesc.From(definition);
            return (AuraResult)NativeMethods.Aura_CreateForceField(_world, ref desc, out var handle) == AuraResult.Success
                ? new AuraForceFieldId(handle.Opaque)
                : AuraForceFieldId.Invalid;
        }

        AuraResult IPhysicsForceFields.UpdateField(AuraForceFieldId field, in AuraForceFieldDefinition definition)
        {
            if (_invalid)
                return AuraResult.InvalidWorld;

            var desc = NativeForceFieldDesc.From(definition);
            return (AuraResult)NativeMethods.Aura_UpdateForceField(_world, new NativeForceFieldHandle { Opaque = field.Value }, ref desc);
        }

        AuraResult IPhysicsForceFields.DestroyField(AuraForceFieldId field) =>
            _invalid ? AuraResult.InvalidWorld : (AuraResult)NativeMethods.Aura_DestroyForceField(_world, new NativeForceFieldHandle { Opaque = field.Value });
    }
}
