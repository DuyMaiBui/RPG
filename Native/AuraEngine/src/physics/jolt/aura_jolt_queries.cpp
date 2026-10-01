#include "aura_jolt_internal.h"

#include <Jolt/Physics/Collision/ObjectLayer.h>

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

    const JPH::Vec3 direction = unit * maxDistance;
    JPH::RRayCast joltRay(ToRVec3(ray.origin), direction);
    JPH::RayCastResult result;
    if (!impl_->physics.GetNarrowPhaseQuery().CastRay(joltRay, result, JPH::BroadPhaseLayerFilter(), layerFilter, bodyFilter))
        return false;

    const float distance = result.mFraction * maxDistance;

    const JPH::RVec3 point = joltRay.GetPointOnRay(result.mFraction);
    const JPH::Vec3 normal = impl_->SurfaceNormal(result.mBodyID, result.mSubShapeID2, point);

    outHit->entity = AuraEntityHandle{ 0, 0 };
    outHit->body = impl_->HandleFromBodyId(result.mBodyID);
    outHit->shape = result.mBodyID.GetIndex();
    outHit->distance = distance;
    outHit->point = ToAura(point);
    outHit->normal = ToAura(normal);
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

    JPH::RRayCast joltRay(ToRVec3(ray.origin), unit * maxDistance);
    RayCollector collector;
    JPH::RayCastSettings settings;
    impl_->physics.GetNarrowPhaseQuery().CastRay(joltRay, settings, collector, JPH::BroadPhaseLayerFilter(), layerFilter, bodyFilter);

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

    const JPH::SphereShape shape(radius);
    const JPH::RMat44 transform = JPH::RMat44::sTranslation(ToRVec3(center));
    JPH::CollideShapeSettings settings;
    settings.mMaxSeparationDistance = 0.0f;
    ShapeCollector collector;
    impl_->physics.GetNarrowPhaseQuery().CollideShape(&shape, JPH::Vec3::sReplicate(1.0f), transform, settings, JPH::RVec3::sZero(), collector, JPH::BroadPhaseLayerFilter(), layerFilter, bodyFilter, JPH::ShapeFilter());

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
        ++count;
    }
    return count;
}

} // namespace aura
