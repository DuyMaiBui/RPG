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

AuraResultCode Aura_SetJointMotor(AuraWorldHandle world, uint64_t joint, const AuraJointMotorDesc* motor)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (motor == nullptr)
        return AURA_INVALID_DEFINITION;
    return instance->SetJointMotor(joint, *motor);
}

AuraResultCode Aura_SetJointLimits(AuraWorldHandle world, uint64_t joint, uint8_t enabled, float minLimit, float maxLimit)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetJointLimits(joint, enabled != 0, minLimit, maxLimit);
}

AuraResultCode Aura_SetJointBreakThreshold(AuraWorldHandle world, uint64_t joint, float maxForce, float maxTorque)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetJointBreakThreshold(joint, maxForce, maxTorque);
}

AuraResultCode Aura_IsJointBroken(AuraWorldHandle world, uint64_t joint, uint8_t* outBroken)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (outBroken == nullptr)
        return AURA_INVALID_DEFINITION;
    bool broken = false;
    const AuraResultCode result = instance->IsJointBroken(joint, &broken);
    if (result == AURA_SUCCESS)
        *outBroken = broken ? 1 : 0;
    return result;
}

AuraResultCode Aura_GetJointFeedback(AuraWorldHandle world, uint64_t joint, AuraJointFeedback* outFeedback)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (outFeedback == nullptr)
        return AURA_INVALID_DEFINITION;
    return instance->GetJointFeedback(joint, outFeedback);
}

} // extern "C"
