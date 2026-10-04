#include "aura_capi_internal.h"

extern "C"
{

AuraResultCode Aura_SetJointTarget(AuraWorldHandle world, uint64_t joint, AuraVec3 target)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetJointTarget(joint, target);
}

}
