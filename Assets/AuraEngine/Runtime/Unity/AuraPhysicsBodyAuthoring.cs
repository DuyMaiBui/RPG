using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [RequireComponent(typeof(AuraPhysicsView))]
    public sealed class AuraPhysicsBodyAuthoring : MonoBehaviour
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Body")]
#endif
        [SerializeField]
        private AuraBodyType _type = AuraBodyType.Dynamic;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Body")]
#endif
        [SerializeField]
        [Min(0.0001f)]
        private float _mass = 1f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Body")]
#endif
        [SerializeField]
        private float _gravityScale = 1f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Damping")]
#endif
        [SerializeField]
        [Min(0f)]
        private float _linearDamping;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Damping")]
#endif
        [SerializeField]
        [Min(0f)]
        private float _angularDamping;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Constraints")]
        [Sirenix.OdinInspector.HorizontalGroup("FreezePos")]
#endif
        [SerializeField]
        private bool _freezePosX;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Constraints")]
        [Sirenix.OdinInspector.HorizontalGroup("FreezePos")]
#endif
        [SerializeField]
        private bool _freezePosY;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Constraints")]
        [Sirenix.OdinInspector.HorizontalGroup("FreezePos")]
#endif
        [SerializeField]
        private bool _freezePosZ;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Constraints")]
        [Sirenix.OdinInspector.HorizontalGroup("FreezeRot")]
#endif
        [SerializeField]
        private bool _freezeRotX;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Constraints")]
        [Sirenix.OdinInspector.HorizontalGroup("FreezeRot")]
#endif
        [SerializeField]
        private bool _freezeRotY;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Constraints")]
        [Sirenix.OdinInspector.HorizontalGroup("FreezeRot")]
#endif
        [SerializeField]
        private bool _freezeRotZ;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rigidbody")]
#endif
        [SerializeField]
        private Vector3 _centerOfMass;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rigidbody")]
#endif
        [SerializeField]
        [Min(0.001f)]
        private float _inertiaMultiplier = 1f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rigidbody")]
#endif
        [SerializeField]
        private AuraBodyCollisionDetection _collisionDetection = AuraBodyCollisionDetection.Discrete;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rigidbody")]
#endif
        [SerializeField]
        private bool _allowSleeping = true;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rigidbody")]
#endif
        [SerializeField]
        [Min(0f)]
        private float _maxLinearVelocity;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rigidbody")]
#endif
        [SerializeField]
        [Min(0f)]
        private float _maxAngularVelocity;

        private AuraSimulationInstance _instance;
        private SimulationEntityId _entity = SimulationEntityId.None;

        public SimulationEntityId EntityId => _entity;

        public AuraBodyType BodyType => _type;

        public bool TryBuildDefinition(out AuraPhysicsBodyDefinition definition, out AuraResult result)
        {
            definition = default;
            result = AuraResult.Success;

            var colliders = GetComponentsInChildren<AuraColliderAuthoring>(true);
            if (colliders.Length == 0)
                return false;

            var shapes = new AuraPhysicsShapeDefinition[colliders.Length];
            for (var index = 0; index < colliders.Length; index++)
            {
                shapes[index] = colliders[index].BuildShape();
                result = shapes[index].Validate();
                if (result != AuraResult.Success)
                    return false;
            }

            definition = new AuraPhysicsBodyDefinition(
                _type,
                transform.ToAuraPose(),
                shapes[0].Layer,
                AuraPhysicsLayerMask.All,
                shapes,
                _mass,
                _gravityScale,
                groupIndex: 0,
                material: AuraPhysicsMaterialDefinition.Default,
                linearDamping: _linearDamping,
                angularDamping: _angularDamping,
                freeze: BuildFreezeFlags(),
                centerOfMass: new AuraVector3(_centerOfMass.x, _centerOfMass.y, _centerOfMass.z),
                inertiaMultiplier: _inertiaMultiplier,
                collisionDetection: _collisionDetection,
                allowSleeping: _allowSleeping,
                maxLinearVelocity: _maxLinearVelocity,
                maxAngularVelocity: _maxAngularVelocity);

            result = definition.Validate();
            return result == AuraResult.Success;
        }

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraPhysicsBodyAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);

            _instance = null;
            _entity = SimulationEntityId.None;
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            if (instance.World.Definition.Mode != AuraPhysicsMode.Full3D)
            {
                Debug.LogError($"{nameof(AuraPhysicsBodyAuthoring)} on '{name}' requires Full3D mode.", this);
                return;
            }

            var view = GetComponent<AuraPhysicsView>();
            if (view == null)
            {
                Debug.LogError($"{nameof(AuraPhysicsBodyAuthoring)} requires an {nameof(AuraPhysicsView)} on the same GameObject.", this);
                return;
            }

            if (!TryBuildDefinition(out var definition, out var result))
            {
                Debug.LogError($"{nameof(AuraPhysicsBodyAuthoring)} has an invalid physics definition ({result}) on '{name}'.", this);
                return;
            }

            var capabilities = instance.World.Capabilities;
            for (var index = 0; index < definition.Shapes.Length; index++)
            {
                var required = RequiredCapability(definition.Shapes[index].Type);
                if ((capabilities & required) == 0)
                {
                    Debug.LogError($"{nameof(AuraPhysicsBodyAuthoring)} on '{name}' uses {definition.Shapes[index].Type}, which the active physics backend does not support.", this);
                    return;
                }
            }

            _entity = instance.AttachView(view, definition);
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (_entity.IsNone)
                return;

            instance.DetachView(_entity);
            _entity = SimulationEntityId.None;
        }

        private AuraBodyFreezeFlags BuildFreezeFlags()
        {
            var flags = AuraBodyFreezeFlags.None;
            if (_freezePosX) flags |= AuraBodyFreezeFlags.PositionX;
            if (_freezePosY) flags |= AuraBodyFreezeFlags.PositionY;
            if (_freezePosZ) flags |= AuraBodyFreezeFlags.PositionZ;
            if (_freezeRotX) flags |= AuraBodyFreezeFlags.RotationX;
            if (_freezeRotY) flags |= AuraBodyFreezeFlags.RotationY;
            if (_freezeRotZ) flags |= AuraBodyFreezeFlags.RotationZ;
            return flags;
        }

        private static AuraPhysicsCapabilities RequiredCapability(AuraShapeType type)
        {
            switch (type)
            {
                case AuraShapeType.Box: return AuraPhysicsCapabilities.ShapeBox;
                case AuraShapeType.Sphere: return AuraPhysicsCapabilities.ShapeSphere;
                case AuraShapeType.Capsule: return AuraPhysicsCapabilities.ShapeCapsule;
                case AuraShapeType.Cylinder: return AuraPhysicsCapabilities.ShapeCylinder;
                case AuraShapeType.ConvexMesh: return AuraPhysicsCapabilities.ShapeConvexMesh;
                case AuraShapeType.TriangleMesh: return AuraPhysicsCapabilities.ShapeTriangleMesh;
                case AuraShapeType.Plane: return AuraPhysicsCapabilities.ShapePlane;
                case AuraShapeType.TaperedCapsule: return AuraPhysicsCapabilities.ShapeTaperedCapsule;
                case AuraShapeType.TaperedCylinder: return AuraPhysicsCapabilities.ShapeTaperedCylinder;
                case AuraShapeType.HeightField: return AuraPhysicsCapabilities.ShapeHeightField;
                default: return AuraPhysicsCapabilities.None;
            }
        }
    }
}
