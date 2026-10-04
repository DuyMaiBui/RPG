using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    /* Periodically pushes a Box2D body (impulse or velocity, optionally alternating direction) to keep a demo scene
       moving. Uses IPhysicsBodyControl through the simulation world. */
    public sealed class AuraDemoImpulseKicker2D : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [SerializeField]
        private AuraPhysicsBody2DAuthoring _body;

        [SerializeField]
        [Min(0.05f)]
        private float _interval = 3f;

        [SerializeField]
        [Min(0f)]
        private float _initialDelay = 1f;

        [SerializeField]
        private Vector2 _impulse = new Vector2(4f, 6f);

        [SerializeField]
        [Tooltip("Set the linear velocity to Impulse instead of adding an impulse.")]
        private bool _setVelocity;

        [SerializeField]
        [Tooltip("Mirror the horizontal component on every other kick.")]
        private bool _alternateDirection = true;

        [SerializeField]
        private float _angularImpulse;

        private float _timer;
        private int _kicks;
        private bool _reportedError;

        public int KickCount => _kicks;

        private void OnEnable()
        {
            _timer = _initialDelay;
            _kicks = 0;
        }

        private void Update()
        {
            if (_instance == null || !_instance.IsCreated || _body == null || _body.EntityId.IsNone)
                return;

            _timer -= Time.deltaTime;
            if (_timer > 0f)
                return;

            _timer += _interval;
            Kick();
        }

        private void Kick()
        {
            var control = _instance.World.BodyControl;
            if (control == null || !_instance.World.TryGetBody(_body.EntityId, out var body))
                return;

            var sign = _alternateDirection && (_kicks & 1) == 1 ? -1f : 1f;
            var vector = new AuraVector3(_impulse.x * sign, _impulse.y, 0f);
            var result = _setVelocity ? control.SetLinearVelocity(body, vector) : control.AddImpulse(body, vector);
            if (result == AuraResult.Success && _angularImpulse != 0f)
                result = control.AddAngularImpulse(body, new AuraVector3(0f, 0f, _angularImpulse * sign));

            if (result != AuraResult.Success)
            {
                if (!_reportedError)
                {
                    _reportedError = true;
                    Debug.LogError($"{nameof(AuraDemoImpulseKicker2D)} could not kick '{_body.name}': {result}.", this);
                }

                return;
            }

            _kicks++;
        }
    }
}
