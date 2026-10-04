using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraSoftBodyAuthoring : MonoBehaviour
    {
        [SerializeField]
        private Vector3[] _vertices = new Vector3[8]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
        };

        [SerializeField]
        private int[] _triangles = new int[36]
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            2, 3, 7, 2, 7, 6,
            0, 4, 7, 0, 7, 3,
            1, 2, 6, 1, 6, 5,
        };

        private AuraSimulationInstance _instance;
        private AuraSoftBodyId _softBody = AuraSoftBodyId.Invalid;

        public AuraSoftBodyId SoftBodyId => _softBody;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraSoftBodyAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);

            _instance = null;
            _softBody = AuraSoftBodyId.Invalid;
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            if ((instance.World.Capabilities & AuraPhysicsCapabilities.SoftBodies) == 0)
            {
                Debug.LogError($"{nameof(AuraSoftBodyAuthoring)} on '{name}' needs a backend that supports soft bodies.", this);
                return;
            }

            if (_vertices == null || _vertices.Length < 3 || _triangles == null || _triangles.Length == 0 || _triangles.Length % 3 != 0)
            {
                Debug.LogError($"{nameof(AuraSoftBodyAuthoring)} on '{name}' has an invalid soft-body mesh.", this);
                return;
            }

            var vertices = new AuraVector3[_vertices.Length];
            for (var index = 0; index < vertices.Length; index++)
                vertices[index] = new AuraVector3(_vertices[index].x, _vertices[index].y, _vertices[index].z);

            var faces = new uint[_triangles.Length];
            for (var index = 0; index < faces.Length; index++)
                faces[index] = (uint)Mathf.Max(0, _triangles[index]);

            var definition = new AuraSoftBodyDefinition(transform.ToAuraPose(), AuraPhysicsLayer.Default, vertices, faces);
            _softBody = instance.AttachSoftBody(definition);
            if (!_softBody.IsValid)
                Debug.LogError($"{nameof(AuraSoftBodyAuthoring)} on '{name}' failed to create a soft body.", this);
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (!_softBody.IsValid)
                return;

            instance.DetachSoftBody(_softBody);
            _softBody = AuraSoftBodyId.Invalid;
        }
    }
}
