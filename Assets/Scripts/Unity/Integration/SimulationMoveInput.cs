using UnityEngine;
using UnityEngine.InputSystem;

namespace RPG.Unity
{
    public sealed class SimulationMoveInput : MonoBehaviour
    {
        [SerializeField] private SimulationUnityBridge _bridge;
        private Vector2 _lastDirection;

        private void Update()
        {
            if (_bridge == null || Keyboard.current == null) return;

            var direction = Vector2.zero;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) direction.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) direction.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) direction.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) direction.y += 1f;
            if (direction == _lastDirection) return;

            _lastDirection = direction;
            _bridge.TrySendMoveIntent(direction);
        }

        private void OnDisable()
        {
            if (_bridge == null || _lastDirection == Vector2.zero) return;
            _lastDirection = Vector2.zero;
            _bridge.TrySendMoveIntent(Vector2.zero);
        }
    }
}
