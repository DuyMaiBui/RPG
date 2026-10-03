#include "aura_capi_internal.h"

AuraResultCode Aura_CreateSoftBody(AuraWorldHandle world, const AuraSoftBodyDesc* desc, AuraSoftBodyHandle* outSoftBody)
{
    aura::IWorld* instance = aura::ToWorld(world);
    if (instance == nullptr || desc == nullptr || outSoftBody == nullptr)
        return instance == nullptr ? AURA_INVALID_WORLD : AURA_INVALID_DEFINITION;
    return instance->CreateSoftBody(*desc, outSoftBody);
}

AuraResultCode Aura_DestroySoftBody(AuraWorldHandle world, AuraSoftBodyHandle softBody)
{
    aura::IWorld* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->DestroySoftBody(softBody);
}

AuraResultCode Aura_GetSoftBodyState(AuraWorldHandle world, AuraSoftBodyHandle softBody, float* vertexPositions, uint32_t vertexCapacity, AuraSoftBodyState* outState)
{
    aura::IWorld* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    return instance->GetSoftBodyState(softBody, vertexPositions, vertexCapacity, outState);
}
