using System.Collections.Generic;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /// <summary>
    /// Thin authoring bridge that feeds world colliders to Verlet cloth/hair views.
    /// References authored Aura collider components (3D and 2D) and/or plain Transforms with an
    /// explicit shape; every fixed step the owners' CURRENT world poses are converted into
    /// engine-free <see cref="AuraVerletCollider"/> values. Nothing is created at runtime; missing
    /// references are reported as authoring errors.
    /// 2D: colliders from the 2D authoring components (and Transform shapes with Planar enabled) are
    /// planar. Planar colliders act on the XY plane only, as infinite extrusions along Z; pair them with
    /// the view's lock-to-plane option so free particles stay on one Z.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuraVerletColliderSource : MonoBehaviour
    {
        [SerializeField]
        private AuraColliderAuthoring[] _colliders3D = System.Array.Empty<AuraColliderAuthoring>();

        [SerializeField]
        private AuraCollider2DAuthoring[] _colliders2D = System.Array.Empty<AuraCollider2DAuthoring>();

        [SerializeField]
        private AuraVerletTransformShape[] _transformShapes = System.Array.Empty<AuraVerletTransformShape>();

        [Tooltip("Treat the Transform shapes as 2D (XY plane, infinite along Z).")]
        [SerializeField]
        private bool _transformShapesPlanar;

        [Tooltip("Distance particles are kept off the collider surface.")]
        [SerializeField]
        [Min(0f)]
        private float _skin = 0.02f;

        private readonly List<AuraVerletColliderBinding> _bindings = new List<AuraVerletColliderBinding>();
        private readonly List<AuraVerletCollider> _current = new List<AuraVerletCollider>();
        private bool _built;
        private float _lastRefreshTime = float.NaN;

        /// <summary>
        /// Rebuilds the collider list from the current world poses (once per fixed step; further calls
        /// in the same step return the cached list). The returned list instance is reused.
        /// </summary>
        public IReadOnlyList<AuraVerletCollider> Refresh()
        {
            EnsureBuilt();
            var time = Time.fixedTime;
            if (time == _lastRefreshTime)
                return _current;

            _lastRefreshTime = time;
            _current.Clear();
            for (var index = 0; index < _bindings.Count; index++)
                _current.Add(_bindings[index].Build(_skin, Time.fixedDeltaTime));
            return _current;
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            for (var index = 0; index < _colliders3D.Length; index++)
            {
                var authoring = _colliders3D[index];
                if (authoring == null)
                {
                    ReportMissing("3D collider", index);
                    continue;
                }

                AddShape(authoring.transform, authoring.BuildShape(), false, authoring);
            }

            for (var index = 0; index < _colliders2D.Length; index++)
            {
                var authoring = _colliders2D[index];
                if (authoring == null)
                {
                    ReportMissing("2D collider", index);
                    continue;
                }

                AddShape(authoring.transform, authoring.BuildShape(), true, authoring);
            }

            for (var index = 0; index < _transformShapes.Length; index++)
            {
                var shape = _transformShapes[index];
                if (shape.Target == null)
                {
                    ReportMissing("Transform shape target", index);
                    continue;
                }

                var radius = Mathf.Max(0.001f, shape.Radius);
                var normal = shape.Normal.sqrMagnitude > 1e-12f ? shape.Normal : Vector3.up;
                _bindings.Add(new AuraVerletColliderBinding(
                    shape.Target,
                    shape.Kind,
                    shape.Center,
                    Quaternion.identity,
                    radius,
                    Mathf.Max(shape.Height, radius * 2f),
                    new Vector3(Mathf.Max(0.001f, shape.Size.x), Mathf.Max(0.001f, shape.Size.y), Mathf.Max(0.001f, shape.Size.z)) * 0.5f,
                    normal,
                    shape.Friction,
                    _transformShapesPlanar));
            }

            if (_bindings.Count > AuraVerletParticles.MaxColliders)
                Debug.LogWarning($"[{nameof(AuraVerletColliderSource)}] '{name}' has {_bindings.Count} colliders; only the first {AuraVerletParticles.MaxColliders} are used.", this);
        }

        private void AddShape(Transform owner, AuraPhysicsShapeDefinition shape, bool planar, Object context)
        {
            if (shape.IsTrigger)
                return;

            var binding = AuraVerletColliderBinding.FromShape(owner, shape, planar);
            if (binding == null)
            {
                Debug.LogWarning($"[{nameof(AuraVerletColliderSource)}] Shape type {shape.Type} on '{owner.name}' is not supported by Verlet collision (sphere, capsule, box, plane only).", context);
                return;
            }

            _bindings.Add(binding);
        }

        private void ReportMissing(string what, int index) =>
            Debug.LogError($"[{nameof(AuraVerletColliderSource)}] {what} reference at index {index} is missing on '{name}'. Author the reference in the scene/prefab.", this);
    }
}
