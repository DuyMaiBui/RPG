using System;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal readonly struct ManagedShapeView
    {
        private ManagedShapeView(ManagedShapeKind kind, AuraVector3 center, AuraQuaternion rotation, AuraVector3 extents, AuraVector3 a, AuraVector3 b, float radius)
        {
            Kind = kind;
            Center = center;
            Rotation = rotation;
            Extents = extents;
            A = a;
            B = b;
            Radius = radius;
        }

        public ManagedShapeKind Kind { get; }
        public AuraVector3 Center { get; }
        public AuraQuaternion Rotation { get; }
        public AuraVector3 Extents { get; }
        public AuraVector3 A { get; }
        public AuraVector3 B { get; }
        public float Radius { get; }

        public static ManagedShapeView Sphere(AuraVector3 center, float radius) =>
            new ManagedShapeView(ManagedShapeKind.Sphere, center, AuraQuaternion.Identity, AuraVector3.Zero, center, center, radius);

        public static ManagedShapeView Capsule(AuraVector3 a, AuraVector3 b, float radius) =>
            new ManagedShapeView(ManagedShapeKind.Capsule, (a + b) * 0.5f, AuraQuaternion.Identity, AuraVector3.Zero, a, b, radius);

        public static ManagedShapeView Box(AuraVector3 center, AuraQuaternion rotation, AuraVector3 halfExtents) =>
            new ManagedShapeView(ManagedShapeKind.Box, center, rotation, halfExtents, center, center, 0f);

        public ManagedShapeView Translated(AuraVector3 offset) =>
            new ManagedShapeView(Kind, Center + offset, Rotation, Extents, A + offset, B + offset, Radius);

        public static ManagedShapeView FromBodyShape(ManagedBody body, ManagedShape shape)
        {
            var center = body.Pose.TransformPoint(shape.LocalPose.Position);
            var rotation = AuraQuaternion.Multiply(body.Pose.Rotation, shape.LocalPose.Rotation).Normalized();

            switch (shape.Type)
            {
                case AuraShapeType.Sphere:
                    return Sphere(center, shape.Radius);
                case AuraShapeType.Capsule:
                case AuraShapeType.Cylinder:
                    var half = MathF.Max(0f, shape.Height * 0.5f - shape.Radius);
                    var axis = rotation.Rotate(new AuraVector3(0f, 1f, 0f));
                    return Capsule(center - axis * half, center + axis * half, shape.Radius);
                case AuraShapeType.Box:
                    return Box(center, rotation, shape.HalfExtents);
                default:
                    return new ManagedShapeView(ManagedShapeKind.None, center, rotation, AuraVector3.Zero, center, center, shape.Radius);
            }
        }

        public static ManagedShapeView FromDefinition(in AuraPhysicsShapeDefinition shape, in AuraPose worldPose)
        {
            var center = worldPose.TransformPoint(shape.LocalPose.Position);
            var rotation = AuraQuaternion.Multiply(worldPose.Rotation, shape.LocalPose.Rotation).Normalized();

            switch (shape.Type)
            {
                case AuraShapeType.Sphere:
                    return Sphere(center, shape.Geometry.Radius);
                case AuraShapeType.Capsule:
                case AuraShapeType.Cylinder:
                    var half = MathF.Max(0f, shape.Geometry.Height * 0.5f - shape.Geometry.Radius);
                    var axis = rotation.Rotate(new AuraVector3(0f, 1f, 0f));
                    return Capsule(center - axis * half, center + axis * half, shape.Geometry.Radius);
                case AuraShapeType.Box:
                    return Box(center, rotation, shape.Geometry.HalfExtents);
                default:
                    return new ManagedShapeView(ManagedShapeKind.None, center, rotation, AuraVector3.Zero, center, center, shape.Geometry.Radius);
            }
        }

        public static AuraVector3 RotatedExtents(AuraQuaternion rotation, AuraVector3 halfExtents)
        {
            var x = rotation.X;
            var y = rotation.Y;
            var z = rotation.Z;
            var w = rotation.W;

            var m00 = 1f - 2f * (y * y + z * z);
            var m01 = 2f * (x * y - z * w);
            var m02 = 2f * (x * z + y * w);
            var m10 = 2f * (x * y + z * w);
            var m11 = 1f - 2f * (x * x + z * z);
            var m12 = 2f * (y * z - x * w);
            var m20 = 2f * (x * z - y * w);
            var m21 = 2f * (y * z + x * w);
            var m22 = 1f - 2f * (x * x + y * y);

            return new AuraVector3(
                MathF.Abs(m00) * halfExtents.X + MathF.Abs(m01) * halfExtents.Y + MathF.Abs(m02) * halfExtents.Z,
                MathF.Abs(m10) * halfExtents.X + MathF.Abs(m11) * halfExtents.Y + MathF.Abs(m12) * halfExtents.Z,
                MathF.Abs(m20) * halfExtents.X + MathF.Abs(m21) * halfExtents.Y + MathF.Abs(m22) * halfExtents.Z);
        }
    }
}
