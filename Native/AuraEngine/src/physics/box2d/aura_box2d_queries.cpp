#include "aura_box2d_internal.h"

/* Box2D query surface (overlap / shape cast). Raycasts and the circle overlap stay with the world adapter.

   Contract (mirrors aura_jolt_queries.cpp):
   - Overlaps return AURA_SUCCESS and write min(hits, capacity) unique bodies; null buffer / zero capacity / invalid
     geometry is AURA_INVALID_DEFINITION. A body with several touching shapes is reported once.
   - Casts report the closest hit (distance = fraction * maxDistance) and *outHasHit; a zero direction is a miss.
   - Filters: layerMask against the shape layer, flags bit 2 ignores filter.ignoredBody, triggerInteraction == 2
     (Ignore) skips sensor shapes. Other trigger modes include sensors.
   - 2D drops z of positions and x/y of rotations; only the z rotation angle is used. */

namespace aura
{
namespace
{
constexpr int32_t kTriggerIgnore = 2;
constexpr int32_t kFlagIgnoreBody = 4;

struct QueryFilterState
{
    uint64_t layerMask;
    bool ignoreTriggers;
    bool ignoreBody;
    AuraBodyHandle ignoredBody;
};

QueryFilterState MakeFilterState(const AuraQueryFilter& filter)
{
    QueryFilterState state{};
    state.layerMask = filter.layerMask;
    state.ignoreTriggers = filter.triggerInteraction == kTriggerIgnore;
    state.ignoreBody = (filter.flags & kFlagIgnoreBody) != 0;
    state.ignoredBody = filter.ignoredBody;
    return state;
}

/* Layer filtering is by the query mask only: categoryBits = all so the hit shape's own collision mask never hides it. */
b2QueryFilter MakeB2Filter(const QueryFilterState& state)
{
    b2QueryFilter filter = b2DefaultQueryFilter();
    filter.categoryBits = ~0ull;
    filter.maskBits = state.layerMask;
    return filter;
}

bool Accepts(const QueryFilterState& state, b2ShapeId shape, AuraBodyHandle& outBody)
{
    if (!b2Shape_IsValid(shape))
        return false;
    outBody = Decode(b2Body_GetUserData(b2Shape_GetBody(shape)));
    if (outBody.index == 0xFFFFFFFFu)
        return false;
    if (state.ignoreBody && outBody.index == state.ignoredBody.index && outBody.generation == state.ignoredBody.generation)
        return false;
    if (state.ignoreTriggers && b2Shape_IsSensor(shape))
        return false;
    return true;
}

struct OverlapCollector
{
    QueryFilterState state;
    b2Vec2 reference;
    AuraQueryHit* buffer;
    uint32_t capacity;
    uint32_t count;
};

bool CollectOverlap(b2ShapeId shape, void* context)
{
    auto* collector = static_cast<OverlapCollector*>(context);
    AuraBodyHandle handle;
    if (!Accepts(collector->state, shape, handle))
        return true;
    for (uint32_t i = 0; i < collector->count; ++i)
    {
        if (collector->buffer[i].body.index == handle.index && collector->buffer[i].body.generation == handle.generation)
            return true;
    }

    const b2Vec2 closest = b2Shape_GetClosestPoint(shape, collector->reference);
    const b2Vec2 away = b2Sub(collector->reference, closest);
    const float length = Length(away);

    AuraQueryHit& hit = collector->buffer[collector->count++];
    hit = AuraQueryHit{};
    hit.entity = AuraEntityHandle{ 0, 0 };
    hit.body = handle;
    hit.shape = handle.index;
    hit.distance = 0.0f;
    hit.point = ToAura(closest);
    hit.normal = length > 1e-6f ? AuraVec3{ away.x / length, away.y / length, 0.0f } : AuraVec3{ 0.0f, 0.0f, 0.0f };
    return collector->count < collector->capacity;
}

struct CastCollector
{
    QueryFilterState state;
    AuraQueryHit hit;
    bool hasHit;
    float maxDistance;
};

float CollectCast(b2ShapeId shape, b2Vec2 point, b2Vec2 normal, float fraction, void* context)
{
    auto* collector = static_cast<CastCollector*>(context);
    AuraBodyHandle handle;
    if (!Accepts(collector->state, shape, handle))
        return -1.0f;
    collector->hit = AuraQueryHit{};
    collector->hit.entity = AuraEntityHandle{ 0, 0 };
    collector->hit.body = handle;
    collector->hit.shape = handle.index;
    collector->hit.distance = fraction * collector->maxDistance;
    collector->hit.point = ToAura(point);
    collector->hit.normal = ToAura(normal);
    collector->hasHit = true;
    return fraction;
}

/* A query shape in world space: a point cloud with a radius (circle = 1 point, capsule = 2, box = 4 corners). */
struct QueryShape
{
    b2ShapeProxy proxy;
    b2Vec2 reference;
};

QueryShape MakeShape(const b2Vec2* localPoints, int count, float radius, b2Vec2 position, float angle)
{
    QueryShape shape{};
    shape.proxy = b2MakeOffsetProxy(localPoints, count, radius, position, b2MakeRot(angle));
    shape.reference = position;
    return shape;
}

QueryShape MakeCircle(const AuraVec3& center, float radius)
{
    const b2Vec2 origin{ 0.0f, 0.0f };
    return MakeShape(&origin, 1, radius, ToVec2(center), 0.0f);
}

QueryShape MakeBox(const AuraVec3& center, float halfX, float halfY, float angle)
{
    const b2Vec2 corners[4] = { { -halfX, -halfY }, { halfX, -halfY }, { halfX, halfY }, { -halfX, halfY } };
    return MakeShape(corners, 4, 0.0f, ToVec2(center), angle);
}

/* Capsule from two world points: the segment is expressed around its midpoint so the reference is the centre. */
QueryShape MakeCapsule(const AuraVec3& pointA, const AuraVec3& pointB, float radius)
{
    const b2Vec2 a = ToVec2(pointA);
    const b2Vec2 b = ToVec2(pointB);
    const b2Vec2 mid{ (a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f };
    const b2Vec2 local[2] = { b2Sub(a, mid), b2Sub(b, mid) };
    return MakeShape(local, 2, radius, mid, 0.0f);
}

bool ValidShapeDesc(const AuraShapeDesc& desc, const AuraPose& pose, QueryShape& out)
{
    if (!IsFinite(pose.position) || !IsFinite(pose.rotation.z) || !IsFinite(pose.rotation.w)
        || !IsFinite(desc.localPose.position) || !IsFinite(desc.localPose.rotation.z) || !IsFinite(desc.localPose.rotation.w))
        return false;

    const float poseAngle = AngleFromQuat(pose.rotation);
    const float localAngle = AngleFromQuat(desc.localPose.rotation);
    const b2Vec2 offset = b2RotateVector(b2MakeRot(poseAngle), ToVec2(desc.localPose.position));
    const AuraVec3 center{ pose.position.x + offset.x, pose.position.y + offset.y, 0.0f };
    const float angle = poseAngle + localAngle;

    switch (desc.type)
    {
    case AURA_SHAPE_SPHERE:
        if (!IsFinite(desc.radius) || desc.radius <= 0.0f)
            return false;
        out = MakeCircle(center, desc.radius);
        return true;
    case AURA_SHAPE_BOX:
        if (!IsFinite(desc.halfExtents) || desc.halfExtents.x < 0.0f || desc.halfExtents.y < 0.0f)
            return false;
        out = MakeBox(center, desc.halfExtents.x, desc.halfExtents.y, angle);
        return true;
    case AURA_SHAPE_CAPSULE:
    case AURA_SHAPE_CYLINDER:
    {
        if (!IsFinite(desc.radius) || !IsFinite(desc.height) || desc.radius < 0.0f || desc.height < 0.0f)
            return false;
        const float half = std::max(0.0f, desc.height * 0.5f - desc.radius);
        const b2Vec2 local[2] = { { 0.0f, -half }, { 0.0f, half } };
        out = MakeShape(local, 2, desc.radius, ToVec2(center), angle);
        return true;
    }
    default:
        return false;
    }
}

} // namespace

namespace
{
AuraResultCode RunOverlapWorld(b2WorldId world, const QueryShape& shape, const AuraQueryFilter& filter,
                               AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    if (buffer == nullptr || outCount == nullptr || capacity == 0)
        return AURA_INVALID_DEFINITION;
    *outCount = 0;

    OverlapCollector collector{};
    collector.state = MakeFilterState(filter);
    collector.reference = shape.reference;
    collector.buffer = buffer;
    collector.capacity = capacity;
    b2World_OverlapShape(world, &shape.proxy, MakeB2Filter(collector.state), &CollectOverlap, &collector);
    *outCount = collector.count;
    return AURA_SUCCESS;
}

AuraResultCode RunCastWorld(b2WorldId world, const QueryShape& shape, const AuraVec3& direction, float maxDistance,
                            const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit)
{
    if (outHit == nullptr || outHasHit == nullptr || !IsFinite(maxDistance) || maxDistance <= 0.0f)
        return AURA_INVALID_DEFINITION;
    *outHasHit = false;
    if (!IsFinite(direction))
        return AURA_INVALID_DEFINITION;

    const b2Vec2 unit = b2Normalize(ToVec2(direction));
    if (unit.x * unit.x + unit.y * unit.y <= 1e-12f)
        return AURA_SUCCESS;

    CastCollector collector{};
    collector.state = MakeFilterState(filter);
    collector.maxDistance = maxDistance;
    b2World_CastShape(world, &shape.proxy, b2Vec2{ unit.x * maxDistance, unit.y * maxDistance }, MakeB2Filter(collector.state), &CollectCast, &collector);
    if (!collector.hasHit)
        return AURA_SUCCESS;
    *outHit = collector.hit;
    *outHasHit = true;
    return AURA_SUCCESS;
}
} // namespace

uint32_t Box2DWorld::OverlapSphere(const AuraVec3& center, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity)
{
    uint32_t count = 0;
    if (!IsFinite(center) || !IsFinite(radius) || radius <= 0.0f)
        return 0;
    return RunOverlapWorld(impl_->world, MakeCircle(center, radius), filter, buffer, capacity, &count) == AURA_SUCCESS ? count : 0;
}

AuraResultCode Box2DWorld::OverlapPoint(const AuraVec3& point, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    if (!IsFinite(point))
        return AURA_INVALID_DEFINITION;
    /* Tiny radius mirrors the Jolt point probe and keeps the GJK proxy non-degenerate. */
    return RunOverlapWorld(impl_->world, MakeCircle(point, 1.0e-4f), filter, buffer, capacity, outCount);
}

AuraResultCode Box2DWorld::OverlapBox(const AuraVec3& center, const AuraVec3& halfExtents, const AuraQuat& rotation, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    if (!IsFinite(center) || !IsFinite(halfExtents) || !IsFinite(rotation.z) || !IsFinite(rotation.w) || halfExtents.x < 0.0f || halfExtents.y < 0.0f)
        return AURA_INVALID_DEFINITION;
    return RunOverlapWorld(impl_->world, MakeBox(center, halfExtents.x, halfExtents.y, AngleFromQuat(rotation)), filter, buffer, capacity, outCount);
}

AuraResultCode Box2DWorld::OverlapCapsule(const AuraVec3& pointA, const AuraVec3& pointB, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    if (!IsFinite(pointA) || !IsFinite(pointB) || !IsFinite(radius) || radius < 0.0f)
        return AURA_INVALID_DEFINITION;
    return RunOverlapWorld(impl_->world, MakeCapsule(pointA, pointB, radius), filter, buffer, capacity, outCount);
}

AuraResultCode Box2DWorld::OverlapShape(const AuraShapeDesc& desc, const AuraPose& pose, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    if (desc.type != AURA_SHAPE_BOX && desc.type != AURA_SHAPE_SPHERE && desc.type != AURA_SHAPE_CAPSULE && desc.type != AURA_SHAPE_CYLINDER)
        return AURA_UNSUPPORTED_SHAPE;
    QueryShape shape{};
    if (!ValidShapeDesc(desc, pose, shape))
        return AURA_INVALID_DEFINITION;
    return RunOverlapWorld(impl_->world, shape, filter, buffer, capacity, outCount);
}

AuraResultCode Box2DWorld::SphereCast(const AuraVec3& origin, float radius, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit)
{
    if (!IsFinite(origin) || !IsFinite(radius) || radius <= 0.0f)
        return AURA_INVALID_DEFINITION;
    return RunCastWorld(impl_->world, MakeCircle(origin, radius), direction, maxDistance, filter, outHit, outHasHit);
}

AuraResultCode Box2DWorld::CapsuleCast(const AuraVec3& pointA, const AuraVec3& pointB, float radius, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit)
{
    if (!IsFinite(pointA) || !IsFinite(pointB) || !IsFinite(radius) || radius < 0.0f)
        return AURA_INVALID_DEFINITION;
    return RunCastWorld(impl_->world, MakeCapsule(pointA, pointB, radius), direction, maxDistance, filter, outHit, outHasHit);
}

AuraResultCode Box2DWorld::BoxCast(const AuraVec3& center, const AuraVec3& halfExtents, const AuraQuat& rotation, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit)
{
    if (!IsFinite(center) || !IsFinite(halfExtents) || !IsFinite(rotation.z) || !IsFinite(rotation.w) || halfExtents.x < 0.0f || halfExtents.y < 0.0f)
        return AURA_INVALID_DEFINITION;
    return RunCastWorld(impl_->world, MakeBox(center, halfExtents.x, halfExtents.y, AngleFromQuat(rotation)), direction, maxDistance, filter, outHit, outHasHit);
}

AuraResultCode Box2DWorld::ShapeCast(const AuraShapeDesc& desc, const AuraPose& pose, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit)
{
    if (desc.type != AURA_SHAPE_BOX && desc.type != AURA_SHAPE_SPHERE && desc.type != AURA_SHAPE_CAPSULE && desc.type != AURA_SHAPE_CYLINDER)
        return AURA_UNSUPPORTED_SHAPE;
    QueryShape shape{};
    if (!ValidShapeDesc(desc, pose, shape))
        return AURA_INVALID_DEFINITION;
    return RunCastWorld(impl_->world, shape, direction, maxDistance, filter, outHit, outHasHit);
}

} // namespace aura
