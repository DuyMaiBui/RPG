#include "aura_box2d_internal.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <vector>

namespace aura
{

struct RayContext
{
    AuraQueryHit* buffer = nullptr;
    uint32_t capacity = 0;
    uint32_t count = 0;
    float maxDistance = 0.0f;
};


namespace
{
float RayCallback(b2ShapeId shapeId, b2Vec2 point, b2Vec2 normal, float fraction, void* context)
{
    auto* ctx = static_cast<RayContext*>(context);
    if (ctx->count >= ctx->capacity)
        return fraction;

    const b2BodyId body = b2Shape_GetBody(shapeId);
    const AuraBodyHandle handle = Decode(b2Body_GetUserData(body));
    if (handle.index == 0xFFFFFFFFu)
        return fraction;

    AuraQueryHit& hit = ctx->buffer[ctx->count];
    hit.entity = AuraEntityHandle{ 0, 0 };
    hit.body = handle;
    hit.shape = 0;
    hit.distance = fraction * ctx->maxDistance;
    hit.point = ToAura(point);
    hit.normal = ToAura(normal);
    ctx->count++;
    return fraction;
}
} // namespace

Box2DWorld::Impl* Box2DWorld::CreateImpl(const AuraWorldDesc& desc)
{
    return new Impl(desc);
}

Box2DWorld::Box2DWorld(const AuraWorldDesc& desc)
    : impl_(CreateImpl(desc))
{
}

Box2DWorld::~Box2DWorld()
{
    b2DestroyWorld(impl_->world);
    delete impl_;
}

AuraResultCode Box2DWorld::CreateBody(const AuraBodyDesc& desc, AuraBodyHandle* outBody)
{
    if (outBody == nullptr || desc.shapeCount == 0 || desc.shapes == nullptr)
        return AURA_INVALID_DEFINITION;

    int index;
    if (!impl_->freeSlots.empty())
    {
        index = impl_->freeSlots.back();
        impl_->freeSlots.pop_back();
    }
    else
    {
        index = static_cast<int>(impl_->slots.size());
        impl_->slots.push_back(Impl::Slot{});
    }

    Impl::Slot& slot = impl_->slots[index];
    slot.area = 0.0f;
    const AuraBodyHandle handle{ static_cast<uint32_t>(index), slot.generation };

    b2BodyDef bodyDef = b2DefaultBodyDef();
    bodyDef.type = desc.type == AURA_BODY_STATIC ? b2_staticBody
        : (desc.type == AURA_BODY_KINEMATIC ? b2_kinematicBody : b2_dynamicBody);
    bodyDef.position = ToVec2(desc.initialPose.position);
    bodyDef.rotation = b2MakeRot(AngleFromQuat(desc.initialPose.rotation));
    bodyDef.gravityScale = desc.gravityScale;
    bodyDef.isBullet = desc.collisionDetection == 1;
    bodyDef.enableSleep = desc.allowSleeping != 0;
    bodyDef.linearDamping = desc.linearDamping;
    bodyDef.angularDamping = desc.angularDamping;
    bodyDef.userData = Encode(handle);

    const b2BodyId body = b2CreateBody(impl_->world, &bodyDef);
    if (!b2Body_IsValid(body))
        return AURA_BACKEND_FAILURE;

    b2Body_SetLinearVelocity(body, ToVec2(desc.initialLinearVelocity));
    b2Body_SetAngularVelocity(body, desc.initialAngularVelocity.z);

    const uint64_t effectiveMask = (desc.collisionMask & impl_->matrix[desc.layer & 63]);
    for (uint32_t i = 0; i < desc.shapeCount; ++i)
    {
        const AuraShapeDesc& source = desc.shapes[i];
        b2ShapeDef shapeDef = b2DefaultShapeDef();
        shapeDef.density = source.density > 0.0f ? source.density : 1000.0f;
        shapeDef.material.friction = source.friction;
        shapeDef.material.restitution = source.restitution;
        shapeDef.filter.categoryBits = 1ull << (source.layer & 63);
        shapeDef.filter.maskBits = effectiveMask;
        shapeDef.filter.groupIndex = desc.groupIndex;
        shapeDef.isSensor = source.isTrigger != 0;
        if (source.isOneWay != 0)
        {
            shapeDef.enablePreSolveEvents = true;
            shapeDef.userData = oneway::EncodeUserData(AngleFromQuat(source.localPose.rotation));
        }

        switch (source.type)
        {
            case AURA_SHAPE_SPHERE:
            {
                const b2Circle circle{ b2Vec2{ source.localPose.position.x, source.localPose.position.y }, source.radius };
                const b2ShapeId shape = b2CreateCircleShape(body, &shapeDef, &circle);
                if (!shapeDef.isSensor)
                    slot.area += b2Shape_GetMassData(shape).mass / shapeDef.density;
                b2Shape_EnableSensorEvents(shape, true);
                break;
            }
            case AURA_SHAPE_CAPSULE:
            case AURA_SHAPE_CYLINDER:
            {
                const float half = std::max(0.0f, source.height * 0.5f - source.radius);
                const b2Vec2 center{ source.localPose.position.x, source.localPose.position.y };
                const b2Rot rotation = b2MakeRot(AngleFromQuat(source.localPose.rotation));
                const b2Vec2 halfAxis = b2RotateVector(rotation, b2Vec2{ 0.0f, half });
                const b2Capsule capsule{ b2Sub(center, halfAxis), b2Add(center, halfAxis), source.radius };
                const b2ShapeId shape = b2CreateCapsuleShape(body, &shapeDef, &capsule);
                if (!shapeDef.isSensor)
                    slot.area += b2Shape_GetMassData(shape).mass / shapeDef.density;
                b2Shape_EnableSensorEvents(shape, true);
                break;
            }
            default:
            {
                const b2Vec2 localCenter{ source.localPose.position.x, source.localPose.position.y };
                const b2Rot localRotation = b2MakeRot(AngleFromQuat(source.localPose.rotation));
                const b2Polygon polygon = b2MakeOffsetBox(source.halfExtents.x, source.halfExtents.y, localCenter, localRotation);
                const b2ShapeId shape = b2CreatePolygonShape(body, &shapeDef, &polygon);
                if (!shapeDef.isSensor)
                    slot.area += b2Shape_GetMassData(shape).mass / shapeDef.density;
                b2Shape_EnableSensorEvents(shape, true);
                break;
            }
        }
    }

    /* Honor the requested mass like the Jolt backend does: keep the centre of mass computed from
       the shapes and scale the rotational inertia, instead of deriving the mass from density. */
    if (desc.type == AURA_BODY_DYNAMIC && desc.mass > 0.0f)
    {
        b2MassData massData = b2Body_GetMassData(body);
        if (massData.mass > 0.0f)
        {
            massData.rotationalInertia *= desc.mass / massData.mass;
            massData.mass = desc.mass;
            b2Body_SetMassData(body, massData);
        }
    }

    b2Body_EnableContactEvents(body, true);

    slot.occupied = true;
    slot.body = body;
    *outBody = handle;
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::DestroyBody(AuraBodyHandle body)
{
    Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    for (size_t index = 0; index < impl_->jointSlots.size(); ++index)
    {
        Impl::JointSlot& joint = impl_->jointSlots[index];
        if (!joint.occupied)
            continue;
        const bool touchesA = joint.bodyA.index == body.index && joint.bodyA.generation == body.generation;
        const bool touchesB = joint.bodyB.index == body.index && joint.bodyB.generation == body.generation;
        if (!touchesA && !touchesB)
            continue;
        if (b2Joint_IsValid(joint.joint))
            b2DestroyJoint(joint.joint);
        joint.joint = b2_nullJointId;
        joint.occupied = false;
        joint.generation += 1;
        impl_->freeJointSlots.push_back(static_cast<int>(index));
    }

    b2DestroyBody(slot->body);
    slot->occupied = false;
    slot->generation += 1;
    impl_->freeSlots.push_back(static_cast<int>(body.index));
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetKinematicTarget(AuraBodyHandle body, const AuraPose& pose)
{
    Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    if (!b2Body_IsEnabled(slot->body))
        return AURA_BODY_DISABLED;
    if (b2Body_GetType(slot->body) != b2_kinematicBody)
        return AURA_UNSUPPORTED_OPERATION;

    b2Transform target;
    target.p = ToVec2(pose.position);
    target.q = b2MakeRot(AngleFromQuat(pose.rotation));
    b2Body_SetTargetTransform(slot->body, target, impl_->lastDelta);
    slot->kinematicTargetPending = true;
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::GetBodyState(AuraBodyHandle body, AuraBodyState* outState) const
{
    const Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr || outState == nullptr)
        return AURA_INVALID_HANDLE;

    impl_->FillState(body, *slot, *outState);
    return AURA_SUCCESS;
}

uint32_t Box2DWorld::CopyBodyStates(AuraBodyState* buffer, uint32_t capacity) const
{
    uint32_t written = 0;
    for (size_t index = 0; index < impl_->slots.size() && written < capacity; ++index)
    {
        const Impl::Slot& slot = impl_->slots[index];
        if (!slot.occupied)
            continue;
        impl_->FillState(AuraBodyHandle{ static_cast<uint32_t>(index), slot.generation }, slot, buffer[written]);
        ++written;
    }
    return written;
}

uint32_t Box2DWorld::BodyCount() const
{
    uint32_t count = 0;
    for (const Impl::Slot& slot : impl_->slots)
        if (slot.occupied)
            ++count;
    return count;
}

AuraResultCode Box2DWorld::Step(float deltaTime)
{
    if (deltaTime > 0.0f)
        impl_->lastDelta = deltaTime;
    impl_->ApplyForceFields(deltaTime);
    /* Joint limits and lengths are eased toward their targets for this step's dt before the solver runs. */
    impl_->EaseJoints(deltaTime);
    b2World_Step(impl_->world, deltaTime, 4);

    /* See JoltWorld::Step: a kinematic target is consumed by one step. */
    for (Impl::Slot& slot : impl_->slots)
    {
        if (!slot.kinematicTargetPending)
            continue;
        slot.kinematicTargetPending = false;
        if (slot.occupied && b2Body_IsValid(slot.body) && b2Body_IsEnabled(slot.body))
        {
            b2Body_SetLinearVelocity(slot.body, b2Vec2{ 0.0f, 0.0f });
            b2Body_SetAngularVelocity(slot.body, 0.0f);
        }
    }

    impl_->GatherEvents();
    impl_->ProcessJointBreaks();
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::CreateWater(const AuraWaterDesc& desc, AuraWaterHandle* outWater)
{
    if (outWater == nullptr || desc.density <= 0.0f)
        return AURA_INVALID_DEFINITION;
    impl_->water = desc;
    impl_->waterActive = true;
    *outWater = AuraWaterHandle{ 1 };
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::DestroyWater(AuraWaterHandle water)
{
    if (!impl_->waterActive || water.opaque != 1)
        return AURA_INVALID_HANDLE;
    impl_->waterActive = false;
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetWaterParameters(AuraWaterHandle water, const AuraWaterDesc& desc)
{
    if (!impl_->waterActive || water.opaque != 1 || desc.density <= 0.0f)
        return AURA_INVALID_HANDLE;
    impl_->water = desc;
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::ApplyWaterStep(AuraWaterHandle water, float deltaTime)
{
    if (!impl_->waterActive || water.opaque != 1 || deltaTime < 0.0f)
        return AURA_INVALID_HANDLE;
    for (const Impl::Slot& slot : impl_->slots)
    {
        if (!slot.occupied || !b2Body_IsValid(slot.body) || !b2Body_IsEnabled(slot.body) || b2Body_GetType(slot.body) != b2_dynamicBody)
            continue;
        /* Archimedes: the fluid weight of the submerged part, estimated from the body's AABB (exact for boxes), with the
           body's displaced area as the 2D volume per metre of depth. */
        const b2AABB bounds = b2Body_ComputeAABB(slot.body);
        const float height = bounds.upperBound.y - bounds.lowerBound.y;
        const float fraction = height > 0.0f ? std::clamp((impl_->water.surfaceHeight - bounds.lowerBound.y) / height, 0.0f, 1.0f) : 0.0f;
        if (fraction <= 0.0f)
            continue;
        /* Drag first (implicit damping at linearDrag (1/s) at full submersion), then the buoyancy impulse: the world step
           that follows adds gravity, so a neutrally buoyant body keeps zero velocity instead of sinking at g*dt. */
        const float damping = 1.0f / (1.0f + std::max(0.0f, impl_->water.linearDrag) * fraction * deltaTime);
        const b2Vec2 velocity = b2Body_GetLinearVelocity(slot.body);
        b2Body_SetLinearVelocity(slot.body, b2Vec2{ velocity.x * damping, velocity.y * damping });
        b2Body_SetAngularVelocity(slot.body, b2Body_GetAngularVelocity(slot.body) * damping);
        /* A force, not an impulse: Box2D integrates in sub-steps, and a force is applied in every sub-step together with
           gravity, so the two cancel exactly for a neutrally buoyant body (an impulse at the start of the step made it creep). */
        const float displaced = impl_->water.density * slot.area * fraction;
        b2Body_ApplyForceToCenter(slot.body, b2Vec2{ -impl_->gravity.x * displaced, -impl_->gravity.y * displaced }, true);
    }
    return AURA_SUCCESS;
}

uint32_t Box2DWorld::PendingEventCount() const
{
    return static_cast<uint32_t>(impl_->events.size());
}

uint32_t Box2DWorld::CopyEvents(AuraPhysicsEvent* buffer, uint32_t capacity)
{
    const uint32_t count = std::min(static_cast<uint32_t>(impl_->events.size()), capacity);
    for (uint32_t i = 0; i < count; ++i)
        buffer[i] = impl_->events[i];
    impl_->events.erase(impl_->events.begin(), impl_->events.begin() + count);
    return count;
}

bool Box2DWorld::Raycast(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit)
{
    if (outHit == nullptr || maxDistance <= 0.0f)
        return false;

    const b2Vec2 direction = b2Normalize(ToVec2(ray.direction));
    b2QueryFilter queryFilter = b2DefaultQueryFilter();
    queryFilter.maskBits = filter.layerMask;
    const b2RayResult result = b2World_CastRayClosest(impl_->world, ToVec2(ray.origin), b2Vec2{ direction.x * maxDistance, direction.y * maxDistance }, queryFilter);
    if (!result.hit)
        return false;

    const AuraBodyHandle handle = Impl::HandleFromShape(result.shapeId);
    outHit->entity = AuraEntityHandle{ 0, 0 };
    outHit->body = handle;
    outHit->shape = 0;
    outHit->distance = result.fraction * maxDistance;
    outHit->point = ToAura(result.point);
    outHit->normal = ToAura(result.normal);
    return true;
}

uint32_t Box2DWorld::RaycastAll(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity)
{
    if (buffer == nullptr || capacity == 0 || maxDistance <= 0.0f)
        return 0;

    const b2Vec2 direction = b2Normalize(ToVec2(ray.direction));
    b2QueryFilter queryFilter = b2DefaultQueryFilter();
    queryFilter.maskBits = filter.layerMask;
    RayContext context;
    context.buffer = buffer;
    context.capacity = capacity;
    context.count = 0;
    context.maxDistance = maxDistance;
    b2World_CastRay(impl_->world, ToVec2(ray.origin), b2Vec2{ direction.x * maxDistance, direction.y * maxDistance }, queryFilter, &RayCallback, &context);
    return context.count;
}

uint64_t Box2DWorld::ComputeStateHash() const
{
    uint64_t hash = 14695981039346656037ull;
    auto combine = [&hash](uint64_t value)
    {
        hash ^= value;
        hash *= 1099511628211ull;
    };

    AuraBodyState state{};
    for (size_t index = 0; index < impl_->slots.size(); ++index)
    {
        const Impl::Slot& slot = impl_->slots[index];
        if (!slot.occupied)
            continue;
        impl_->FillState(AuraBodyHandle{ static_cast<uint32_t>(index), slot.generation }, slot, state);
        combine(index);
        combine(static_cast<uint32_t>(state.pose.position.x * 1000.0f));
        combine(static_cast<uint32_t>(state.pose.position.y * 1000.0f));
    }
    return hash;
}

AuraResultCode Box2DWorld::ApplyStates(const AuraBodyState* states, uint32_t count)
{
    return ApplyStatesWithExtras(states, nullptr, count);
}

uint32_t Box2DWorld::CopyBodyExtras(BodyExtra* buffer, uint32_t capacity) const
{
    uint32_t written = 0;
    for (size_t index = 0; index < impl_->slots.size() && written < capacity; ++index)
    {
        const Impl::Slot& slot = impl_->slots[index];
        if (!slot.occupied)
            continue;
        const b2Rot rotation = b2Body_GetRotation(slot.body);
        const b2BodyType type = b2Body_GetType(slot.body);
        BodyExtra extra{};
        extra.rotationA = rotation.c;
        extra.rotationB = rotation.s;
        extra.motionType = type == b2_staticBody ? AURA_BODY_STATIC : (type == b2_kinematicBody ? AURA_BODY_KINEMATIC : AURA_BODY_DYNAMIC);
        extra.flags = kBodyExtraHasRotation | kBodyExtraHasMotionType | (slot.kinematicTargetPending ? kBodyExtraKinematicPending : 0u);
        buffer[written++] = extra;
    }
    return written;
}

/* The caller (Aura_DeserializeState) has validated every handle and value, so this cannot fail half way. */
AuraResultCode Box2DWorld::ApplyStatesWithExtras(const AuraBodyState* states, const BodyExtra* extras, uint32_t count)
{
    for (uint32_t i = 0; i < count; ++i)
    {
        const AuraBodyState& state = states[i];
        Impl::Slot* slot = impl_->Find(state.body);
        if (slot == nullptr)
            return AURA_INVALID_HANDLE;

        /* Snapshots carry the enabled flag: restore the pose with the body in the
           simulation, then disable it again when it was disabled when captured. */
        if (!b2Body_IsEnabled(slot->body))
            b2Body_Enable(slot->body);

        const bool hasExtra = extras != nullptr;
        if (hasExtra && (extras[i].flags & kBodyExtraHasMotionType) != 0u)
        {
            const b2BodyType wanted = extras[i].motionType == AURA_BODY_STATIC ? b2_staticBody
                : (extras[i].motionType == AURA_BODY_KINEMATIC ? b2_kinematicBody : b2_dynamicBody);
            if (b2Body_GetType(slot->body) != wanted)
                b2Body_SetType(slot->body, wanted);
        }

        /* The world holds the rotation as a (cos, sin) pair; restoring that pair avoids the approximate b2MakeRot. */
        b2Transform transform;
        transform.p = ToVec2(state.pose.position);
        if (hasExtra && (extras[i].flags & kBodyExtraHasRotation) != 0u)
            transform.q = b2Rot{ extras[i].rotationA, extras[i].rotationB };
        else
            transform.q = b2MakeRot(AngleFromQuat(state.pose.rotation));
        b2Body_SetTransform(slot->body, transform.p, transform.q);
        if (state.isAwake != 0 && !b2Body_IsAwake(slot->body))
            b2Body_SetAwake(slot->body, true);
        b2Body_SetLinearVelocity(slot->body, ToVec2(state.linearVelocity));
        b2Body_SetAngularVelocity(slot->body, state.angularVelocity.z);
        slot->kinematicTargetPending = hasExtra && (extras[i].flags & kBodyExtraKinematicPending) != 0u;
        if ((state.flags & AURA_BODY_FLAG_DISABLED) != 0u)
            b2Body_Disable(slot->body);
    }

    /* Second pass: put the bodies that slept when captured back to sleep. Done after every body is awake and placed
       because sleeping is per island and a later body of the same island would otherwise wake it again. */
    for (uint32_t i = 0; i < count; ++i)
    {
        const AuraBodyState& state = states[i];
        if (state.isAwake != 0 || (state.flags & AURA_BODY_FLAG_DISABLED) != 0u)
            continue;
        const Impl::Slot* slot = impl_->Find(state.body);
        if (slot != nullptr && b2Body_IsEnabled(slot->body))
            b2Body_SetAwake(slot->body, false);
    }
    return AURA_SUCCESS;
}

uint32_t Box2DWorld::CopyContacts(AuraContact* buffer, uint32_t capacity) const
{
    uint32_t written = 0;
    for (size_t index = 0; index < impl_->slots.size() && written < capacity; ++index)
    {
        const Impl::Slot& slot = impl_->slots[index];
        if (!slot.occupied)
            continue;

        b2ContactData data[8];
        const int count = b2Body_GetContactData(slot.body, data, 8);
        for (int contact = 0; contact < count && written < capacity; ++contact)
        {
            const b2Manifold& manifold = data[contact].manifold;
            if (manifold.pointCount <= 0)
                continue;

            const b2BodyId other = b2Shape_GetBody(data[contact].shapeIdB);
            int otherIndex = -1;
            for (size_t candidate = 0; candidate < impl_->slots.size(); ++candidate)
            {
                if (impl_->slots[candidate].occupied && B2_ID_EQUALS(impl_->slots[candidate].body, other))
                {
                    otherIndex = static_cast<int>(candidate);
                    break;
                }
            }

            if (otherIndex < 0 || static_cast<int>(index) > otherIndex)
                continue;

            AuraContact& out = buffer[written++];
            out = AuraContact{};
            out.bodyA = AuraBodyHandle{ static_cast<uint32_t>(index), slot.generation };
            out.bodyB = AuraBodyHandle{ static_cast<uint32_t>(otherIndex), impl_->slots[otherIndex].generation };
            out.point = AuraVec3{ manifold.points[0].point.x, manifold.points[0].point.y, 0.0f };
            out.normal = AuraVec3{ manifold.normal.x, manifold.normal.y, 0.0f };
            out.penetration = -manifold.points[0].separation;
            out.impulse = manifold.points[0].normalImpulse;
        }
    }
    return written;
}

AuraResultCode Box2DWorld::SetSurfaceVelocity(AuraBodyHandle body, const AuraVec3& velocity)
{
    Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    b2Body_SetLinearVelocity(slot->body, ToVec2(velocity));
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::CreateCharacter(const AuraCharacterDesc& desc, uint64_t* outCharacter)
{
    return impl_->characters.Create(impl_->matrix, desc, outCharacter);
}

AuraResultCode Box2DWorld::DestroyCharacter(uint64_t character)
{
    return impl_->characters.Destroy(character);
}

AuraResultCode Box2DWorld::GetCharacterState(uint64_t character, AuraCharacterState* outState) const
{
    return impl_->characters.GetState(character, outState);
}

AuraResultCode Box2DWorld::MoveCharacter(uint64_t character, const AuraVec3& desiredTranslation, float deltaTime)
{
    return impl_->characters.Move(impl_->world, ToVec2(impl_->gravity), impl_->lastDelta, character, desiredTranslation, deltaTime);
}

uint32_t Box2DWorld::CharacterCount() const
{
    return impl_->characters.Count();
}

uint32_t Box2DWorld::CopyCharacterStates(AuraCharacterState* buffer, uint32_t capacity) const
{
    return impl_->characters.CopyStates(buffer, capacity);
}

AuraResultCode Box2DWorld::ApplyCharacterStates(const AuraCharacterState* states, uint32_t count)
{
    return impl_->characters.ApplyStates(states, count);
}

AuraResultCode Box2DWorld::CreateSoftBody(const AuraSoftBodyDesc&, AuraSoftBodyHandle*)
{
    return AURA_UNSUPPORTED_SHAPE;
}

AuraResultCode Box2DWorld::DestroySoftBody(AuraSoftBodyHandle)
{
    return AURA_UNSUPPORTED_SHAPE;
}

AuraResultCode Box2DWorld::GetSoftBodyState(AuraSoftBodyHandle, float*, uint32_t, AuraSoftBodyState*) const
{
    return AURA_UNSUPPORTED_SHAPE;
}

} // namespace aura

namespace aura
{

/* ---- v10 body control. 2D ignores z of linear vectors and x/y of angular vectors. ---- */

namespace
{
enum : uint32_t
{
    kNeedEnabled = 1u,
    kNeedMovable = 2u,
    kNeedDynamic = 4u
};
} // namespace

#define AURA_B2_RESOLVE(handle, requirements)                                                           \
    Impl::Slot* slot = impl_->Find(handle);                                                             \
    if (slot == nullptr)                                                                                \
        return AURA_INVALID_HANDLE;                                                                     \
    if (((requirements) & kNeedEnabled) != 0u && !b2Body_IsEnabled(slot->body))                         \
        return AURA_BODY_DISABLED;                                                                      \
    if (((requirements) & (kNeedMovable | kNeedDynamic)) != 0u)                                         \
    {                                                                                                   \
        const b2BodyType motion = b2Body_GetType(slot->body);                                           \
        if (motion == b2_staticBody || (((requirements) & kNeedDynamic) != 0u && motion != b2_dynamicBody)) \
            return AURA_INVALID_DEFINITION;                                                             \
    }

AuraResultCode Box2DWorld::SetLinearVelocity(AuraBodyHandle body, const AuraVec3& velocity)
{
    AURA_B2_RESOLVE(body, kNeedEnabled | kNeedMovable)
    if (!IsFinite(velocity))
        return AURA_INVALID_DEFINITION;
    slot->kinematicTargetPending = false;
    b2Body_SetLinearVelocity(slot->body, ToVec2(velocity));
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetAngularVelocity(AuraBodyHandle body, const AuraVec3& velocity)
{
    AURA_B2_RESOLVE(body, kNeedEnabled | kNeedMovable)
    if (!IsFinite(velocity))
        return AURA_INVALID_DEFINITION;
    b2Body_SetAngularVelocity(slot->body, velocity.z);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::AddForce(AuraBodyHandle body, const AuraVec3& force)
{
    AURA_B2_RESOLVE(body, kNeedEnabled | kNeedDynamic)
    if (!IsFinite(force))
        return AURA_INVALID_DEFINITION;
    b2Body_ApplyForceToCenter(slot->body, ToVec2(force), true);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::AddImpulse(AuraBodyHandle body, const AuraVec3& impulse)
{
    AURA_B2_RESOLVE(body, kNeedEnabled | kNeedDynamic)
    if (!IsFinite(impulse))
        return AURA_INVALID_DEFINITION;
    b2Body_ApplyLinearImpulseToCenter(slot->body, ToVec2(impulse), true);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::AddTorque(AuraBodyHandle body, const AuraVec3& torque)
{
    AURA_B2_RESOLVE(body, kNeedEnabled | kNeedDynamic)
    if (!IsFinite(torque))
        return AURA_INVALID_DEFINITION;
    b2Body_ApplyTorque(slot->body, torque.z, true);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::AddAngularImpulse(AuraBodyHandle body, const AuraVec3& impulse)
{
    AURA_B2_RESOLVE(body, kNeedEnabled | kNeedDynamic)
    if (!IsFinite(impulse))
        return AURA_INVALID_DEFINITION;
    b2Body_ApplyAngularImpulse(slot->body, impulse.z, true);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetBodyPose(AuraBodyHandle body, const AuraPose& pose, bool zeroVelocity)
{
    AURA_B2_RESOLVE(body, 0u)
    const float lengthSq = pose.rotation.z * pose.rotation.z + pose.rotation.w * pose.rotation.w;
    if (!IsFinite(pose.position) || !IsFinite(pose.rotation.x) || !IsFinite(pose.rotation.y)
        || !IsFinite(pose.rotation.z) || !IsFinite(pose.rotation.w) || lengthSq < 1.0e-12f)
        return AURA_INVALID_DEFINITION;

    b2Body_SetTransform(slot->body, ToVec2(pose.position), b2MakeRot(AngleFromQuat(pose.rotation)));
    if (zeroVelocity && b2Body_GetType(slot->body) != b2_staticBody)
    {
        b2Body_SetLinearVelocity(slot->body, b2Vec2{ 0.0f, 0.0f });
        b2Body_SetAngularVelocity(slot->body, 0.0f);
    }
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetGravityScale(AuraBodyHandle body, float gravityScale)
{
    AURA_B2_RESOLVE(body, 0u)
    if (!IsFinite(gravityScale))
        return AURA_INVALID_DEFINITION;
    b2Body_SetGravityScale(slot->body, gravityScale);
    Wake(slot->body);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetFriction(AuraBodyHandle body, float friction)
{
    AURA_B2_RESOLVE(body, 0u)
    if (!IsFinite(friction) || friction < 0.0f)
        return AURA_INVALID_DEFINITION;
    std::vector<b2ShapeId> shapes(static_cast<size_t>(std::max(0, b2Body_GetShapeCount(slot->body))));
    const int count = b2Body_GetShapes(slot->body, shapes.data(), static_cast<int>(shapes.size()));
    for (int i = 0; i < count; ++i)
        b2Shape_SetFriction(shapes[i], friction);
    Wake(slot->body);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetRestitution(AuraBodyHandle body, float restitution)
{
    AURA_B2_RESOLVE(body, 0u)
    if (!IsFinite(restitution) || restitution < 0.0f)
        return AURA_INVALID_DEFINITION;
    std::vector<b2ShapeId> shapes(static_cast<size_t>(std::max(0, b2Body_GetShapeCount(slot->body))));
    const int count = b2Body_GetShapes(slot->body, shapes.data(), static_cast<int>(shapes.size()));
    for (int i = 0; i < count; ++i)
        b2Shape_SetRestitution(shapes[i], restitution);
    Wake(slot->body);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetMotionType(AuraBodyHandle body, AuraBodyType type)
{
    AURA_B2_RESOLVE(body, 0u)
    if (type != AURA_BODY_STATIC && type != AURA_BODY_DYNAMIC && type != AURA_BODY_KINEMATIC)
        return AURA_INVALID_DEFINITION;
    b2Body_SetType(slot->body, type == AURA_BODY_STATIC ? b2_staticBody
        : (type == AURA_BODY_KINEMATIC ? b2_kinematicBody : b2_dynamicBody));
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetBodyLayer(AuraBodyHandle body, AuraLayer layer, uint64_t collisionMask)
{
    AURA_B2_RESOLVE(body, 0u)
    if (layer >= kMaxLayers)
        return AURA_INVALID_DEFINITION;
    std::vector<b2ShapeId> shapes(static_cast<size_t>(std::max(0, b2Body_GetShapeCount(slot->body))));
    const int count = b2Body_GetShapes(slot->body, shapes.data(), static_cast<int>(shapes.size()));
    for (int i = 0; i < count; ++i)
    {
        b2Filter filter = b2Shape_GetFilter(shapes[i]);
        filter.categoryBits = 1ull << layer;
        filter.maskBits = collisionMask & impl_->matrix[layer];
        b2Shape_SetFilter(shapes[i], filter);
    }
    Wake(slot->body);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::SetBodyEnabled(AuraBodyHandle body, bool enabled)
{
    AURA_B2_RESOLVE(body, 0u)
    if (enabled && !b2Body_IsEnabled(slot->body))
        b2Body_Enable(slot->body);
    else if (!enabled && b2Body_IsEnabled(slot->body))
        b2Body_Disable(slot->body);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::IsBodyEnabled(AuraBodyHandle body, bool* outEnabled) const
{
    const Impl::Slot* slot = impl_->Find(body);
    if (slot == nullptr || outEnabled == nullptr)
        return AURA_INVALID_HANDLE;
    *outEnabled = b2Body_IsEnabled(slot->body);
    return AURA_SUCCESS;
}

#undef AURA_B2_RESOLVE

} // namespace aura
