using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraJointAuthoring : MonoBehaviour
    {
        [Header("Bodies")]
        [SerializeField]
        private AuraPhysicsBodyAuthoring _bodyA;

        [SerializeField]
        private AuraPhysicsBodyAuthoring _bodyB;

        [Header("Joint")]
        [SerializeField]
        private AuraJointType _type = AuraJointType.Hinge;

        [SerializeField]
        private Vector3 _anchorA;

        [SerializeField]
        private Vector3 _anchorB;

        [SerializeField]
        private Vector3 _axisA = Vector3.up;

        [SerializeField]
        private Vector3 _axisB = Vector3.up;

        [SerializeField]
        private float _distance = 1f;

        [Header("Limits")]
        [SerializeField]
        private bool _enableLimit;

        [SerializeField]
        private float _minLimit;

        [SerializeField]
        private float _maxLimit;

        [SerializeField]
        private float _swingLimit;

        [Header("Motor")]
        [SerializeField]
        private bool _motorEnabled;

        [SerializeField]
        private float _motorTargetVelocity;

        [SerializeField]
        private float _maxMotorForce = 1f;

        [Header("Spring")]
        [SerializeField]
        private float _springFrequency = 2f;

        [SerializeField]
        private float _springDamping = 0.5f;

        private AuraSimulationInstance _instance;
        private AuraJointId _joint = AuraJointId.Invalid;

        public AuraJointId JointId => _joint;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraJointAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
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
            if (_bodyA == null || _bodyB == null)
            {
                Debug.LogError($"{nameof(AuraJointAuthoring)} on '{name}' is missing a body reference.", this);
                return;
            }

            if (_bodyA.EntityId.IsNone || _bodyB.EntityId.IsNone)
            {
                Debug.LogError($"{nameof(AuraJointAuthoring)} on '{name}' references a body that has no entity.", this);
                return;
            }

            var anchorA = ToAura(_bodyA.transform.TransformPoint(_anchorA));
            var anchorB = ToAura(_bodyB.transform.TransformPoint(_anchorB));
            var axisA = ToAura(_bodyA.transform.TransformDirection(_axisA));
            var axisB = ToAura(_bodyB.transform.TransformDirection(_axisB));

            var definition = new AuraJointDefinition(
                _type,
                PhysicsBodyId.Invalid,
                PhysicsBodyId.Invalid,
                anchorA,
                anchorB,
                _distance,
                axisA,
                axisB,
                _enableLimit,
                _minLimit,
                _maxLimit,
                AuraVector3.UnitZ,
                AuraVector3.UnitZ,
                _swingLimit,
                _motorEnabled,
                _motorTargetVelocity,
                _maxMotorForce,
                _springFrequency,
                _springDamping);

            _joint = instance.AttachJoint(_bodyA.EntityId, _bodyB.EntityId, definition);
            if (!_joint.IsValid)
                Debug.LogError($"{nameof(AuraJointAuthoring)} on '{name}' failed to create a {_type} joint.", this);
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (!_joint.IsValid)
                return;

            instance.DetachJoint(_joint);
            _joint = AuraJointId.Invalid;
        }

        private static AuraVector3 ToAura(Vector3 value) => new AuraVector3(value.x, value.y, value.z);
    }
}
