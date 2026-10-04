using AuraEngine.Core;
using AuraEngine.Physics;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* Thin bridge for a Jolt 3D swing-twist joint (shoulder, hip, ragdoll limb): a ball joint with a swing cone, a
       twist range, optional friction and an optional twist motor. Axes are body local; angles are degrees.
       The twist axis is the limb axis; the plane axis is perpendicular to it and defines the swing frame. */
    [DisallowMultipleComponent]
    public sealed class AuraSwingTwistJointAuthoring : AuraJointAuthoringBase
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private Vector3 _anchorA;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private Vector3 _anchorB;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private Vector3 _twistAxisA = Vector3.right;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private Vector3 _twistAxisB = Vector3.right;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private Vector3 _planeAxisA = Vector3.up;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private Vector3 _planeAxisB = Vector3.up;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Limits")]
#endif
        [Tooltip("Swing cone half angle around the normal direction, degrees.")]
        [SerializeField]
        [Range(0f, 180f)]
        private float _normalSwing = 45f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Limits")]
#endif
        [Tooltip("Swing cone half angle in the plane direction, degrees. 0 reuses the normal swing.")]
        [SerializeField]
        [Range(0f, 180f)]
        private float _planeSwing;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Limits")]
#endif
        [SerializeField]
        [Range(-180f, 180f)]
        private float _twistMin = -30f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Limits")]
#endif
        [SerializeField]
        [Range(-180f, 180f)]
        private float _twistMax = 30f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Limits")]
#endif
        [SerializeField]
        private bool _pyramidSwing;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Limits")]
#endif
        [Tooltip("Friction torque (N*m) while no motor drives the joint.")]
        [SerializeField]
        [Min(0f)]
        private float _maxFriction;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Twist motor")]
#endif
        [SerializeField]
        private bool _twistMotor;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Twist motor")]
        [Sirenix.OdinInspector.ShowIf("_twistMotor")]
#endif
        [Tooltip("Target angular velocity about the twist axis, degrees per second.")]
        [SerializeField]
        private float _twistMotorVelocity = 90f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Twist motor")]
        [Sirenix.OdinInspector.ShowIf("_twistMotor")]
#endif
        [SerializeField]
        [Min(0.01f)]
        private float _twistMotorMaxTorque = 50f;

        public AuraResult SetAxisMotor(int axis, in AuraJointMotorDefinition motor)
        {
            var control = Instance != null && Instance.World != null ? Instance.World.JointAxisControl : null;
            return control == null ? AuraResult.UnsupportedOperation : control.SetAxisMotor(JointId, axis, motor);
        }

        public AuraResult SetAxisLimits(int axis, in AuraJointAxisLimit limit)
        {
            var control = Instance != null && Instance.World != null ? Instance.World.JointAxisControl : null;
            return control == null ? AuraResult.UnsupportedOperation : control.SetAxisLimits(JointId, axis, limit);
        }

        protected override bool TryCreateDefinition(AuraSimulationInstance instance, out AuraJointDefinition definition)
        {
            definition = AuraJointDefinition.CreateSwingTwist(
                PhysicsBodyId.Invalid, PhysicsBodyId.Invalid,
                AnchorOnA(_anchorA), AnchorOnB(_anchorB),
                DirectionOnA(_twistAxisA), DirectionOnB(_twistAxisB), DirectionOnA(_planeAxisA), DirectionOnB(_planeAxisB),
                _normalSwing * Mathf.Deg2Rad, _twistMin * Mathf.Deg2Rad, _twistMax * Mathf.Deg2Rad,
                _planeSwing * Mathf.Deg2Rad, _maxFriction, _pyramidSwing);
            return true;
        }

        protected override void OnJointCreated(AuraSimulationInstance instance)
        {
            if (!_twistMotor)
                return;

            var motor = AuraJointMotorDefinition.Velocity(_twistMotorVelocity * Mathf.Deg2Rad, _twistMotorMaxTorque);
            var result = SetAxisMotor(0, motor);
            if (result != AuraResult.Success)
                Debug.LogError($"{nameof(AuraSwingTwistJointAuthoring)} on '{name}' could not start the twist motor: {result}.", this);
        }
    }
}
