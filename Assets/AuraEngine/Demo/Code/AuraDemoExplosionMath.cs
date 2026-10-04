using System;
using AuraEngine.Core;

namespace AuraEngine.Demo
{
    /* Radial blast impulse with distance falloff. Plain C#, no engine references. */
    public static class AuraDemoExplosionMath
    {
        /* Impulse on a body at position for a blast at center. Zero outside radius. The direction points away from
           the centre, tilted toward +Y by upwardBias (0 = pure radial, 1 = equal parts up). */
        public static AuraVector3 RadialImpulse(AuraVector3 center, AuraVector3 position, float magnitude, float radius, float upwardBias, bool linearFalloff)
        {
            if (!(radius > 0f))
                return AuraVector3.Zero;

            var offset = position - center;
            var distance = offset.Length;
            if (distance > radius)
                return AuraVector3.Zero;

            var direction = distance < 1e-4f ? AuraVector3.UnitY : offset / distance;
            direction = new AuraVector3(direction.X, direction.Y + Math.Max(0f, upwardBias), direction.Z).Normalized();
            var scale = linearFalloff ? 1f - distance / radius : 1f;
            return direction * (magnitude * scale);
        }
    }
}
