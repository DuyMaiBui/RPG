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

        [SerializeField]
        [Tooltip("Seconds between jumps; 0 disables jumping.")]
        private float _jumpInterval;

        [SerializeField]
        private float _jumpSpeed = 6f;

        private float _time;
        private float _nextJump;
        private bool _jumpPending;

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

            if (_jumpPending && !_character.IsGrounded)
                _jumpPending = false;

            if (_jumpInterval > 0f && _time >= _nextJump && _character.IsGrounded)
            {
                _nextJump = _time + _jumpInterval;
                _jumpPending = true;
            }

            // Hold the up command until a simulation tick has lifted the character off the ground,
            // because Update can run several times per fixed step.
            if (_jumpPending)
            {
                var velocity = _character.DesiredVelocity;
                _character.DesiredVelocity = new AuraVector3(velocity.X, _jumpSpeed, velocity.Z);
            }
        }
    }
}
