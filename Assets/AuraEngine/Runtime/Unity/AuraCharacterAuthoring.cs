using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraCharacterAuthoring : MonoBehaviour
    {
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Capsule")]
#endif
        [SerializeField]
        [Min(0.01f)]
        private float _radius = 0.4f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Capsule")]
#endif
        [SerializeField]
        [Min(0.01f)]
        private float _height = 1.8f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Capsule")]
#endif
        [SerializeField]
        [Min(0f)]
        private float _mass = 70f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Movement")]
#endif
        [SerializeField]
        [Range(0f, 89f)]
        private float _maxSlopeAngle = 50f;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.BoxGroup("Movement")]
#endif
        [SerializeField]
        private bool _syncTransform = true;

        private AuraSimulationInstance _instance;
        private AuraCharacterId _character = AuraCharacterId.Invalid;
        private AuraVector3 _desiredVelocity;

        public AuraCharacterId CharacterId => _character;

        public bool IsGrounded { get; private set; }

        public AuraVector3 DesiredVelocity
        {
            get => _desiredVelocity;
            set => _desiredVelocity = value;
        }

        private void OnEnable()
        {
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraCharacterAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
                return;
            }

            _instance.Register(this);
        }

        private void OnDisable()
        {
            if (_instance != null)
                _instance.Unregister(this);

            _instance = null;
            _character = AuraCharacterId.Invalid;
        }

        public void BuildInto(AuraSimulationInstance instance)
        {
            if (instance.World.Definition.Mode != AuraPhysicsMode.Full3D)
            {
                Debug.LogError($"{nameof(AuraCharacterAuthoring)} on '{name}' requires Full3D mode.", this);
                return;
            }

            if ((instance.World.Capabilities & AuraPhysicsCapabilities.Characters) == 0)
            {
                Debug.LogError($"{nameof(AuraCharacterAuthoring)} on '{name}' needs a backend that supports characters.", this);
                return;
            }

            var definition = new AuraCharacterDefinition(
                transform.ToAuraPose(),
                _radius,
                _height,
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                _mass,
                _maxSlopeAngle * Mathf.Deg2Rad);
            _character = instance.AttachCharacter(definition);
            if (!_character.IsValid)
                Debug.LogError($"{nameof(AuraCharacterAuthoring)} on '{name}' failed to create a character.", this);
        }

        public void ReleaseFrom(AuraSimulationInstance instance)
        {
            if (!_character.IsValid)
                return;

            instance.DetachCharacter(_character);
            _character = AuraCharacterId.Invalid;
        }

        public void Tick(float deltaTime)
        {
            if (_instance == null || !_character.IsValid)
                return;

            var translation = new AuraVector3(
                _desiredVelocity.X * deltaTime,
                _desiredVelocity.Y * deltaTime,
                _desiredVelocity.Z * deltaTime);
            _instance.MoveCharacter(_character, translation, deltaTime);

            if (_instance.TryGetCharacterState(_character, out var state))
            {
                IsGrounded = state.IsGrounded;
                if (_syncTransform)
                    transform.position = new Vector3(state.Position.X, state.Position.Y, state.Position.Z);
            }
        }
    }
}
