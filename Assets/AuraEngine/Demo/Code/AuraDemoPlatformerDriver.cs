using AuraEngine.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AuraEngine.Demo
{
    /* Moves an AuraCharacter2DAuthoring with the keyboard (A/D or arrows, Space/W/Up to jump) through the
       Input System, or replays a scripted sequence so smoke tests can run without a keyboard. */
    public sealed class AuraDemoPlatformerDriver : MonoBehaviour
    {
        [SerializeField]
        private AuraCharacter2DAuthoring _character;

        [SerializeField]
        [Min(0f)]
        private float _speed = 5f;

        [SerializeField]
        private bool _useScript;

        [SerializeField]
        private bool _loopScript = true;

        [SerializeField]
        private AuraDemoPlatformerInputStep[] _script = new AuraDemoPlatformerInputStep[0];

        private int _stepIndex = -1;
        private float _stepTime;

        private void Update()
        {
            if (_character == null)
                return;

            float move;
            var jump = false;
            if (_useScript)
                move = ReadScript(out jump);
            else
                move = ReadKeyboard(out jump);

            _character.DesiredVelocityX = move * _speed;
            if (jump)
                _character.RequestJump();
        }

        private static float ReadKeyboard(out bool jump)
        {
            jump = false;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return 0f;

            var move = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                move -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                move += 1f;

            jump = keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
            return move;
        }

        private float ReadScript(out bool jump)
        {
            jump = false;
            if (_script == null || _script.Length == 0)
                return 0f;

            if (_stepIndex < 0)
            {
                _stepIndex = 0;
                _stepTime = 0f;
                jump = _script[0].Jump;
            }

            _stepTime += Time.deltaTime;
            while (_stepTime >= _script[_stepIndex].Duration)
            {
                _stepTime -= _script[_stepIndex].Duration;
                if (_stepIndex + 1 >= _script.Length && !_loopScript)
                    return 0f;

                _stepIndex = (_stepIndex + 1) % _script.Length;
                jump |= _script[_stepIndex].Jump;
                if (_script[_stepIndex].Duration <= 0f)
                    break;
            }

            return _script[_stepIndex].Move;
        }
    }
}
