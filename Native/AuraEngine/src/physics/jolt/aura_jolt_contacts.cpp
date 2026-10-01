#include "aura_jolt_internal.h"

namespace aura
{

/* Contact manifolds, collision/trigger events and conveyor surface velocity.
   Kept together so contact enrichment (per-triangle friction, shape filters,
   active edges) can land in this one translation unit. */
void JoltWorld::Impl::ApplySurfaceVelocity(const JPH::Body& body1, const JPH::Body& body2, JPH::ContactSettings& settings)
{
    const AuraBodyHandle a = EntityHandleBody(body1);
    const AuraBodyHandle b = EntityHandleBody(body2);
    auto ia = surfaceVelocities.find(a.index);
    auto ib = surfaceVelocities.find(b.index);
    if (ia == surfaceVelocities.end() && ib == surfaceVelocities.end())
        return;


    const AuraVec3 velocityA = ia != surfaceVelocities.end() ? ia->second : AuraVec3{ 0.0f, 0.0f, 0.0f };
    const AuraVec3 velocityB = ib != surfaceVelocities.end() ? ib->second : AuraVec3{ 0.0f, 0.0f, 0.0f };

    /* Jolt solves lambda = K^-1 (J v - b) to zero, so body B reaches the
       tangential velocity -b. To carry body B at body A's surface velocity
       we need b = vB_s - vA_s. */
    settings.mRelativeLinearSurfaceVelocity = ToVec3(AuraVec3{ velocityB.x - velocityA.x, velocityB.y - velocityA.y, velocityB.z - velocityA.z });

    /* A moving surface must keep the touching dynamic bodies awake or they
       sleep and stop being carried. */
    if (body1.GetMotionType() == JPH::EMotionType::Dynamic)
        wakeOnStep.insert(a.index);
    if (body2.GetMotionType() == JPH::EMotionType::Dynamic)
        wakeOnStep.insert(b.index);
}

void JoltWorld::Impl::RecordContact(const AuraBodyHandle& a, const AuraBodyHandle& b, uint64_t key, const JPH::ContactManifold& manifold)
{
    if (manifold.mRelativeContactPointsOn1.empty())
        return;

    const JPH::RVec3 point = manifold.GetWorldSpaceContactPointOn1(0);
    AuraContact contact{};
    contact.bodyA = a;
    contact.bodyB = b;
    contact.point = AuraVec3{ static_cast<float>(point.GetX()), static_cast<float>(point.GetY()), static_cast<float>(point.GetZ()) };
    contact.normal = AuraVec3{ manifold.mWorldSpaceNormal.GetX(), manifold.mWorldSpaceNormal.GetY(), manifold.mWorldSpaceNormal.GetZ() };
    contact.penetration = manifold.mPenetrationDepth;
    contact.impulse = 0.0f;

    std::lock_guard<std::mutex> lock(eventMutex);
    contacts[key] = contact;
}

void JoltWorld::Impl::OnContactPersisted(const JPH::Body& body1, const JPH::Body& body2, const JPH::ContactManifold& manifold)
{
    const AuraBodyHandle a = EntityHandleBody(body1);
    const AuraBodyHandle b = EntityHandleBody(body2);
    const uint32_t ia = a.index < b.index ? a.index : b.index;
    const uint32_t ib = a.index < b.index ? b.index : a.index;
    const uint64_t key = (static_cast<uint64_t>(ia) << 32) | ib;
    RecordContact(a, b, key, manifold);
}

void JoltWorld::Impl::OnContactAdded(const JPH::Body& body1, const JPH::Body& body2, const JPH::ContactManifold& manifold)
{
    const bool trigger = body1.IsSensor() || body2.IsSensor();
    const AuraBodyHandle a = EntityHandleBody(body1);
    const AuraBodyHandle b = EntityHandleBody(body2);
    const uint32_t ia = a.index < b.index ? a.index : b.index;
    const uint32_t ib = a.index < b.index ? b.index : a.index;
    const uint64_t key = (static_cast<uint64_t>(ia) << 32) | ib;

    RecordContact(a, b, key, manifold);

    std::lock_guard<std::mutex> lock(eventMutex);
    contactTrigger[key] = trigger;

    AuraPhysicsEvent event{};
    event.type = trigger ? 2 : 0;
    event.bodyA = a;
    event.bodyB = b;
    events.push_back(event);
}

void JoltWorld::Impl::OnContactRemoved(const JPH::SubShapeIDPair& pair)
{
    const AuraBodyHandle a = HandleFromBodyId(pair.GetBody1ID());
    const AuraBodyHandle b = HandleFromBodyId(pair.GetBody2ID());
    if (a.index == 0xFFFFFFFFu || b.index == 0xFFFFFFFFu)
        return;

    const uint32_t ia = a.index < b.index ? a.index : b.index;
    const uint32_t ib = a.index < b.index ? b.index : a.index;
    const uint64_t key = (static_cast<uint64_t>(ia) << 32) | ib;

    const Slot* slotA = Find(a);
    const Slot* slotB = Find(b);
    const bool deactivating =
        (slotA != nullptr && slotA->body != nullptr && !slotA->body->IsActive()) ||
        (slotB != nullptr && slotB->body != nullptr && !slotB->body->IsActive());

    std::lock_guard<std::mutex> lock(eventMutex);
    const bool trigger = contactTrigger.count(key) > 0 && contactTrigger[key];
    contactTrigger.erase(key);
    if (!deactivating)
        contacts.erase(key);

    AuraPhysicsEvent event{};
    event.type = trigger ? 3 : 1;
    event.bodyA = a;
    event.bodyB = b;
    events.push_back(event);
}

uint32_t JoltWorld::CopyContacts(AuraContact* buffer, uint32_t capacity) const
{
    std::lock_guard<std::mutex> lock(impl_->eventMutex);
    uint32_t written = 0;
    for (const auto& entry : impl_->contacts)
    {
        if (written >= capacity)
            break;
        buffer[written++] = entry.second;
    }
    return written;
}

AuraResultCode JoltWorld::SetSurfaceVelocity(AuraBodyHandle body, const AuraVec3& velocity)
{
    Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    std::lock_guard<std::mutex> lock(impl_->eventMutex);
    impl_->surfaceVelocities[body.index] = velocity;
    return AURA_SUCCESS;
}

} // namespace aura
