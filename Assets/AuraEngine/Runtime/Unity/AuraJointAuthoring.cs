using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraJointAuthoring : AuraJointAuthoringBase
    {
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

        public AuraJointType JointType => _type;

        protected override bool TryCreateDefinition(AuraSimulationInstance instance, out AuraJointDefinition definition)
        {
            definition = new AuraJointDefinition(
                _type,
                PhysicsBodyId.Invalid,
                PhysicsBodyId.Invalid,
                AnchorOnA(_anchorA),
                AnchorOnB(_anchorB),
                _distance,
                DirectionOnA(_axisA),
                DirectionOnB(_axisB),
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
            return true;
        }
    }
}
