#include "aura_capi_internal.h"

extern "C"
{

AuraResultCode Aura_CreateVehicle(AuraWorldHandle world, const AuraVehicleDesc* desc, AuraVehicleHandle* outVehicle)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || desc == nullptr || outVehicle == nullptr)
        return AURA_INVALID_WORLD;
    return instance->CreateVehicle(*desc, outVehicle);
}

AuraResultCode Aura_DestroyVehicle(AuraWorldHandle world, AuraVehicleHandle vehicle)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->DestroyVehicle(vehicle);
}

AuraResultCode Aura_SetVehicleInput(AuraWorldHandle world, AuraVehicleHandle vehicle, float forward, float steering, float brake, float handBrake)
{
    auto* instance = aura::ToWorld(world);
    return instance == nullptr ? AURA_INVALID_WORLD : instance->SetVehicleInput(vehicle, forward, steering, brake, handBrake);
}

AuraResultCode Aura_GetVehicleWheelState(AuraWorldHandle world, AuraVehicleHandle vehicle, uint32_t wheelIndex, AuraVehicleWheelState* outState)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || outState == nullptr)
        return AURA_INVALID_WORLD;
    return instance->GetVehicleWheelState(vehicle, wheelIndex, outState);
}

}
