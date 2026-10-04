using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* Thin bridge for a kernel force field zone (planet gravity, gravity well, wind tunnel, water current,
       buoyant or low-gravity volume). Values are world space and ignore the transform scale. A moving
       transform re-poses the zone every fixed update. Needs AuraPhysicsCapabilities.ForceFields. */
    [DisallowMultipleComponent]
    public sealed class AuraForceFieldAuthoring : MonoBehaviour
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Zone")]
#endif
        [SerializeField]
        private AuraForceFieldShape _shape = AuraForceFieldShape.Sphere;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Zone")]
        [Sirenix.OdinInspector.ShowIf("@_shape == AuraEngine.Core.AuraForceFieldShape.Sphere")]
#endif
        [SerializeField]
        [Min(0.01f)]
        private float _radius = 5f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Zone")]
        [Sirenix.OdinInspector.ShowIf("@_shape == AuraEngine.Core.AuraForceFieldShape.Box")]
#endif
        [SerializeField]
        private Vector3 _size = new Vector3(10f, 10f, 10f);

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Zone")]
#endif
        [SerializeField]
        private bool _fieldEnabled = true;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Zone")]
#endif
        [Tooltip("Bit i set = affects bodies on physics layer i. Everything (-1) affects every layer.")]
        [SerializeField]
        private int _layerMask = -1;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Force")]
#endif
        [SerializeField]
        private AuraForceFieldKind _kind = AuraForceFieldKind.Radial;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Force")]
#endif
        [Tooltip("Acceleration follows the body gravity scale and ignores mass; Force divides by mass.")]
        [SerializeField]
        private AuraForceFieldMode _mode = AuraForceFieldMode.Acceleration;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Force")]
        [Sirenix.OdinInspector.HideIf("@_kind == AuraEngine.Core.AuraForceFieldKind.Radial")]
#endif
        [Tooltip("Directional: acceleration or force vector. Drag: wind velocity.")]
        [SerializeField]
        private Vector3 _vector = new Vector3(0f, 9.81f, 0f);

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Force")]
        [Sirenix.OdinInspector.HideIf("@_kind == AuraEngine.Core.AuraForceFieldKind.Directional")]
#endif
        [Tooltip("Radial: positive pulls toward the centre, negative pushes away (magnitude at distance 1 for inverse square). Drag: drag rate.")]
        [SerializeField]
        private float _strength = 20f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Force")]
        [Sirenix.OdinInspector.ShowIf("@_kind == AuraEngine.Core.AuraForceFieldKind.Radial")]
#endif
        [SerializeField]
        private AuraForceFieldFalloff _falloff = AuraForceFieldFalloff.None;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Force")]
        [Sirenix.OdinInspector.ShowIf("@_kind == AuraEngine.Core.AuraForceFieldKind.Radial")]
#endif
        [SerializeField]
        [Min(0f)]
        private float _minRadius;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Force")]
        [Sirenix.OdinInspector.ShowIf("@_kind == AuraEngine.Core.AuraForceFieldKind.Radial")]
#endif
        [Tooltip("No effect beyond this distance from the centre (0 = the zone extent).")]
        [SerializeField]
        [Min(0f)]
        private float _maxRadius;

        private AuraSimulationInstance _instance;
        private AuraForceFieldId _field = AuraForceFieldId.Invalid;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;

        public AuraForceFieldId FieldId => _field;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraForceFieldAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);

            _instance = null;
            _field = AuraForceFieldId.Invalid;
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            if ((instance.World.Capabilities & AuraPhysicsCapabilities.ForceFields) == 0)
            {
                Debug.LogError($"{nameof(AuraForceFieldAuthoring)} on '{name}': the physics backend has no force field support.", this);
                return;
            }

            _field = instance.AttachForceField(BuildDefinition());
            if (!_field.IsValid)
            {
                Debug.LogError($"{nameof(AuraForceFieldAuthoring)} on '{name}' was rejected by the backend (check radius, size and ranges).", this);
                return;
            }

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (!_field.IsValid)
                return;

            instance.DetachForceField(_field);
            _field = AuraForceFieldId.Invalid;
        }

        private void FixedUpdate()
        {
            if (!_field.IsValid || _instance == null)
                return;

            if (transform.position == _lastPosition && transform.rotation == _lastRotation)
                return;

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _instance.UpdateForceField(_field, BuildDefinition());
        }

        private AuraForceFieldDefinition BuildDefinition()
        {
            var position = transform.position;
            var rotation = transform.rotation;
            var layers = _layerMask == -1 ? AuraPhysicsLayerMask.All : new AuraPhysicsLayerMask((ulong)(uint)_layerMask);
            return new AuraForceFieldDefinition(
                _shape,
                _kind,
                new AuraPose(new AuraVector3(position.x, position.y, position.z), new AuraQuaternion(rotation.x, rotation.y, rotation.z, rotation.w)),
                _radius,
                new AuraVector3(_size.x * 0.5f, _size.y * 0.5f, _size.z * 0.5f),
                new AuraVector3(_vector.x, _vector.y, _vector.z),
                _strength,
                _mode,
                _falloff,
                _minRadius,
                _maxRadius,
                layers,
                _fieldEnabled);
        }

        private void OnDrawGizmos() => DrawZone(0.35f);

        private void OnDrawGizmosSelected() => DrawZone(1f);

        private void DrawZone(float alpha)
        {
            var color = _kind == AuraForceFieldKind.Radial ? new Color(0.9f, 0.5f, 1f, alpha)
                : _kind == AuraForceFieldKind.Drag ? new Color(0.4f, 0.8f, 1f, alpha)
                : new Color(1f, 0.8f, 0.3f, alpha);
            Gizmos.color = _fieldEnabled ? color : new Color(0.5f, 0.5f, 0.5f, alpha);

            var previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            if (_shape == AuraForceFieldShape.Sphere)
                Gizmos.DrawWireSphere(Vector3.zero, _radius);
            else
                Gizmos.DrawWireCube(Vector3.zero, _size);

            if (_kind == AuraForceFieldKind.Radial && _maxRadius > 0f)
                Gizmos.DrawWireSphere(Vector3.zero, _maxRadius);

            Gizmos.matrix = previous;

            if (_kind != AuraForceFieldKind.Radial && _vector.sqrMagnitude > 1e-6f)
                Gizmos.DrawRay(transform.position, _vector.normalized * Mathf.Min(2f, _vector.magnitude));
        }
    }
}
