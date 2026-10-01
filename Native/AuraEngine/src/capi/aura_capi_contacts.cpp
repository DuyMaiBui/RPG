#include "aura_capi_internal.h"

extern "C"
{

AuraResultCode Aura_CopyContacts(AuraWorldHandle world, AuraContact* buffer, uint32_t capacity, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || outCount == nullptr)
        return AURA_INVALID_WORLD;
    *outCount = instance->CopyContacts(buffer, capacity);
    return AURA_SUCCESS;
}

AuraResultCode Aura_SetSurfaceVelocity(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 velocity)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    return instance->SetSurfaceVelocity(body, velocity);
}

} // extern "C"
