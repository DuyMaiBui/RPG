using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class AuraSoftBodyView : MonoBehaviour
    {
        [SerializeField]
        private AuraSoftBodyAuthoring _softBody;

        private Mesh _mesh;
        private Vector3[] _scratch;

        private void Awake()
        {
            _mesh = new Mesh { name = $"{name}_SoftBody" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private void Update()
        {
            if (_softBody == null)
                return;

            var id = _softBody.SoftBodyId;
            if (!id.IsValid)
                return;

            var instance = GetComponentInParent<AuraSimulationInstance>();
            if (instance == null || !instance.IsCreated)
                return;

            if (!instance.World.TryGetSoftBodyState(id, out var state) || state.Vertices == null)
                return;

            if (_scratch == null || _scratch.Length != state.Vertices.Length)
                _scratch = new Vector3[state.Vertices.Length];

            for (var index = 0; index < _scratch.Length; index++)
                _scratch[index] = new Vector3(state.Vertices[index].X, state.Vertices[index].Y, state.Vertices[index].Z);

            _mesh.SetVertices(_scratch);
            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
        }
    }
}
