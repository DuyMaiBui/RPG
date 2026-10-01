using System;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    internal readonly struct ManagedAabb
    {
        public ManagedAabb(AuraVector3 min, AuraVector3 max)
        {
            Min = min;
            Max = max;
        }

        public AuraVector3 Min { get; }
        public AuraVector3 Max { get; }

        public bool Overlaps(in ManagedAabb other) =>
            Min.X <= other.Max.X && Max.X >= other.Min.X &&
            Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
            Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

        public static ManagedAabb FromShape(in ManagedShapeView view)
        {
            switch (view.Kind)
            {
                case ManagedShapeKind.Sphere:
                    return FromCenterExtents(view.Center, new AuraVector3(view.Radius, view.Radius, view.Radius));
                case ManagedShapeKind.Capsule:
                    var radius = new AuraVector3(view.Radius, view.Radius, view.Radius);
                    var min = new AuraVector3(MathF.Min(view.A.X, view.B.X), MathF.Min(view.A.Y, view.B.Y), MathF.Min(view.A.Z, view.B.Z)) - radius;
                    var max = new AuraVector3(MathF.Max(view.A.X, view.B.X), MathF.Max(view.A.Y, view.B.Y), MathF.Max(view.A.Z, view.B.Z)) + radius;
                    return new ManagedAabb(min, max);
                case ManagedShapeKind.Box:
                    return FromCenterExtents(view.Center, ManagedShapeView.RotatedExtents(view.Rotation, view.Extents));
                case ManagedShapeKind.Triangle:
                    return FromPoints(view.A, view.B, view.C);
                case ManagedShapeKind.TriangleMesh:
                    return FromMesh(view.MeshVertices);
                case ManagedShapeKind.HeightField:
                    return FromHeightField(view);
                default:
                    return new ManagedAabb(view.Center, view.Center);
            }
        }

        private static ManagedAabb FromCenterExtents(AuraVector3 center, AuraVector3 extents) =>
            new ManagedAabb(center - extents, center + extents);

        private static ManagedAabb FromPoints(AuraVector3 a, AuraVector3 b, AuraVector3 c)
        {
            var min = new AuraVector3(MathF.Min(a.X, MathF.Min(b.X, c.X)), MathF.Min(a.Y, MathF.Min(b.Y, c.Y)), MathF.Min(a.Z, MathF.Min(b.Z, c.Z)));
            var max = new AuraVector3(MathF.Max(a.X, MathF.Max(b.X, c.X)), MathF.Max(a.Y, MathF.Max(b.Y, c.Y)), MathF.Max(a.Z, MathF.Max(b.Z, c.Z)));
            return new ManagedAabb(min, max);
        }

        private static ManagedAabb FromMesh(AuraVector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0)
                return new ManagedAabb(AuraVector3.Zero, AuraVector3.Zero);

            var min = vertices[0];
            var max = vertices[0];
            for (var index = 1; index < vertices.Length; index++)
            {
                var v = vertices[index];
                min = new AuraVector3(MathF.Min(min.X, v.X), MathF.Min(min.Y, v.Y), MathF.Min(min.Z, v.Z));
                max = new AuraVector3(MathF.Max(max.X, v.X), MathF.Max(max.Y, v.Y), MathF.Max(max.Z, v.Z));
            }

            return new ManagedAabb(min, max);
        }

        private static ManagedAabb FromHeightField(in ManagedShapeView view)
        {
            if (view.HeightSamples == null || view.HeightResolution < 2)
                return new ManagedAabb(view.Center, view.Center);

            var resolution = view.HeightResolution;
            var minHeight = float.MaxValue;
            var maxHeight = float.MinValue;
            for (var index = 0; index < view.HeightSamples.Length; index++)
            {
                var sample = view.HeightSamples[index];
                minHeight = MathF.Min(minHeight, sample);
                maxHeight = MathF.Max(maxHeight, sample);
            }

            var spanX = (resolution - 1) * view.HeightScale.X;
            var spanZ = (resolution - 1) * view.HeightScale.Z;
            var min = new AuraVector3(0f, minHeight * view.HeightScale.Y, 0f);
            var max = new AuraVector3(spanX, maxHeight * view.HeightScale.Y, spanZ);
            return new ManagedAabb(min, max);
        }
    }
}
