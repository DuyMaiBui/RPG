using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraJoint2DAuthoring : MonoBehaviour
    {
        [Header("Bodies")]
        [SerializeField] private AuraPhysicsBody2DAuthoring _bodyA;
        [SerializeField] private AuraPhysicsBody2DAuthoring _bodyB;

        [Header("Joint")]
        [SerializeField] private AuraJointType _type = AuraJointType.Hinge;
        [SerializeField] private Vector2 _anchorA;
        [SerializeField] private Vector2 _anchorB;
        [SerializeField] private Vector2 _axis = Vector2.right;
        [SerializeField] private float _distance = 1f;

        [Header("Limits / motor")]
        [SerializeField] private bool _enableLimit;
        [SerializeField] private float _minLimit;
        [SerializeField] private float _maxLimit;
        [SerializeField] private bool _motorEnabled;
        [SerializeField] private float _motorTargetVelocity;
        [SerializeField] private float _maxMotorForce = 1f;
        [SerializeField] private float _springFrequency = 2f;
        [SerializeField] private float _springDamping = 0.5f;

        private AuraSimulationInstance _instance;
        private AuraJointId _joint = AuraJointId.Invalid;

        public AuraJointId JointId => _joint;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraJoint2DAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);
            _instance = null;
            _joint = AuraJointId.Invalid;
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            if (instance.World.Definition.Mode != AuraPhysicsMode.Plane2D)
            {
                Debug.LogError($"{nameof(AuraJoint2DAuthoring)} on '{name}' requires Plane2D mode.", this);
                return;
            }

            if (_bodyA == null || _bodyB == null || _bodyA.EntityId.IsNone || _bodyB.EntityId.IsNone)
            {
                Debug.LogError($"{nameof(AuraJoint2DAuthoring)} on '{name}' requires two registered 2D bodies.", this);
                return;
            }

            if (_type != AuraJointType.Hinge && _type != AuraJointType.Distance &&
                _type != AuraJointType.Spring && _type != AuraJointType.Fixed &&
                _type != AuraJointType.Point && _type != AuraJointType.Slider)
            {
                Debug.LogError($"{nameof(AuraJoint2DAuthoring)} on '{name}' does not support {_type} in Box2D mode.", this);
                return;
            }

            var anchorA = ToWorldAnchor(_bodyA.transform, _anchorA);
            var anchorB = ToWorldAnchor(_bodyB.transform, _anchorB);
            var worldAxis = _bodyA.transform.TransformDirection(new Vector3(_axis.x, _axis.y, 0f));
            var axis = new AuraVector3(worldAxis.x, worldAxis.y, 0f);
            var definition = new AuraJointDefinition(
                _type,
                PhysicsBodyId.Invalid,
                PhysicsBodyId.Invalid,
                anchorA,
                anchorB,
                _distance,
                axis,
                axis,
                _enableLimit,
                _minLimit,
                _maxLimit,
                AuraVector3.UnitZ,
                AuraVector3.UnitZ,
                0f,
                _motorEnabled,
                _motorTargetVelocity,
                _maxMotorForce,
                _springFrequency,
                _springDamping);

            _joint = instance.AttachJoint(_bodyA.EntityId, _bodyB.EntityId, definition);
            if (!_joint.IsValid)
                Debug.LogError($"{nameof(AuraJoint2DAuthoring)} on '{name}' failed to create a {_type} joint.", this);
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (_joint.IsValid)
                instance.DetachJoint(_joint);
            _joint = AuraJointId.Invalid;
        }

        private static AuraVector3 ToWorldAnchor(Transform body, Vector2 localAnchor)
        {
            var point = body.TransformPoint(new Vector3(localAnchor.x, localAnchor.y, 0f));
            return new AuraVector3(point.x, point.y, 0f);
        }
    }
}
