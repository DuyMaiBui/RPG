#include "aura_jolt_internal.h"

#include <cmath>

namespace aura
{
namespace
{
bool IsFinite(float value) { return std::isfinite(value); }
bool IsFinite(const AuraVec3& v) { return std::isfinite(v.x) && std::isfinite(v.y) && std::isfinite(v.z); }
} // namespace

JoltWorld::Impl::Slot* JoltWorld::Impl::Resolve(AuraBodyHandle handle, uint32_t requirements, AuraResultCode& result)
{
    Slot* slot = Find(handle);
    if (slot == nullptr)
    {
        result = AURA_INVALID_HANDLE;
        return nullptr;
    }
    if ((requirements & kNeedEnabled) != 0u && !slot->enabled)
    {
        result = AURA_BODY_DISABLED;
        return nullptr;
    }
    if ((requirements & (kNeedMovable | kNeedDynamic)) != 0u)
    {
        const JPH::EMotionType motion = physics.GetBodyInterface().GetMotionType(slot->id);
        if (motion == JPH::EMotionType::Static || ((requirements & kNeedDynamic) != 0u && motion != JPH::EMotionType::Dynamic))
        {
            result = AURA_INVALID_DEFINITION;
            return nullptr;
        }
    }
    result = AURA_SUCCESS;
    return slot;
}

bool JoltWorld::Impl::IsVehicleChassis(AuraBodyHandle handle) const
{
    for (const VehicleSlot& vehicle : vehicleSlots)
        if (vehicle.occupied && vehicle.chassis.index == handle.index && vehicle.chassis.generation == handle.generation)
            return true;
    return false;
}

void JoltWorld::Impl::SetEnabledInternal(Slot& slot, AuraBodyHandle handle, bool enable)
{
    if (slot.enabled == enable)
        return;

    JPH::BodyInterface& bi = physics.GetBodyInterface();
    if (enable)
    {
        bi.AddBody(slot.id, JPH::EActivation::Activate);
    }
    else
    {
        bi.RemoveBody(slot.id);
        std::lock_guard<std::mutex> lock(eventMutex);
        for (auto it = contacts.begin(); it != contacts.end();)
        {
            const bool touchesA = it->second.bodyA.index == handle.index && it->second.bodyA.generation == handle.generation;
            const bool touchesB = it->second.bodyB.index == handle.index && it->second.bodyB.generation == handle.generation;
            it = (touchesA || touchesB) ? contacts.erase(it) : ++it;
        }
    }
    slot.enabled = enable;
    RefreshJointsFor(handle);
}

namespace
{
/* Resting bodies sleep through property changes, so wake them (not static bodies, not disabled ones). */
void Wake(JPH::BodyInterface& bi, bool enabled, const JPH::BodyID& id)
{
    if (enabled && bi.GetMotionType(id) != JPH::EMotionType::Static)
        bi.ActivateBody(id);
}
} // namespace

AuraResultCode JoltWorld::SetLinearVelocity(AuraBodyHandle body, const AuraVec3& velocity)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, Impl::kNeedEnabled | Impl::kNeedMovable, result);
    if (slot == nullptr)
        return result;
    if (!IsFinite(velocity))
        return AURA_INVALID_DEFINITION;
    impl_->physics.GetBodyInterface().SetLinearVelocity(slot->id, ToVec3(velocity));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetAngularVelocity(AuraBodyHandle body, const AuraVec3& velocity)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, Impl::kNeedEnabled | Impl::kNeedMovable, result);
    if (slot == nullptr)
        return result;
    if (!IsFinite(velocity))
        return AURA_INVALID_DEFINITION;
    impl_->physics.GetBodyInterface().SetAngularVelocity(slot->id, ToVec3(velocity));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::AddForce(AuraBodyHandle body, const AuraVec3& force)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, Impl::kNeedEnabled | Impl::kNeedDynamic, result);
    if (slot == nullptr)
        return result;
    if (!IsFinite(force))
        return AURA_INVALID_DEFINITION;
    impl_->physics.GetBodyInterface().AddForce(slot->id, ToVec3(force));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::AddImpulse(AuraBodyHandle body, const AuraVec3& impulse)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, Impl::kNeedEnabled | Impl::kNeedDynamic, result);
    if (slot == nullptr)
        return result;
    if (!IsFinite(impulse))
        return AURA_INVALID_DEFINITION;
    impl_->physics.GetBodyInterface().AddImpulse(slot->id, ToVec3(impulse));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::AddTorque(AuraBodyHandle body, const AuraVec3& torque)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, Impl::kNeedEnabled | Impl::kNeedDynamic, result);
    if (slot == nullptr)
        return result;
    if (!IsFinite(torque))
        return AURA_INVALID_DEFINITION;
    impl_->physics.GetBodyInterface().AddTorque(slot->id, ToVec3(torque));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::AddAngularImpulse(AuraBodyHandle body, const AuraVec3& impulse)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, Impl::kNeedEnabled | Impl::kNeedDynamic, result);
    if (slot == nullptr)
        return result;
    if (!IsFinite(impulse))
        return AURA_INVALID_DEFINITION;
    impl_->physics.GetBodyInterface().AddAngularImpulse(slot->id, ToVec3(impulse));
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetBodyPose(AuraBodyHandle body, const AuraPose& pose, bool zeroVelocity)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, 0u, result);
    if (slot == nullptr)
        return result;

    const JPH::Quat rotation = ToQuat(pose.rotation);
    if (!IsFinite(pose.position) || !IsFinite(pose.rotation.x) || !IsFinite(pose.rotation.y)
        || !IsFinite(pose.rotation.z) || !IsFinite(pose.rotation.w) || rotation.LengthSq() < 1.0e-12f)
        return AURA_INVALID_DEFINITION;

    JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    bi.SetPositionAndRotation(slot->id, ToRVec3(pose.position), rotation.Normalized(),
        slot->enabled ? JPH::EActivation::Activate : JPH::EActivation::DontActivate);
    if (zeroVelocity && bi.GetMotionType(slot->id) != JPH::EMotionType::Static)
        bi.SetLinearAndAngularVelocity(slot->id, JPH::Vec3::sZero(), JPH::Vec3::sZero());
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetGravityScale(AuraBodyHandle body, float gravityScale)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, 0u, result);
    if (slot == nullptr)
        return result;
    if (!IsFinite(gravityScale))
        return AURA_INVALID_DEFINITION;
    if (slot->body == nullptr || slot->body->GetMotionPropertiesUnchecked() == nullptr)
        return AURA_UNSUPPORTED_OPERATION;
    impl_->physics.GetBodyInterface().SetGravityFactor(slot->id, gravityScale);
    Wake(impl_->physics.GetBodyInterface(), slot->enabled, slot->id);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetFriction(AuraBodyHandle body, float friction)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, 0u, result);
    if (slot == nullptr)
        return result;
    if (!IsFinite(friction) || friction < 0.0f)
        return AURA_INVALID_DEFINITION;
    impl_->physics.GetBodyInterface().SetFriction(slot->id, friction);
    Wake(impl_->physics.GetBodyInterface(), slot->enabled, slot->id);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetRestitution(AuraBodyHandle body, float restitution)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, 0u, result);
    if (slot == nullptr)
        return result;
    if (!IsFinite(restitution) || restitution < 0.0f)
        return AURA_INVALID_DEFINITION;
    impl_->physics.GetBodyInterface().SetRestitution(slot->id, restitution);
    Wake(impl_->physics.GetBodyInterface(), slot->enabled, slot->id);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetMotionType(AuraBodyHandle body, AuraBodyType type)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, 0u, result);
    if (slot == nullptr)
        return result;
    if (type != AURA_BODY_STATIC && type != AURA_BODY_DYNAMIC && type != AURA_BODY_KINEMATIC)
        return AURA_INVALID_DEFINITION;
    if (impl_->IsVehicleChassis(body))
        return AURA_INVALID_DEFINITION;

    const JPH::EMotionType motion = type == AURA_BODY_STATIC ? JPH::EMotionType::Static
        : (type == AURA_BODY_KINEMATIC ? JPH::EMotionType::Kinematic : JPH::EMotionType::Dynamic);
    JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    if (bi.GetMotionType(slot->id) == motion)
        return AURA_SUCCESS;
    if (motion != JPH::EMotionType::Static && !slot->canChangeMotion)
        return AURA_UNSUPPORTED_OPERATION;

    bi.SetMotionType(slot->id, motion, JPH::EActivation::Activate);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetBodyLayer(AuraBodyHandle body, AuraLayer layer, uint64_t collisionMask)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, 0u, result);
    if (slot == nullptr)
        return result;
    if (layer >= kMaxLayers)
        return AURA_INVALID_DEFINITION;

    impl_->physics.GetBodyInterface().SetObjectLayer(slot->id, static_cast<JPH::ObjectLayer>(layer));
    const uint32_t index = slot->id.GetIndex();
    if (impl_->simBodyCollisionMasks.size() <= index)
        impl_->simBodyCollisionMasks.resize(index + 1, ~0ull);
    impl_->simBodyCollisionMasks[index] = collisionMask;
    Wake(impl_->physics.GetBodyInterface(), slot->enabled, slot->id);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetBodyEnabled(AuraBodyHandle body, bool enabled)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, 0u, result);
    if (slot == nullptr)
        return result;
    if (impl_->IsVehicleChassis(body))
        return AURA_INVALID_DEFINITION;
    impl_->SetEnabledInternal(*slot, body, enabled);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::IsBodyEnabled(AuraBodyHandle body, bool* outEnabled) const
{
    const Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr || outEnabled == nullptr)
        return AURA_INVALID_HANDLE;
    *outEnabled = slot->enabled;
    return AURA_SUCCESS;
}

} // namespace aura
