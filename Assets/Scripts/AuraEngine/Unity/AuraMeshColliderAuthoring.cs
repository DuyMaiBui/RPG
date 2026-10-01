using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    public sealed class AuraMeshColliderAuthoring : AuraColliderAuthoring
    {
        [SerializeField]
        private AuraPhysicsMeshData _meshData;

        public override AuraShapeType ShapeType => AuraShapeType.TriangleMesh;

        protected override AuraShapeGeometry Geometry =>
            _meshData != null
                ? AuraShapeGeometry.TriangleMesh(_meshData.Vertices.ToAuraArray(), _meshData.Indices, _meshData.AssetId)
                : AuraShapeGeometry.TriangleMesh(null, null);

        public void ApplyBakedMesh(AuraPhysicsMeshData data)
        {
            _meshData = data;
        }
    }
}
