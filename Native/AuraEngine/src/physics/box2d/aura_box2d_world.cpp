#include "aura_box2d_world.h"

#include <box2d/box2d.h>

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <vector>

namespace aura
{
namespace
{
constexpr uint32_t kMaxLayers = 64;

float AngleFromQuat(const AuraQuat& q) { return 2.0f * std::atan2(q.z, q.w); }

AuraQuat QuatFromAngle(float angle)
{
    AuraQuat q{};
    q.z = std::sin(angle * 0.5f);
    q.w = std::cos(angle * 0.5f);
    return q;
}

b2Vec2 ToVec2(const AuraVec3& v) { return b2Vec2{ v.x, v.y }; }
AuraVec3 ToAura(const b2Vec2& v) { return AuraVec3{ v.x, v.y, 0.0f }; }
AuraQuat ToQuat(const b2Rot& rot) { return QuatFromAngle(b2Rot_GetAngle(rot)); }

uint64_t UserDataFromHandleValue(AuraBodyHandle handle)
{
    return (static_cast<uint64_t>(handle.index) << 32) | handle.generation;
}

void* Encode(AuraBodyHandle handle)
{
    return reinterpret_cast<void*>(UserDataFromHandleValue(handle));
}

AuraBodyHandle Decode(void* userData)
{
    const uint64_t value = reinterpret_cast<uint64_t>(userData);
    return AuraBodyHandle{ static_cast<uint32_t>(value >> 32), static_cast<uint32_t>(value & 0xFFFFFFFFu) };
}
} // namespace

struct OverlapContext
{
    AuraQueryHit* buffer = nullptr;
    uint32_t capacity = 0;
    uint32_t count = 0;
    bool ignoreTriggers = false;
};

struct RayContext
{
    AuraQueryHit* buffer = nullptr;
    uint32_t capacity = 0;
    uint32_t count = 0;
    float maxDistance = 0.0f;
};

struct Box2DWorld::Impl
{
    struct Slot
    {
        bool occupied = false;
        uint32_t generation = 0;
        b2BodyId body = b2_nullBodyId;
    };

    struct JointSlot
    {
        bool occupied = false;
        uint32_t generation = 0;
        b2JointId joint = b2_nullJointId;
        AuraBodyHandle bodyA{};
        AuraBodyHandle bodyB{};
    };

    b2WorldId world = b2_nullWorldId;
    std::vector<Slot> slots;
    std::vector<int> freeSlots;
    std::vector<JointSlot> jointSlots;
    std::vector<int> freeJointSlots;
    std::vector<AuraPhysicsEvent> events;
    uint64_t matrix[kMaxLayers];
    float lastDelta = 1.0f / 60.0f;

    static uint64_t MakeJointHandle(const JointSlot& slot, int index)
    {
        return (static_cast<uint64_t>(slot.generation) << 32) | static_cast<uint32_t>(index);
    }

    JointSlot* FindJoint(uint64_t handle)
    {
        const uint32_t index = static_cast<uint32_t>(handle & 0xFFFFFFFFull);
        const uint32_t generation = static_cast<uint32_t>(handle >> 32);
        if (index < jointSlots.size() && jointSlots[index].occupied && jointSlots[index].generation == generation)
            return &jointSlots[index];
        return nullptr;
    }

    const JointSlot* FindJoint(uint64_t handle) const
    {
        const uint32_t index = static_cast<uint32_t>(handle & 0xFFFFFFFFull);
        const uint32_t generation = static_cast<uint32_t>(handle >> 32);
        if (index < jointSlots.size() && jointSlots[index].occupied && jointSlots[index].generation == generation)
            return &jointSlots[index];
        return nullptr;
    }

    explicit Impl(const AuraWorldDesc& desc)
    {
        for (uint32_t i = 0; i < kMaxLayers; ++i)
            matrix[i] = ~0ull;
        if (desc.collisionMasks != nullptr)
        {
            for (uint32_t i = 0; i < desc.collisionMaskCount && i < kMaxLayers; ++i)
                matrix[i] = desc.collisionMasks[i];
        }

        b2WorldDef worldDef = b2DefaultWorldDef();
        worldDef.gravity = b2Vec2{ desc.gravity.x, desc.gravity.y };
        world = b2CreateWorld(&worldDef);
    }

