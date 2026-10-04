using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* Thin bridge for a Jolt 3D gear pair. Body A and body B are the two gears; each is already held by its own
       Hinge AuraJointAuthoring (usually to a static body), referenced below. The gear couples them as
       angle(A) = -(teethB / teethA) * angle(B), so B turns the opposite way at teethA / teethB of A's speed. Hinge axes are body local. The referenced hinges are built first.
       Removing either hinge dissolves the gear (it then reports broken). */
    [DisallowMultipleComponent]
    public sealed class AuraGearJointAuthoring : AuraJointAuthoringBase
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Hinges")]
#endif
        [SerializeField]
        private AuraJointAuthoring _hingeA;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Hinges")]
#endif
        [SerializeField]
        private AuraJointAuthoring _hingeB;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Gear")]
#endif
        [SerializeField]
        private Vector3 _hingeAxisA = Vector3.forward;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Gear")]
#endif
        [SerializeField]
        private Vector3 _hingeAxisB = Vector3.forward;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Gear")]
#endif
        [SerializeField]
        [Min(1)]
        private int _teethA = 10;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Gear")]
#endif
        [SerializeField]
        [Min(1)]
        private int _teethB = 10;

        protected override bool TryCreateDefinition(AuraSimulationInstance instance, out AuraJointDefinition definition)
        {
            definition = default;
            if (!TryBuild(_hingeA, AuraJointType.Hinge, nameof(_hingeA), instance) || !TryBuild(_hingeB, AuraJointType.Hinge, nameof(_hingeB), instance))
                return false;

            definition = AuraJointDefinition.CreateGear(
                PhysicsBodyId.Invalid, PhysicsBodyId.Invalid,
                DirectionOnA(_hingeAxisA), DirectionOnB(_hingeAxisB),
                (float)_teethB / _teethA, _hingeA.JointId, _hingeB.JointId);
            return true;
        }

        private bool TryBuild(AuraJointAuthoring joint, AuraJointType expected, string field, AuraSimulationInstance instance)
        {
            if (joint == null || joint.JointType != expected)
            {
                Debug.LogError($"{nameof(AuraGearJointAuthoring)} on '{name}' needs {field} to reference a {expected} joint.", this);
                return false;
            }

            joint.BuildInto(instance);
            if (!joint.JointId.IsValid)
            {
                Debug.LogError($"{nameof(AuraGearJointAuthoring)} on '{name}': the joint referenced by {field} could not be built.", this);
                return false;
            }

            return true;
        }
    }
}
