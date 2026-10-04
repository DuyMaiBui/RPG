using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    public sealed class AuraDemoCharacterDriver : MonoBehaviour
    {
        [SerializeField]
        private AuraCharacterAuthoring _character;

        [SerializeField]
        private float _speed = 3f;

        [SerializeField]
        private float _turnInterval = 4f;

        [SerializeField]
        private Vector3 _direction = Vector3.forward;

        private float _time;

        private void Update()
        {
            if (_character == null)
                return;

            _time += Time.deltaTime;
            var sign = Mathf.FloorToInt(_time / Mathf.Max(0.01f, _turnInterval)) % 2 == 0 ? 1f : -1f;
            _character.DesiredVelocity = new AuraVector3(
                _direction.x * _speed * sign,
                0f,
                _direction.z * _speed * sign);
        }
    }
}
