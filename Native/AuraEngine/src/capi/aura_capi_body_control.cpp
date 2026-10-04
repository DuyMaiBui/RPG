#include "aura_capi_internal.h"

extern "C"
{

/* v10 runtime body control. Argument validation that depends on the body
   (stale handle, disabled, motion type) lives in the backend. */

AuraResultCode Aura_SetLinearVelocity(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 velocity)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetLinearVelocity(body, velocity);
}

AuraResultCode Aura_SetAngularVelocity(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 velocity)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetAngularVelocity(body, velocity);
}

AuraResultCode Aura_AddForce(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 force)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->AddForce(body, force);
}

AuraResultCode Aura_AddImpulse(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 impulse)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->AddImpulse(body, impulse);
}

AuraResultCode Aura_AddTorque(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 torque)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->AddTorque(body, torque);
}

AuraResultCode Aura_AddAngularImpulse(AuraWorldHandle world, AuraBodyHandle body, AuraVec3 impulse)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->AddAngularImpulse(body, impulse);
}

AuraResultCode Aura_SetBodyPose(AuraWorldHandle world, AuraBodyHandle body, const AuraPose* pose, uint8_t zeroVelocity)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (pose == nullptr)
        return AURA_INVALID_DEFINITION;
    return instance->SetBodyPose(body, *pose, zeroVelocity != 0);
}

AuraResultCode Aura_SetGravityScale(AuraWorldHandle world, AuraBodyHandle body, float gravityScale)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetGravityScale(body, gravityScale);
}

AuraResultCode Aura_SetFriction(AuraWorldHandle world, AuraBodyHandle body, float friction)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetFriction(body, friction);
}

AuraResultCode Aura_SetRestitution(AuraWorldHandle world, AuraBodyHandle body, float restitution)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetRestitution(body, restitution);
}

AuraResultCode Aura_SetMotionType(AuraWorldHandle world, AuraBodyHandle body, int32_t bodyType)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (bodyType < AURA_BODY_STATIC || bodyType > AURA_BODY_KINEMATIC)
        return AURA_INVALID_DEFINITION;
    return instance->SetMotionType(body, static_cast<AuraBodyType>(bodyType));
}

AuraResultCode Aura_SetBodyLayer(AuraWorldHandle world, AuraBodyHandle body, AuraLayer layer, uint64_t collisionMask)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetBodyLayer(body, layer, collisionMask);
}

AuraResultCode Aura_SetBodyEnabled(AuraWorldHandle world, AuraBodyHandle body, uint8_t enabled)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetBodyEnabled(body, enabled != 0);
}

AuraResultCode Aura_IsBodyEnabled(AuraWorldHandle world, AuraBodyHandle body, uint8_t* outEnabled)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr)
        return AURA_INVALID_WORLD;
    if (outEnabled == nullptr)
        return AURA_INVALID_DEFINITION;
    bool enabled = false;
    const AuraResultCode result = instance->IsBodyEnabled(body, &enabled);
    if (result == AURA_SUCCESS)
        *outEnabled = enabled ? 1 : 0;
    return result;
}

} // extern "C"
