using System;

namespace AuraEngine.Core
{
    public readonly struct AuraPhysicsShapeDefinition : IEquatable<AuraPhysicsShapeDefinition>
    {
        public AuraPhysicsShapeDefinition(
            AuraShapeType type,
            AuraPose localPose,
            bool isTrigger,
            AuraPhysicsMaterialDefinition material,
            AuraPhysicsLayer layer,
            AuraShapeGeometry geometry)
        {
            Type = type;
            LocalPose = localPose;
            IsTrigger = isTrigger;
            Material = material;
            Layer = layer;
            Geometry = geometry;
        }

        public AuraShapeType Type { get; }
        public AuraPose LocalPose { get; }
        public bool IsTrigger { get; }
        public AuraPhysicsMaterialDefinition Material { get; }
        public AuraPhysicsLayer Layer { get; }
        public AuraShapeGeometry Geometry { get; }

        public static AuraPhysicsShapeDefinition Box(AuraVector3 halfExtents, AuraPhysicsLayer layer = default) =>
            new AuraPhysicsShapeDefinition(
                AuraShapeType.Box,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                layer,
                AuraShapeGeometry.Box(halfExtents));

        public static AuraPhysicsShapeDefinition Sphere(float radius, AuraPhysicsLayer layer = default) =>
            new AuraPhysicsShapeDefinition(
                AuraShapeType.Sphere,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                layer,
                AuraShapeGeometry.Sphere(radius));

        public static AuraPhysicsShapeDefinition HeightField(float[] samples, int resolution, AuraVector3 scale, AuraPhysicsLayer layer = default) =>
            new AuraPhysicsShapeDefinition(
                AuraShapeType.HeightField,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                layer,
                AuraShapeGeometry.HeightField(samples, resolution, scale));

        public static AuraPhysicsShapeDefinition TriangleMesh(AuraVector3[] vertices, int[] indices, int meshAssetId = -1, AuraPhysicsLayer layer = default) =>
            new AuraPhysicsShapeDefinition(
                AuraShapeType.TriangleMesh,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                layer,
                AuraShapeGeometry.TriangleMesh(vertices, indices, meshAssetId));

        public AuraPhysicsShapeDefinition WithLocalPose(AuraPose localPose) =>
            new AuraPhysicsShapeDefinition(Type, localPose, IsTrigger, Material, Layer, Geometry);

        public AuraPhysicsShapeDefinition AsTrigger() =>
            new AuraPhysicsShapeDefinition(Type, LocalPose, true, Material, Layer, Geometry);

        public AuraPhysicsShapeDefinition WithLayer(AuraPhysicsLayer layer) =>
            new AuraPhysicsShapeDefinition(Type, LocalPose, IsTrigger, Material, layer, Geometry);

        public AuraPhysicsShapeDefinition WithMaterial(AuraPhysicsMaterialDefinition material) =>
            new AuraPhysicsShapeDefinition(Type, LocalPose, IsTrigger, material, Layer, Geometry);

        public AuraResult Validate()
        {
            if (Geometry.Validate(Type) != AuraResult.Success)
                return AuraResult.InvalidDefinition;

            return Material.Validate();
        }

        bool IEquatable<AuraPhysicsShapeDefinition>.Equals(AuraPhysicsShapeDefinition other) =>
            Type == other.Type &&
            LocalPose.Equals(other.LocalPose) &&
            IsTrigger == other.IsTrigger &&
            Material.Equals(other.Material) &&
            Layer == other.Layer &&
            Geometry.Equals(other.Geometry);

        public override bool Equals(object obj) => obj is AuraPhysicsShapeDefinition other && ((IEquatable<AuraPhysicsShapeDefinition>)this).Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)Type, LocalPose, IsTrigger, Material, Layer, Geometry);

        public override string ToString() => $"{Type} trigger={IsTrigger} layer={Layer}";

        public static bool operator ==(AuraPhysicsShapeDefinition left, AuraPhysicsShapeDefinition right) =>
            ((IEquatable<AuraPhysicsShapeDefinition>)left).Equals(right);

        public static bool operator !=(AuraPhysicsShapeDefinition left, AuraPhysicsShapeDefinition right) =>
            !((IEquatable<AuraPhysicsShapeDefinition>)left).Equals(right);
    }
}
