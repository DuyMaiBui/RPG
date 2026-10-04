using System.Collections.Generic;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraRagdollChain2DAuthoring : MonoBehaviour
    {
        [SerializeField] private AuraPhysicsBody2DAuthoring[] _limbs = new AuraPhysicsBody2DAuthoring[0];
        [SerializeField] private Vector2 _hingeAxis = Vector2.right;
        [SerializeField] private bool _motorEnabled;
        [SerializeField] private float _motorTargetVelocity;
        [SerializeField] private float _maxMotorForce = 1f;

        private AuraSimulationInstance _instance;
        private readonly List<AuraJointId> _joints = new List<AuraJointId>();

        public IReadOnlyList<AuraJointId> Joints => _joints;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraRagdollChain2DAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
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
            if (instance.World.Definition.Mode != AuraPhysicsMode.Plane2D ||
                (instance.World.Capabilities & AuraPhysicsCapabilities.Joints) == 0)
            {
                Debug.LogError($"{nameof(AuraRagdollChain2DAuthoring)} on '{name}' requires Plane2D joint support.", this);
                return;
            }

            if (_limbs == null || _limbs.Length < 2)
            {
                Debug.LogError($"{nameof(AuraRagdollChain2DAuthoring)} on '{name}' requires at least two 2D limbs.", this);
                return;
            }

            for (var index = 0; index < _limbs.Length - 1; index++)
            {
                var a = _limbs[index];
                var b = _limbs[index + 1];
                if (a == null || b == null || a.EntityId.IsNone || b.EntityId.IsNone)
                {
                    Debug.LogError($"{nameof(AuraRagdollChain2DAuthoring)} on '{name}' has an unregistered limb pair at {index}.", this);
                    continue;
                }

                var midpoint = (a.transform.position + b.transform.position) * 0.5f;
                var worldAxis = a.transform.TransformDirection(new Vector3(_hingeAxis.x, _hingeAxis.y, 0f));
                var axis = new AuraVector3(worldAxis.x, worldAxis.y, 0f);
                var anchor = new AuraVector3(midpoint.x, midpoint.y, 0f);
                var definition = new AuraJointDefinition(
                    AuraJointType.Hinge,
                    PhysicsBodyId.Invalid,
                    PhysicsBodyId.Invalid,
                    anchor,
                    anchor,
                    axisA: axis,
                    axisB: axis,
                    motorEnabled: _motorEnabled,
                    motorTargetVelocity: _motorTargetVelocity,
                    maxMotorForce: _maxMotorForce);
                var joint = instance.AttachJoint(a.EntityId, b.EntityId, definition);
                if (joint.IsValid)
                    _joints.Add(joint);
                else
                    Debug.LogError($"{nameof(AuraRagdollChain2DAuthoring)} on '{name}' failed to create hinge at pair {index}.", this);
            }
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            for (var index = 0; index < _joints.Count; index++)
                instance.DetachJoint(_joints[index]);
            _joints.Clear();
        }
    }
}
