#include "aura_jolt_internal.h"

#include <Jolt/Physics/Collision/ObjectLayer.h>
#include <Jolt/Physics/Collision/ShapeCast.h>

namespace aura
{
namespace
{
/* Query filters mirrored from the reference backend so both backends honour
   AuraQueryFilter.layerMask and the "ignore body" flag (flags bit 2). */
class QueryLayerFilter final : public JPH::ObjectLayerFilter
{
public:
    uint64_t mask = ~0ull;

    bool ShouldCollide(JPH::ObjectLayer layer) const override
    {
        return layer < 64 && ((mask >> layer) & 1ull) != 0ull;
    }
};
} // namespace

namespace
{
struct CastShapeCollector final : public JPH::CastShapeCollector
{
    JPH::ShapeCastResult result;
    bool hasHit = false;
    void AddHit(const JPH::ShapeCastResult& hit) override
    {
        if (!hasHit || hit.GetEarlyOutFraction() < result.GetEarlyOutFraction()) { result = hit; hasHit = true; }
    }
};

void ConfigureShapeFilter(AuraQueryShapeFilter& result, const AuraQueryFilter& filter,
                          const std::unordered_map<uint32_t, uint32_t>& groups,
                          const std::unordered_map<uint32_t, uint32_t>& masks)
{
    result.groups = &groups;
    result.masks = &masks;
    result.group = filter.shapeFilterGroup == 0u ? 1u : filter.shapeFilterGroup;
    result.mask = filter.shapeFilterMask == 0u ? ~0u : filter.shapeFilterMask;
}

uint32_t MaterialIndex(const JPH::Body& body, const JPH::SubShapeID& subShape)
{
    const JPH::Shape* shape = body.GetShape();
    const JPH::MeshShape* mesh = shape != nullptr && shape->GetSubType() == JPH::EShapeSubType::Mesh
        ? static_cast<const JPH::MeshShape*>(shape) : nullptr;
    return mesh != nullptr ? mesh->GetMaterialIndex(subShape) : 0u;
}

template <typename TImpl>
AuraResultCode FillShapeOverlap(TImpl* impl, const JPH::Shape* shape, const JPH::RMat44& transform, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    if (shape == nullptr || buffer == nullptr || outCount == nullptr || capacity == 0)
        return AURA_INVALID_DEFINITION;
    QueryLayerFilter layerFilter;
    layerFilter.mask = filter.layerMask;
    const JPH::IgnoreSingleBodyFilter bodyFilter(impl->IgnoredBody(filter));
    AuraQueryShapeFilter shapeFilter;
    ConfigureShapeFilter(shapeFilter, filter, impl->shapeFilterGroups, impl->shapeFilterMasks);
    JPH::CollideShapeSettings settings;
    settings.mActiveEdgeMode = filter.activeEdgeMode == AURA_ACTIVE_EDGES_ALL ? JPH::EActiveEdgeMode::CollideWithAll : JPH::EActiveEdgeMode::CollideOnlyWithActive;
    settings.mActiveEdgeMovementDirection = ToVec3(filter.activeEdgeMovementDirection);
    ShapeCollector collector;
    impl->physics.GetNarrowPhaseQuery().CollideShape(shape, JPH::Vec3::sReplicate(1.0f), transform, settings, JPH::RVec3::sZero(), collector, JPH::BroadPhaseLayerFilter(), layerFilter, bodyFilter, shapeFilter);
    *outCount = std::min(collector.count, capacity);
    for (uint32_t i = 0; i < *outCount; ++i)
    {
        const JPH::CollideShapeResult& result = collector.results[i];
        buffer[i].entity = AuraEntityHandle{ 0, 0 };
        buffer[i].body = impl->HandleFromBodyId(result.mBodyID2);
        buffer[i].shape = result.mBodyID2.GetIndex();
        buffer[i].distance = result.mPenetrationDepth;
        buffer[i].point = ToAura(JPH::RVec3(result.mContactPointOn2));
        buffer[i].normal = ToAura(result.mPenetrationAxis.NormalizedOr(JPH::Vec3::sZero()));
        const JPH::Body* body = impl->physics.GetBodyLockInterface().TryGetBody(result.mBodyID2);
        buffer[i].materialIndex = body != nullptr ? MaterialIndex(*body, result.mSubShapeID2) : 0u;
    }
    return AURA_SUCCESS;
}

JPH::RMat44 Transform(const AuraPose& pose)
{
    const float norm = pose.rotation.x * pose.rotation.x + pose.rotation.y * pose.rotation.y + pose.rotation.z * pose.rotation.z + pose.rotation.w * pose.rotation.w;
    const AuraQuat rotation = norm > 1e-8f ? pose.rotation : AuraQuat{ 0.0f, 0.0f, 0.0f, 1.0f };
    return JPH::RMat44::sRotationTranslation(ToQuat(rotation), ToRVec3(pose.position));
}
JPH::RMat44 ShapeTransform(const AuraPose& pose, const AuraShapeDesc& shape) { return Transform(pose) * Transform(shape.localPose); }

AuraShapeDesc CapsuleDescription(const AuraVec3& pointA, const AuraVec3& pointB, float radius, AuraPose& pose)
{
    const JPH::Vec3 axis = ToVec3(AuraVec3{ pointB.x - pointA.x, pointB.y - pointA.y, pointB.z - pointA.z });
    const float length = axis.Length();
    pose.position = AuraVec3{ (pointA.x + pointB.x) * 0.5f, (pointA.y + pointB.y) * 0.5f, (pointA.z + pointB.z) * 0.5f };
    pose.rotation = ToAura(length > 1e-6f ? JPH::Quat::sFromTo(JPH::Vec3::sAxisY(), axis / length) : JPH::Quat::sIdentity());
    AuraShapeDesc desc{}; desc.type = AURA_SHAPE_CAPSULE; desc.radius = radius; desc.height = length + radius * 2.0f; return desc;
}
} // namespace

/* Jolt query surface (raycast / overlap). New query families belong here. */
JPH::Vec3 JoltWorld::Impl::SurfaceNormal(JPH::BodyID id, const JPH::SubShapeID& subShape, JPH::RVec3Arg point) const
{
    const JPH::Body* body = physics.GetBodyLockInterface().TryGetBody(id);
    return body != nullptr ? body->GetWorldSpaceSurfaceNormal(subShape, point) : JPH::Vec3::sZero();
}

bool JoltWorld::Raycast(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit)
{
    if (outHit == nullptr || maxDistance <= 0.0f)
        return false;

    const JPH::Vec3 unit = ToVec3(ray.direction).Normalized();
    if (unit.LengthSq() <= 1e-12f)
        return false;

    QueryLayerFilter layerFilter;
    layerFilter.mask = filter.layerMask;
    const JPH::IgnoreSingleBodyFilter bodyFilter(impl_->IgnoredBody(filter));
    AuraQueryShapeFilter shapeFilter;
    ConfigureShapeFilter(shapeFilter, filter, impl_->shapeFilterGroups, impl_->shapeFilterMasks);

    const JPH::Vec3 direction = unit * maxDistance;
    JPH::RRayCast joltRay(ToRVec3(ray.origin), direction);
    JPH::RayCastResult result;
    RayCollector rayCollector;
    JPH::RayCastSettings raySettings;
    impl_->physics.GetNarrowPhaseQuery().CastRay(joltRay, raySettings, rayCollector, JPH::BroadPhaseLayerFilter(), layerFilter, bodyFilter, shapeFilter);
    if (rayCollector.count == 0)
        return false;
    result = rayCollector.results[0];

    const float distance = result.mFraction * maxDistance;

    const JPH::RVec3 point = joltRay.GetPointOnRay(result.mFraction);
    const JPH::Vec3 normal = impl_->SurfaceNormal(result.mBodyID, result.mSubShapeID2, point);

    outHit->entity = AuraEntityHandle{ 0, 0 };
    outHit->body = impl_->HandleFromBodyId(result.mBodyID);
    outHit->shape = result.mBodyID.GetIndex();
    outHit->distance = distance;
    outHit->point = ToAura(point);
    outHit->normal = ToAura(normal);
    const JPH::Body* body = impl_->physics.GetBodyLockInterface().TryGetBody(result.mBodyID);
    outHit->materialIndex = body != nullptr ? MaterialIndex(*body, result.mSubShapeID2) : 0u;
    return true;
}

uint32_t JoltWorld::RaycastAll(const AuraRay& ray, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity)
{
    if (buffer == nullptr || capacity == 0)
        return 0;

    const JPH::Vec3 unit = ToVec3(ray.direction).Normalized();
    if (unit.LengthSq() <= 1e-12f)
        return 0;

    QueryLayerFilter layerFilter;
    layerFilter.mask = filter.layerMask;
    const JPH::IgnoreSingleBodyFilter bodyFilter(impl_->IgnoredBody(filter));
    AuraQueryShapeFilter shapeFilter;
    ConfigureShapeFilter(shapeFilter, filter, impl_->shapeFilterGroups, impl_->shapeFilterMasks);

    JPH::RRayCast joltRay(ToRVec3(ray.origin), unit * maxDistance);
    RayCollector collector;
    JPH::RayCastSettings settings;
    impl_->physics.GetNarrowPhaseQuery().CastRay(joltRay, settings, collector, JPH::BroadPhaseLayerFilter(), layerFilter, bodyFilter, shapeFilter);

    uint32_t count = 0;
    for (uint32_t i = 0; i < collector.count && count < capacity; ++i)
    {
        const JPH::RayCastResult& result = collector.results[i];
        const float distance = result.mFraction * maxDistance;

        const JPH::RVec3 point = joltRay.GetPointOnRay(result.mFraction);
        buffer[count].entity = AuraEntityHandle{ 0, 0 };
        buffer[count].body = impl_->HandleFromBodyId(result.mBodyID);
        buffer[count].shape = result.mBodyID.GetIndex();
        buffer[count].distance = distance;
        buffer[count].point = ToAura(point);
        buffer[count].normal = ToAura(impl_->SurfaceNormal(result.mBodyID, result.mSubShapeID2, point));
        const JPH::Body* body = impl_->physics.GetBodyLockInterface().TryGetBody(result.mBodyID);
        buffer[count].materialIndex = body != nullptr ? MaterialIndex(*body, result.mSubShapeID2) : 0u;
        ++count;
    }
    return count;
}

uint32_t JoltWorld::OverlapSphere(const AuraVec3& center, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity)
{
    if (buffer == nullptr || capacity == 0 || radius <= 0.0f)
        return 0;

    QueryLayerFilter layerFilter;
    layerFilter.mask = filter.layerMask;
    const JPH::IgnoreSingleBodyFilter bodyFilter(impl_->IgnoredBody(filter));
    AuraQueryShapeFilter shapeFilter;
    ConfigureShapeFilter(shapeFilter, filter, impl_->shapeFilterGroups, impl_->shapeFilterMasks);

    const JPH::SphereShape shape(radius);
    const JPH::RMat44 transform = JPH::RMat44::sTranslation(ToRVec3(center));
    JPH::CollideShapeSettings settings;
    settings.mMaxSeparationDistance = 0.0f;
    ShapeCollector collector;
    impl_->physics.GetNarrowPhaseQuery().CollideShape(&shape, JPH::Vec3::sReplicate(1.0f), transform, settings, JPH::RVec3::sZero(), collector, JPH::BroadPhaseLayerFilter(), layerFilter, bodyFilter, shapeFilter);

    uint32_t count = 0;
    for (uint32_t i = 0; i < collector.count && count < capacity; ++i)
    {
        const JPH::CollideShapeResult& result = collector.results[i];
        buffer[count].entity = AuraEntityHandle{ 0, 0 };
        buffer[count].body = impl_->HandleFromBodyId(result.mBodyID2);
        buffer[count].shape = result.mBodyID2.GetIndex();
        buffer[count].distance = result.mPenetrationDepth;
        buffer[count].point = ToAura(JPH::RVec3(result.mContactPointOn2));
        buffer[count].normal = ToAura(result.mPenetrationAxis.Normalized());
        const JPH::Body* body = impl_->physics.GetBodyLockInterface().TryGetBody(result.mBodyID2);
        buffer[count].materialIndex = body != nullptr ? MaterialIndex(*body, result.mSubShapeID2) : 0u;
        ++count;
    }
    return count;
}

AuraResultCode JoltWorld::OverlapPoint(const AuraVec3& point, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{ const JPH::SphereShape shape(0.0001f); return FillShapeOverlap(impl_, &shape, JPH::RMat44::sTranslation(ToRVec3(point)), filter, buffer, capacity, outCount); }
AuraResultCode JoltWorld::OverlapBox(const AuraVec3& center, const AuraVec3& halfExtents, const AuraQuat& rotation, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{ const JPH::BoxShape shape(ToVec3(halfExtents)); return FillShapeOverlap(impl_, &shape, Transform(AuraPose{ center, rotation }), filter, buffer, capacity, outCount); }
AuraResultCode JoltWorld::OverlapCapsule(const AuraVec3& pointA, const AuraVec3& pointB, float radius, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{ AuraPose pose{}; const AuraShapeDesc desc = CapsuleDescription(pointA, pointB, radius, pose); bool sensor = false; const JPH::RefConst<JPH::Shape> shape = MakeShape(desc, sensor); return FillShapeOverlap(impl_, shape.GetPtr(), Transform(pose), filter, buffer, capacity, outCount); }
AuraResultCode JoltWorld::OverlapShape(const AuraShapeDesc& desc, const AuraPose& pose, const AuraQueryFilter& filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{ bool sensor = false; const JPH::RefConst<JPH::Shape> shape = MakeShape(desc, sensor); return FillShapeOverlap(impl_, shape.GetPtr(), ShapeTransform(pose, desc), filter, buffer, capacity, outCount); }

AuraResultCode JoltWorld::ShapeCast(const AuraShapeDesc& desc, const AuraPose& pose, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit)
{
    if (outHit == nullptr || outHasHit == nullptr || maxDistance <= 0.0f) return AURA_INVALID_DEFINITION;
    *outHasHit = false; bool sensor = false; const JPH::RefConst<JPH::Shape> shape = MakeShape(desc, sensor);
    if (shape == nullptr) return AURA_UNSUPPORTED_SHAPE;
    const JPH::Vec3 unit = ToVec3(direction).Normalized(); if (unit.LengthSq() <= 1e-12f) return AURA_SUCCESS;
    const JPH::RShapeCast cast = JPH::RShapeCast::sFromWorldTransform(shape.GetPtr(), JPH::Vec3::sReplicate(1.0f), ShapeTransform(pose, desc), unit * maxDistance);
    CastShapeCollector collector; JPH::ShapeCastSettings settings; QueryLayerFilter layerFilter; layerFilter.mask = filter.layerMask;
    settings.mActiveEdgeMode = filter.activeEdgeMode == AURA_ACTIVE_EDGES_ALL ? JPH::EActiveEdgeMode::CollideWithAll : JPH::EActiveEdgeMode::CollideOnlyWithActive;
    settings.mActiveEdgeMovementDirection = ToVec3(filter.activeEdgeMovementDirection);
    const JPH::IgnoreSingleBodyFilter bodyFilter(impl_->IgnoredBody(filter));
    AuraQueryShapeFilter shapeFilter;
    ConfigureShapeFilter(shapeFilter, filter, impl_->shapeFilterGroups, impl_->shapeFilterMasks);
    impl_->physics.GetNarrowPhaseQuery().CastShape(cast, settings, JPH::RVec3::sZero(), collector, JPH::BroadPhaseLayerFilter(), layerFilter, bodyFilter, shapeFilter);
    if (!collector.hasHit) return AURA_SUCCESS;
    const JPH::ShapeCastResult& result = collector.result;
    outHit->entity = AuraEntityHandle{ 0, 0 }; outHit->body = impl_->HandleFromBodyId(result.mBodyID2); outHit->shape = result.mBodyID2.GetIndex();
    outHit->distance = result.mFraction * maxDistance; outHit->point = ToAura(JPH::RVec3(result.mContactPointOn2)); outHit->normal = ToAura(result.mPenetrationAxis.NormalizedOr(JPH::Vec3::sZero())); *outHasHit = true;
    const JPH::Body* body = impl_->physics.GetBodyLockInterface().TryGetBody(result.mBodyID2);
    outHit->materialIndex = body != nullptr ? MaterialIndex(*body, result.mSubShapeID2) : 0u;
    return AURA_SUCCESS;
}

AuraResultCode JoltWorld::SphereCast(const AuraVec3& origin, float radius, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit)
{ AuraShapeDesc desc{}; desc.type = AURA_SHAPE_SPHERE; desc.radius = radius; return ShapeCast(desc, AuraPose{ origin, AuraQuat{ 0, 0, 0, 1 } }, direction, maxDistance, filter, outHit, outHasHit); }
AuraResultCode JoltWorld::CapsuleCast(const AuraVec3& pointA, const AuraVec3& pointB, float radius, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit)
{ AuraPose pose{}; const AuraShapeDesc desc = CapsuleDescription(pointA, pointB, radius, pose); return ShapeCast(desc, pose, direction, maxDistance, filter, outHit, outHasHit); }
AuraResultCode JoltWorld::BoxCast(const AuraVec3& center, const AuraVec3& halfExtents, const AuraQuat& rotation, const AuraVec3& direction, float maxDistance, const AuraQueryFilter& filter, AuraQueryHit* outHit, bool* outHasHit)
{ AuraShapeDesc desc{}; desc.type = AURA_SHAPE_BOX; desc.halfExtents = halfExtents; return ShapeCast(desc, AuraPose{ center, rotation }, direction, maxDistance, filter, outHit, outHasHit); }

} // namespace aura
