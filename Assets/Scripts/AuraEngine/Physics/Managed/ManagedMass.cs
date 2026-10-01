using System;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal static class ManagedMass
    {
        public static void Compute(in AuraPhysicsBodyDefinition definition, out float mass, out AuraVector3 inertia)
        {
            mass = 0f;
            var volume = 0f;
            var halfExtents = AuraVector3.Zero;

            for (var index = 0; index < definition.Shapes.Length; index++)
            {
                var shape = definition.Shapes[index];
                var density = shape.Material.Density > 0f ? shape.Material.Density : 1000f;
                halfExtents = new AuraVector3(
                    MathF.Max(halfExtents.X, shape.Geometry.HalfExtents.X),
                    MathF.Max(halfExtents.Y, shape.Geometry.HalfExtents.Y),
                    MathF.Max(halfExtents.Z, shape.Geometry.HalfExtents.Z));
                volume += ShapeVolume(shape);
                mass += density * ShapeVolume(shape);
            }

            if (mass <= 0f)
                mass = 1f;

            if (halfExtents.LengthSquared <= 1e-8f)
                halfExtents = new AuraVector3(0.5f, 0.5f, 0.5f);

            var ex = halfExtents.X * 2f;
            var ey = halfExtents.Y * 2f;
            var ez = halfExtents.Z * 2f;
            inertia = new AuraVector3(
                mass * (ey * ey + ez * ez) / 12f,
                mass * (ex * ex + ez * ez) / 12f,
                mass * (ex * ex + ey * ey) / 12f);
        }

        private static float ShapeVolume(in AuraPhysicsShapeDefinition shape)
        {
            switch (shape.Type)
            {
                case AuraShapeType.Sphere:
                    return 4f / 3f * MathF.PI * shape.Geometry.Radius * shape.Geometry.Radius * shape.Geometry.Radius;
                case AuraShapeType.Capsule:
                case AuraShapeType.Cylinder:
                    var r = shape.Geometry.Radius;
                    var h = MathF.Max(0f, shape.Geometry.Height - 2f * r);
                    return MathF.PI * r * r * h + 4f / 3f * MathF.PI * r * r * r;
                default:
                    return 8f * shape.Geometry.HalfExtents.X * shape.Geometry.HalfExtents.Y * shape.Geometry.HalfExtents.Z;
            }
        }
    }
}
