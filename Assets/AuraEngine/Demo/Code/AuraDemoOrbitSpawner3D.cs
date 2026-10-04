using AuraEngine.Core;
using AuraEngine.Unity;
using UnityEngine;

namespace AuraEngine.Demo
{
    /* Gives bodies the tangential speed of a circular orbit around a radial force field once the simulation exists.
       The speed comes from the kernel's force field formula (AuraDemoOrbitMath) for each body's current distance, so
       no velocity needs to be authored. Set the world gravity to zero (or tick _zeroWorldGravity) for a clean orbit.
       The kernel integrates with a fixed step, so a speed scale slightly below or above 1 gives ellipses. */
    public sealed class AuraDemoOrbitSpawner3D : MonoBehaviour
    {
        [SerializeField]
        private AuraSimulationInstance _instance;

        [Tooltip("A Radial force field with a positive (attracting) strength.")]
        [SerializeField]
        private AuraForceFieldAuthoring _field;

        [SerializeField]
        private AuraPhysicsBodyAuthoring[] _bodies = new AuraPhysicsBodyAuthoring[0];

        [Tooltip("Normal of the orbital plane; the orbit turns counter-clockwise around it.")]
        [SerializeField]
        private Vector3 _orbitAxis = Vector3.up;

        [SerializeField]
        private bool _clockwise;

        [Tooltip("1 = circular orbit, below 1 falls inward on an ellipse, above 1 climbs outward.")]
        [SerializeField]
        [Min(0f)]
        private float _speedScale = 1f;

        [Tooltip("Gravity scale of the bodies (Acceleration mode fields scale by it).")]
        [SerializeField]
        private float _bodyGravityScale = 1f;

        [Tooltip("Mass of the bodies (Force mode fields divide by it).")]
        [SerializeField]
        [Min(0.001f)]
        private float _bodyMass = 1f;

        [SerializeField]
        private bool _zeroWorldGravity = true;

        private bool _applied;

        private void Update()
        {
            if (_applied || _instance == null || !_instance.IsCreated || _field == null || !_field.FieldId.IsValid)
                return;

            _applied = true;
            Apply();
        }

        private void Apply()
        {
            if (_field.Kind != AuraForceFieldKind.Radial || !(_field.Strength > 0f))
            {
                Debug.LogError($"{nameof(AuraDemoOrbitSpawner3D)} on '{name}' needs a Radial field with positive strength.", this);
                return;
            }

            var world = _instance.World;
            if (_zeroWorldGravity)
            {
                var gravityResult = world.ForceFields.SetGravity(AuraVector3.Zero);
                if (gravityResult != AuraResult.Success)
                    Debug.LogError($"{nameof(AuraDemoOrbitSpawner3D)} on '{name}' could not zero the world gravity: {gravityResult}.", this);
            }

            var position = _field.transform.position;
            var center = new AuraVector3(position.x, position.y, position.z);
            var axis = new AuraVector3(_orbitAxis.x, _orbitAxis.y, _orbitAxis.z);
            var accelerationScale = _field.Mode == AuraForceFieldMode.Force ? 1f / _bodyMass : _bodyGravityScale;
            var control = world.BodyControl;
            for (var index = 0; index < _bodies.Length; index++)
            {
                var authoring = _bodies[index];
                if (authoring == null || authoring.EntityId.IsNone || !world.TryGetBody(authoring.EntityId, out var body)
                    || !world.TryGetBodyState(body, out var state))
                {
                    Debug.LogError($"{nameof(AuraDemoOrbitSpawner3D)} on '{name}': body {index} is missing or not built.", this);
                    continue;
                }

                var radial = state.Pose.Position - center;
                var distance = radial.Length;
                var speed = AuraDemoOrbitMath.CircularSpeed(_field.Falloff, _field.Strength, distance, _field.MinRadius, _field.MaxRadius, _field.ZoneExtent, accelerationScale) * _speedScale;
                var tangent = AuraVector3.Cross(axis, radial);
                if (!(speed > 0f) || tangent.LengthSquared < 1e-8f)
                {
                    Debug.LogWarning($"{nameof(AuraDemoOrbitSpawner3D)} on '{name}': body {index} is outside the field, on the orbit axis or at its centre; no orbit velocity applied.", this);
                    continue;
                }

                tangent = tangent.Normalized() * (_clockwise ? -speed : speed);
                var result = control.SetLinearVelocity(body, tangent);
                if (result != AuraResult.Success)
                    Debug.LogWarning($"{nameof(AuraDemoOrbitSpawner3D)} on '{name}': body {index} returned {result}.", this);
            }
        }
    }
}
