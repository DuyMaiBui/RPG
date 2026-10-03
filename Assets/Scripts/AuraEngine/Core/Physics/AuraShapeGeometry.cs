using System;

namespace AuraEngine.Core
{
    public readonly struct AuraShapeGeometry : IEquatable<AuraShapeGeometry>
    {
        private AuraShapeGeometry(
            AuraVector3 halfExtents,
            float radius,
            float height,
            int meshAssetId,
            AuraVector3[] meshVertices,
            int[] meshIndices,
            AuraVector3 planeNormal,
            float topRadius,
            float[] heightSamples = null,
            int heightResolution = 0,
            AuraVector3 heightScale = default,
            int[] materialIndices = null)
        {
            HalfExtents = halfExtents;
            Radius = radius;
            Height = height;
            MeshAssetId = meshAssetId;
            MeshVertices = meshVertices;
            MeshIndices = meshIndices;
            PlaneNormal = planeNormal;
            TopRadius = topRadius;
            HeightSamples = heightSamples;
            HeightResolution = heightResolution;
            HeightScale = heightScale;
            MaterialIndices = materialIndices;
        }

        public AuraVector3 HalfExtents { get; }
        public float Radius { get; }
        public float Height { get; }
        public int MeshAssetId { get; }
        public AuraVector3[] MeshVertices { get; }
        public int[] MeshIndices { get; }
        public AuraVector3 PlaneNormal { get; }
        public float TopRadius { get; }
        public float[] HeightSamples { get; }
        public int HeightResolution { get; }
        public AuraVector3 HeightScale { get; }
        public int[] MaterialIndices { get; }

        public AuraShapeGeometry WithMaterialIndices(int[] materialIndices) =>
            new AuraShapeGeometry(HalfExtents, Radius, Height, MeshAssetId, MeshVertices, MeshIndices, PlaneNormal, TopRadius,
                HeightSamples, HeightResolution, HeightScale, materialIndices);

        public AuraShapeGeometry Scaled(AuraVector3 scale)
        {
            if (scale.X == 1f && scale.Y == 1f && scale.Z == 1f)
                return this;

            var radiusScale = Max(MathF.Abs(scale.X), MathF.Abs(scale.Z));
            var halfExtents = new AuraVector3(HalfExtents.X * scale.X, HalfExtents.Y * scale.Y, HalfExtents.Z * scale.Z);
            var radius = Radius * radiusScale;
            var height = Height * MathF.Abs(scale.Y);
            var topRadius = TopRadius * radiusScale;

            AuraVector3[] vertices = null;
            if (MeshVertices != null)
            {
                vertices = new AuraVector3[MeshVertices.Length];
                for (var index = 0; index < vertices.Length; index++)
                {
                    var vertex = MeshVertices[index];
                    vertices[index] = new AuraVector3(vertex.X * scale.X, vertex.Y * scale.Y, vertex.Z * scale.Z);
                }
            }

            return new AuraShapeGeometry(halfExtents, radius, height, MeshAssetId, vertices, MeshIndices, PlaneNormal, topRadius,
                HeightSamples, HeightResolution, new AuraVector3(HeightScale.X * scale.X, HeightScale.Y * scale.Y, HeightScale.Z * scale.Z), MaterialIndices);
        }

        private static float Max(float left, float right) => left > right ? left : right;

        public static AuraShapeGeometry Box(AuraVector3 halfExtents) =>
            new AuraShapeGeometry(halfExtents, 0f, 0f, -1, null, null, default, 0f);

        public static AuraShapeGeometry Box(float halfX, float halfY, float halfZ) =>
            Box(new AuraVector3(halfX, halfY, halfZ));

        public static AuraShapeGeometry Sphere(float radius) =>
            new AuraShapeGeometry(AuraVector3.Zero, radius, 0f, -1, null, null, default, 0f);

        public static AuraShapeGeometry Capsule(float radius, float height) =>
            new AuraShapeGeometry(AuraVector3.Zero, radius, height, -1, null, null, default, 0f);

        public static AuraShapeGeometry Cylinder(float radius, float height) =>
            new AuraShapeGeometry(AuraVector3.Zero, radius, height, -1, null, null, default, 0f);

        public static AuraShapeGeometry Plane(AuraVector3 normal) =>
            new AuraShapeGeometry(AuraVector3.Zero, 0f, 0f, -1, null, null, normal, 0f);

        public static AuraShapeGeometry TaperedCapsule(float bottomRadius, float topRadius, float height) =>
            new AuraShapeGeometry(AuraVector3.Zero, bottomRadius, height, -1, null, null, default, topRadius);

        public static AuraShapeGeometry TaperedCylinder(float bottomRadius, float topRadius, float height) =>
            new AuraShapeGeometry(AuraVector3.Zero, bottomRadius, height, -1, null, null, default, topRadius);

        public static AuraShapeGeometry ConvexMesh(AuraVector3[] vertices, int[] indices = null, int meshAssetId = -1) =>
            new AuraShapeGeometry(AuraVector3.Zero, 0f, 0f, meshAssetId, vertices, indices, default, 0f);

        public static AuraShapeGeometry TriangleMesh(AuraVector3[] vertices, int[] indices, int meshAssetId = -1) =>
            new AuraShapeGeometry(AuraVector3.Zero, 0f, 0f, meshAssetId, vertices, indices, default, 0f);

        public static AuraShapeGeometry HeightField(float[] samples, int resolution, AuraVector3 scale) =>
            new AuraShapeGeometry(AuraVector3.Zero, 0f, 0f, -1, null, null, default, 0f, samples, resolution, scale);

        public AuraResult Validate(AuraShapeType type)
        {
            switch (type)
            {
                case AuraShapeType.Box:
                    return HalfExtents.X > 0f && HalfExtents.Y > 0f && HalfExtents.Z > 0f
                        ? AuraResult.Success
                        : AuraResult.InvalidDefinition;
                case AuraShapeType.Sphere:
                    return Radius > 0f ? AuraResult.Success : AuraResult.InvalidDefinition;
                case AuraShapeType.Capsule:
                case AuraShapeType.Cylinder:
                    return Radius > 0f && Height > 0f ? AuraResult.Success : AuraResult.InvalidDefinition;
                case AuraShapeType.TaperedCapsule:
                case AuraShapeType.TaperedCylinder:
                    return Radius > 0f && Height > 0f && TopRadius >= 0f ? AuraResult.Success : AuraResult.InvalidDefinition;
                case AuraShapeType.Plane:
                    return PlaneNormal.LengthSquared > 1e-6f ? AuraResult.Success : AuraResult.InvalidDefinition;
                case AuraShapeType.ConvexMesh:
                    return MeshVertices != null && MeshVertices.Length >= 4 ? AuraResult.Success : AuraResult.InvalidDefinition;
                case AuraShapeType.TriangleMesh:
                    return MeshVertices != null && MeshVertices.Length >= 3 &&
                           MeshIndices != null && MeshIndices.Length >= 3 && MeshIndices.Length % 3 == 0
                        ? AuraResult.Success
                        : AuraResult.InvalidDefinition;
                case AuraShapeType.HeightField:
                    return HeightSamples != null && HeightResolution >= 2 &&
                           HeightSamples.Length == HeightResolution * HeightResolution &&
                           HeightScale.X > 0f && HeightScale.Y > 0f && HeightScale.Z > 0f
                        ? AuraResult.Success
                        : AuraResult.InvalidDefinition;
                default:
                    return AuraResult.UnsupportedShape;
            }
        }

        bool IEquatable<AuraShapeGeometry>.Equals(AuraShapeGeometry other) =>
            HalfExtents.Equals(other.HalfExtents) &&
            Radius.Equals(other.Radius) &&
            Height.Equals(other.Height) &&
            MeshAssetId == other.MeshAssetId &&
            ReferenceEquals(MeshVertices, other.MeshVertices) &&
            ReferenceEquals(MeshIndices, other.MeshIndices) &&
            PlaneNormal.Equals(other.PlaneNormal) &&
            TopRadius.Equals(other.TopRadius) &&
            ReferenceEquals(HeightSamples, other.HeightSamples) &&
            HeightResolution == other.HeightResolution &&
            HeightScale.Equals(other.HeightScale);

        public override bool Equals(object obj) => obj is AuraShapeGeometry other && ((IEquatable<AuraShapeGeometry>)this).Equals(other);
        public override int GetHashCode() => HashCode.Combine(HalfExtents, Radius, Height, MeshAssetId, PlaneNormal, TopRadius, HeightResolution, HeightScale);
        public override string ToString() => $"half={HalfExtents} r={Radius} h={Height} top={TopRadius} mesh={MeshAssetId}";

        public static bool operator ==(AuraShapeGeometry left, AuraShapeGeometry right) => ((IEquatable<AuraShapeGeometry>)left).Equals(right);
        public static bool operator !=(AuraShapeGeometry left, AuraShapeGeometry right) => !((IEquatable<AuraShapeGeometry>)left).Equals(right);
    }
}
