using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Physics
{
    public static class CollisionShapeQueries
    {
        public static bool Overlaps(
            CollisionShape left,
            SimulationVector2 leftCenter,
            CollisionShape right,
            SimulationVector2 rightCenter)
        {
            if (left.Type == CollisionShapeType.Circle && right.Type == CollisionShapeType.Circle)
                return (rightCenter - leftCenter).LengthSquared <=
                       (left.Radius + right.Radius) * (left.Radius + right.Radius);

            if (left.Type == CollisionShapeType.Circle)
                return CircleOverlapsConvex(left, leftCenter, right, rightCenter);

            if (right.Type == CollisionShapeType.Circle)
                return CircleOverlapsConvex(right, rightCenter, left, leftCenter);

            return ConvexOverlapsConvex(left, leftCenter, right, rightCenter);
        }

        private static bool CircleOverlapsConvex(
            CollisionShape circle,
            SimulationVector2 circleCenter,
            CollisionShape polygon,
            SimulationVector2 polygonCenter)
        {
            if (HasSeparatingAxis(polygon, polygonCenter, circle, circleCenter))
                return false;

            var first = polygonCenter + polygon.GetLocalVertex(0);
            var closestPoint = first;
            var closestDistance = float.MaxValue;
            for (var index = 0; index < polygon.VertexCount; index++)
            {
                var next = polygonCenter + polygon.GetLocalVertex((index + 1) % polygon.VertexCount);
                var edge = next - first;
                var edgeLengthSquared = edge.LengthSquared;
                var projection = edgeLengthSquared <= 0.000001f
                    ? 0f
                    : ((circleCenter - first).X * edge.X + (circleCenter - first).Y * edge.Y) /
                      edgeLengthSquared;
                projection = SimulationMath.Max(0f, SimulationMath.Min(1f, projection));
                var point = first + edge * projection;
                var distance = (point - circleCenter).LengthSquared;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPoint = point;
                }

                first = next;
            }

            var axis = closestPoint - circleCenter;
            return axis.LengthSquared <= 0.000001f ||
                   !HasSeparatingAxis(polygon, polygonCenter, circle, circleCenter, axis);
        }

        private static bool ConvexOverlapsConvex(
            CollisionShape left,
            SimulationVector2 leftCenter,
            CollisionShape right,
            SimulationVector2 rightCenter)
        {
            return !HasSeparatingAxis(left, leftCenter, right, rightCenter) &&
                   !HasSeparatingAxis(right, rightCenter, left, leftCenter);
        }

        private static bool HasSeparatingAxis(
            CollisionShape source,
            SimulationVector2 sourceCenter,
            CollisionShape target,
            SimulationVector2 targetCenter)
        {
            for (var index = 0; index < source.VertexCount; index++)
            {
                var current = sourceCenter + source.GetLocalVertex(index);
                var next = sourceCenter + source.GetLocalVertex((index + 1) % source.VertexCount);
                var edge = next - current;
                var axis = new SimulationVector2(-edge.Y, edge.X);
                if (HasSeparatingAxis(source, sourceCenter, target, targetCenter, axis))
                    return true;
            }

            return false;
        }

        private static bool HasSeparatingAxis(
            CollisionShape source,
            SimulationVector2 sourceCenter,
            CollisionShape target,
            SimulationVector2 targetCenter,
            SimulationVector2 axis)
        {
            Project(source, sourceCenter, axis, out var sourceMin, out var sourceMax);
            Project(target, targetCenter, axis, out var targetMin, out var targetMax);
            return sourceMax < targetMin || targetMax < sourceMin;
        }

        private static void Project(
            CollisionShape shape,
            SimulationVector2 center,
            SimulationVector2 axis,
            out float minimum,
            out float maximum)
        {
            if (shape.Type == CollisionShapeType.Circle)
            {
                var centerProjection = center.X * axis.X + center.Y * axis.Y;
                var radiusProjection = shape.Radius * SimulationMath.Sqrt(axis.LengthSquared);
                minimum = centerProjection - radiusProjection;
                maximum = centerProjection + radiusProjection;
                return;
            }

            var first = center + shape.GetLocalVertex(0);
            var firstProjection = first.X * axis.X + first.Y * axis.Y;
            minimum = firstProjection;
            maximum = firstProjection;
            for (var index = 1; index < shape.VertexCount; index++)
            {
                var vertex = center + shape.GetLocalVertex(index);
                var projection = vertex.X * axis.X + vertex.Y * axis.Y;
                minimum = SimulationMath.Min(minimum, projection);
                maximum = SimulationMath.Max(maximum, projection);
            }
        }

        /// <summary>Casts a ray against one convex shape (circle, box or polygon) and reports the first contact within
        /// <paramref name="maxDistance"/>. The ray direction does not need to be normalised. A ray that starts inside
        /// the shape reports a hit at distance zero with the normal opposite the direction.</summary>
        public static bool Raycast(
            CollisionRay ray,
            float maxDistance,
            CollisionShape shape,
            SimulationVector2 shapeCenter,
            out CollisionHit hit)
        {
            hit = default;
            if (maxDistance <= 0f)
                return false;

            var lengthSquared = ray.Direction.LengthSquared;
            if (lengthSquared <= 0.000000000001f)
                return false;

            var direction = ray.Direction * (1f / SimulationMath.Sqrt(lengthSquared));
            if (!RaycastDirection(ray.Origin, direction, maxDistance, shape, shapeCenter, out var distance, out var normal))
                return false;

            hit = new CollisionHit(ray.Origin + direction * distance, normal, distance);
            return true;
        }

        /// <summary>Sweeps a circle of <paramref name="radius"/> from <paramref name="from"/> to <paramref name="to"/>
        /// against one convex shape and reports the first contact. Fast movement cannot tunnel because the swept
        /// (Minkowski) geometry is solved directly instead of sampling the path.</summary>
        public static bool SweepCircle(
            float radius,
            SimulationVector2 from,
            SimulationVector2 to,
            CollisionShape shape,
            SimulationVector2 shapeCenter,
            out CollisionHit hit)
        {
            hit = default;
            if (radius < 0f)
                return false;

            var delta = to - from;
            var length = SimulationMath.Sqrt(delta.LengthSquared);
            if (length <= 0.000001f)
                return false;

            var direction = delta * (1f / length);

            if (radius <= 0.000001f)
                return Raycast(new CollisionRay(from, delta), length, shape, shapeCenter, out hit);

            if (Overlaps(CollisionShape.Circle(radius), from, shape, shapeCenter))
            {
                OverlapContact(from, shape, shapeCenter, direction, out var overlapNormal, out var overlapPoint);
                hit = new CollisionHit(overlapPoint, overlapNormal, 0f);
                return true;
            }

            if (shape.Type == CollisionShapeType.Circle)
            {
                if (!RaycastCircle(from, direction, length, shapeCenter, radius + shape.Radius, out var distance, out var normal))
                    return false;

                hit = new CollisionHit(shapeCenter + normal * shape.Radius, normal, distance);
                return true;
            }

            return SweepCircleAgainstConvex(radius, from, direction, length, shape, shapeCenter, out hit);
        }

        private static bool RaycastDirection(
            SimulationVector2 origin,
            SimulationVector2 direction,
            float maxDistance,
            CollisionShape shape,
            SimulationVector2 shapeCenter,
            out float distance,
            out SimulationVector2 normal)
        {
            if (shape.Type == CollisionShapeType.Circle)
                return RaycastCircle(origin, direction, maxDistance, shapeCenter, shape.Radius, out distance, out normal);

            return RaycastConvex(origin, direction, maxDistance, shape, shapeCenter, out distance, out normal);
        }

        private static bool RaycastCircle(
            SimulationVector2 origin,
            SimulationVector2 direction,
            float maxDistance,
            SimulationVector2 center,
            float radius,
            out float distance,
            out SimulationVector2 normal)
        {
            distance = 0f;
            normal = default;

            var offset = origin - center;
            var projection = offset.X * direction.X + offset.Y * direction.Y;
            var constant = offset.LengthSquared - radius * radius;
            if (constant > 0f && projection > 0f)
                return false;

            var discriminant = projection * projection - constant;
            if (discriminant < 0f)
                return false;

            var entry = -projection - SimulationMath.Sqrt(discriminant);
            if (entry < 0f)
                entry = 0f;
            if (entry > maxDistance)
                return false;

            distance = entry;
            var point = origin + direction * entry;
            var outward = point - center;
            normal = outward.LengthSquared > 0.00000001f
                ? outward.Normalized()
                : new SimulationVector2(-direction.X, -direction.Y);
            return true;
        }

        private static bool RaycastConvex(
            SimulationVector2 origin,
            SimulationVector2 direction,
            float maxDistance,
            CollisionShape shape,
            SimulationVector2 center,
            out float distance,
            out SimulationVector2 normal)
        {
            distance = 0f;
            normal = default;

            var count = shape.VertexCount;
            var localOrigin = origin - center;

            var centroid = SimulationVector2.Zero;
            for (var index = 0; index < count; index++)
                centroid += shape.GetLocalVertex(index);
            centroid /= count;

            var enter = 0f;
            var exit = maxDistance;
            var enterNormal = new SimulationVector2(-direction.X, -direction.Y);
            var originInside = true;

            for (var index = 0; index < count; index++)
            {
                var current = shape.GetLocalVertex(index);
                var next = shape.GetLocalVertex((index + 1) % count);
                var edge = next - current;
                var edgeLengthSquared = edge.LengthSquared;
                if (edgeLengthSquared <= 0.000001f)
                    continue;

                var axis = new SimulationVector2(-edge.Y, edge.X) * (1f / SimulationMath.Sqrt(edgeLengthSquared));
                var midpoint = (current + next) * 0.5f;
                if (axis.X * (midpoint.X - centroid.X) + axis.Y * (midpoint.Y - centroid.Y) < 0f)
                    axis = axis * -1f;

                var offset = (localOrigin.X - current.X) * axis.X + (localOrigin.Y - current.Y) * axis.Y;
                if (offset > 0.000001f)
                    originInside = false;

                var slope = direction.X * axis.X + direction.Y * axis.Y;
                if (SimulationMath.Abs(slope) <= 0.00000001f)
                {
                    if (offset > 0f)
                        return false;
                    continue;
                }

                var crossing = -offset / slope;
                if (slope > 0f)
                {
                    if (crossing < exit)
                        exit = crossing;
                }
                else if (crossing > enter)
                {
                    enter = crossing;
                    enterNormal = axis;
                }

                if (enter > exit)
                    return false;
            }

            if (originInside)
            {
                normal = new SimulationVector2(-direction.X, -direction.Y);
                return true;
            }

            if (exit < 0f || enter > maxDistance)
                return false;

            if (enter < 0f)
            {
                normal = new SimulationVector2(-direction.X, -direction.Y);
                return true;
            }

            distance = enter;
            normal = enterNormal;
            return true;
        }

        private static bool SweepCircleAgainstConvex(
            float radius,
            SimulationVector2 from,
            SimulationVector2 direction,
            float length,
            CollisionShape shape,
            SimulationVector2 center,
            out CollisionHit hit)
        {
            hit = default;

            var count = shape.VertexCount;
            var centroid = SimulationVector2.Zero;
            for (var index = 0; index < count; index++)
                centroid += shape.GetLocalVertex(index);
            centroid = center + centroid * (1f / count);

            var best = float.MaxValue;
            var bestNormal = new SimulationVector2(-direction.X, -direction.Y);

            for (var index = 0; index < count; index++)
            {
                var current = center + shape.GetLocalVertex(index);
                var next = center + shape.GetLocalVertex((index + 1) % count);
                var edge = next - current;
                var edgeLengthSquared = edge.LengthSquared;
                if (edgeLengthSquared <= 0.000001f)
                    continue;

                var axis = new SimulationVector2(-edge.Y, edge.X) * (1f / SimulationMath.Sqrt(edgeLengthSquared));
                var midpoint = (current + next) * 0.5f;
                if (axis.X * (midpoint.X - centroid.X) + axis.Y * (midpoint.Y - centroid.Y) < 0f)
                    axis = axis * -1f;

                if (RaycastSegment(from, direction, length, current + axis * radius, next + axis * radius, out var edgeDistance) &&
                    edgeDistance < best)
                {
                    best = edgeDistance;
                    bestNormal = axis;
                }

                if (RaycastCircle(from, direction, length, current, radius, out var vertexDistance, out var vertexNormal) &&
                    vertexDistance < best)
                {
                    best = vertexDistance;
                    bestNormal = vertexNormal;
                }
            }

            if (best == float.MaxValue)
                return false;

            hit = new CollisionHit(from + direction * best - bestNormal * radius, bestNormal, best);
            return true;
        }

        private static bool RaycastSegment(
            SimulationVector2 origin,
            SimulationVector2 direction,
            float maxDistance,
            SimulationVector2 start,
            SimulationVector2 end,
            out float distance)
        {
            distance = 0f;
            var edge = end - start;
            var denominator = direction.X * edge.Y - direction.Y * edge.X;
            if (SimulationMath.Abs(denominator) <= 0.00000001f)
                return false;

            var offset = start - origin;
            var alongRay = (offset.X * edge.Y - offset.Y * edge.X) / denominator;
            var alongEdge = (offset.X * direction.Y - offset.Y * direction.X) / denominator;
            if (alongRay < 0f || alongRay > maxDistance || alongEdge < -0.00001f || alongEdge > 1.00001f)
                return false;

            distance = alongRay;
            return true;
        }

        private static void OverlapContact(
            SimulationVector2 position,
            CollisionShape shape,
            SimulationVector2 shapeCenter,
            SimulationVector2 direction,
            out SimulationVector2 normal,
            out SimulationVector2 point)
        {
            var fallback = new SimulationVector2(-direction.X, -direction.Y);

            if (shape.Type == CollisionShapeType.Circle)
            {
                var outward = position - shapeCenter;
                normal = outward.LengthSquared > 0.00000001f ? outward.Normalized() : fallback;
                point = shapeCenter + normal * shape.Radius;
                return;
            }

            var count = shape.VertexCount;
            var closest = shapeCenter + shape.GetLocalVertex(0);
            var closestDistance = (closest - position).LengthSquared;
            for (var index = 0; index < count; index++)
            {
                var current = shapeCenter + shape.GetLocalVertex(index);
                var next = shapeCenter + shape.GetLocalVertex((index + 1) % count);
                var candidate = ClosestPointOnSegment(position, current, next);
                var candidateDistance = (candidate - position).LengthSquared;
                if (candidateDistance < closestDistance)
                {
                    closestDistance = candidateDistance;
                    closest = candidate;
                }
            }

            var delta = position - closest;
            normal = delta.LengthSquared > 0.00000001f ? delta.Normalized() : fallback;
            point = closest;
        }

        private static SimulationVector2 ClosestPointOnSegment(
            SimulationVector2 point,
            SimulationVector2 start,
            SimulationVector2 end)
        {
            var edge = end - start;
            var edgeLengthSquared = edge.LengthSquared;
            if (edgeLengthSquared <= 0.000001f)
                return start;

            var projection = ((point.X - start.X) * edge.X + (point.Y - start.Y) * edge.Y) / edgeLengthSquared;
            projection = SimulationMath.Max(0f, SimulationMath.Min(1f, projection));
            return start + edge * projection;
        }
    }
}
