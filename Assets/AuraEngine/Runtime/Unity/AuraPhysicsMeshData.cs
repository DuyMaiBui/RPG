using UnityEngine;

namespace AuraEngine.Unity
{
    [CreateAssetMenu(menuName = "AuraEngine/Physics Mesh", fileName = "AuraPhysicsMesh")]
    public sealed class AuraPhysicsMeshData : ScriptableObject
    {
        [SerializeField]
        private Vector3[] _vertices;

        [SerializeField]
        private int[] _indices;

        [SerializeField]
        private int _assetId = -1;

        [SerializeField]
        private string _sourceGuid;

        public Vector3[] Vertices => _vertices;

        public int[] Indices => _indices;

        public int AssetId => _assetId;

        public string SourceGuid => _sourceGuid;

        public void Initialize(Vector3[] vertices, int[] indices, int assetId, string sourceGuid)
        {
            _vertices = vertices;
            _indices = indices;
            _assetId = assetId;
            _sourceGuid = sourceGuid;
        }
    }
}
