using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    /* Thin driver that kicks bodies for hit-reaction demos: a one-shot or periodic impulse / velocity change along a
       direction, or a radial blast around a point with distance falloff. Works on every body authoring in the list
       (chain ragdoll limbs, crates, a character-sized capsule). Bodies owned by AuraRagdollAuthoring live inside the
       kernel and cannot be addressed, so they are not kickable. */
    public sealed class AuraDemoImpulseKicker3D : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [SerializeField]
        private AuraPhysicsBodyAuthoring[] _bodies = new AuraPhysicsBodyAuthoring[0];

        [SerializeField]
        private AuraDemoKickMode _mode = AuraDemoKickMode.Impulse;

        [Tooltip("World direction of a non-radial kick.")]
        [SerializeField]
        private Vector3 _direction = Vector3.up;

        [Tooltip("Impulse (N*s) or velocity change (m/s) at full strength.")]
        [SerializeField]
        private float _magnitude = 20f;

        [Tooltip("Angular impulse or angular velocity change added to every kicked body.")]
        [SerializeField]
        private Vector3 _spin;

        [SerializeField]
        [Min(0f)]
        private float _startDelay = 1f;

        [Tooltip("Seconds between kicks; 0 = a single kick after the start delay.")]
        [SerializeField]
        [Min(0f)]
        private float _interval;

        [Tooltip("Blast centre. When set the kick is radial and _direction is ignored.")]
        [SerializeField]
        private Transform _explosionCenter;

        [SerializeField]
        [Min(0.01f)]
        private float _explosionRadius = 5f;

        [SerializeField]
        private bool _linearFalloff = true;

        [Tooltip("Tilts the blast direction toward +Y; 0 = purely radial.")]
        [SerializeField]
        [Range(0f, 2f)]
        private float _upwardBias = 0.4f;

        private AuraDemoIntervalTimer _timer;

        private void OnEnable() => _timer = new AuraDemoIntervalTimer(_startDelay, _interval, _interval > 0f);

        private void Update()
        {
            if (_instance == null || !_instance.IsCreated || _timer == null)
                return;

            if (_timer.Advance(Time.deltaTime))
                Kick();
        }

        public void Kick()
        {
            if (_instance == null || !_instance.IsCreated)
                return;

            var world = _instance.World;
            var control = world.BodyControl;
            var center = _explosionCenter != null ? new AuraVector3(_explosionCenter.position.x, _explosionCenter.position.y, _explosionCenter.position.z) : AuraVector3.Zero;
            var linear = _direction.sqrMagnitude > 1e-8f ? _direction.normalized * _magnitude : Vector3.zero;
            var fixedKick = new AuraVector3(linear.x, linear.y, linear.z);
            var angular = new AuraVector3(_spin.x, _spin.y, _spin.z);

            for (var index = 0; index < _bodies.Length; index++)
            {
                var authoring = _bodies[index];
                if (authoring == null || authoring.EntityId.IsNone || !world.TryGetBody(authoring.EntityId, out var body)
                    || !world.TryGetBodyState(body, out var state))
                {
                    Debug.LogError($"{nameof(AuraDemoImpulseKicker3D)} on '{name}': body {index} is missing or not built.", this);
                    continue;
                }

                var kick = fixedKick;
                if (_explosionCenter != null)
                {
                    kick = AuraDemoExplosionMath.RadialImpulse(center, state.Pose.Position, _magnitude, _explosionRadius, _upwardBias, _linearFalloff);
                    if (kick == AuraVector3.Zero)
                        continue;
                }

                var result = _mode == AuraDemoKickMode.Impulse
                    ? control.AddImpulse(body, kick)
                    : control.SetLinearVelocity(body, state.LinearVelocity + kick);
                if (result == AuraResult.Success && angular != AuraVector3.Zero)
                {
                    result = _mode == AuraDemoKickMode.Impulse
                        ? control.AddAngularImpulse(body, angular)
                        : control.SetAngularVelocity(body, state.AngularVelocity + angular);
                }

                if (result != AuraResult.Success)
                    Debug.LogWarning($"{nameof(AuraDemoImpulseKicker3D)} on '{name}': kicking body {index} returned {result}.", this);
            }
        }
    }
}
