using AuraEngine.Core;
using AuraEngine.Physics;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* Thin bridge for a Box2D (Plane2D) joint. Supported types: Hinge, Distance, Spring, Fixed, Point, Slider and the
       ABI 11 types Wheel (axis = suspension axis in body A space, spring frequency 0 = rigid, motor target in rad/s,
       limits = suspension travel), Mouse (body A is the static anchor body, anchor B on body B is the initial target,
       spring frequency/damping and max motor force tune the drag; move it at runtime with IPhysicsJointTarget) and
       Rope (distance = maximum length, never pushes). Break thresholds of 0 mean unbreakable. */
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

        [Header("Break thresholds (0 = unbreakable)")]
        [Min(0f)]
        [Tooltip("Reaction force (N) above which the joint snaps.")]
        [SerializeField] private float _breakForce;
        [Min(0f)]
        [Tooltip("Reaction torque (N*m) above which the joint snaps (Fixed, Hinge, Slider, Wheel).")]
        [SerializeField] private float _breakTorque;

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
                _type != AuraJointType.Point && _type != AuraJointType.Slider &&
                _type != AuraJointType.Wheel && _type != AuraJointType.Mouse && _type != AuraJointType.Rope)
            {
                Debug.LogError($"{nameof(AuraJoint2DAuthoring)} on '{name}' does not support {_type} in Box2D mode.", this);
                return;
            }

            var anchorA = ToWorldAnchor(_bodyA.transform, _anchorA);
            var anchorB = ToWorldAnchor(_bodyB.transform, _anchorB);
            var worldAxis = _bodyA.transform.TransformDirection(new Vector3(_axis.x, _axis.y, 0f));
            var axis = new AuraVector3(worldAxis.x, worldAxis.y, 0f);
            if (_type == AuraJointType.Rope && !(_distance > 0f))
            {
                Debug.LogError($"{nameof(AuraJoint2DAuthoring)} on '{name}' needs a positive rope length (Distance).", this);
                return;
            }

            AuraJointDefinition definition;
            if (_type == AuraJointType.Mouse)
                definition = AuraJointDefinition.CreateMouse(PhysicsBodyId.Invalid, PhysicsBodyId.Invalid, anchorB, _springFrequency, _springDamping, _maxMotorForce);
            else if (_type == AuraJointType.Rope)
                definition = AuraJointDefinition.CreateRope(PhysicsBodyId.Invalid, PhysicsBodyId.Invalid, anchorA, anchorB, _distance);
            else
                definition = new AuraJointDefinition(
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
            {
                Debug.LogError($"{nameof(AuraJoint2DAuthoring)} on '{name}' failed to create a {_type} joint.", this);
                return;
            }

            if (_breakForce > 0f || _breakTorque > 0f)
            {
                var jointControl = instance.World.JointControl;
                var result = jointControl == null ? AuraResult.UnsupportedOperation : jointControl.SetBreakThreshold(_joint, _breakForce, _breakTorque);
                if (result != AuraResult.Success)
                    Debug.LogError($"{nameof(AuraJoint2DAuthoring)} on '{name}' could not set the break threshold: {result}.", this);
            }
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
