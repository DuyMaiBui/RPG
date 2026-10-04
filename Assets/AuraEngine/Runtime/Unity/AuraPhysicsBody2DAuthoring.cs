using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AuraPhysicsView))]
    public sealed class AuraPhysicsBody2DAuthoring : MonoBehaviour
    {
        [SerializeField]
        private AuraBodyType _type = AuraBodyType.Dynamic;

        [SerializeField]
        [Min(0.0001f)]
        private float _mass = 1f;

        [SerializeField]
        private float _gravityScale = 1f;

        [SerializeField]
        [Min(0f)]
        private float _linearDamping;

        [SerializeField]
        [Min(0f)]
        private float _angularDamping;

        [SerializeField]
        private bool _freezePositionX;

        [SerializeField]
        private bool _freezePositionY;

        [SerializeField]
        private bool _freezeRotation;

        [SerializeField]
        private bool _allowSleeping = true;

        private AuraSimulationInstance _instance;
        private SimulationEntityId _entity = SimulationEntityId.None;

        public SimulationEntityId EntityId => _entity;
        public AuraBodyType BodyType => _type;

        public AuraResult TryBuildDefinition(out AuraPhysicsBodyDefinition definition)
        {
            definition = default;
            var colliders = GetComponentsInChildren<AuraCollider2DAuthoring>(true);
            if (colliders.Length == 0)
                return AuraResult.InvalidDefinition;

            var shapes = new AuraPhysicsShapeDefinition[colliders.Length];
            for (var index = 0; index < colliders.Length; index++)
            {
                shapes[index] = colliders[index].BuildShape();
                var shapeResult = shapes[index].Validate();
                if (shapeResult != AuraResult.Success)
                    return shapeResult;
            }

            var position = transform.position;
            var halfAngle = transform.eulerAngles.z * Mathf.Deg2Rad * 0.5f;
            var freeze = AuraBodyFreezeFlags.PositionZ | AuraBodyFreezeFlags.RotationX | AuraBodyFreezeFlags.RotationY;
            if (_freezePositionX) freeze |= AuraBodyFreezeFlags.PositionX;
            if (_freezePositionY) freeze |= AuraBodyFreezeFlags.PositionY;
            if (_freezeRotation) freeze |= AuraBodyFreezeFlags.RotationZ;

            definition = new AuraPhysicsBodyDefinition(
                _type,
                new AuraPose(
                    new AuraVector3(position.x, position.y, 0f),
                    new AuraQuaternion(0f, 0f, Mathf.Sin(halfAngle), Mathf.Cos(halfAngle))),
                shapes[0].Layer,
                AuraPhysicsLayerMask.All,
                shapes,
                _mass,
                _gravityScale,
                linearDamping: _linearDamping,
                angularDamping: _angularDamping,
                freeze: freeze,
                allowSleeping: _allowSleeping);
            return definition.Validate();
        }

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraPhysicsBody2DAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
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
            if (instance.World.Definition.Mode != AuraPhysicsMode.Plane2D)
            {
                Debug.LogError($"{nameof(AuraPhysicsBody2DAuthoring)} on '{name}' requires Plane2D mode.", this);
                return;
            }

            var view = GetComponent<AuraPhysicsView>();
            if (view == null)
            {
                Debug.LogError($"{nameof(AuraPhysicsBody2DAuthoring)} on '{name}' requires an {nameof(AuraPhysicsView)} on the same GameObject.", this);
                return;
            }

            var result = TryBuildDefinition(out var definition);
            if (result != AuraResult.Success)
            {
                Debug.LogError($"{nameof(AuraPhysicsBody2DAuthoring)} on '{name}' has invalid XY-plane collider data ({result}).", this);
                return;
            }

            var capabilities = instance.World.Capabilities;
            for (var index = 0; index < definition.Shapes.Length; index++)
            {
                var required = definition.Shapes[index].Type == AuraShapeType.Box
                    ? AuraPhysicsCapabilities.ShapeBox
                    : definition.Shapes[index].Type == AuraShapeType.Capsule
                        ? AuraPhysicsCapabilities.ShapeCapsule
                        : AuraPhysicsCapabilities.ShapeSphere;
                if ((capabilities & required) == 0)
                {
                    Debug.LogError($"{nameof(AuraPhysicsBody2DAuthoring)} on '{name}' uses unsupported 2D shape {definition.Shapes[index].Type}.", this);
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
    }
}
