#include "aura_capi_internal.h"

extern "C"
{
AuraResultCode Aura_CreateWater(AuraWorldHandle world, const AuraWaterDesc* desc, AuraWaterHandle* outWater)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || desc == nullptr || outWater == nullptr)
        return AURA_INVALID_WORLD;
    return instance->CreateWater(*desc, outWater);
}

AuraResultCode Aura_DestroyWater(AuraWorldHandle world, AuraWaterHandle water)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->DestroyWater(water);
}

AuraResultCode Aura_SetWaterParameters(AuraWorldHandle world, AuraWaterHandle water, const AuraWaterDesc* desc)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || desc == nullptr)
        return AURA_INVALID_WORLD;
    return instance->SetWaterParameters(water, *desc);
}

AuraResultCode Aura_ApplyWaterStep(AuraWorldHandle world, AuraWaterHandle water, float deltaTime)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->ApplyWaterStep(water, deltaTime);
}
}
