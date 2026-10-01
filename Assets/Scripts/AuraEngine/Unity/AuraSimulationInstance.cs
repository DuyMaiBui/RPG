using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using UnityEngine;

namespace AuraEngine.Unity
{
    [DisallowMultipleComponent]
    public sealed class AuraSimulationInstance : MonoBehaviour
    {
        [Header("Simulation")]
        [SerializeField]
        private bool _autoTick = true;

        [SerializeField]
        private int _tickRate = 60;

        [SerializeField]
        private int _initialBodyCapacity = 256;

        [SerializeField]
        private AuraPhysicsMode _mode = AuraPhysicsMode.Full3D;

        [SerializeField]
        private Vector3 _gravity = new Vector3(0f, -9.81f, 0f);

        [SerializeField]
        private bool _autoCreateOnStart = true;

        [SerializeField]
        private AuraBackendKind _backendKind = AuraBackendKind.Managed;

        [SerializeField]
        private AuraPhysicsLayers _layers;

        private readonly List<AuraPhysicsBodyAuthoring> _authoring = new List<AuraPhysicsBodyAuthoring>();
        private readonly List<AuraJointAuthoring> _jointAuthoring = new List<AuraJointAuthoring>();
        private readonly List<AuraCharacterAuthoring> _characterAuthoring = new List<AuraCharacterAuthoring>();
        private AuraSimulationWorld _world;
        private AuraViewRegistry _registry;
        private AuraEventDispatcher _dispatcher;
        private IPhysicsBackend _backend;
        private string _backendName = "none";
        private uint _tick;

        public AuraSimulationWorld World => _world;

        public AuraViewRegistry Registry => _registry;

        public bool IsCreated => _world != null;

        public string BackendName => _backendName;

        public AuraPhysicsLayers Layers => _layers;

        public void SetPhysicsBackend(IPhysicsBackend backend)
        {
            if (_world != null)
                throw new InvalidOperationException("The backend must be assigned before the simulation world is created.");

            _backend = backend;
        }

        public void CreateWorld()
        {
            if (_world != null)
                return;

            _registry = new AuraViewRegistry();
            _dispatcher = new AuraEventDispatcher();
            var definition = new AuraWorldDefinition(
                _mode,
                new AuraVector3(_gravity.x, _gravity.y, _gravity.z),
                collisionMatrix: _layers != null ? _layers.ToCollisionMatrix() : null,
                initialBodyCapacity: _initialBodyCapacity,
                fixedDeltaTime: 1f / Mathf.Max(1, _tickRate));
            IPhysicsBackend backend = _backend;
            if (backend == null)
            {
                if (_backendKind == AuraBackendKind.Native && AuraEngine.Physics.Native.NativePhysicsBackend.IsAvailable())
                    backend = new AuraEngine.Physics.Native.NativePhysicsBackend();
                else
                    backend = ManagedPhysicsBackend.Instance;
            }

            _backendName = backend.Name;
            _world = new AuraSimulationWorld(backend, definition);

            for (var index = 0; index < _authoring.Count; index++)
                _authoring[index].BuildInto(this);

            if ((_world.Capabilities & AuraPhysicsCapabilities.Joints) != 0)
            {
                for (var index = 0; index < _jointAuthoring.Count; index++)
                    _jointAuthoring[index].BuildInto(this);
            }

            if ((_world.Capabilities & AuraPhysicsCapabilities.Characters) != 0)
            {
                for (var index = 0; index < _characterAuthoring.Count; index++)
                    _characterAuthoring[index].BuildInto(this);
            }

            _tick = 0;
        }

        public bool TryGetBodyState(SimulationEntityId entity, out AuraBodyState state)
        {
            if (_world == null)
            {
                state = default;
                return false;
            }

            return _world.TryGetBodyState(entity, out state);
        }

        public void Register(AuraPhysicsBodyAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_authoring.Contains(authoring))
                return;

            _authoring.Add(authoring);
            if (_world != null)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraPhysicsBodyAuthoring authoring)
        {
            if (authoring == null)
                return;

            _authoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraJointAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_jointAuthoring.Contains(authoring))
                return;

            _jointAuthoring.Add(authoring);
            if (_world != null && (_world.Capabilities & AuraPhysicsCapabilities.Joints) != 0)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraJointAuthoring authoring)
        {
            if (authoring == null)
                return;

            _jointAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraCharacterAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_characterAuthoring.Contains(authoring))
                return;

            _characterAuthoring.Add(authoring);
            if (_world != null && (_world.Capabilities & AuraPhysicsCapabilities.Characters) != 0)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraCharacterAuthoring authoring)
        {
            if (authoring == null)
                return;

            _characterAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public AuraCharacterId AttachCharacter(in AuraCharacterDefinition definition)
        {
            if (_world == null)
                throw new InvalidOperationException("The simulation world has not been created.");

            return _world.CreateCharacter(definition);
        }

        public AuraResult DetachCharacter(AuraCharacterId character)
        {
            if (_world == null)
                return AuraResult.InvalidWorld;

            return _world.DestroyCharacter(character);
        }

        public bool TryGetCharacterState(AuraCharacterId character, out AuraCharacterState state)
        {
            if (_world == null)
            {
                state = default;
                return false;
            }

            return _world.TryGetCharacterState(character, out state);
        }

        public void MoveCharacter(AuraCharacterId character, AuraVector3 desiredTranslation, float deltaTime)
        {
            if (_world == null)
                return;

            _world.MoveCharacter(character, desiredTranslation, deltaTime);
        }

        public AuraJointId AttachJoint(SimulationEntityId a, SimulationEntityId b, in AuraJointDefinition definition)
        {
            if (_world == null)
                throw new InvalidOperationException("The simulation world has not been created.");

            return _world.CreateJoint(a, b, definition);
        }

        public AuraResult DetachJoint(AuraJointId joint)
        {
            if (_world == null)
                return AuraResult.InvalidWorld;

            return _world.DestroyJoint(joint);
        }

        public SimulationEntityId AttachView(AuraPhysicsView view, in AuraPhysicsBodyDefinition definition)
        {
            if (_world == null)
                throw new InvalidOperationException("The simulation world has not been created.");

            var entity = _world.CreateEntity();
            var body = _world.AttachBody(entity, definition);
            if (!body.IsValid)
            {
                _world.DestroyEntity(entity);
                throw new AuraException(AuraResult.InvalidDefinition, "The backend rejected the body definition.");
            }

            if (view != null)
            {
                view.Bind(this, entity);
                _registry.Register(entity, view);
            }

            return entity;
        }

        public void DetachView(SimulationEntityId entity)
        {
            if (_world == null)
                return;

            if (_registry != null && _registry.TryGet(entity, out var view))
            {
                view.Unbind();
                _registry.Unregister(entity);
            }

            _world.DestroyEntity(entity);
        }

        private void Start()
        {
            if (_autoCreateOnStart)
                CreateWorld();
        }

        private void FixedUpdate()
        {
            if (!_autoTick || _world == null)
                return;

            var deltaTime = Time.fixedDeltaTime;
            for (var index = 0; index < _characterAuthoring.Count; index++)
                _characterAuthoring[index].Tick(deltaTime);

            _world.Step(new SimulationStep(new SimulationTick(_tick++), deltaTime));
            _dispatcher.Dispatch(_world, _registry);
        }

        private void OnDestroy()
        {
            _registry?.Clear();
            if (_world != null)
                ((IDisposable)_world).Dispose();

            _world = null;
        }
    }
}
