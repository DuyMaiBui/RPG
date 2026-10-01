using System;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal static class ManagedCollision
    {
        private const float Epsilon = 1e-5f;
        private const float Slop = 0.01f;

        public static bool Contact(in ManagedShapeView a, in ManagedShapeView b, AuraPhysicsMode mode, ManagedManifold manifold)
        {
            manifold.Reset();

            if (a.Kind == ManagedShapeKind.None || b.Kind == ManagedShapeKind.None)
                return false;

            if (a.Kind == ManagedShapeKind.Box && b.Kind == ManagedShapeKind.Box)
                return BoxBox(a, b, mode, manifold);

            if (a.Kind == ManagedShapeKind.Sphere && b.Kind == ManagedShapeKind.Sphere)
                return Single(SingleContact(a.Center, a.Radius, b.Center, b.Radius), manifold);

            if (a.Kind == ManagedShapeKind.Sphere && b.Kind == ManagedShapeKind.Capsule)
                return Single(SphereCapsule(a.Center, a.Radius, b.A, b.B, b.Radius), manifold);

            if (a.Kind == ManagedShapeKind.Capsule && b.Kind == ManagedShapeKind.Sphere)
                return Single(Flip(SphereCapsule(b.Center, b.Radius, a.A, a.B, a.Radius)), manifold);

            if (a.Kind == ManagedShapeKind.Capsule && b.Kind == ManagedShapeKind.Capsule)
                return Single(CapsuleCapsule(a.A, a.B, a.Radius, b.A, b.B, b.Radius), manifold);

            if (a.Kind == ManagedShapeKind.Sphere && b.Kind == ManagedShapeKind.Box)
                return Single(Flip(SphereBox(a.Center, a.Radius, b, mode)), manifold);

            if (a.Kind == ManagedShapeKind.Box && b.Kind == ManagedShapeKind.Sphere)
                return Single(SphereBox(b.Center, b.Radius, a, mode), manifold);

            if (a.Kind == ManagedShapeKind.Capsule && b.Kind == ManagedShapeKind.Box)
                return Single(Flip(CapsuleBox(a.A, a.B, a.Radius, b, mode)), manifold);

            if (a.Kind == ManagedShapeKind.Box && b.Kind == ManagedShapeKind.Capsule)
                return Single(CapsuleBox(b.A, b.B, b.Radius, a, mode), manifold);

            return false;
        }

        public static bool Contains(AuraVector3 point, in ManagedShapeView view, AuraPhysicsMode mode)
        {
            switch (view.Kind)
            {
                case ManagedShapeKind.Sphere:
                    return AuraVector3.Distance(point, view.Center) <= view.Radius;
                case ManagedShapeKind.Capsule:
                    return DistancePointSegment(point, view.A, view.B) <= view.Radius;
                case ManagedShapeKind.Box:
                    var local = view.Rotation.InverseRotate(point - view.Center);
                    if (MathF.Abs(local.X) > view.Extents.X || MathF.Abs(local.Y) > view.Extents.Y)
                        return false;
                    return mode == AuraPhysicsMode.Plane2D || MathF.Abs(local.Z) <= view.Extents.Z;
                default:
                    return false;
            }
        }

        public static bool Ray(in AuraRay ray, in ManagedShapeView view, AuraPhysicsMode mode, float maxDistance, out float distance, out AuraVector3 point, out AuraVector3 normal)
        {
            switch (view.Kind)
            {
                case ManagedShapeKind.Sphere:
                    return RaySphere(ray, view.Center, view.Radius, maxDistance, out distance, out point, out normal);
                case ManagedShapeKind.Box:
                    return RayBox(ray, view, mode, maxDistance, out distance, out point, out normal);
                case ManagedShapeKind.Capsule:
                    return RayCapsule(ray, view.A, view.B, view.Radius, maxDistance, out distance, out point, out normal);
                default:
                    distance = 0f;
                    point = AuraVector3.Zero;
                    normal = AuraVector3.Zero;
                    return false;
            }
        }

        public static float DistancePointSegment(AuraVector3 point, AuraVector3 a, AuraVector3 b) =>
            AuraVector3.Distance(point, ClosestPointOnSegment(point, a, b));

        public static AuraVector3 ClosestPointOnSegment(AuraVector3 point, AuraVector3 a, AuraVector3 b)
        {
            var segment = b - a;
            var lengthSquared = segment.LengthSquared;
            if (lengthSquared <= Epsilon)
                return a;

            var t = Math.Clamp(AuraVector3.Dot(point - a, segment) / lengthSquared, 0f, 1f);
            return a + segment * t;
        }

        private static bool Single(ContactResult result, ManagedManifold manifold)
        {
            if (!result.Hit)
                return false;

            manifold.Normal = result.Normal;
            manifold.Add(result.Point, result.Penetration, result.FeatureId);
            return true;
        }

        private static ContactResult Flip(ContactResult result)
        {
            result.Normal = -result.Normal;
            return result;
        }

        private struct ContactResult
        {
            public bool Hit;
            public AuraVector3 Normal;
            public AuraVector3 Point;
            public float Penetration;
            public int FeatureId;
        }

        private static ContactResult SingleContact(AuraVector3 ca, float ra, AuraVector3 cb, float rb)
        {
            var delta = cb - ca;
            var distance = delta.Length;
            var sum = ra + rb;
            if (distance >= sum)
                return default;

            var normal = distance > Epsilon ? delta / distance : AuraVector3.UnitX;
            return new ContactResult { Hit = true, Normal = normal, Point = ca + normal * ra, Penetration = sum - distance };
        }

        private static ContactResult SphereCapsule(AuraVector3 center, float radius, AuraVector3 a, AuraVector3 b, float capsuleRadius)
        {
            var closest = ClosestPointOnSegment(center, a, b);
            var delta = closest - center;
            var distance = delta.Length;
            var sum = radius + capsuleRadius;
            if (distance >= sum)
                return default;

            var normal = distance > Epsilon ? delta / distance : AuraVector3.UnitX;
            return new ContactResult { Hit = true, Normal = normal, Point = center + normal * radius, Penetration = sum - distance };
        }

        private static ContactResult CapsuleCapsule(AuraVector3 a0, AuraVector3 a1, float ra, AuraVector3 b0, AuraVector3 b1, float rb)
        {
            ClosestPointsSegments(a0, a1, b0, b1, out var p, out var q);
            var delta = q - p;
            var distance = delta.Length;
            var sum = ra + rb;
            if (distance >= sum)
                return default;

            var normal = distance > Epsilon ? delta / distance : AuraVector3.UnitX;
            return new ContactResult { Hit = true, Normal = normal, Point = p + normal * ra, Penetration = sum - distance };
        }

        private static ContactResult SphereBox(AuraVector3 center, float radius, in ManagedShapeView box, AuraPhysicsMode mode)
        {
            var local = box.Rotation.InverseRotate(center - box.Center);
            var extents = mode == AuraPhysicsMode.Plane2D ? new AuraVector3(box.Extents.X, box.Extents.Y, 0f) : box.Extents;
            var clamped = new AuraVector3(
                Math.Clamp(local.X, -extents.X, extents.X),
                Math.Clamp(local.Y, -extents.Y, extents.Y),
                Math.Clamp(local.Z, -extents.Z, extents.Z));
            var delta = local - clamped;
            var distance = delta.Length;

            if (distance > radius + Epsilon)
                return default;

            AuraVector3 normalWorld;
            AuraVector3 point;
            float penetration;

            if (distance > Epsilon)
            {
                normalWorld = box.Rotation.Rotate(delta / distance);
                penetration = radius - distance;
                point = box.Center + box.Rotation.Rotate(clamped);
            }
            else
            {
                var ox = extents.X - MathF.Abs(local.X);
                var oy = extents.Y - MathF.Abs(local.Y);
                var oz = extents.Z - MathF.Abs(local.Z);
                AuraVector3 axis;
                if (ox <= oy && ox <= oz)
                    axis = new AuraVector3(local.X >= 0f ? 1f : -1f, 0f, 0f);
                else if (oy <= oz)
                    axis = new AuraVector3(0f, local.Y >= 0f ? 1f : -1f, 0f);
                else
                    axis = new AuraVector3(0f, 0f, local.Z >= 0f ? 1f : -1f);

                normalWorld = box.Rotation.Rotate(axis);
                penetration = radius + MathF.Min(ox, MathF.Min(oy, oz));
                point = center;
            }

            return new ContactResult { Hit = true, Normal = normalWorld, Point = point, Penetration = penetration };
        }

        private static ContactResult CapsuleBox(AuraVector3 a, AuraVector3 b, float radius, in ManagedShapeView box, AuraPhysicsMode mode)
        {
            var best = a;
            var bestDistance = float.MaxValue;
            const int samples = 16;
            for (var index = 0; index <= samples; index++)
            {
                var sample = AuraVector3.Lerp(a, b, index / (float)samples);
                var local = box.Rotation.InverseRotate(sample - box.Center);
                var dist = new AuraVector3(
                    MathF.Max(0f, MathF.Abs(local.X) - box.Extents.X),
                    MathF.Max(0f, MathF.Abs(local.Y) - box.Extents.Y),
                    mode == AuraPhysicsMode.Plane2D ? 0f : MathF.Max(0f, MathF.Abs(local.Z) - box.Extents.Z)).Length;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    best = sample;
                }
            }

            return SphereBox(best, radius, box, mode);
        }

        private static bool BoxBox(in ManagedShapeView a, in ManagedShapeView b, AuraPhysicsMode mode, ManagedManifold manifold)
        {
            var aAxes = Axes(a.Rotation, mode);
            var bAxes = Axes(b.Rotation, mode);
            var delta = b.Center - a.Center;

            var minOverlap = float.MaxValue;
            var bestAxis = AuraVector3.UnitX;

            for (var index = 0; index < aAxes.Length; index++)
                if (!AxisOverlap(aAxes[index], a, b, delta, ref minOverlap, ref bestAxis))
                    return false;

            for (var index = 0; index < bAxes.Length; index++)
                if (!AxisOverlap(bAxes[index], a, b, delta, ref minOverlap, ref bestAxis))
                    return false;

            if (mode == AuraPhysicsMode.Full3D)
            {
                for (var i = 0; i < 3; i++)
                {
                    for (var j = 0; j < 3; j++)
                    {
                        var cross = AuraVector3.Cross(aAxes[i], bAxes[j]);
                        if (cross.LengthSquared < 1e-6f)
                            continue;
                        if (!AxisOverlap(cross.Normalized(), a, b, delta, ref minOverlap, ref bestAxis))
                            return false;
                    }
                }
            }

            if (AuraVector3.Dot(bestAxis, delta) < 0f)
                bestAxis = -bestAxis;

            manifold.Normal = bestAxis;

            var added = CollectVertices(b, a, mode, bestAxis, minOverlap, manifold);
            if (!added)
                added = CollectVertices(a, b, mode, -bestAxis, minOverlap, manifold);

            if (!added)
            {
                var supportB = Support(b, -bestAxis);
                var supportA = Support(a, bestAxis);
                manifold.Points[0] = new ManagedContactPoint { Position = (supportA + supportB) * 0.5f, Penetration = minOverlap };
                manifold.Count = 1;
            }

            return true;
        }

        private static bool AxisOverlap(AuraVector3 axis, in ManagedShapeView a, in ManagedShapeView b, AuraVector3 delta, ref float minOverlap, ref AuraVector3 bestAxis)
        {
            var ra = ProjectedRadius(a, axis);
            var rb = ProjectedRadius(b, axis);
            var distance = MathF.Abs(AuraVector3.Dot(axis, delta));
            var overlap = ra + rb - distance;
            if (overlap < -Slop)
                return false;

            if (overlap < minOverlap)
            {
                minOverlap = MathF.Max(overlap, 0f);
                bestAxis = axis;
            }

            return true;
        }

        private static float ProjectedRadius(in ManagedShapeView view, AuraVector3 axis)
        {
            var axes = Axes(view.Rotation, AuraPhysicsMode.Full3D);
            return MathF.Abs(AuraVector3.Dot(axis, axes[0])) * view.Extents.X +
                   MathF.Abs(AuraVector3.Dot(axis, axes[1])) * view.Extents.Y +
                   MathF.Abs(AuraVector3.Dot(axis, axes[2])) * view.Extents.Z;
        }

        private static AuraVector3[] Axes(AuraQuaternion rotation, AuraPhysicsMode mode)
        {
            var x = rotation.Rotate(new AuraVector3(1f, 0f, 0f));
            var y = rotation.Rotate(new AuraVector3(0f, 1f, 0f));
            if (mode == AuraPhysicsMode.Plane2D)
                return new[] { x, y };

            var z = rotation.Rotate(new AuraVector3(0f, 0f, 1f));
            return new[] { x, y, z };
        }

        private static bool CollectVertices(in ManagedShapeView source, in ManagedShapeView target, AuraPhysicsMode mode, AuraVector3 normal, float penetration, ManagedManifold manifold)
        {
            manifold.Count = 0;
            var ex = source.Extents;
            var count = 0;
            for (var sx = -1; sx <= 1; sx += 2)
            {
                for (var sy = -1; sy <= 1; sy += 2)
                {
                    var zCount = mode == AuraPhysicsMode.Plane2D ? 1 : 2;
                    for (var zi = 0; zi < zCount; zi++)
                    {
                        var sz = mode == AuraPhysicsMode.Plane2D ? 0f : (zi == 0 ? -1f : 1f);
                        var localCorner = new AuraVector3(sx * ex.X, sy * ex.Y, sz * ex.Z);
                        var world = source.Center + source.Rotation.Rotate(localCorner);
                        var targetLocal = target.Rotation.InverseRotate(world - target.Center);
                        var inside =
                            MathF.Abs(targetLocal.X) <= target.Extents.X + Slop &&
                            MathF.Abs(targetLocal.Y) <= target.Extents.Y + Slop &&
                            (mode == AuraPhysicsMode.Plane2D || MathF.Abs(targetLocal.Z) <= target.Extents.Z + Slop);

                        if (!inside)
                            continue;

                        if (count >= 4)
                            break;

                        manifold.Points[count] = new ManagedContactPoint { Position = world, Penetration = penetration, FeatureId = count };
                        count++;
                    }
                }
            }

            manifold.Count = count;
            return count > 0;
        }

        private static AuraVector3 Support(in ManagedShapeView view, AuraVector3 direction)
        {
            var local = view.Rotation.InverseRotate(direction);
            var sign = new AuraVector3(
                local.X >= 0f ? 1f : -1f,
                local.Y >= 0f ? 1f : -1f,
                local.Z >= 0f ? 1f : -1f);
            return view.Center + view.Rotation.Rotate(new AuraVector3(sign.X * view.Extents.X, sign.Y * view.Extents.Y, sign.Z * view.Extents.Z));
        }

        private static void ClosestPointsSegments(AuraVector3 p1, AuraVector3 q1, AuraVector3 p2, AuraVector3 q2, out AuraVector3 c1, out AuraVector3 c2)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            var a = AuraVector3.Dot(d1, d1);
            var e = AuraVector3.Dot(d2, d2);
            var f = AuraVector3.Dot(d2, r);

            if (a <= Epsilon && e <= Epsilon)
            {
                c1 = p1;
                c2 = p2;
                return;
            }

            float s;
            float t;
            if (a <= Epsilon)
            {
                s = 0f;
                t = Math.Clamp(f / e, 0f, 1f);
            }
            else
            {
                var c = AuraVector3.Dot(d1, r);
                if (e <= Epsilon)
                {
                    t = 0f;
                    s = Math.Clamp(-c / a, 0f, 1f);
                }
                else
                {
                    var b = AuraVector3.Dot(d1, d2);
                    var denominator = a * e - b * b;
                    s = denominator > Epsilon ? Math.Clamp((b * f - c * e) / denominator, 0f, 1f) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Math.Clamp(-c / a, 0f, 1f);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Math.Clamp((b - c) / a, 0f, 1f);
                    }
                }
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }

        private static bool RaySphere(in AuraRay ray, AuraVector3 center, float radius, float maxDistance, out float distance, out AuraVector3 point, out AuraVector3 normal)
        {
            var originToCenter = ray.Origin - center;
            var b = AuraVector3.Dot(originToCenter, ray.Direction);
            var c = AuraVector3.Dot(originToCenter, originToCenter) - radius * radius;
            var discriminant = b * b - c;
            if (discriminant < 0f)
            {
                distance = 0f;
                point = AuraVector3.Zero;
                normal = AuraVector3.Zero;
                return false;
            }

            var root = MathF.Sqrt(discriminant);
            var t = -b - root;
            if (t < 0f)
                t = -b + root;

            if (t < 0f || t > maxDistance)
            {
                distance = 0f;
                point = AuraVector3.Zero;
                normal = AuraVector3.Zero;
                return false;
            }

            distance = t;
            point = ray.GetPoint(t);
            normal = (point - center).Normalized();
            return true;
        }

        private static bool RayBox(in AuraRay ray, in ManagedShapeView box, AuraPhysicsMode mode, float maxDistance, out float distance, out AuraVector3 point, out AuraVector3 normal)
        {
            var localOrigin = box.Rotation.InverseRotate(ray.Origin - box.Center);
            var localDirection = box.Rotation.InverseRotate(ray.Direction);
            var min = 0f;
            var max = maxDistance;
            var axis = 0;
            var sign = 0f;
            var axes = mode == AuraPhysicsMode.Plane2D ? 2 : 3;

            for (var component = 0; component < axes; component++)
            {
                var origin = Component(localOrigin, component);
                var direction = Component(localDirection, component);
                var half = Component(box.Extents, component);
                if (MathF.Abs(direction) < Epsilon)
                {
                    if (origin < -half || origin > half)
                    {
                        distance = 0f;
                        point = AuraVector3.Zero;
                        normal = AuraVector3.Zero;
                        return false;
                    }

                    continue;
                }

                var inverse = 1f / direction;
                var t1 = (-half - origin) * inverse;
                var t2 = (half - origin) * inverse;
                var entrySign = -1f;
                if (t1 > t2)
                {
                    (t1, t2) = (t2, t1);
                    entrySign = 1f;
                }

                if (t1 > min)
                {
                    min = t1;
                    axis = component;
                    sign = entrySign;
                }

                if (t2 < max)
                    max = t2;

                if (min > max)
                {
                    distance = 0f;
                    point = AuraVector3.Zero;
                    normal = AuraVector3.Zero;
                    return false;
                }
            }

            if (min < 0f)
            {
                distance = 0f;
                point = AuraVector3.Zero;
                normal = AuraVector3.Zero;
                return false;
            }

            distance = min;
            point = ray.GetPoint(min);
            normal = box.Rotation.Rotate(ComponentNormal(axis, sign));
            return true;
        }

        private static bool RayCapsule(in AuraRay ray, AuraVector3 a, AuraVector3 b, float radius, float maxDistance, out float distance, out AuraVector3 point, out AuraVector3 normal)
        {
            distance = 0f;
            point = AuraVector3.Zero;
            normal = AuraVector3.Zero;

            const int samples = 256;
            var step = maxDistance / samples;
            for (var index = 0; index <= samples; index++)
            {
                var t = index * step;
                var sample = ray.GetPoint(t);
                var closest = ClosestPointOnSegment(sample, a, b);
                if (AuraVector3.Distance(sample, closest) <= radius)
                {
                    distance = t;
                    point = sample;
                    var delta = sample - closest;
                    normal = delta.Length > Epsilon ? delta.Normalized() : -ray.Direction;
                    return true;
                }
            }

            return false;
        }

        private static float Component(AuraVector3 value, int component)
        {
            switch (component)
            {
                case 0: return value.X;
                case 1: return value.Y;
                default: return value.Z;
            }
        }

        private static AuraVector3 ComponentNormal(int axis, float sign)
        {
            switch (axis)
            {
                case 0: return new AuraVector3(sign, 0f, 0f);
                case 1: return new AuraVector3(0f, sign, 0f);
                default: return new AuraVector3(0f, 0f, sign);
            }
        }
    }
}
