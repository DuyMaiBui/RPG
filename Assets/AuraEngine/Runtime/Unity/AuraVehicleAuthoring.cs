using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraVehicleAuthoring : MonoBehaviour
    {
        [Header("Chassis")]
        [SerializeField]
        private AuraPhysicsBodyAuthoring _chassis;

        [Header("Wheels")]
        [SerializeField]
        private Vector3[] _wheelPositions = new Vector3[4]
        {
            new Vector3(-0.9f, -0.3f, 1.2f),
            new Vector3(0.9f, -0.3f, 1.2f),
            new Vector3(-0.9f, -0.3f, -1.2f),
            new Vector3(0.9f, -0.3f, -1.2f),
        };

        [SerializeField]
        [Min(0.05f)]
        private float _wheelRadius = 0.35f;

        [SerializeField]
        [Min(0.05f)]
        private float _wheelWidth = 0.25f;

        [Header("Suspension")]
        [SerializeField]
        [Min(0.01f)]
        private float _suspensionMinLength = 0.2f;

        [SerializeField]
        [Min(0.01f)]
        private float _suspensionMaxLength = 0.5f;

        [SerializeField]
        [Min(0.1f)]
        private float _suspensionFrequency = 4f;

        [SerializeField]
        [Min(0f)]
        private float _suspensionDamping = 0.7f;

        [Header("Drive")]
        [SerializeField]
        [Range(0f, 60f)]
        private float _maxSteerAngle = 30f;

        [SerializeField]
        [Range(1f, 180f)]
        private float _maxPitchRollAngle = AuraVehicleDefinition.DefaultMaxPitchRollAngle * Mathf.Rad2Deg;

        [SerializeField]
        [Min(0f)]
        private float _maxEngineTorque = 800f;

        private AuraSimulationInstance _instance;
        private AuraVehicleId _vehicle = AuraVehicleId.Invalid;

        public AuraVehicleId VehicleId => _vehicle;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraVehicleAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);

            _instance = null;
            _vehicle = AuraVehicleId.Invalid;
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            if ((instance.World.Capabilities & AuraPhysicsCapabilities.Vehicles) == 0)
            {
                Debug.LogError($"{nameof(AuraVehicleAuthoring)} on '{name}' needs a backend that supports vehicles.", this);
                return;
            }

            if (_chassis == null)
            {
                Debug.LogError($"{nameof(AuraVehicleAuthoring)} on '{name}' is missing a chassis body reference.", this);
                return;
            }

            if (_chassis.EntityId.IsNone)
            {
                Debug.LogError($"{nameof(AuraVehicleAuthoring)} on '{name}' references a chassis that has no entity.", this);
                return;
            }

            var wheels = new AuraVector3[_wheelPositions.Length];
            for (var index = 0; index < wheels.Length; index++)
                wheels[index] = new AuraVector3(_wheelPositions[index].x, _wheelPositions[index].y, _wheelPositions[index].z);

            var forward = transform.forward;
            var up = transform.up;
            var definition = new AuraVehicleDefinition(
                PhysicsBodyId.Invalid,
                new AuraVector3(up.x, up.y, up.z),
                new AuraVector3(forward.x, forward.y, forward.z),
                wheels,
                _wheelRadius,
                _wheelWidth,
                _suspensionMinLength,
                _suspensionMaxLength,
                _suspensionFrequency,
                _suspensionDamping,
                _maxSteerAngle * Mathf.Deg2Rad,
                _maxPitchRollAngle * Mathf.Deg2Rad,
                _maxEngineTorque);

            _vehicle = instance.AttachVehicle(_chassis.EntityId, definition);
            if (!_vehicle.IsValid)
                Debug.LogError($"{nameof(AuraVehicleAuthoring)} on '{name}' failed to create a vehicle.", this);
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (!_vehicle.IsValid)
                return;

            instance.DetachVehicle(_vehicle);
            _vehicle = AuraVehicleId.Invalid;
        }
    }
}
