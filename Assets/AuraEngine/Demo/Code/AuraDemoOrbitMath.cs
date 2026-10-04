using System;
using AuraEngine.Core;

namespace AuraEngine.Demo
{
    /* Mirrors the radial force field formula of the kernel (aura_force_fields.cpp) so a body can be given the speed
       of a circular orbit around the field centre. Plain C#, no engine references. */
    public static class AuraDemoOrbitMath
    {
        private const float MinDistance = 0.01f;

        /* Zone extent the kernel uses as the linear falloff reach when MaxRadius is 0: the sphere radius or the
           half diagonal of the box. */
        public static float ZoneExtent(AuraForceFieldShape shape, float radius, float halfX, float halfY, float halfZ) =>
            shape == AuraForceFieldShape.Sphere
                ? radius
                : (float)Math.Sqrt(halfX * halfX + halfY * halfY + halfZ * halfZ);

        /* Field strength toward the centre at the given distance (before the per-body acceleration scale). Zero
           beyond maxRadius (when > 0) or for a non-positive result. */
        public static float Magnitude(AuraForceFieldFalloff falloff, float strength, float distance, float minRadius, float maxRadius, float zoneExtent)
        {
            if (distance < 1e-6f || (maxRadius > 0f && distance > maxRadius))
                return 0f;

            var effective = Math.Max(distance, minRadius);
            switch (falloff)
            {
                case AuraForceFieldFalloff.Linear:
                    var reach = maxRadius > 0f ? maxRadius : zoneExtent;
                    return strength * Math.Max(0f, 1f - effective / reach);
                case AuraForceFieldFalloff.InverseSquare:
                    var clamped = Math.Max(effective, MinDistance);
                    return strength / (clamped * clamped);
                default:
                    return strength;
            }
        }

        /* Speed of a circular orbit, v = sqrt(a * r) with a = Magnitude * accelerationScale. The scale is the body
           gravity scale for Acceleration mode fields and 1 / mass for Force mode fields. Returns 0 when the field
           does not pull at that distance. */
        public static float CircularSpeed(AuraForceFieldFalloff falloff, float strength, float distance, float minRadius, float maxRadius, float zoneExtent, float accelerationScale)
        {
            var acceleration = Magnitude(falloff, strength, distance, minRadius, maxRadius, zoneExtent) * accelerationScale;
            return acceleration > 0f ? (float)Math.Sqrt(acceleration * distance) : 0f;
        }
    }
}