    Slot* Find(AuraBodyHandle handle)
    {
        if (handle.index < slots.size())
        {
            Slot& slot = slots[handle.index];
            if (slot.occupied && slot.generation == handle.generation)
                return &slot;
        }
        return nullptr;
    }

    const Slot* Find(AuraBodyHandle handle) const
    {
        if (handle.index < slots.size())
        {
            const Slot& slot = slots[handle.index];
            if (slot.occupied && slot.generation == handle.generation)
                return &slot;
        }
        return nullptr;
    }

    static AuraBodyHandle HandleFromShape(b2ShapeId shape)
    {
        if (!b2Shape_IsValid(shape))
            return AuraBodyHandle{ 0xFFFFFFFFu, 0xFFFFFFFFu };
        const b2BodyId body = b2Shape_GetBody(shape);
        return Decode(b2Body_GetUserData(body));
    }

    void FillState(AuraBodyHandle handle, const Slot& slot, AuraBodyState& state) const
    {
        state.body = handle;
        state.entity = AuraEntityHandle{ 0, 0 };
        const b2Vec2 position = b2Body_GetPosition(slot.body);
        state.pose.position = ToAura(position);
        state.pose.rotation = ToQuat(b2Body_GetRotation(slot.body));
        state.linearVelocity = ToAura(b2Body_GetLinearVelocity(slot.body));
        const float angular = b2Body_GetAngularVelocity(slot.body);
        state.angularVelocity = AuraVec3{ 0.0f, 0.0f, angular };
        state.isAwake = b2Body_IsAwake(slot.body) ? 1 : 0;
        state.flags = 0;
    }

    void GatherEvents()
    {
        events.clear();

        const b2ContactEvents contacts = b2World_GetContactEvents(world);
        for (int i = 0; i < contacts.beginCount; ++i)
        {
            const AuraBodyHandle a = HandleFromShape(contacts.beginEvents[i].shapeIdA);
            const AuraBodyHandle b = HandleFromShape(contacts.beginEvents[i].shapeIdB);
            if (a.index == 0xFFFFFFFFu || b.index == 0xFFFFFFFFu)
                continue;
            AuraPhysicsEvent event{};
            event.type = 0;
            event.bodyA = a;
            event.bodyB = b;
            events.push_back(event);
        }
        for (int i = 0; i < contacts.endCount; ++i)
        {
            const AuraBodyHandle a = HandleFromShape(contacts.endEvents[i].shapeIdA);
            const AuraBodyHandle b = HandleFromShape(contacts.endEvents[i].shapeIdB);
            if (a.index == 0xFFFFFFFFu || b.index == 0xFFFFFFFFu)
                continue;
            AuraPhysicsEvent event{};
            event.type = 1;
            event.bodyA = a;
            event.bodyB = b;
            events.push_back(event);
        }

        const b2SensorEvents sensors = b2World_GetSensorEvents(world);
        for (int i = 0; i < sensors.beginCount; ++i)
        {
            const AuraBodyHandle sensor = HandleFromShape(sensors.beginEvents[i].sensorShapeId);
            const AuraBodyHandle visitor = HandleFromShape(sensors.beginEvents[i].visitorShapeId);
            if (sensor.index == 0xFFFFFFFFu || visitor.index == 0xFFFFFFFFu)
                continue;
            AuraPhysicsEvent event{};
            event.type = 2;
            event.bodyA = sensor;
            event.bodyB = visitor;
            events.push_back(event);
        }
        for (int i = 0; i < sensors.endCount; ++i)
        {
            const AuraBodyHandle sensor = HandleFromShape(sensors.endEvents[i].sensorShapeId);
            const AuraBodyHandle visitor = HandleFromShape(sensors.endEvents[i].visitorShapeId);
            if (sensor.index == 0xFFFFFFFFu || visitor.index == 0xFFFFFFFFu)
                continue;
            AuraPhysicsEvent event{};
            event.type = 3;
            event.bodyA = sensor;
            event.bodyB = visitor;
            events.push_back(event);
        }
    }
};

namespace
{
bool OverlapCallback(b2ShapeId shapeId, void* context)
{
    auto* ctx = static_cast<OverlapContext*>(context);
    if (ctx->count >= ctx->capacity)
        return false;

    const b2BodyId body = b2Shape_GetBody(shapeId);
    const AuraBodyHandle handle = Decode(b2Body_GetUserData(body));
    if (handle.index == 0xFFFFFFFFu)
        return true;

    ctx->buffer[ctx->count].entity = AuraEntityHandle{ 0, 0 };
    ctx->buffer[ctx->count].body = handle;
    ctx->buffer[ctx->count].shape = 0;
    ctx->buffer[ctx->count].distance = 0.0f;
    ctx->buffer[ctx->count].point = ToAura(b2Shape_GetClosestPoint(shapeId, b2Body_GetPosition(body)));
    ctx->buffer[ctx->count].normal = AuraVec3{ 0.0f, 0.0f, 0.0f };
    ctx->count++;
    return true;
}

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
    const AuraBodyHandle handle{ static_cast<uint32_t>(index), slot.generation };

