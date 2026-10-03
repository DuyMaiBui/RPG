#include "aura_capi_internal.h"

extern "C"
{
AuraResultCode Aura_CreateRagdoll(AuraWorldHandle world, const AuraRagdollDesc* desc, AuraRagdollHandle* outRagdoll)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr || desc == nullptr ? AURA_INVALID_WORLD : instance->CreateRagdoll(*desc, outRagdoll);
}

AuraResultCode Aura_DestroyRagdoll(AuraWorldHandle world, AuraRagdollHandle ragdoll)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->DestroyRagdoll(ragdoll);
}

AuraResultCode Aura_GetRagdollPose(AuraWorldHandle world, AuraRagdollHandle ragdoll, AuraPose* buffer, uint32_t capacity, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->GetRagdollPose(ragdoll, buffer, capacity, outCount);
}

AuraResultCode Aura_SetRagdollPose(AuraWorldHandle world, AuraRagdollHandle ragdoll, const AuraPose* poses, uint32_t poseCount)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetRagdollPose(ragdoll, poses, poseCount);
}
}
