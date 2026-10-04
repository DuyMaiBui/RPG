using System;

namespace AuraEngine.Core
{
    /// <summary>
    /// Allocation-free signed-distance queries against <see cref="AuraVerletCollider"/>.
    /// </summary>
    public static class AuraVerletCollisions
    {
        private const float PlanarInfinity = 1e9f;

        /// <summary>
        /// Computes how far <paramref name="position"/> must move along <paramref name="normal"/> to sit
        /// <see cref="AuraVerletCollider.Skin"/> outside the surface. <paramref name="depth"/> is
        /// skin minus signed distance: positive means penetrating the skin shell.
        /// </summary>
        public static void Query(in AuraVerletCollider collider, AuraVector3 position, out AuraVector3 normal, out float depth)
        {
            var delta = position - collider.Position;
            if (collider.Planar)
                delta = new AuraVector3(delta.X, delta.Y, 0f);

            float signedDistance;
            switch (collider.Kind)
            {
                case AuraVerletColliderKind.Sphere:
                    signedDistance = RadialDistance(delta, collider.Radius, out normal);
                    break;

                case AuraVerletColliderKind.Capsule:
                {
                    var axis = collider.Rotation.Rotate(AuraVector3.UnitY);
                    var t = AuraVector3.Dot(delta, axis);
                    var limit = collider.HalfSegment;
                    t = t < -limit ? -limit : (t > limit ? limit : t);
                    signedDistance = RadialDistance(delta - axis * t, collider.Radius, out normal);
                    break;
                }

                case AuraVerletColliderKind.Box:
                    signedDistance = BoxDistance(collider, delta, out normal);
                    break;

                default:
                    normal = collider.Normal;
                    signedDistance = AuraVector3.Dot(delta, normal);
                    break;
            }

            depth = collider.Skin - signedDistance;
        }

        private static float RadialDistance(AuraVector3 offset, float radius, out AuraVector3 normal)
        {
            var lengthSquared = offset.LengthSquared;
            if (lengthSquared <= 1e-12f)
            {
                normal = AuraVector3.UnitY;
                return -radius;
            }

            var length = MathF.Sqrt(lengthSquared);
            normal = offset / length;
            return length - radius;
        }

        private static float BoxDistance(in AuraVerletCollider collider, AuraVector3 delta, out AuraVector3 normal)
        {
            var local = collider.Rotation.InverseRotate(delta);
            var half = collider.HalfExtents;
            var hz = collider.Planar ? PlanarInfinity : half.Z;
            if (collider.Planar)
                local = new AuraVector3(local.X, local.Y, 0f);

            var qx = MathF.Abs(local.X) - half.X;
            var qy = MathF.Abs(local.Y) - half.Y;
            var qz = MathF.Abs(local.Z) - hz;

            var ox = qx > 0f ? qx : 0f;
            var oy = qy > 0f ? qy : 0f;
            var oz = qz > 0f ? qz : 0f;
            var outside = ox * ox + oy * oy + oz * oz;

            AuraVector3 localNormal;
            float distance;
            if (outside > 0f)
            {
                distance = MathF.Sqrt(outside);
                localNormal = new AuraVector3(
                    local.X < 0f ? -ox / distance : ox / distance,
                    local.Y < 0f ? -oy / distance : oy / distance,
                    local.Z < 0f ? -oz / distance : oz / distance);
            }
            else if (qx >= qy && qx >= qz)
            {
                distance = qx;
                localNormal = new AuraVector3(local.X < 0f ? -1f : 1f, 0f, 0f);
            }
            else if (qy >= qz)
            {
                distance = qy;
                localNormal = new AuraVector3(0f, local.Y < 0f ? -1f : 1f, 0f);
            }
            else
            {
                distance = qz;
                localNormal = new AuraVector3(0f, 0f, local.Z < 0f ? -1f : 1f);
            }

            normal = collider.Rotation.Rotate(localNormal);
            if (collider.Planar)
                normal = new AuraVector3(normal.X, normal.Y, 0f).Normalized();
            return distance;
        }
    }
}
