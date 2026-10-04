using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* Thin bridge for a Jolt 3D pulley: |bodyA - fixedA| + ratio * |bodyB - fixedB| stays within min..max length.
       The two fixed points are scene Transforms (the pulley wheels). Equal min and max is a rigid rope; a min of 0 is a
       rope that can go slack. A zero max length uses the length at creation as a rigid rope. */
    [DisallowMultipleComponent]
    public sealed class AuraPulleyJointAuthoring : AuraJointAuthoringBase
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rope")]
#endif
        [SerializeField]
        private Vector3 _anchorA;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rope")]
#endif
        [SerializeField]
        private Vector3 _anchorB;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rope")]
#endif
        [SerializeField]
        private Transform _fixedPointA;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rope")]
#endif
        [SerializeField]
        private Transform _fixedPointB;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rope")]
#endif
        [Tooltip("Scales segment B: A length + ratio * B length stays constant.")]
        [SerializeField]
        [Min(0.001f)]
        private float _ratio = 1f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rope")]
#endif
        [SerializeField]
        [Min(0f)]
        private float _minLength;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Rope")]
#endif
        [Tooltip("0 keeps the rope length at creation.")]
        [SerializeField]
        [Min(0f)]
        private float _maxLength;

        protected override bool TryCreateDefinition(AuraSimulationInstance instance, out AuraJointDefinition definition)
        {
            definition = default;
            if (_fixedPointA == null || _fixedPointB == null)
            {
                Debug.LogError($"{nameof(AuraPulleyJointAuthoring)} on '{name}' is missing a fixed point reference.", this);
                return false;
            }

            var anchorA = AnchorOnA(_anchorA);
            var anchorB = AnchorOnB(_anchorB);
            var fixedA = ToAura(_fixedPointA.position);
            var fixedB = ToAura(_fixedPointB.position);
            var maxLength = _maxLength;
            var minLength = _minLength;
            if (maxLength <= 0f)
            {
                maxLength = (anchorA - fixedA).Length + _ratio * (anchorB - fixedB).Length;
                minLength = maxLength;
            }

            definition = AuraJointDefinition.CreatePulley(PhysicsBodyId.Invalid, PhysicsBodyId.Invalid, anchorA, anchorB, fixedA, fixedB, _ratio, minLength, maxLength);
            return true;
        }
    }
}
