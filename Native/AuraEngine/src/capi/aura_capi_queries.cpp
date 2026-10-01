#include "aura_capi_internal.h"

extern "C"
{

AuraResultCode Aura_Raycast(AuraWorldHandle world, const AuraRay* ray, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || ray == nullptr || filter == nullptr || outHit == nullptr || outHasHit == nullptr)
        return AURA_INVALID_WORLD;
    *outHasHit = instance->Raycast(*ray, maxDistance, *filter, outHit) ? 1 : 0;
    return AURA_SUCCESS;
}

AuraResultCode Aura_RaycastAll(AuraWorldHandle world, const AuraRay* ray, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || ray == nullptr || filter == nullptr || buffer == nullptr || outCount == nullptr)
        return AURA_INVALID_WORLD;
    *outCount = instance->RaycastAll(*ray, maxDistance, *filter, buffer, capacity);
    return AURA_SUCCESS;
}

AuraResultCode Aura_OverlapSphere(AuraWorldHandle world, AuraVec3 center, float radius, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || filter == nullptr || buffer == nullptr || outCount == nullptr)
        return AURA_INVALID_WORLD;
    *outCount = instance->OverlapSphere(center, radius, *filter, buffer, capacity);
    return AURA_SUCCESS;
}

} // extern "C"
