#include "aura_jolt_internal.h"

namespace aura
{

AuraResultCode JoltWorld::CreateVehicle(const AuraVehicleDesc& desc, AuraVehicleHandle* outVehicle)
{
    if (outVehicle == nullptr)
        return AURA_INVALID_HANDLE;

    Impl::Slot* chassis = impl_->Find(desc.chassis);
    if (chassis == nullptr || chassis->body == nullptr || chassis->body->GetMotionType() != JPH::EMotionType::Dynamic)
        return AURA_INVALID_HANDLE;

    jolt_vehicle::VehicleConfig config;
    config.up = ToVec3(desc.up);
    config.forward = ToVec3(desc.forward);
    for (uint32_t index = 0; index < 4; ++index)
        config.wheelPositions[index] = ToVec3(desc.wheelPositions[index]);
    config.wheelRadius = desc.wheelRadius;
    config.wheelWidth = desc.wheelWidth;
    config.suspensionMinLength = desc.suspensionMinLength;
    config.suspensionMaxLength = desc.suspensionMaxLength;
    config.suspensionFrequency = desc.suspensionFrequency;
    config.suspensionDamping = desc.suspensionDamping;
    config.maxSteerAngle = desc.maxSteerAngle;
    config.maxPitchRollAngle = desc.maxPitchRollAngle;
    config.maxEngineTorque = desc.maxEngineTorque;
    config.wheelObjectLayer = desc.wheelObjectLayer;

    std::string error;
    std::unique_ptr<jolt_vehicle::Vehicle> vehicle = jolt_vehicle::Vehicle::Create(impl_->physics, *chassis->body, config, &error);
    if (vehicle == nullptr)
        return AURA_INVALID_DEFINITION;

    int index;
    if (!impl_->freeVehicleSlots.empty())
    {
        index = impl_->freeVehicleSlots.back();
        impl_->freeVehicleSlots.pop_back();
    }
    else
    {
        index = static_cast<int>(impl_->vehicleSlots.size());
        impl_->vehicleSlots.emplace_back();
    }

    Impl::VehicleSlot& slot = impl_->vehicleSlots[index];
    slot.occupied = true;
    slot.vehicle = std::move(vehicle);
    slot.chassis = desc.chassis;
    *outVehicle = Impl::MakeVehicleHandle(slot, index);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::DestroyVehicle(AuraVehicleHandle vehicle)
{
    Impl::VehicleSlot* slot = impl_->FindVehicle(vehicle);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    slot->vehicle.reset();
    slot->occupied = false;
    slot->generation += 1;
    impl_->freeVehicleSlots.push_back(static_cast<int>(vehicle.opaque & 0xFFFFFFFFull));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetVehicleInput(AuraVehicleHandle vehicle, float forward, float steering, float brake, float handBrake)
{
    Impl::VehicleSlot* slot = impl_->FindVehicle(vehicle);
    if (slot == nullptr || slot->vehicle == nullptr)
        return AURA_INVALID_HANDLE;
    slot->vehicle->SetDriverInput(forward, steering, brake, handBrake);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::GetVehicleWheelState(AuraVehicleHandle vehicle, uint32_t wheelIndex, AuraVehicleWheelState* outState) const
{
    const Impl::VehicleSlot* slot = impl_->FindVehicle(vehicle);
    if (slot == nullptr || slot->vehicle == nullptr || outState == nullptr)
        return AURA_INVALID_HANDLE;

    jolt_vehicle::WheelState state;
    if (!slot->vehicle->GetWheelState(wheelIndex, state))
        return AURA_INVALID_HANDLE;
    outState->hasContact = state.hasContact ? 1 : 0;
    outState->suspensionLength = state.suspensionLength;
    outState->steerAngle = state.steerAngle;
    outState->angularVelocity = state.angularVelocity;
    outState->contactNormal = ToAura(state.contactNormal);
    return AURA_SUCCESS;
}

} // namespace aura
