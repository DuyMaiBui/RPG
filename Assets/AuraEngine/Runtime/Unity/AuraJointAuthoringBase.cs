using System;
using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* Shared thin bridge for 3D joint authoring: the two body references, registration with the owning
       AuraSimulationInstance and the lifetime of the kernel joint. Subclasses only translate their serialized
       fields into an AuraJointDefinition. Building is idempotent so a joint that couples other joints (gear, rack and
       pinion) can build the joints it references first. Missing references and invalid values are authoring errors:
       they are logged and the joint is not created. */
    public abstract class AuraJointAuthoringBase : MonoBehaviour
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Bodies")]
#endif
        [SerializeField]
        private AuraPhysicsBodyAuthoring _bodyA;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Bodies")]
#endif
        [SerializeField]
        private AuraPhysicsBodyAuthoring _bodyB;

        private AuraSimulationInstance _instance;
        private AuraJointId _joint = AuraJointId.Invalid;

        public AuraJointId JointId => _joint;

        protected AuraPhysicsBodyAuthoring BodyA => _bodyA;

        protected AuraPhysicsBodyAuthoring BodyB => _bodyB;

        protected AuraSimulationInstance Instance => _instance;

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{GetType().Name} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);

            _instance = null;
            _joint = AuraJointId.Invalid;
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            if (_joint.IsValid)
                return;

            if (_bodyA == null || _bodyB == null)
            {
                Debug.LogError($"{GetType().Name} on '{name}' is missing a body reference.", this);
                return;
            }

            if (_bodyA.EntityId.IsNone || _bodyB.EntityId.IsNone)
            {
                Debug.LogError($"{GetType().Name} on '{name}' references a body that has no entity.", this);
                return;
            }

            AuraJointDefinition definition;
            try
            {
                if (!TryCreateDefinition(instance, out definition))
                    return;
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"{GetType().Name} on '{name}' has an invalid setting: {exception.Message}", this);
                return;
            }

            _joint = instance.AttachJoint(_bodyA.EntityId, _bodyB.EntityId, definition);
            if (!_joint.IsValid)
            {
                Debug.LogError($"{GetType().Name} on '{name}' failed to create a {definition.Type} joint.", this);
                return;
            }

            OnJointCreated(instance);
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (!_joint.IsValid)
                return;

            instance.DetachJoint(_joint);
            _joint = AuraJointId.Invalid;
        }

        /* Return false after logging when a reference is missing; throw ArgumentException for invalid values. */
        protected abstract bool TryCreateDefinition(AuraSimulationInstance instance, out AuraJointDefinition definition);

        protected virtual void OnJointCreated(AuraSimulationInstance instance)
        {
        }

        protected static AuraVector3 ToAura(Vector3 value) => new AuraVector3(value.x, value.y, value.z);

        protected AuraVector3 AnchorOnA(Vector3 local) => ToAura(_bodyA.transform.TransformPoint(local));

        protected AuraVector3 AnchorOnB(Vector3 local) => ToAura(_bodyB.transform.TransformPoint(local));

        protected AuraVector3 DirectionOnA(Vector3 local) => ToAura(_bodyA.transform.TransformDirection(local));

        protected AuraVector3 DirectionOnB(Vector3 local) => ToAura(_bodyB.transform.TransformDirection(local));
    }
}
