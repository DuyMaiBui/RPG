using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraRagdollAuthoring : MonoBehaviour
    {
        [Header("Limbs (one collider authoring per limb, no body authoring)")]
        [SerializeField]
        private AuraColliderAuthoring[] _limbs = new AuraColliderAuthoring[0];

        [Header("Rig")]
        [SerializeField]
        private int[] _parentIndices = new int[0];

        [SerializeField]
        [Min(0.01f)]
        private float _mass = 1f;

        private AuraSimulationInstance _instance;
        private AuraRagdollId _ragdoll = AuraRagdollId.Invalid;

        public AuraRagdollId RagdollId => _ragdoll;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraRagdollAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);

            _instance = null;
            _ragdoll = AuraRagdollId.Invalid;
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            if ((instance.World.Capabilities & AuraPhysicsCapabilities.Ragdolls) == 0)
            {
                Debug.LogError($"{nameof(AuraRagdollAuthoring)} on '{name}' needs a backend that supports ragdolls.", this);
                return;
            }

            if (_limbs == null || _limbs.Length == 0 || _parentIndices == null || _parentIndices.Length != _limbs.Length)
            {
                Debug.LogError($"{nameof(AuraRagdollAuthoring)} on '{name}' needs one parent index per limb.", this);
                return;
            }

            var bodies = new AuraPhysicsBodyDefinition[_limbs.Length];
            var joints = new AuraJointDefinition[_limbs.Length];
            var bindPoses = new AuraPose[_limbs.Length];
            for (var index = 0; index < _limbs.Length; index++)
            {
                if (_limbs[index] == null)
                {
                    Debug.LogError($"{nameof(AuraRagdollAuthoring)} on '{name}' has a missing limb at index {index}.", this);
                    return;
                }

                var shape = _limbs[index].BuildShape();
                if (shape.Validate() != AuraResult.Success)
                {
                    Debug.LogError($"{nameof(AuraRagdollAuthoring)} on '{name}' has an invalid limb shape at index {index}.", this);
                    return;
                }

                var limbPose = _limbs[index].transform.ToAuraPose();
                bindPoses[index] = limbPose;
                bodies[index] = AuraPhysicsBodyDefinition.CreateDynamic(
                    limbPose, shape.Layer, AuraPhysicsLayerMask.All, shape);

                var anchor = limbPose.Position;
                if (_parentIndices[index] >= 0 && _parentIndices[index] < _limbs.Length && _limbs[_parentIndices[index]] != null)
                {
                    var parentPose = _limbs[_parentIndices[index]].transform.ToAuraPose().Position;
                    anchor = new AuraVector3(
                        (limbPose.Position.X + parentPose.X) * 0.5f,
                        (limbPose.Position.Y + parentPose.Y) * 0.5f,
                        (limbPose.Position.Z + parentPose.Z) * 0.5f);
                }

                joints[index] = new AuraJointDefinition(
                    AuraJointType.Fixed,
                    PhysicsBodyId.Invalid,
                    PhysicsBodyId.Invalid,
                    anchor,
                    anchor,
                    0f,
                    AuraVector3.UnitY,
                    AuraVector3.UnitY,
                    false, 0f, 0f,
                    AuraVector3.UnitZ,
                    AuraVector3.UnitZ,
                    0f, false, 0f, 0f, 0f, 0f);
            }

            var definition = new AuraRagdollDefinition(new AuraRigDefinition(_parentIndices, bindPoses), bodies, joints);
            _ragdoll = instance.AttachRagdoll(definition);
            if (!_ragdoll.IsValid)
                Debug.LogError($"{nameof(AuraRagdollAuthoring)} on '{name}' failed to create a ragdoll.", this);
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (!_ragdoll.IsValid)
                return;

            instance.DetachRagdoll(_ragdoll);
            _ragdoll = AuraRagdollId.Invalid;
        }
    }
}
