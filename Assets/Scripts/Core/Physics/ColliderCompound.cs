using System;
using RPG.Simulation.Contracts;

namespace RPG.Core.Physics
{
    public sealed class ColliderCompound
    {
        private readonly ColliderShapeData[] _shapes;

        public ColliderCompound(ColliderShapeData[] shapes)
        {
            if (shapes == null || shapes.Length == 0)
                throw new ArgumentException("A collider compound requires at least one shape.", nameof(shapes));

            _shapes = (ColliderShapeData[])shapes.Clone();
        }

        public int Count => _shapes.Length;

        public float BoundingRadius
        {
            get
            {
                var radius = 0f;
                for (var index = 0; index < _shapes.Length; index++)
                {
                    var shape = _shapes[index];
                    var bounds = shape.Shape.GetBoundsHalfExtents();
                    if (shape.Shape.Type == CollisionShapeType.Circle)
                    {
                        radius = MathF.Max(radius,
                            MathF.Sqrt(shape.LocalOffset.LengthSquared) + shape.Shape.Radius);
                        continue;
                    }

                    var extentX = MathF.Abs(shape.LocalOffset.X) + bounds.X;
                    var extentY = MathF.Abs(shape.LocalOffset.Y) + bounds.Y;
                    radius = MathF.Max(radius, MathF.Sqrt(extentX * extentX + extentY * extentY));
                }

                return radius;
            }
        }

        public ColliderShapeData GetAt(int index) => _shapes[index];

        public bool HasInteraction(ColliderMode mode, ColliderFilter queryFilter)
        {
            for (var index = 0; index < _shapes.Length; index++)
            {
                var shape = _shapes[index];
                if (shape.Mode == mode && shape.Filter.CanInteractWith(queryFilter))
                    return true;
            }

            return false;
        }

        public bool Overlaps(
            SimulationVector2 position,
            float rotationRadians,
            CollisionShape queryShape,
            SimulationVector2 queryPosition,
            ColliderMode queryMode,
            ColliderFilter queryFilter)
        {
            for (var index = 0; index < _shapes.Length; index++)
            {
                var shape = _shapes[index];
                if (shape.Mode != queryMode || !shape.Filter.CanInteractWith(queryFilter))
                    continue;

                var offset = Rotate(shape.LocalOffset, rotationRadians);
                var shapePosition = position + offset;
                var worldShape = shape.Shape.WithRotation(rotationRadians + shape.LocalRotationRadians);
                if (CollisionShapeQueries.Overlaps(worldShape, shapePosition, queryShape, queryPosition))
                    return true;
            }

            return false;
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
