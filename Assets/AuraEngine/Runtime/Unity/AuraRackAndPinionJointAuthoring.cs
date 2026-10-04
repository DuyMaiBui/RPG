using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* Thin bridge for a Jolt 3D rack and pinion. Body A is the pinion (held by a Hinge AuraJointAuthoring), body B the
       rack (held by a Slider AuraJointAuthoring); both are referenced below and built first. Moving the rack by one
       rack length turns the pinion by teethRack / teethPinion revolutions. Axes are body local.
       Removing either referenced joint dissolves the coupling (it then reports broken). */
    [DisallowMultipleComponent]
    public sealed class AuraRackAndPinionJointAuthoring : AuraJointAuthoringBase
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Joints")]
#endif
        [SerializeField]
        private AuraJointAuthoring _pinionHinge;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Joints")]
#endif
        [SerializeField]
        private AuraJointAuthoring _rackSlider;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Coupling")]
#endif
        [SerializeField]
        private Vector3 _hingeAxis = Vector3.forward;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Coupling")]
#endif
        [SerializeField]
        private Vector3 _sliderAxis = Vector3.right;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Coupling")]
#endif
        [SerializeField]
        [Min(1)]
        private int _rackTeeth = 20;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Coupling")]
#endif
        [Tooltip("Length of the toothed part of the rack in metres.")]
        [SerializeField]
        [Min(0.01f)]
        private float _rackLength = 2f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Coupling")]
#endif
        [SerializeField]
        [Min(1)]
        private int _pinionTeeth = 10;

        protected override bool TryCreateDefinition(AuraSimulationInstance instance, out AuraJointDefinition definition)
        {
            definition = default;
            if (!TryBuild(_pinionHinge, AuraJointType.Hinge, nameof(_pinionHinge), instance) || !TryBuild(_rackSlider, AuraJointType.Slider, nameof(_rackSlider), instance))
                return false;

            var radiansPerMetre = 2f * Mathf.PI * _rackTeeth / (_rackLength * _pinionTeeth);
            definition = AuraJointDefinition.CreateRackAndPinion(
                PhysicsBodyId.Invalid, PhysicsBodyId.Invalid,
                DirectionOnA(_hingeAxis), DirectionOnB(_sliderAxis),
                radiansPerMetre, _pinionHinge.JointId, _rackSlider.JointId);
            return true;
        }

        private bool TryBuild(AuraJointAuthoring joint, AuraJointType expected, string field, AuraSimulationInstance instance)
        {
            if (joint == null || joint.JointType != expected)
            {
                Debug.LogError($"{nameof(AuraRackAndPinionJointAuthoring)} on '{name}' needs {field} to reference a {expected} joint.", this);
                return false;
            }

            joint.BuildInto(instance);
            if (!joint.JointId.IsValid)
            {
                Debug.LogError($"{nameof(AuraRackAndPinionJointAuthoring)} on '{name}': the joint referenced by {field} could not be built.", this);
                return false;
            }

            return true;
        }
    }
}
