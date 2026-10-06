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
        private bool _trianglesAssigned;

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

            // The kernel reports world-space vertices; the mesh lives in this object's local space.
            for (var index = 0; index < _scratch.Length; index++)
                _scratch[index] = transform.InverseTransformPoint(new Vector3(state.Vertices[index].X, state.Vertices[index].Y, state.Vertices[index].Z));

            _mesh.SetVertices(_scratch);
            if (!_trianglesAssigned && _softBody.Triangles != null)
            {
                // Without triangles the soft body is simulated but invisible.
                _mesh.SetTriangles(_softBody.Triangles, 0);
                _trianglesAssigned = true;
            }

            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
        }
    }
}
