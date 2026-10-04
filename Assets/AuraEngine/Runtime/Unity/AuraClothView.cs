using System;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class AuraClothView : MonoBehaviour
    {
        [SerializeField]
        private int _width = 8;

        [SerializeField]
        private int _height = 8;

        [SerializeField]
        private float _spacing = 0.25f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _damping = 0.98f;

        [SerializeField]
        private float _gravityScale = 1f;

        [SerializeField]
        private Vector3 _gravity = new Vector3(0f, -9.81f, 0f);

        [SerializeField]
        private Vector3 _wind = Vector3.zero;

        private AuraClothSolver _solver;
        private Mesh _mesh;
        private Vector3[] _scratch;

        private void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null)
            {
                Debug.LogError($"[{nameof(AuraClothView)}] Missing MeshFilter on '{name}'. Author MeshFilter and MeshRenderer in the scene/prefab.", this);
                return;
            }

            if (GetComponent<MeshRenderer>() == null)
            {
                Debug.LogError($"[{nameof(AuraClothView)}] Missing MeshRenderer on '{name}'. Author MeshFilter and MeshRenderer in the scene/prefab.", this);
                return;
            }

            // Simulation runs in the view's local space; the transform places the result.
            try
            {
                var definition = new AuraClothDefinition(_width, _height, _spacing, AuraPose.Identity, _damping, _gravityScale);
                _solver = new AuraClothSolver(definition);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(AuraClothView)}] Failed to build cloth solver on '{name}': {exception.Message}", this);
                _solver = null;
                return;
            }

            // Geometry data only: no GameObjects or components are created here.
            _mesh = new Mesh { name = $"{name}_Cloth" };
            _scratch = new Vector3[_solver.ParticleCount];
            WritePositions(_scratch);
            _mesh.vertices = _scratch;
            _mesh.uv = BuildUVs(_solver.Width, _solver.Height);
            _mesh.triangles = BuildTriangles(_solver.Width, _solver.Height);
            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
            filter.sharedMesh = _mesh;
        }

        private void FixedUpdate()
        {
            if (_solver == null || _mesh == null)
                return;

            _solver.Step(Time.fixedDeltaTime, _gravity.ToAura(), _wind.ToAura());

            if (_scratch == null || _scratch.Length != _solver.ParticleCount)
                _scratch = new Vector3[_solver.ParticleCount];

            WritePositions(_scratch);
            _mesh.vertices = _scratch;
            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
        }

        private void WritePositions(Vector3[] destination)
        {
            for (var y = 0; y < _solver.Height; y++)
            {
                for (var x = 0; x < _solver.Width; x++)
                {
                    var position = _solver.GetPosition(x, y);
                    destination[y * _solver.Width + x] = new Vector3(position.X, position.Y, position.Z);
                }
            }
        }

        private static Vector2[] BuildUVs(int width, int height)
        {
            var uvs = new Vector2[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    uvs[y * width + x] = new Vector2((float)x / (width - 1), 1f - (float)y / (height - 1));
            }

            return uvs;
        }

        private static int[] BuildTriangles(int width, int height)
        {
            var triangles = new int[(width - 1) * (height - 1) * 6];
            var cursor = 0;
            for (var y = 0; y < height - 1; y++)
            {
                for (var x = 0; x < width - 1; x++)
                {
                    var a = y * width + x;
                    var b = a + 1;
                    var c = a + width;
                    var d = c + 1;
                    triangles[cursor++] = a;
                    triangles[cursor++] = c;
                    triangles[cursor++] = b;
                    triangles[cursor++] = b;
                    triangles[cursor++] = c;
                    triangles[cursor++] = d;
                }
            }

            return triangles;
        }
    }
}
