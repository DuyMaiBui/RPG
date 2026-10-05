using AuraEngine.Core;
using UnityEngine;

namespace AuraEngine.Unity
{
    /* XY-plane counterpart of AuraCharacterAuthoring for the Plane2D backend: a
       kinematic capsule mover with slope limit, step height, jumping, coyote time
       and a jump buffer. Drive it through DesiredVelocityX and RequestJump. */
    [DisallowMultipleComponent]
    public sealed class AuraCharacter2DAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Min(0.02f)]
        private float _width = 0.8f;

        [SerializeField]
        [Min(0.04f)]
        private float _height = 1.8f;

        [SerializeField]
        [Min(0f)]
        private float _mass = 70f;

        [SerializeField]
        [Range(0f, 89f)]
        private float _maxSlopeAngle = 50f;

        [SerializeField]
        [Min(0f)]
        private float _stepHeight = 0.3f;

        [SerializeField]
        [Min(0f)]
        private float _jumpSpeed = 8f;

        [SerializeField]
        [Min(0f)]
        private float _coyoteTime = 0.1f;

        [SerializeField]
        [Min(0f)]
        private float _jumpBufferTime = 0.1f;

        [SerializeField]
        [AuraLayer]
        private int _layer;

        [SerializeField]
        private bool _syncTransform = true;

        private AuraSimulationInstance _instance;
        private AuraCharacterId _character = AuraCharacterId.Invalid;
        private AuraJumpAssist _jumpAssist;
        private float _desiredVelocityX;
        private bool _jumpRequested;

        public AuraCharacterId CharacterId => _character;

        public bool IsGrounded { get; private set; }

        public AuraVector3 Velocity { get; private set; }

        public float JumpSpeed
        {
            get => _jumpSpeed;
            set => _jumpSpeed = Mathf.Max(0f, value);
        }

        /* Horizontal velocity in m/s applied on the next simulation tick. */
        public float DesiredVelocityX
        {
            get => _desiredVelocityX;
            set => _desiredVelocityX = value;
        }

        /* Remembers a jump press until a tick consumes it (or the buffer window expires). */
        public void RequestJump() => _jumpRequested = true;

        private void OnEnable()
        {
            _jumpAssist = new AuraJumpAssist(_coyoteTime, _jumpBufferTime);
            _instance = GetComponentInParent<AuraSimulationInstance>();
            if (_instance == null)
            {
                Debug.LogError($"{nameof(AuraCharacter2DAuthoring)} requires an {nameof(AuraSimulationInstance)} in its parent hierarchy.", this);
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
            if (instance.World.Definition.Mode != AuraPhysicsMode.Plane2D)
            {
                Debug.LogError($"{nameof(AuraCharacter2DAuthoring)} on '{name}' requires Plane2D mode.", this);
                return;
            }

            if ((instance.World.Capabilities & AuraPhysicsCapabilities.Characters) == 0)
            {
                Debug.LogError($"{nameof(AuraCharacter2DAuthoring)} on '{name}' needs a backend that supports characters (Plane2D).", this);
                return;
            }

            var layers = instance.Layers;
            var layer = layers != null && layers.HasLayer(_layer) ? _layer : 0;
            var height = Mathf.Max(_height, 0.04f);
            var radius = Mathf.Min(Mathf.Max(_width, 0.02f) * 0.5f, height * 0.5f);
            var definition = new AuraCharacterDefinition(
                transform.ToAuraPose(),
                radius,
                height,
                new AuraPhysicsLayer(Mathf.Clamp(layer, 0, AuraPhysicsLayer.MaxLayers - 1)),
                AuraPhysicsLayerMask.All,
                _mass,
                _maxSlopeAngle * Mathf.Deg2Rad,
                _stepHeight);
            _jumpAssist = new AuraJumpAssist(_coyoteTime, _jumpBufferTime);
            _character = instance.AttachCharacter(definition);
            if (!_character.IsValid)
                Debug.LogError($"{nameof(AuraCharacter2DAuthoring)} on '{name}' failed to create a character.", this);
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
            if (_instance == null || !_character.IsValid || deltaTime <= 0f)
                return;

            var jump = _jumpAssist.Update(deltaTime, IsGrounded, _jumpRequested);
            _jumpRequested = false;

            // A positive vertical translation is the explicit jump command; otherwise the kernel owns gravity.
            var translation = new AuraVector3(_desiredVelocityX * deltaTime, jump ? _jumpSpeed * deltaTime : 0f, 0f);
            _instance.MoveCharacter(_character, translation, deltaTime);

            if (!_instance.TryGetCharacterState(_character, out var state))
                return;

            IsGrounded = state.IsGrounded;
            Velocity = state.Velocity;
            if (_syncTransform)
                transform.position = new Vector3(state.Position.X, state.Position.Y, transform.position.z);
        }
    }
}
