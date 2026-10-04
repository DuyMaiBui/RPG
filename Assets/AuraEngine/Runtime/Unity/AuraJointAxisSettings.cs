using System;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* Serialized settings of one joint axis: freedom, range, friction and an optional start-up motor.
       Translation values are metres; rotation values are degrees (converted to radians for the kernel). */
    [Serializable]
    public struct AuraJointAxisSettings
    {
        [SerializeField]
        private AuraJointAxisMode _mode;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ShowIf("@_mode == AuraEngine.Core.AuraJointAxisMode.Limited")]
#endif
        [SerializeField]
        private float _min;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ShowIf("@_mode == AuraEngine.Core.AuraJointAxisMode.Limited")]
#endif
        [SerializeField]
        private float _max;

        [Tooltip("Friction force (N) or torque (N*m) while no motor drives the axis. 0 = none.")]
        [SerializeField]
        [Min(0f)]
        private float _maxFriction;

        [SerializeField]
        private AuraJointMotorMode _motorMode;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ShowIf("@_motorMode != AuraEngine.Core.AuraJointMotorMode.Off")]
#endif
        [Tooltip("Velocity mode: m/s or deg/s. Position mode: m or degrees relative to the creation pose.")]
        [SerializeField]
        private float _motorTarget;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ShowIf("@_motorMode != AuraEngine.Core.AuraJointMotorMode.Off")]
#endif
        [Tooltip("Maximum motor force (N) or torque (N*m). Must be positive while the motor is on.")]
        [SerializeField]
        [Min(0f)]
        private float _motorMaxForce;

        public AuraJointAxisMode Mode => _mode;

        public AuraJointMotorMode MotorMode => _motorMode;

        public AuraJointAxisLimit ToLimit(bool rotation)
        {
            var scale = rotation ? Mathf.Deg2Rad : 1f;
            return new AuraJointAxisLimit(_mode, _min * scale, _max * scale, _maxFriction);
        }

        public AuraJointMotorDefinition ToMotor(bool rotation)
        {
            var scale = rotation ? Mathf.Deg2Rad : 1f;
            return new AuraJointMotorDefinition(_motorMode, _motorTarget * scale, _motorMaxForce);
        }
    }
}
