#include "aura_capi_internal.h"

extern "C"
{

AuraResultCode Aura_Raycast(AuraWorldHandle world, const AuraRay* ray, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || ray == nullptr || filter == nullptr || outHit == nullptr || outHasHit == nullptr)
        return AURA_INVALID_WORLD;
    *outHasHit = instance->Raycast(*ray, maxDistance, *filter, outHit) ? 1 : 0;
    return AURA_SUCCESS;
}

AuraResultCode Aura_RaycastAll(AuraWorldHandle world, const AuraRay* ray, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || ray == nullptr || filter == nullptr || buffer == nullptr || outCount == nullptr)
        return AURA_INVALID_WORLD;
    *outCount = instance->RaycastAll(*ray, maxDistance, *filter, buffer, capacity);
    return AURA_SUCCESS;
}

AuraResultCode Aura_OverlapSphere(AuraWorldHandle world, AuraVec3 center, float radius, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{
    auto* instance = aura::ToWorld(world);
    if (instance == nullptr || filter == nullptr || buffer == nullptr || outCount == nullptr)
        return AURA_INVALID_WORLD;
    *outCount = instance->OverlapSphere(center, radius, *filter, buffer, capacity);
    return AURA_SUCCESS;
}

AuraResultCode Aura_OverlapPoint(AuraWorldHandle world, AuraVec3 point, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{ auto* instance = aura::ToWorld(world); if (instance == nullptr || filter == nullptr || buffer == nullptr || outCount == nullptr) return AURA_INVALID_WORLD; return instance->OverlapPoint(point, *filter, buffer, capacity, outCount); }
AuraResultCode Aura_OverlapBox(AuraWorldHandle world, AuraVec3 center, AuraVec3 halfExtents, AuraQuat rotation, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{ auto* instance = aura::ToWorld(world); if (instance == nullptr || filter == nullptr || buffer == nullptr || outCount == nullptr) return AURA_INVALID_WORLD; return instance->OverlapBox(center, halfExtents, rotation, *filter, buffer, capacity, outCount); }
AuraResultCode Aura_OverlapCapsule(AuraWorldHandle world, AuraVec3 pointA, AuraVec3 pointB, float radius, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{ auto* instance = aura::ToWorld(world); if (instance == nullptr || filter == nullptr || buffer == nullptr || outCount == nullptr) return AURA_INVALID_WORLD; return instance->OverlapCapsule(pointA, pointB, radius, *filter, buffer, capacity, outCount); }

AuraResultCode Aura_OverlapShape(AuraWorldHandle world, const AuraShapeDesc* shape, const AuraPose* pose, const AuraQueryFilter* filter, AuraQueryHit* buffer, uint32_t capacity, uint32_t* outCount)
{ auto* instance = aura::ToWorld(world); if (instance == nullptr || shape == nullptr || pose == nullptr || filter == nullptr || buffer == nullptr || outCount == nullptr) return AURA_INVALID_WORLD; return instance->OverlapShape(*shape, *pose, *filter, buffer, capacity, outCount); }

AuraResultCode Aura_SphereCast(AuraWorldHandle world, AuraVec3 origin, float radius, AuraVec3 direction, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit)
{ auto* instance = aura::ToWorld(world); if (instance == nullptr || filter == nullptr || outHit == nullptr || outHasHit == nullptr) return AURA_INVALID_WORLD; bool hasHit = false; const AuraResultCode result = instance->SphereCast(origin, radius, direction, maxDistance, *filter, outHit, &hasHit); *outHasHit = hasHit ? 1 : 0; return result; }
AuraResultCode Aura_CapsuleCast(AuraWorldHandle world, AuraVec3 pointA, AuraVec3 pointB, float radius, AuraVec3 direction, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit)
{ auto* instance = aura::ToWorld(world); if (instance == nullptr || filter == nullptr || outHit == nullptr || outHasHit == nullptr) return AURA_INVALID_WORLD; bool hasHit = false; const AuraResultCode result = instance->CapsuleCast(pointA, pointB, radius, direction, maxDistance, *filter, outHit, &hasHit); *outHasHit = hasHit ? 1 : 0; return result; }
AuraResultCode Aura_BoxCast(AuraWorldHandle world, AuraVec3 center, AuraVec3 halfExtents, AuraQuat rotation, AuraVec3 direction, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit)
{ auto* instance = aura::ToWorld(world); if (instance == nullptr || filter == nullptr || outHit == nullptr || outHasHit == nullptr) return AURA_INVALID_WORLD; bool hasHit = false; const AuraResultCode result = instance->BoxCast(center, halfExtents, rotation, direction, maxDistance, *filter, outHit, &hasHit); *outHasHit = hasHit ? 1 : 0; return result; }

AuraResultCode Aura_ShapeCast(AuraWorldHandle world, const AuraShapeDesc* shape, const AuraPose* pose, AuraVec3 direction, float maxDistance, const AuraQueryFilter* filter, AuraQueryHit* outHit, uint8_t* outHasHit)
{ auto* instance = aura::ToWorld(world); if (instance == nullptr || shape == nullptr || pose == nullptr || filter == nullptr || outHit == nullptr || outHasHit == nullptr) return AURA_INVALID_WORLD; bool hasHit = false; const AuraResultCode result = instance->ShapeCast(*shape, *pose, direction, maxDistance, *filter, outHit, &hasHit); *outHasHit = hasHit ? 1 : 0; return result; }

} // extern "C"
