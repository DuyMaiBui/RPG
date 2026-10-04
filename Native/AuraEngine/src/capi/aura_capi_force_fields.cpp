#include "aura_capi_internal.h"

#include "aura_force_fields.h"

/* Keep in lockstep with the managed mirror NativeForceFieldDesc. */
static_assert(sizeof(AuraForceFieldDesc) == 104, "AuraForceFieldDesc layout changed");

extern "C"
{

AuraResultCode Aura_SetWorldGravity(AuraWorldHandle world, AuraVec3 gravity)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetWorldGravity(gravity);
}

AuraResultCode Aura_GetWorldGravity(AuraWorldHandle world, AuraVec3* outGravity)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (outGravity == nullptr)
        return AURA_INVALID_DEFINITION;
    return instance->GetWorldGravity(outGravity);
}

AuraResultCode Aura_SetBodyCollisionDetection(AuraWorldHandle world, AuraBodyHandle body, int32_t collisionDetection)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (collisionDetection < 0 || collisionDetection > 1)
        return AURA_INVALID_DEFINITION;
    return instance->SetBodyCollisionDetection(body, collisionDetection);
}

AuraResultCode Aura_CreateForceField(AuraWorldHandle world, const AuraForceFieldDesc* desc, AuraForceFieldHandle* outField)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (desc == nullptr || outField == nullptr)
        return AURA_INVALID_DEFINITION;
    aura::ForceFieldRegistry* fields = instance->ForceFields();
    return fields == nullptr ? AURA_UNSUPPORTED_OPERATION : fields->Create(*desc, outField);
}

AuraResultCode Aura_UpdateForceField(AuraWorldHandle world, AuraForceFieldHandle field, const AuraForceFieldDesc* desc)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (desc == nullptr)
        return AURA_INVALID_DEFINITION;
    aura::ForceFieldRegistry* fields = instance->ForceFields();
    return fields == nullptr ? AURA_UNSUPPORTED_OPERATION : fields->Update(field, *desc);
}

AuraResultCode Aura_DestroyForceField(AuraWorldHandle world, AuraForceFieldHandle field)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    aura::ForceFieldRegistry* fields = instance->ForceFields();
    return fields == nullptr ? AURA_UNSUPPORTED_OPERATION : fields->Destroy(field);
}

} // extern "C"
