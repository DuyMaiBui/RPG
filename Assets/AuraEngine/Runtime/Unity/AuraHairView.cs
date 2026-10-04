using System;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraHairView : MonoBehaviour
    {
        [Tooltip("One authored LineRenderer per strand (useWorldSpace must be enabled). Never created at runtime.")]
        [SerializeField]
        private LineRenderer[] _strands = Array.Empty<LineRenderer>();

        [Tooltip("Local-space root offset per strand, matching _strands order.")]
        [SerializeField]
        private Vector3[] _strandRoots = Array.Empty<Vector3>();

        [SerializeField]
        private int _pointsPerStrand = 8;

        [SerializeField]
        private float _segmentLength = 0.1f;

        [SerializeField]
        private Vector3 _gravity = new Vector3(0f, -9.81f, 0f);

        [SerializeField]
        private Vector3 _wind = Vector3.zero;

        [Tooltip("Peak acceleration of a sinusoidal gust added to Wind. Keeps hanging hair in motion; set to zero for still air.")]
        [SerializeField]
        private Vector3 _gustAmplitude = new Vector3(3f, 0f, 0f);

        [SerializeField]
        private float _gustFrequency = 0.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _damping = 0.98f;

        private float _elapsed;
        private AuraHairStrandSolver _solver;

        private void Awake()
        {
            BuildSolver();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            BuildSolver();
        }

        private void BuildSolver()
        {
            _solver = null;
            if (_strands == null || _strands.Length == 0)
            {
                Debug.LogError($"[{nameof(AuraHairView)}] No strand LineRenderers authored on '{name}'. Author one LineRenderer per strand in the scene/prefab.", this);
                return;
            }

            for (var index = 0; index < _strands.Length; index++)
            {
                if (_strands[index] == null)
                {
                    Debug.LogError($"[{nameof(AuraHairView)}] Strand LineRenderer at index {index} is missing on '{name}'. Author the reference in the scene/prefab.", this);
                    return;
                }
            }

            if (_strandRoots == null || _strandRoots.Length != _strands.Length)
            {
                Debug.LogError($"[{nameof(AuraHairView)}] Strand root count ({_strandRoots?.Length ?? 0}) does not match LineRenderer count ({_strands.Length}) on '{name}'. Author matching references in the scene/prefab.", this);
                return;
            }

            try
            {
                var roots = new AuraVector3[_strandRoots.Length];
                for (var index = 0; index < _strandRoots.Length; index++)
                    roots[index] = _strandRoots[index].ToAura();

                var definition = new AuraHairDefinition(transform.ToAuraPose(), roots, _pointsPerStrand, _segmentLength);
                _solver = new AuraHairStrandSolver(definition, _damping);

                for (var index = 0; index < _strands.Length; index++)
                    _strands[index].positionCount = _pointsPerStrand;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(AuraHairView)}] Failed to build hair solver on '{name}': {exception.Message}", this);
                _solver = null;
                return;
            }

            PushToRenderers();
        }

        private void FixedUpdate()
        {
            if (_solver == null)
                return;

            _elapsed += Time.fixedDeltaTime;
            _solver.SetRootPose(transform.ToAuraPose());
            _solver.Step(
                Time.fixedDeltaTime,
                _gravity.ToAura(),
                AuraVerletWind.Sample(_wind.ToAura(), _gustAmplitude.ToAura(), _gustFrequency, _elapsed));
            PushToRenderers();
        }

        private void PushToRenderers()
        {
            if (_solver == null || _strands == null)
                return;

            for (var strand = 0; strand < _solver.StrandCount && strand < _strands.Length; strand++)
            {
                var line = _strands[strand];
                if (line == null)
                    continue;

                for (var point = 0; point < _solver.PointsPerStrand; point++)
                    line.SetPosition(point, _solver.GetPoint(strand, point).ToUnity());
            }
        }
    }
}