    b2BodyDef bodyDef = b2DefaultBodyDef();
    bodyDef.type = desc.type == AURA_BODY_STATIC ? b2_staticBody
        : (desc.type == AURA_BODY_KINEMATIC ? b2_kinematicBody : b2_dynamicBody);
    bodyDef.position = ToVec2(desc.initialPose.position);
    bodyDef.rotation = b2MakeRot(AngleFromQuat(desc.initialPose.rotation));
    bodyDef.gravityScale = desc.gravityScale;
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

        switch (source.type)
        {
            case AURA_SHAPE_SPHERE:
            {
                const b2Circle circle{ b2Vec2{ source.localPose.position.x, source.localPose.position.y }, source.radius };
                const b2ShapeId shape = b2CreateCircleShape(body, &shapeDef, &circle);
                b2Shape_EnableSensorEvents(shape, true);
                break;
            }
            case AURA_SHAPE_CAPSULE:
            case AURA_SHAPE_CYLINDER:
            {
                const float half = std::max(0.0f, source.height * 0.5f - source.radius);
                const b2Capsule capsule{ b2Vec2{ source.localPose.position.x, source.localPose.position.y - half },
                                         b2Vec2{ source.localPose.position.x, source.localPose.position.y + half }, source.radius };
                const b2ShapeId shape = b2CreateCapsuleShape(body, &shapeDef, &capsule);
                b2Shape_EnableSensorEvents(shape, true);
                break;
            }
            default:
            {
                const b2Polygon polygon = b2MakeBox(source.halfExtents.x, source.halfExtents.y);
                const b2ShapeId shape = b2CreatePolygonShape(body, &shapeDef, &polygon);
                b2Shape_EnableSensorEvents(shape, true);
                break;
            }
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

    b2Transform target;
    target.p = ToVec2(pose.position);
    target.q = b2MakeRot(AngleFromQuat(pose.rotation));
    b2Body_SetTargetTransform(slot->body, target, impl_->lastDelta);
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

void Box2DWorld::Step(float deltaTime)
{
    impl_->lastDelta = deltaTime;
    b2World_Step(impl_->world, deltaTime, 4);
    impl_->GatherEvents();
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

uint32_t Box2DWorld::OverlapSphere(const AuraVec3& center, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity)
{
    if (buffer == nullptr || capacity == 0 || radius <= 0.0f)
        return 0;

    b2QueryFilter queryFilter = b2DefaultQueryFilter();
    queryFilter.maskBits = filter.layerMask;
    const b2Vec2 point{ 0.0f, 0.0f };
    const b2ShapeProxy proxy = b2MakeOffsetProxy(&point, 1, radius, b2Vec2{ center.x, center.y }, b2MakeRot(0.0f));

    OverlapContext context;
    context.buffer = buffer;
    context.capacity = capacity;
    context.count = 0;
    b2World_OverlapShape(impl_->world, &proxy, queryFilter, &OverlapCallback, &context);
    return context.count;
}

#define AURA_UNSUPPORTED_QUERY_METHOD(name, signature) AuraResultCode Box2DWorld::name signature { return AURA_UNSUPPORTED_QUERY; }
AURA_UNSUPPORTED_QUERY_METHOD(OverlapPoint, (const AuraVec3&, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*))
AURA_UNSUPPORTED_QUERY_METHOD(OverlapBox, (const AuraVec3&, const AuraVec3&, const AuraQuat&, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*))
AURA_UNSUPPORTED_QUERY_METHOD(OverlapCapsule, (const AuraVec3&, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*))
AURA_UNSUPPORTED_QUERY_METHOD(OverlapShape, (const AuraShapeDesc&, const AuraPose&, const AuraQueryFilter&, AuraQueryHit*, uint32_t, uint32_t*))
AURA_UNSUPPORTED_QUERY_METHOD(SphereCast, (const AuraVec3&, float, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*))
AURA_UNSUPPORTED_QUERY_METHOD(CapsuleCast, (const AuraVec3&, const AuraVec3&, float, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*))
AURA_UNSUPPORTED_QUERY_METHOD(BoxCast, (const AuraVec3&, const AuraVec3&, const AuraQuat&, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*))
AURA_UNSUPPORTED_QUERY_METHOD(ShapeCast, (const AuraShapeDesc&, const AuraPose&, const AuraVec3&, float, const AuraQueryFilter&, AuraQueryHit*, bool*))
#undef AURA_UNSUPPORTED_QUERY_METHOD

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
    for (uint32_t i = 0; i < count; ++i)
    {
        const AuraBodyState& state = states[i];
        Impl::Slot* slot = impl_->Find(state.body);
        if (slot == nullptr)
            return AURA_INVALID_HANDLE;

        b2Transform transform;
        transform.p = ToVec2(state.pose.position);
        transform.q = b2MakeRot(AngleFromQuat(state.pose.rotation));
        b2Body_SetTransform(slot->body, transform.p, transform.q);
        b2Body_SetLinearVelocity(slot->body, ToVec2(state.linearVelocity));
        b2Body_SetAngularVelocity(slot->body, state.angularVelocity.z);
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

AuraResultCode Box2DWorld::CreateJoint(const AuraJointDesc& desc, uint64_t* outJoint)
{
    if (outJoint == nullptr)
        return AURA_INVALID_HANDLE;

    Impl::Slot* slotA = impl_->Find(desc.bodyA);
    Impl::Slot* slotB = impl_->Find(desc.bodyB);
    if (slotA == nullptr || slotB == nullptr)
        return AURA_INVALID_HANDLE;
    if (slotA == slotB)
        return AURA_INVALID_DEFINITION;

    const b2Vec2 anchorA = ToVec2(desc.anchorA);
    const b2Vec2 anchorB = ToVec2(desc.anchorB);

    b2JointId joint = b2_nullJointId;
    switch (static_cast<AuraJointType>(desc.type))
    {
    case AURA_JOINT_DISTANCE:
    case AURA_JOINT_SPRING:
    {
        b2DistanceJointDef def = b2DefaultDistanceJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        def.length = desc.distance;
        if (desc.type == AURA_JOINT_SPRING)
        {
            def.enableSpring = true;
            def.hertz = desc.springFrequency > 0.0f ? desc.springFrequency : 1.0f;
            def.dampingRatio = desc.springDamping > 0.0f ? desc.springDamping : 1.0f;
        }
        joint = b2CreateDistanceJoint(impl_->world, &def);
        break;
    }
    case AURA_JOINT_POINT:
    case AURA_JOINT_HINGE:
    {
        b2RevoluteJointDef def = b2DefaultRevoluteJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        if (desc.type == AURA_JOINT_HINGE)
        {
            if (desc.enableLimit != 0)
            {
                def.enableLimit = true;
                def.lowerAngle = desc.minLimit;
                def.upperAngle = desc.maxLimit;
            }
            if (desc.motorEnabled != 0)
            {
                def.enableMotor = true;
                def.motorSpeed = desc.motorTargetVelocity;
                def.maxMotorTorque = desc.maxMotorForce;
            }
        }
        joint = b2CreateRevoluteJoint(impl_->world, &def);
        break;
    }
    case AURA_JOINT_FIXED:
    {
        b2WeldJointDef def = b2DefaultWeldJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        joint = b2CreateWeldJoint(impl_->world, &def);
        break;
    }
    case AURA_JOINT_SLIDER:
    {
        b2PrismaticJointDef def = b2DefaultPrismaticJointDef();
        def.bodyIdA = slotA->body;
        def.bodyIdB = slotB->body;
        def.localAnchorA = b2Body_GetLocalPoint(slotA->body, anchorA);
        def.localAnchorB = b2Body_GetLocalPoint(slotB->body, anchorB);
        const b2Vec2 axis = b2Body_GetLocalVector(slotA->body, ToVec2(desc.axisA));
        def.localAxisA = b2Normalize(axis);
        if (desc.enableLimit != 0)
        {
            def.enableLimit = true;
            def.lowerTranslation = desc.minLimit;
            def.upperTranslation = desc.maxLimit;
        }
        if (desc.motorEnabled != 0)
        {
            def.enableMotor = true;
            def.motorSpeed = desc.motorTargetVelocity;
            def.maxMotorForce = desc.maxMotorForce;
        }
        joint = b2CreatePrismaticJoint(impl_->world, &def);
        break;
    }
    default:
        return AURA_UNSUPPORTED_QUERY;
    }

    if (!b2Joint_IsValid(joint))
        return AURA_OUT_OF_MEMORY;

    int index;
    if (!impl_->freeJointSlots.empty())
    {
        index = impl_->freeJointSlots.back();
        impl_->freeJointSlots.pop_back();
    }
    else
    {
        index = static_cast<int>(impl_->jointSlots.size());
        impl_->jointSlots.emplace_back();
    }

    Impl::JointSlot& slot = impl_->jointSlots[index];
    slot.occupied = true;
    slot.joint = joint;
    slot.bodyA = desc.bodyA;
    slot.bodyB = desc.bodyB;
    *outJoint = Impl::MakeJointHandle(slot, index);
    return AURA_SUCCESS;
}

AuraResultCode Box2DWorld::DestroyJoint(uint64_t joint)
{
    Impl::JointSlot* slot = impl_->FindJoint(joint);
    if (slot == nullptr)
        return AURA_INVALID_HANDLE;

    if (b2Joint_IsValid(slot->joint))
        b2DestroyJoint(slot->joint);
    slot->joint = b2_nullJointId;
    slot->occupied = false;
    slot->generation += 1;
    impl_->freeJointSlots.push_back(static_cast<int>(joint & 0xFFFFFFFFull));
    return AURA_SUCCESS;
}

bool Box2DWorld::HasJoint(uint64_t joint) const
{
    return impl_->FindJoint(joint) != nullptr;
}

AuraResultCode Box2DWorld::CreateCharacter(const AuraCharacterDesc& desc, uint64_t* outCharacter)
{
    (void)desc;
    (void)outCharacter;
    return AURA_UNSUPPORTED_QUERY;
}

AuraResultCode Box2DWorld::DestroyCharacter(uint64_t character)
{
    (void)character;
    return AURA_INVALID_HANDLE;
}

AuraResultCode Box2DWorld::GetCharacterState(uint64_t character, AuraCharacterState* outState) const
{
    (void)character;
    (void)outState;
    return AURA_INVALID_HANDLE;
}

AuraResultCode Box2DWorld::MoveCharacter(uint64_t character, const AuraVec3& desiredTranslation, float deltaTime)
{
    (void)character;
    (void)desiredTranslation;
    (void)deltaTime;
    return AURA_INVALID_HANDLE;
}

uint32_t Box2DWorld::CharacterCount() const
{
    return 0;
}

uint32_t Box2DWorld::CopyCharacterStates(AuraCharacterState* buffer, uint32_t capacity) const
{
    (void)buffer;
    (void)capacity;
    return 0;
}

AuraResultCode Box2DWorld::ApplyCharacterStates(const AuraCharacterState* states, uint32_t count)
{
    (void)states;
    (void)count;
    return AURA_UNSUPPORTED_QUERY;
}

} // namespace aura
