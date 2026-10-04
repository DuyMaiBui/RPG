#include "aura_jolt_internal.h"

#include <cmath>

namespace aura
{

/* Package E: world gravity, runtime motion quality (CCD) and the Jolt side of the force field zones.
   The zone math lives in the backend-neutral ForceFieldRegistry. */

void JoltWorld::Impl::ApplyForceFields(float deltaTime)
{
    if (fields.Empty())
        return;

    JPH::BodyInterface& bi = physics.GetBodyInterface();
    const bool planar = mode == AURA_MODE_PLANE_2D;

    /* Slot order is body creation order, so every step visits bodies in the same sequence. */
    for (const Slot& slot : slots)
    {
        if (!slot.occupied || !slot.enabled || slot.body == nullptr || !slot.body->IsDynamic())
            continue;
        const JPH::MotionProperties* motion = slot.body->GetMotionPropertiesUnchecked();
        if (motion == nullptr || motion->GetInverseMass() <= 0.0f)
            continue;

        ForceFieldRegistry::BodyView view{};
        view.position = ToAura(slot.body->GetCenterOfMassPosition());
        view.velocity = ToAura(slot.body->GetLinearVelocity());
        view.mass = 1.0f / motion->GetInverseMass();
        view.gravityScale = motion->GetGravityFactor();
        view.layer = slot.body->GetObjectLayer();

        const AuraVec3 next = fields.Integrate(view, deltaTime, planar);
        if (next.x == view.velocity.x && next.y == view.velocity.y && next.z == view.velocity.z)
            continue;
        bi.SetLinearVelocity(slot.id, ToVec3(next));
        bi.ActivateBody(slot.id);
    }
}

AuraResultCode JoltWorld::SetWorldGravity(const AuraVec3& gravity)
{
    if (!std::isfinite(gravity.x) || !std::isfinite(gravity.y) || !std::isfinite(gravity.z))
        return AURA_INVALID_DEFINITION;

    impl_->gravity = impl_->mode == AURA_MODE_PLANE_2D ? AuraVec3{ gravity.x, gravity.y, 0.0f } : gravity;
    impl_->physics.SetGravity(ToVec3(impl_->gravity));

    /* Resting bodies sleep through the change, so wake every dynamic body. */
    JPH::BodyInterface& bi = impl_->physics.GetBodyInterface();
    for (const Impl::Slot& slot : impl_->slots)
        if (slot.occupied && slot.enabled && bi.GetMotionType(slot.id) == JPH::EMotionType::Dynamic)
            bi.ActivateBody(slot.id);
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::GetWorldGravity(AuraVec3* outGravity) const
{
    if (outGravity == nullptr)
        return AURA_INVALID_DEFINITION;
    *outGravity = impl_->gravity;
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SetBodyCollisionDetection(AuraBodyHandle body, int32_t collisionDetection)
{
    AuraResultCode result;
    Impl::Slot* slot = impl_->Resolve(body, Impl::kNeedMovable, result);
    if (slot == nullptr)
        return result;
    if (collisionDetection < 0 || collisionDetection > 1 || slot->body == nullptr || slot->body->GetMotionPropertiesUnchecked() == nullptr)
        return AURA_INVALID_DEFINITION;

    impl_->physics.GetBodyInterface().SetMotionQuality(
        slot->id, collisionDetection == 1 ? JPH::EMotionQuality::LinearCast : JPH::EMotionQuality::Discrete);
    return AURA_SUCCESS;
}

ForceFieldRegistry* JoltWorld::ForceFields()
{
    return &impl_->fields;
}

} // namespace aura
