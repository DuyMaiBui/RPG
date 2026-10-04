using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    /* Slow-motion pulse (world TimeScale) and optional per-body hit-stop (AuraHitStop) on impacts or on a timer.
       Put it on the same GameObject (or a child) of an AuraPhysicsView so collision events reach it. The time scale is
       restored on disable. Real (unscaled) time drives the envelope, so slow motion never slows its own recovery. */
    public sealed class AuraDemoHitStopTrigger3D : MonoBehaviour, IAuraPhysicsEventReceiver
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [Tooltip("Collision impulse needed to trigger; 0 triggers on every collision.")]
        [SerializeField]
        [Min(0f)]
        private float _minImpulse = 5f;

        [Tooltip("Minimum real seconds between triggers.")]
        [SerializeField]
        [Min(0f)]
        private float _cooldown = 0.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _slowScale = 0.15f;

        [SerializeField]
        [Min(0f)]
        private float _holdSeconds = 0.25f;

        [SerializeField]
        [Min(0f)]
        private float _recoverSeconds = 0.4f;

        [Tooltip("Simulation steps to freeze both colliding bodies; 0 disables hit-stop.")]
        [SerializeField]
        [Min(0)]
        private int _hitStopTicks;

        [Tooltip("Gravity scale AuraHitStop restores on the frozen bodies.")]
        [SerializeField]
        private float _restoreGravityScale = 1f;

        [Tooltip("Real seconds between timed pulses; 0 = only impacts trigger.")]
        [SerializeField]
        [Min(0f)]
        private float _timerInterval;

        [SerializeField]
        [Min(0f)]
        private float _timerStartDelay = 2f;

        private readonly AuraDemoSlowMotionEnvelope _envelope = new AuraDemoSlowMotionEnvelope();
        private AuraDemoIntervalTimer _timer;
        private float _baseScale = 1f;
        private float _lastTrigger = float.NegativeInfinity;
        private bool _hasBaseScale;
        private bool _pendingTrigger;

        private void OnEnable()
        {
            _hasBaseScale = false;
            _timer = _timerInterval > 0f ? new AuraDemoIntervalTimer(_timerStartDelay, _timerInterval, true) : null;
        }

        private void OnDisable()
        {
            if (_hasBaseScale && _instance != null)
                _instance.TimeScale = _baseScale;

            _hasBaseScale = false;
        }

        private void Update()
        {
            if (_instance == null || !_instance.IsCreated)
                return;

            var delta = Time.unscaledDeltaTime;
            if (_timer != null && _timer.Advance(delta))
                _pendingTrigger = true;

            if (_pendingTrigger)
            {
                _pendingTrigger = false;
                if (!_hasBaseScale)
                {
                    _baseScale = _instance.TimeScale;
                    _hasBaseScale = true;
                }

                _envelope.Trigger(_slowScale, _holdSeconds, _recoverSeconds);
                _lastTrigger = Time.unscaledTime;
            }

            if (_hasBaseScale)
            {
                _instance.TimeScale = _baseScale * _envelope.Advance(delta);
                if (!_envelope.IsActive)
                    _hasBaseScale = false;
            }
        }

        void IAuraPhysicsEventReceiver.OnCollisionEnter(in AuraPhysicsEvent value)
        {
            if (value.Impulse < _minImpulse || Time.unscaledTime - _lastTrigger < _cooldown)
                return;

            _pendingTrigger = true;
            _lastTrigger = Time.unscaledTime;
            if (_hitStopTicks > 0 && _instance != null && _instance.IsCreated)
            {
                Freeze(value.BodyA);
                Freeze(value.BodyB);
            }
        }

        void IAuraPhysicsEventReceiver.OnCollisionExit(in AuraPhysicsEvent value)
        {
        }

        void IAuraPhysicsEventReceiver.OnTriggerEnter(in AuraPhysicsEvent value)
        {
        }

        void IAuraPhysicsEventReceiver.OnTriggerExit(in AuraPhysicsEvent value)
        {
        }

        private void Freeze(PhysicsBodyId body)
        {
            var result = _instance.World.HitStop.Begin(body, _hitStopTicks, _restoreGravityScale);

            // Static and disabled bodies cannot be frozen; that is expected for the ground.
            if (result != AuraResult.Success && result != AuraResult.InvalidDefinition && result != AuraResult.BodyDisabled)
                Debug.LogWarning($"{nameof(AuraDemoHitStopTrigger3D)} on '{name}': hit-stop returned {result}.", this);
        }
    }
}
