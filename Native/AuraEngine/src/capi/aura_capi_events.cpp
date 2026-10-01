#include "aura_capi_internal.h"

extern "C"
{

AuraResultCode Aura_PendingEventCount(AuraWorldHandle world, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || outCount == nullptr)
        return AURA_INVALID_WORLD;
    *outCount = instance->PendingEventCount();
    return AURA_SUCCESS;
}

AuraResultCode Aura_CopyEvents(AuraWorldHandle world, AuraPhysicsEvent* buffer, uint32_t capacity, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || buffer == nullptr || outCount == nullptr)
        return AURA_INVALID_WORLD;
    *outCount = instance->CopyEvents(buffer, capacity);
    return AURA_SUCCESS;
}

} // extern "C"
