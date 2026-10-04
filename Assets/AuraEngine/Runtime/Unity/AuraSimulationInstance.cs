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

        [Tooltip("1 = real time, 0 = paused, below 1 = slow motion. The kernel step stays fixed; steps are skipped or accumulated.")]
        [SerializeField]
        [Min(0f)]
        private float _timeScale = 1f;

        [SerializeField]
        private AuraBackendKind _backendKind = AuraBackendKind.Native;

        [SerializeField]
        private AuraPhysicsLayers _layers;

        private readonly List<AuraPhysicsBodyAuthoring> _authoring = new List<AuraPhysicsBodyAuthoring>();
        private readonly List<AuraPhysicsBody2DAuthoring> _authoring2D = new List<AuraPhysicsBody2DAuthoring>();
        private readonly List<AuraJointAuthoringBase> _jointAuthoring = new List<AuraJointAuthoringBase>();
        private readonly List<AuraJoint2DAuthoring> _joint2DAuthoring = new List<AuraJoint2DAuthoring>();
        private readonly List<AuraRagdollChainAuthoring> _chainAuthoring = new List<AuraRagdollChainAuthoring>();
        private readonly List<AuraRagdollChain2DAuthoring> _chain2DAuthoring = new List<AuraRagdollChain2DAuthoring>();
        private readonly List<AuraCharacterAuthoring> _characterAuthoring = new List<AuraCharacterAuthoring>();
        private readonly List<AuraCharacter2DAuthoring> _character2DAuthoring = new List<AuraCharacter2DAuthoring>();
        private readonly List<AuraVehicleAuthoring> _vehicleAuthoring = new List<AuraVehicleAuthoring>();
        private readonly List<AuraSoftBodyAuthoring> _softBodyAuthoring = new List<AuraSoftBodyAuthoring>();
        private readonly List<AuraRagdollAuthoring> _ragdollAuthoring = new List<AuraRagdollAuthoring>();
        private readonly List<AuraWaterAuthoring> _waterAuthoring = new List<AuraWaterAuthoring>();
        private readonly List<AuraWaterId> _waters = new List<AuraWaterId>();
        private readonly List<AuraForceFieldAuthoring> _forceFieldAuthoring = new List<AuraForceFieldAuthoring>();
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
                if (!AuraEngine.Physics.Native.NativePhysicsBackend.IsAvailable())
                {
                    throw new InvalidOperationException(
                        "The AuraEngine native physics plugin (libaura) could not be loaded. " +
                        "Build it with Native/AuraEngine/build_plugin.sh for this platform.");
                }

                backend = new AuraEngine.Physics.Native.NativePhysicsBackend();
            }

            _backendName = backend.Name;
            _world = new AuraSimulationWorld(backend, definition);

            for (var index = 0; index < _authoring.Count; index++)
                _authoring[index].BuildInto(this);

            for (var index = 0; index < _authoring2D.Count; index++)
                _authoring2D[index].BuildInto(this);

            if ((_world.Capabilities & AuraPhysicsCapabilities.Joints) != 0)
            {
                for (var index = 0; index < _joint2DAuthoring.Count; index++)
                    _joint2DAuthoring[index].BuildInto(this);

                for (var index = 0; index < _chain2DAuthoring.Count; index++)
                    _chain2DAuthoring[index].BuildInto(this);

                for (var index = 0; index < _jointAuthoring.Count; index++)
                    _jointAuthoring[index].BuildInto(this);

                for (var index = 0; index < _chainAuthoring.Count; index++)
                    _chainAuthoring[index].BuildInto(this);
            }

            if ((_world.Capabilities & AuraPhysicsCapabilities.Characters) != 0)
            {
                for (var index = 0; index < _characterAuthoring.Count; index++)
                    _characterAuthoring[index].BuildInto(this);

                for (var index = 0; index < _character2DAuthoring.Count; index++)
                    _character2DAuthoring[index].BuildInto(this);
            }

            if ((_world.Capabilities & AuraPhysicsCapabilities.Vehicles) != 0)
            {
                for (var index = 0; index < _vehicleAuthoring.Count; index++)
                    _vehicleAuthoring[index].BuildInto(this);
            }

            for (var index = 0; index < _softBodyAuthoring.Count; index++)
                _softBodyAuthoring[index].BuildInto(this);

            for (var index = 0; index < _ragdollAuthoring.Count; index++)
                _ragdollAuthoring[index].BuildInto(this);

            for (var index = 0; index < _waterAuthoring.Count; index++)
                _waterAuthoring[index].BuildInto(this);

            if ((_world.Capabilities & AuraPhysicsCapabilities.ForceFields) != 0)
            {
                for (var index = 0; index < _forceFieldAuthoring.Count; index++)
                    _forceFieldAuthoring[index].BuildInto(this);
            }

            _world.TimeScale = _timeScale;
            _tick = 0;
        }

        /* Time scale: 1 = real time, 0 = pause, below 1 = slow motion / hit-stop. Takes effect on the next fixed update. */
        public float TimeScale
        {
            get => _world != null ? _world.TimeScale : _timeScale;
            set
            {
                _timeScale = Mathf.Max(0f, value);
                if (_world != null)
                    _world.TimeScale = _timeScale;
            }
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

        public void Register(AuraPhysicsBody2DAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_authoring2D.Contains(authoring))
                return;

            _authoring2D.Add(authoring);
            if (_world != null)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraPhysicsBody2DAuthoring authoring)
        {
            if (authoring == null)
                return;

            _authoring2D.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraJoint2DAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));
            if (_joint2DAuthoring.Contains(authoring))
                return;

            _joint2DAuthoring.Add(authoring);
            if (_world != null && (_world.Capabilities & AuraPhysicsCapabilities.Joints) != 0)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraJoint2DAuthoring authoring)
        {
            if (authoring == null)
                return;
            _joint2DAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraRagdollChain2DAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));
            if (_chain2DAuthoring.Contains(authoring))
                return;

            _chain2DAuthoring.Add(authoring);
            if (_world != null && (_world.Capabilities & AuraPhysicsCapabilities.Joints) != 0)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraRagdollChain2DAuthoring authoring)
        {
            if (authoring == null)
                return;
            _chain2DAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraJointAuthoringBase authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_jointAuthoring.Contains(authoring))
                return;

            _jointAuthoring.Add(authoring);
            if (_world != null && (_world.Capabilities & AuraPhysicsCapabilities.Joints) != 0)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraJointAuthoringBase authoring)
        {
            if (authoring == null)
                return;

            _jointAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraRagdollChainAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_chainAuthoring.Contains(authoring))
                return;

            _chainAuthoring.Add(authoring);
            if (_world != null && (_world.Capabilities & AuraPhysicsCapabilities.Joints) != 0)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraRagdollChainAuthoring authoring)
        {
            if (authoring == null)
                return;

            _chainAuthoring.Remove(authoring);
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

        public void Register(AuraCharacter2DAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_character2DAuthoring.Contains(authoring))
                return;

            _character2DAuthoring.Add(authoring);
            if (_world != null && (_world.Capabilities & AuraPhysicsCapabilities.Characters) != 0)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraCharacter2DAuthoring authoring)
        {
            if (authoring == null)
                return;

            _character2DAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraVehicleAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_vehicleAuthoring.Contains(authoring))
                return;

            _vehicleAuthoring.Add(authoring);
            if (_world != null && (_world.Capabilities & AuraPhysicsCapabilities.Vehicles) != 0)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraVehicleAuthoring authoring)
        {
            if (authoring == null)
                return;

            _vehicleAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraSoftBodyAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_softBodyAuthoring.Contains(authoring))
                return;

            _softBodyAuthoring.Add(authoring);
            if (_world != null)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraSoftBodyAuthoring authoring)
        {
            if (authoring == null)
                return;

            _softBodyAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraRagdollAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_ragdollAuthoring.Contains(authoring))
                return;

            _ragdollAuthoring.Add(authoring);
            if (_world != null)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraRagdollAuthoring authoring)
        {
            if (authoring == null)
                return;

            _ragdollAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraWaterAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_waterAuthoring.Contains(authoring))
                return;

            _waterAuthoring.Add(authoring);
            if (_world != null)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraWaterAuthoring authoring)
        {
            if (authoring == null)
                return;

            _waterAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public void Register(AuraForceFieldAuthoring authoring)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (_forceFieldAuthoring.Contains(authoring))
                return;

            _forceFieldAuthoring.Add(authoring);
            if (_world != null && (_world.Capabilities & AuraPhysicsCapabilities.ForceFields) != 0)
                authoring.BuildInto(this);
        }

        public void Unregister(AuraForceFieldAuthoring authoring)
        {
            if (authoring == null)
                return;

            _forceFieldAuthoring.Remove(authoring);
            authoring.ReleaseFrom(this);
        }

        public AuraForceFieldId AttachForceField(in AuraForceFieldDefinition definition)
        {
            if (_world == null)
                throw new InvalidOperationException("The simulation world has not been created.");

            return _world.ForceFields.CreateField(definition);
        }

        public AuraResult UpdateForceField(AuraForceFieldId field, in AuraForceFieldDefinition definition)
        {
            if (_world == null)
                return AuraResult.InvalidWorld;

            return _world.ForceFields.UpdateField(field, definition);
        }

        public AuraResult DetachForceField(AuraForceFieldId field)
        {
            if (_world == null)
                return AuraResult.InvalidWorld;

            return _world.ForceFields.DestroyField(field);
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

        public AuraVehicleId AttachVehicle(SimulationEntityId chassis, in AuraVehicleDefinition definition)
        {
            if (_world == null)
                throw new InvalidOperationException("The simulation world has not been created.");

            return _world.CreateVehicle(chassis, definition);
        }

        public AuraResult DetachVehicle(AuraVehicleId vehicle)
        {
            if (_world == null)
                return AuraResult.InvalidWorld;

            return _world.DestroyVehicle(vehicle);
        }

        public AuraResult SetVehicleInput(AuraVehicleId vehicle, float forward, float steering, float brake, float handBrake)
        {
            if (_world == null)
                return AuraResult.InvalidWorld;

            return _world.SetVehicleInput(vehicle, forward, steering, brake, handBrake);
        }

        public AuraSoftBodyId AttachSoftBody(in AuraSoftBodyDefinition definition)
        {
            if (_world == null)
                throw new InvalidOperationException("The simulation world has not been created.");

            return _world.CreateSoftBody(definition);
        }

        public AuraResult DetachSoftBody(AuraSoftBodyId softBody)
        {
            if (_world == null)
                return AuraResult.InvalidWorld;

            return _world.DestroySoftBody(softBody);
        }

        public AuraRagdollId AttachRagdoll(in AuraRagdollDefinition definition)
        {
            if (_world == null)
                throw new InvalidOperationException("The simulation world has not been created.");

            return _world.CreateRagdoll(definition);
        }

        public AuraResult DetachRagdoll(AuraRagdollId ragdoll)
        {
            if (_world == null)
                return AuraResult.InvalidWorld;

            return _world.DestroyRagdoll(ragdoll);
        }

        public bool TryGetRagdollPose(AuraRagdollId ragdoll, Span<AuraPose> poses)
        {
            if (_world == null)
                return false;

            return _world.GetRagdollPose(ragdoll, poses) == AuraResult.Success;
        }

        public AuraWaterId AttachWater(in AuraWaterDefinition definition)
        {
            if (_world == null)
                throw new InvalidOperationException("The simulation world has not been created.");

            var water = _world.CreateWater(definition);
            if (water.IsValid)
                _waters.Add(water);

            return water;
        }

        public AuraResult DetachWater(AuraWaterId water)
        {
            _waters.Remove(water);
            if (_world == null)
                return AuraResult.InvalidWorld;

            return _world.DestroyWater(water);
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

            // The kernel step stays Time.fixedDeltaTime; the time scale only changes how many steps run per tick.
            var deltaTime = Time.fixedDeltaTime;
            var steps = _world.PlanSteps(deltaTime, deltaTime);
            for (var step = 0; step < steps; step++)
            {
                for (var index = 0; index < _characterAuthoring.Count; index++)
                    _characterAuthoring[index].Tick(deltaTime);

                for (var index = 0; index < _character2DAuthoring.Count; index++)
                    _character2DAuthoring[index].Tick(deltaTime);

                for (var index = 0; index < _waters.Count; index++)
                    _world.ApplyWaterStep(_waters[index], deltaTime);

                _world.Step(new SimulationStep(new SimulationTick(_tick++), deltaTime));
            }

            if (steps > 0)
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
