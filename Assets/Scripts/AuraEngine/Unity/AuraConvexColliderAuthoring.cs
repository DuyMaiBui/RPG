using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public sealed class AuraConvexColliderAuthoring : AuraColliderAuthoring
    {
        [SerializeField]
        private AuraPhysicsMeshData _meshData;

        public override AuraShapeType ShapeType => AuraShapeType.ConvexMesh;

        protected override AuraShapeGeometry Geometry =>
            _meshData != null
                ? AuraShapeGeometry.ConvexMesh(_meshData.Vertices.ToAuraArray(), _meshData.Indices, _meshData.AssetId)
                : AuraShapeGeometry.ConvexMesh(null);

        public void ApplyBakedMesh(AuraPhysicsMeshData data)
        {
            _meshData = data;
        }
    }
}
