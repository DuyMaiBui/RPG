#include "aura_box2d_internal.h"

namespace aura
{

/* Package E: world gravity, runtime bullet flag (CCD) and the Box2D side of the force field zones.
   The zone math lives in the backend-neutral ForceFieldRegistry. */

void Box2DWorld::Impl::ApplyForceFields(float deltaTime)
{
    if (fields.Empty())
        return;

    /* Slot order is body creation order, so every step visits bodies in the same sequence. */
    for (const Slot& slot : slots)
    {
        if (!slot.occupied || !b2Body_IsValid(slot.body) || !b2Body_IsEnabled(slot.body) || b2Body_GetType(slot.body) != b2_dynamicBody)
            continue;

        ForceFieldRegistry::BodyView view{};
        view.position = ToAura(b2Body_GetWorldCenterOfMass(slot.body));
        view.velocity = ToAura(b2Body_GetLinearVelocity(slot.body));
        view.mass = b2Body_GetMass(slot.body);
        view.gravityScale = b2Body_GetGravityScale(slot.body);

        b2ShapeId shape;
        view.layer = 64u;
        if (b2Body_GetShapes(slot.body, &shape, 1) > 0)
        {
            const uint64_t category = b2Shape_GetFilter(shape).categoryBits;
            for (uint32_t layer = 0; layer < kMaxLayers; ++layer)
            {
                if ((category >> layer) & 1ull)
                {
                    view.layer = layer;
                    break;
                }
            }
        }

        const AuraVec3 next = fields.Integrate(view, deltaTime, true);
        if (next.x == view.velocity.x && next.y == view.velocity.y)
            continue;
        b2Body_SetLinearVelocity(slot.body, b2Vec2{ next.x, next.y });
        Wake(slot.body);
    }
}

AuraResultCode Box2DWorld::SetWorldGravity(const AuraVec3& gravity)
{
    if (!IsFinite(gravity))
        return AURA_INVALID_DEFINITION;

    impl_->gravity = AuraVec3{ gravity.x, gravity.y, 0.0f };
    b2World_SetGravity(impl_->world, b2Vec2{ gravity.x, gravity.y });

    /* Resting bodies sleep through the change, so wake every dynamic body. */
    for (const Impl::Slot& slot : impl_->slots)
        if (slot.occupied && b2Body_IsValid(slot.body))
            Wake(slot.body);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::GetWorldGravity(AuraVec3* outGravity) const
{
    if (outGravity == nullptr)
        return AURA_INVALID_DEFINITION;
    *outGravity = impl_->gravity;
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetBodyCollisionDetection(AuraBodyHandle body, int32_t collisionDetection)
{
    const Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;
    if (collisionDetection < 0 || collisionDetection > 1 || b2Body_GetType(slot->body) == b2_staticBody)
        return AURA_INVALID_DEFINITION;

    b2Body_SetBullet(slot->body, collisionDetection == 1);
    return AURA_SUCCESS;
}

ForceFieldRegistry* Box2DWorld::ForceFields()
{
    return &impl_->fields;
}

} // namespace aura
