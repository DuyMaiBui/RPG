using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    public sealed class AuraDemoVehicleDriver : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [SerializeField]
        private AuraVehicleAuthoring _vehicle;

        [SerializeField]
        [Range(-1f, 1f)]
        private float _forward = 0.6f;

        [SerializeField]
        private float _steerAmplitude = 0.4f;

        [SerializeField]
        private float _steerSpeed = 0.6f;

        private float _time;

        private void Update()
        {
            if (_instance == null || !_instance.IsCreated || _vehicle == null)
                return;

            var id = _vehicle.VehicleId;
            if (!id.IsValid)
                return;

            _time += Time.deltaTime * _steerSpeed;
            var steering = Mathf.Sin(_time) * _steerAmplitude;
            _instance.SetVehicleInput(id, _forward, steering, 0f, 0f);
        }
    }
}
