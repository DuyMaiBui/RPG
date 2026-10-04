using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    /* Thin driver that drives a joint motor with a constant or periodic target. Hinge and Slider authoring use
       IPhysicsJointControl.SetMotor; SixDof and SwingTwist authoring use IPhysicsJointAxisControl on the chosen axis.
       Rotational targets are degrees (deg/s in Velocity mode), translational ones metres (m/s). Target =
       offset + amplitude * wave(time). */
    public sealed class AuraDemoMotorDriver3D : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [Tooltip("AuraJointAuthoring (Hinge or Slider), AuraSixDofJointAuthoring or AuraSwingTwistJointAuthoring.")]
        [SerializeField]
        private AuraJointAuthoringBase _joint;

        [Tooltip("SixDof: 0-2 translation XYZ, 3-5 rotation XYZ. SwingTwist: 0 twist, 1 normal swing, 2 plane swing. Ignored for Hinge and Slider.")]
        [SerializeField]
        [Range(0, 5)]
        private int _axis;

        [SerializeField]
        private AuraJointMotorMode _motorMode = AuraJointMotorMode.Velocity;

        [SerializeField]
        private AuraDemoWaveKind _wave = AuraDemoWaveKind.Sine;

        [SerializeField]
        private float _offset;

        [SerializeField]
        private float _amplitude = 90f;

        [SerializeField]
        [Min(0f)]
        private float _frequencyHz = 0.25f;

        [Tooltip("Maximum motor force (N) or torque (N*m).")]
        [SerializeField]
        [Min(0.01f)]
        private float _maxForce = 100f;

        [Tooltip("Position mode spring tuning; 0 keeps the backend default.")]
        [SerializeField]
        [Min(0f)]
        private float _springFrequency;

        [SerializeField]
        [Min(0f)]
        private float _springDamping;

        private float _time;
        private float _lastSent = float.NaN;
        private bool _failed;

        private void Update()
        {
            if (_failed || _instance == null || !_instance.IsCreated || _joint == null || !_joint.JointId.IsValid)
                return;

            if (_motorMode == AuraJointMotorMode.Off)
                return;

            _time += Time.deltaTime;
            var target = _offset + _amplitude * AuraDemoWave.Evaluate(_wave, _time, _frequencyHz);
            if (_wave == AuraDemoWaveKind.Constant && target == _lastSent)
                return;

            _lastSent = target;
            var rotational = IsRotational(out var supported);
            if (!supported)
            {
                Fail("the joint type has no motor (use Hinge, Slider, SixDof or SwingTwist).");
                return;
            }

            var scaled = rotational ? target * Mathf.Deg2Rad : target;
            var motor = _motorMode == AuraJointMotorMode.Velocity
                ? AuraJointMotorDefinition.Velocity(scaled, _maxForce)
                : AuraJointMotorDefinition.Position(scaled, _maxForce, _springFrequency, _springDamping);

            var world = _instance.World;
            AuraResult result;
            if (_joint is AuraJointAuthoring)
            {
                var control = world.JointControl;
                result = control == null ? AuraResult.UnsupportedOperation : control.SetMotor(_joint.JointId, motor);
            }
            else
            {
                IPhysicsJointAxisControl axisControl = world.JointAxisControl;
                result = axisControl == null ? AuraResult.UnsupportedOperation : axisControl.SetAxisMotor(_joint.JointId, _axis, motor);
            }

            if (result != AuraResult.Success)
                Fail($"the motor command returned {result}.");
        }

        private bool IsRotational(out bool supported)
        {
            supported = true;
            if (_joint is AuraJointAuthoring authoring)
            {
                supported = authoring.JointType == AuraJointType.Hinge || authoring.JointType == AuraJointType.Slider;
                return authoring.JointType == AuraJointType.Hinge;
            }

            if (_joint is AuraSixDofJointAuthoring)
                return _axis >= 3;

            if (_joint is AuraSwingTwistJointAuthoring)
                return true;

            supported = false;
            return false;
        }

        private void Fail(string message)
        {
            _failed = true;
            Debug.LogError($"{nameof(AuraDemoMotorDriver3D)} on '{name}': {message}", this);
        }
    }
}
