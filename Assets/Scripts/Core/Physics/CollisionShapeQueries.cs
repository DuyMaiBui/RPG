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
                projection = MathF.Max(0f, MathF.Min(1f, projection));
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
                var radiusProjection = shape.Radius * MathF.Sqrt(axis.LengthSquared);
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
                minimum = MathF.Min(minimum, projection);
                maximum = MathF.Max(maximum, projection);
            }
        }
    }
}
