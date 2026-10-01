using System;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public interface IPhysicsQuery
    {
        bool Raycast(in AuraRay ray, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit);

        int RaycastAll(in AuraRay ray, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results);

        bool SphereCast(
            AuraVector3 origin,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit);

        int SphereCastAll(
            AuraVector3 origin,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results);

        bool CapsuleCast(
            AuraVector3 pointA,
            AuraVector3 pointB,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit);

        int CapsuleCastAll(
            AuraVector3 pointA,
            AuraVector3 pointB,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results);

        bool BoxCast(
            AuraVector3 center,
            AuraVector3 halfExtents,
            AuraQuaternion rotation,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit);

        int BoxCastAll(
            AuraVector3 center,
            AuraVector3 halfExtents,
            AuraQuaternion rotation,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results);

        bool ShapeCast(
            in AuraPhysicsShapeDefinition shape,
            AuraPose pose,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit);

        int ShapeCastAll(
            in AuraPhysicsShapeDefinition shape,
            AuraPose pose,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results);

        int OverlapPoint(AuraVector3 point, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results);

        int OverlapSphere(
            AuraVector3 center,
            float radius,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results);

        int OverlapBox(
            AuraVector3 center,
            AuraVector3 halfExtents,
            AuraQuaternion rotation,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results);

        int OverlapCapsule(
            AuraVector3 pointA,
            AuraVector3 pointB,
            float radius,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results);

        int OverlapShape(
            in AuraPhysicsShapeDefinition shape,
            AuraPose pose,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results);
    }
}
