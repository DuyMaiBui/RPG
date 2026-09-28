using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Physics
{
    public readonly struct CollisionShape
    {
        private CollisionShape(
            CollisionShapeType type,
            SimulationVector2 halfExtents,
            float radius,
            float rotationRadians,
            SimulationVector2[] vertices)
        {
            Type = type;
            HalfExtents = halfExtents;
            Radius = radius;
            RotationRadians = rotationRadians;
            Vertices = vertices;
        }

        public CollisionShapeType Type { get; }
        public SimulationVector2 HalfExtents { get; }
        public float Radius { get; }
        public float RotationRadians { get; }
        internal SimulationVector2[] Vertices { get; }

        public static CollisionShape Box(SimulationVector2 halfExtents, float rotationRadians = 0f)
        {
            if (halfExtents.X < 0f || halfExtents.Y < 0f)
                throw new ArgumentOutOfRangeException(nameof(halfExtents));

            return new CollisionShape(CollisionShapeType.Box, halfExtents, 0f, rotationRadians, null);
        }

        public static CollisionShape Circle(float radius)
        {
            if (radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));

            return new CollisionShape(CollisionShapeType.Circle, new SimulationVector2(radius, radius), radius, 0f, null);
        }

        public CollisionShape WithRotation(float rotationRadians) =>
            new(Type, HalfExtents, Radius, rotationRadians, Vertices);

        public static CollisionShape Polygon(SimulationVector2[] vertices, float rotationRadians = 0f)
        {
            if (vertices == null || vertices.Length < 3)
                throw new ArgumentException("A polygon requires at least three vertices.", nameof(vertices));

            var turnSign = 0f;
            for (var index = 0; index < vertices.Length; index++)
            {
                var current = vertices[index];
                var next = vertices[(index + 1) % vertices.Length];
                var following = vertices[(index + 2) % vertices.Length];
                var firstEdge = next - current;
                var secondEdge = following - next;
                var cross = firstEdge.X * secondEdge.Y - firstEdge.Y * secondEdge.X;
                if (MathF.Abs(cross) <= 0.000001f)
                    continue;
                if (turnSign == 0f)
                    turnSign = cross;
                else if (turnSign * cross < 0f)
                    throw new ArgumentException("Collision polygons must be convex.", nameof(vertices));
            }

            var copy = new SimulationVector2[vertices.Length];
            Array.Copy(vertices, copy, vertices.Length);
            return new CollisionShape(CollisionShapeType.Polygon, default, 0f, rotationRadians, copy);
        }

        public SimulationVector2 GetBoundsHalfExtents()
        {
            if (Type == CollisionShapeType.Circle)
                return new SimulationVector2(Radius, Radius);

            var count = VertexCount;
            var maxX = 0f;
            var maxY = 0f;
            for (var index = 0; index < count; index++)
            {
                var vertex = GetLocalVertex(index);
                maxX = MathF.Max(maxX, MathF.Abs(vertex.X));
                maxY = MathF.Max(maxY, MathF.Abs(vertex.Y));
            }

            return new SimulationVector2(maxX, maxY);
        }

        public bool IntersectsAabb(
            SimulationVector2 shapeCenter,
            SimulationVector2 cellCenter,
            SimulationVector2 cellHalfExtents)
        {
            return CollisionShapeQueries.Overlaps(
                this,
                shapeCenter,
                Box(cellHalfExtents),
                cellCenter);
        }

        internal int VertexCount => Type == CollisionShapeType.Box ? 4 : Vertices.Length;

        internal SimulationVector2 GetLocalVertex(int index)
        {
            if (Type == CollisionShapeType.Box)
            {
                var x = index == 0 || index == 3 ? -HalfExtents.X : HalfExtents.X;
                var y = index < 2 ? -HalfExtents.Y : HalfExtents.Y;
                return Rotate(new SimulationVector2(x, y), RotationRadians);
            }

            return Rotate(Vertices[index], RotationRadians);
        }

        private static SimulationVector2 Rotate(SimulationVector2 value, float radians)
        {
            var cosine = MathF.Cos(radians);
            var sine = MathF.Sin(radians);
            return new SimulationVector2(
                value.X * cosine - value.Y * sine,
                value.X * sine + value.Y * cosine);
        }
    }
}
