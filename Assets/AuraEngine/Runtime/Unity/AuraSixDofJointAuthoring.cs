using AuraEngine.Core;
using AuraEngine.Physics;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* Thin bridge for a Jolt 3D six degree of freedom joint. Each axis is Locked, Free or Limited; axes are the joint
       frame X (axis) and Y (normal) on each body, given in body local space. Rotation values are degrees. For
       rotation Y/Z a Limited range is a swing cone (only Max, the half angle, is used) unless pyramid swing is on.
       Start-up motors are applied after creation; drive them at runtime with SetAxisMotor / SetAxisLimits. */
    [DisallowMultipleComponent]
    public sealed class AuraSixDofJointAuthoring : AuraJointAuthoringBase
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
        private Vector3 _axisA = Vector3.right;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private Vector3 _normalA = Vector3.up;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private Vector3 _axisB = Vector3.right;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private Vector3 _normalB = Vector3.up;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Frame")]
#endif
        [SerializeField]
        private bool _pyramidSwing;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Translation (m)")]
#endif
        [SerializeField]
        private AuraJointAxisSettings _translationX;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Translation (m)")]
#endif
        [SerializeField]
        private AuraJointAxisSettings _translationY;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Translation (m)")]
#endif
        [SerializeField]
        private AuraJointAxisSettings _translationZ;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rotation (deg)")]
#endif
        [SerializeField]
        private AuraJointAxisSettings _rotationX;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rotation (deg)")]
#endif
        [SerializeField]
        private AuraJointAxisSettings _rotationY;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rotation (deg)")]
#endif
        [SerializeField]
        private AuraJointAxisSettings _rotationZ;

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
            var limits = new AuraSixDofLimits(
                _translationX.ToLimit(false), _translationY.ToLimit(false), _translationZ.ToLimit(false),
                _rotationX.ToLimit(true), _rotationY.ToLimit(true), _rotationZ.ToLimit(true));
            definition = AuraJointDefinition.CreateSixDof(
                PhysicsBodyId.Invalid, PhysicsBodyId.Invalid,
                AnchorOnA(_anchorA), AnchorOnB(_anchorB), limits,
                DirectionOnA(_axisA), DirectionOnA(_normalA), DirectionOnB(_axisB), DirectionOnB(_normalB), _pyramidSwing);
            return true;
        }

        protected override void OnJointCreated(AuraSimulationInstance instance)
        {
            var axes = new[] { _translationX, _translationY, _translationZ, _rotationX, _rotationY, _rotationZ };
            for (var axis = 0; axis < axes.Length; axis++)
            {
                if (axes[axis].MotorMode == AuraJointMotorMode.Off)
                    continue;

                var result = SetAxisMotor(axis, axes[axis].ToMotor(axis >= 3));
                if (result != AuraResult.Success)
                    Debug.LogError($"{nameof(AuraSixDofJointAuthoring)} on '{name}' could not start the motor on axis {axis}: {result}.", this);
            }
        }
    }
}
