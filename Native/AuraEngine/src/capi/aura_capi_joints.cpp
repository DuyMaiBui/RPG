#include "aura_capi_internal.h"

extern "C"
{

AuraResultCode Aura_CreateJoint(AuraWorldHandle world, const AuraJointDesc* desc, uint64_t* outJoint)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || desc == nullptr || outJoint == nullptr)
        return AURA_INVALID_WORLD;
    return instance->CreateJoint(*desc, outJoint);
}

AuraResultCode Aura_DestroyJoint(AuraWorldHandle world, uint64_t joint)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    return instance->DestroyJoint(joint);
}

AuraResultCode Aura_HasJoint(AuraWorldHandle world, uint64_t joint, uint8_t* outHas)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || outHas == nullptr)
        return AURA_INVALID_WORLD;
    *outHas = instance->HasJoint(joint) ? 1 : 0;
    return AURA_SUCCESS;
}

} // extern "C"
