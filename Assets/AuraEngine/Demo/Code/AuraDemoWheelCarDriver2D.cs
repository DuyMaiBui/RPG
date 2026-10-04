using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AuraEngine.Demo
{
    /* Drives a Box2D wheel car through the motors of its wheel joints (AuraJoint2DAuthoring of type Wheel).
       D/Right accelerates forward, A/Left reverses, Space brakes; or replays a scripted sequence for smoke runs.
       Positive joint motor speed spins the wheel counter-clockwise, so a car facing +X needs negative speed:
       keep Invert Drive on for that layout. */
    public sealed class AuraDemoWheelCarDriver2D : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [SerializeField]
        private AuraJoint2DAuthoring[] _driveWheels = new AuraJoint2DAuthoring[0];

        [SerializeField]
        [Min(0f)]
        [Tooltip("Wheel speed in rad/s at full throttle.")]
        private float _maxWheelSpeed = 30f;

        [SerializeField]
        [Min(0f)]
        private float _driveTorque = 60f;

        [SerializeField]
        [Min(0f)]
        private float _brakeTorque = 120f;

        [SerializeField]
        private bool _invertDrive = true;

        [SerializeField]
        [Min(0.01f)]
        private float _riseRate = 2f;

        [SerializeField]
        [Min(0.01f)]
        private float _fallRate = 4f;

        [SerializeField]
        private bool _useScript;

        [SerializeField]
        private bool _loopScript = true;

        [SerializeField]
        private AuraDemoWheelCarInputStep[] _script = new AuraDemoWheelCarInputStep[0];

        private AuraThrottleRamp _ramp;
        private AuraScriptPlayer _player;
        private bool _reportedError;

        /* Current smoothed throttle, -1..1. */
        public float Throttle => _ramp == null ? 0f : _ramp.Value;

        private void OnEnable()
        {
            _ramp = new AuraThrottleRamp(_riseRate, _fallRate);
            _player = null;
        }

        private void Update()
        {
            if (_instance == null || !_instance.IsCreated || _driveWheels == null || _ramp == null)
                return;

            float targetThrottle;
            float brake;
            if (_useScript)
                ReadScript(out targetThrottle, out brake);
            else
                ReadKeyboard(out targetThrottle, out brake);

            var throttle = _ramp.Step(targetThrottle, Time.deltaTime);
            var jointControl = _instance.World.JointControl;
            if (jointControl == null)
                return;

            var motor = brake > 0f
                ? AuraJointMotorDefinition.Velocity(0f, Mathf.Max(0.0001f, _brakeTorque * brake))
                : Mathf.Abs(throttle) < 0.001f
                    ? AuraJointMotorDefinition.Off
                    : AuraJointMotorDefinition.Velocity(throttle * _maxWheelSpeed * (_invertDrive ? -1f : 1f), Mathf.Max(0.0001f, _driveTorque * Mathf.Abs(throttle)));

            for (var index = 0; index < _driveWheels.Length; index++)
            {
                var wheel = _driveWheels[index];
                if (wheel == null || !wheel.JointId.IsValid)
                    continue;

                var result = jointControl.SetMotor(wheel.JointId, motor);
                if (result != AuraResult.Success && result != AuraResult.InvalidHandle && !_reportedError)
                {
                    _reportedError = true;
                    Debug.LogError($"{nameof(AuraDemoWheelCarDriver2D)} could not set the motor of '{wheel.name}': {result}.", this);
                }
            }
        }

        private static void ReadKeyboard(out float throttle, out float brake)
        {
            throttle = 0f;
            brake = 0f;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                throttle -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                throttle += 1f;
            if (keyboard.spaceKey.isPressed)
                brake = 1f;
        }

        private void ReadScript(out float throttle, out float brake)
        {
            throttle = 0f;
            brake = 0f;
            if (_script == null || _script.Length == 0)
                return;

            if (_player == null)
            {
                var durations = new float[_script.Length];
                for (var index = 0; index < durations.Length; index++)
                    durations[index] = _script[index].Duration;
                _player = new AuraScriptPlayer(durations, _loopScript);
            }

            var segment = _player.Advance(Time.deltaTime);
            if (segment < 0)
                return;

            throttle = _script[segment].Throttle;
            brake = _script[segment].Brake;
        }
    }
}
