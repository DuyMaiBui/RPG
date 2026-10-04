using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    /* Animates the vector of a Drag (wind velocity) or Directional (acceleration) force field into gusts: the speed
       follows AuraDemoGustCurve between the minimum and maximum, and the direction can sway around +Y. */
    public sealed class AuraDemoWindGust3D : MonoBehaviour
    {
        [SerializeField]
        private AuraForceFieldAuthoring _field;

        [SerializeField]
        private Vector3 _direction = Vector3.right;

        [Tooltip("Speed (Drag) or acceleration (Directional) in the lulls.")]
        [SerializeField]
        private float _minSpeed;

        [SerializeField]
        private float _maxSpeed = 12f;

        [SerializeField]
        [Min(0.1f)]
        private float _gustPeriod = 4f;

        [Tooltip("1 = smooth swell, larger = short strong gusts with long lulls.")]
        [SerializeField]
        [Min(0.1f)]
        private float _sharpness = 2f;

        [SerializeField]
        [Range(0f, 90f)]
        private float _swayDegrees = 15f;

        [SerializeField]
        [Min(0.1f)]
        private float _swayPeriod = 7f;

        private float _time;

        private void Update()
        {
            if (_field == null || _direction.sqrMagnitude < 1e-8f)
                return;

            _time += Time.deltaTime;
            var gust = AuraDemoGustCurve.Evaluate(_time, _gustPeriod, _sharpness);
            var speed = Mathf.Lerp(_minSpeed, _maxSpeed, gust);
            var sway = _swayDegrees * Mathf.Sin(_time * 2f * Mathf.PI / _swayPeriod);
            var direction = Quaternion.AngleAxis(sway, Vector3.up) * _direction.normalized;
            _field.SetVector(direction * speed);
        }
    }
}
