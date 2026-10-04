using System.Collections.Generic;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraRagdollChainAuthoring : MonoBehaviour
    {
        [Header("Limbs")]
        [SerializeField]
        private AuraPhysicsBodyAuthoring[] _limbs;

        [Header("Hinge")]
        [SerializeField]
        private Vector3 _hingeAxis = Vector3.forward;

        [SerializeField]
        private float _motorTargetVelocity;

        [SerializeField]
        private float _maxMotorForce;

        private AuraSimulationInstance _instance;
        private readonly List<AuraJointId> _joints = new List<AuraJointId>();

        public IReadOnlyList<AuraJointId> Joints => _joints;

        public int LimbCount => _limbs != null ? _limbs.Length : 0;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraRagdollChainAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);

            _instance = null;
            _joints.Clear();
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            if ((instance.World.Capabilities & AuraPhysicsCapabilities.Joints) == 0)
            {
                Debug.LogError($"{nameof(AuraRagdollChainAuthoring)} on '{name}' requires a world with {AuraPhysicsCapabilities.Joints} capability.", this);
                return;
            }

            if (_limbs == null || _limbs.Length < 2)
            {
                Debug.LogError($"{nameof(AuraRagdollChainAuthoring)} on '{name}' requires at least 2 limbs.", this);
                return;
            }

            bool motorEnabled = !Mathf.Approximately(_motorTargetVelocity, 0f);

            for (int i = 0; i < _limbs.Length - 1; i++)
            {
                AuraPhysicsBodyAuthoring limbA = _limbs[i];
                AuraPhysicsBodyAuthoring limbB = _limbs[i + 1];

                if (limbA == null || limbB == null)
                {
                    Debug.LogError($"{nameof(AuraRagdollChainAuthoring)} on '{name}' has a missing limb reference at pair ({i}, {i + 1}).", this);
                    continue;
                }

                if (limbA.EntityId.IsNone || limbB.EntityId.IsNone)
                {
                    Debug.LogError($"{nameof(AuraRagdollChainAuthoring)} on '{name}' references a limb with no entity at pair ({i}, {i + 1}).", this);
                    continue;
                }

                Vector3 midpoint = (limbA.transform.position + limbB.transform.position) * 0.5f;
                var anchor = ToAura(midpoint);
                var axisA = ToAura(limbA.transform.TransformDirection(_hingeAxis));
                var axisB = ToAura(limbB.transform.TransformDirection(_hingeAxis));

                var definition = new AuraJointDefinition(
                    AuraJointType.Hinge,
                    PhysicsBodyId.Invalid,
                    PhysicsBodyId.Invalid,
                    anchor,
                    anchor,
                    0f,
                    axisA,
                    axisB,
                    false,
                    0f,
                    0f,
                    AuraVector3.UnitZ,
                    AuraVector3.UnitZ,
                    0f,
                    motorEnabled,
                    _motorTargetVelocity,
                    _maxMotorForce,
                    0f,
                    0f);

                AuraJointId joint = instance.AttachJoint(limbA.EntityId, limbB.EntityId, definition);
                if (!joint.IsValid)
                {
                    Debug.LogError($"{nameof(AuraRagdollChainAuthoring)} on '{name}' failed to create a hinge joint for limb pair ({i}, {i + 1}).", this);
                    continue;
                }

                _joints.Add(joint);
            }

            // No rollback of already-created joints on partial failure: joints are
            // simulation-owned and released with the world; ReleaseFrom detaches all
            // stored joints when this authoring is unregistered.
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            for (int i = 0; i < _joints.Count; i++)
                instance.DetachJoint(_joints[i]);

            _joints.Clear();
        }

        private static AuraVector3 ToAura(Vector3 value) => new AuraVector3(value.x, value.y, value.z);
    }
}
