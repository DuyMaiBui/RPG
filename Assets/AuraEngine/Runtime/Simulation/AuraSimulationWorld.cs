using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Simulation
{
    public sealed class AuraSimulationWorld : IDisposable, IAuraSimulationContext
    {
        private readonly IPhysicsWorld _physics;
        private readonly EntityRegistry _entities;
        private readonly List<IAuraSimulationSystem> _systems = new List<IAuraSimulationSystem>(8);
        private readonly Dictionary<ushort, IAuraCommandHandler> _commandHandlers =
            new Dictionary<ushort, IAuraCommandHandler>();
        private readonly List<IAuraCommand> _pendingCommands = new List<IAuraCommand>(32);
        private readonly List<IAuraCommand> _commandScratch = new List<IAuraCommand>(32);
        private readonly List<IAuraSimulationSystem> _systemsScratch = new List<IAuraSimulationSystem>(8);
        private readonly AuraEventBuffer _events = new AuraEventBuffer();
        private AuraBodyState[] _stateScratch;
        private AuraPhysicsEvent[] _eventScratch = new AuraPhysicsEvent[64];
        private readonly AuraTimeStepper _timeStepper = new AuraTimeStepper();
        private AuraHitStop _hitStop;
        private SimulationTick _currentTick;
        private bool _hasStepped;
        private bool _disposed;

        public AuraSimulationWorld(IPhysicsBackend backend, in AuraWorldDefinition definition)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));

            if (definition.Validate() != AuraResult.Success)
                throw new ArgumentException("Invalid world definition.", nameof(definition));

            Definition = definition;
            _physics = backend.CreateWorld(definition);
            _entities = new EntityRegistry(_physics);
            _stateScratch = new AuraBodyState[Math.Max(16, definition.InitialBodyCapacity)];
        }

        public AuraWorldDefinition Definition { get; }

        public SimulationTick CurrentTick => _currentTick;

        public EntityRegistry Entities => _entities;

        public IPhysicsQuery Queries => _physics;

        public IPhysicsWorld Physics => _physics;

        /* Runtime body mutation (velocity, forces, teleport, motion type, layer, enable). Check
           Capabilities for AuraPhysicsCapabilities.BodyControl; unsupported backends return
           AuraResult.UnsupportedOperation instead of ignoring the call. */
        public IPhysicsBodyControl BodyControl
        {
            get
            {
                ThrowIfDisposed();
                return _physics.BodyControl;
            }
        }

        /* Runtime joint mutation (motor, limits, break threshold, feedback). Check
           Capabilities for AuraPhysicsCapabilities.JointControl. */
        public IPhysicsJointControl JointControl
        {
            get
            {
                ThrowIfDisposed();
                return _physics.JointControl;
            }
        }

        /* Per-axis joint control for SixDof and SwingTwist (Jolt 3D). Null when the backend has none. */
        public IPhysicsJointAxisControl JointAxisControl
        {
            get
            {
                ThrowIfDisposed();
                return _physics.JointControl as IPhysicsJointAxisControl;
            }
        }

        /* World gravity and force field zones. Check Capabilities for AuraPhysicsCapabilities.ForceFields. */
        public IPhysicsForceFields ForceFields
        {
            get
            {
                ThrowIfDisposed();
                return _physics.ForceFields;
            }
        }

        /* Time scale applied by Advance (1 = real time, 0 = paused, 0.1 = slow motion). The kernel's fixed step
           never changes; see AuraTimeStepper for the accumulation rules. Step() itself always runs one step. */
        public float TimeScale
        {
            get => _timeStepper.Scale;
            set => _timeStepper.Scale = value;
        }

        /* Per-body freeze helper; ticked automatically before every physics step. */
        public AuraHitStop HitStop
        {
            get
            {
                ThrowIfDisposed();
                return _hitStop ?? (_hitStop = new AuraHitStop(_physics));
            }
        }

        public int EntityCount => _entities.AliveCount;

        public int BodyCount => _physics.BodyCount;

        public int SystemCount => _systems.Count;

        public int PendingEventCount => _events.Count;

        public AuraPhysicsCapabilities Capabilities => _physics.Capabilities;

        public void AddSystem(IAuraSimulationSystem system)
        {
            ThrowIfDisposed();
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (!_systems.Contains(system))
                _systems.Add(system);
        }

        public bool RemoveSystem(IAuraSimulationSystem system)
        {
            ThrowIfDisposed();
            return _systems.Remove(system);
        }

        public void RegisterCommandHandler(IAuraCommandHandler handler)
        {
            ThrowIfDisposed();
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_commandHandlers.ContainsKey(handler.TypeId))
                throw new InvalidOperationException($"A command handler for TypeId {handler.TypeId} is already registered.");

            _commandHandlers.Add(handler.TypeId, handler);
        }

        public void EnqueueCommand(IAuraCommand command)
        {
            ThrowIfDisposed();
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            _pendingCommands.Add(command);
        }

        public void Step(in SimulationStep step)
        {
            ThrowIfDisposed();
            _currentTick = step.Tick;

            _hasStepped = true;

            DispatchCommands();
            RunSystems(step);
            _hitStop?.Tick();
            _physics.Step(step.DeltaTime);
            CaptureEvents();
        }

        /* Advances by wall-clock time under TimeScale: runs zero or more fixed steps of stepDeltaTime each and
           returns how many ran. Tick numbers continue from CurrentTick. Pass the fixed step the caller owns
           (for example Definition.FixedDeltaTime) as stepDeltaTime; it is never rescaled. */
        /* How many fixed steps of stepDeltaTime the wall time buys under TimeScale (see AuraTimeStepper). Callers
           that interleave their own per-step work (character moves, water) use this and call Step themselves. */
        public int PlanSteps(float wallDeltaTime, float stepDeltaTime)
        {
            ThrowIfDisposed();
            return _timeStepper.Advance(wallDeltaTime, stepDeltaTime);
        }

        public int Advance(float wallDeltaTime, float stepDeltaTime)
        {
            var steps = PlanSteps(wallDeltaTime, stepDeltaTime);
            for (var index = 0; index < steps; index++)
                Step(new SimulationStep(_hasStepped ? _currentTick.Next() : _currentTick, stepDeltaTime));

            return steps;
        }

        public SimulationEntityId CreateEntity()
        {
            ThrowIfDisposed();
            return _entities.CreateEntity();
        }

        public bool DestroyEntity(SimulationEntityId entity)
        {
            ThrowIfDisposed();
            return _entities.DestroyEntity(entity);
        }

        public bool IsAlive(SimulationEntityId entity)
        {
            ThrowIfDisposed();
            return _entities.IsAlive(entity);
        }

        public PhysicsBodyId AttachBody(SimulationEntityId entity, in AuraPhysicsBodyDefinition definition)
        {
            ThrowIfDisposed();
            return _entities.AttachBody(entity, definition);
        }

        public bool TryGetBody(SimulationEntityId entity, out PhysicsBodyId body)
        {
            ThrowIfDisposed();
            return _entities.TryGetBody(entity, out body);
        }

        public bool TryResolveEntity(PhysicsBodyId body, out SimulationEntityId entity)
        {
            ThrowIfDisposed();
            return _entities.TryResolveBody(body, out entity);
        }

        public AuraJointId CreateJoint(SimulationEntityId entityA, SimulationEntityId entityB, in AuraJointDefinition definition)
        {
            ThrowIfDisposed();
            if (!_entities.TryGetBody(entityA, out var bodyA) || !_entities.TryGetBody(entityB, out var bodyB))
                return AuraJointId.Invalid;

            var resolved = definition.WithBodies(bodyA, bodyB);

            return _physics.Joints.CreateJoint(resolved);
        }

        public AuraResult DestroyJoint(AuraJointId joint)
        {
            ThrowIfDisposed();
            return _physics.Joints.DestroyJoint(joint);
        }

        public bool HasJoint(AuraJointId joint)
        {
            ThrowIfDisposed();
            return _physics.Joints.HasJoint(joint);
        }

        public AuraVehicleId CreateVehicle(SimulationEntityId chassisEntity, in AuraVehicleDefinition definition)
        {
            ThrowIfDisposed();
            if (!_entities.TryGetBody(chassisEntity, out var chassis))
                return AuraVehicleId.Invalid;

            var resolved = new AuraVehicleDefinition(
                chassis,
                definition.Up,
                definition.Forward,
                definition.WheelPositions,
                definition.WheelRadius,
                definition.WheelWidth,
                definition.SuspensionMinLength,
                definition.SuspensionMaxLength,
                definition.SuspensionFrequency,
                definition.SuspensionDamping,
                definition.MaxSteerAngle,
                definition.MaxPitchRollAngle,
                definition.MaxEngineTorque,
                definition.WheelObjectLayer);

            return _physics.Vehicles.CreateVehicle(resolved);
        }

        public AuraResult DestroyVehicle(AuraVehicleId vehicle)
        {
            ThrowIfDisposed();
            return _physics.Vehicles.DestroyVehicle(vehicle);
        }

        public AuraResult SetVehicleInput(AuraVehicleId vehicle, float forward, float steering, float brake, float handBrake)
        {
            ThrowIfDisposed();
            return _physics.Vehicles.SetVehicleInput(vehicle, forward, steering, brake, handBrake);
        }

        public bool TryGetWheelState(AuraVehicleId vehicle, int wheelIndex, out AuraVehicleWheelState state)
        {
            ThrowIfDisposed();
            return _physics.Vehicles.TryGetWheelState(vehicle, wheelIndex, out state);
        }

        public AuraSoftBodyId CreateSoftBody(in AuraSoftBodyDefinition definition)
        {
            ThrowIfDisposed();
            return _physics.SoftBodies.CreateSoftBody(definition);
        }

        public AuraResult DestroySoftBody(AuraSoftBodyId softBody)
        {
            ThrowIfDisposed();
            return _physics.SoftBodies.DestroySoftBody(softBody);
        }

        public bool TryGetSoftBodyState(AuraSoftBodyId softBody, out AuraSoftBodyState state)
        {
            ThrowIfDisposed();
            return _physics.SoftBodies.TryGetState(softBody, out state);
        }

        public AuraRagdollId CreateRagdoll(in AuraRagdollDefinition definition)
        {
            ThrowIfDisposed();
            return _physics.Ragdolls.CreateRagdoll(definition);
        }

        public AuraResult DestroyRagdoll(AuraRagdollId ragdoll)
        {
            ThrowIfDisposed();
            return _physics.Ragdolls.DestroyRagdoll(ragdoll);
        }

        public AuraResult GetRagdollPose(AuraRagdollId ragdoll, Span<AuraPose> poses)
        {
            ThrowIfDisposed();
            return _physics.Ragdolls.GetPose(ragdoll, poses);
        }

        public AuraResult SetRagdollPose(AuraRagdollId ragdoll, ReadOnlySpan<AuraPose> poses)
        {
            ThrowIfDisposed();
            return _physics.Ragdolls.SetPose(ragdoll, poses);
        }

        public AuraWaterId CreateWater(in AuraWaterDefinition definition)
        {
            ThrowIfDisposed();
            return _physics.Water.CreateWater(definition);
        }

        public AuraResult DestroyWater(AuraWaterId water)
        {
            ThrowIfDisposed();
            return _physics.Water.DestroyWater(water);
        }

        public AuraResult SetWaterParameters(AuraWaterId water, in AuraWaterDefinition definition)
        {
            ThrowIfDisposed();
            return _physics.Water.SetWaterParameters(water, definition);
        }

        public AuraResult ApplyWaterStep(AuraWaterId water, float deltaTime)
        {
            ThrowIfDisposed();
            return _physics.Water.ApplyWaterStep(water, deltaTime);
        }

        public AuraCharacterId CreateCharacter(in AuraCharacterDefinition definition)
        {
            ThrowIfDisposed();
            return _physics.Characters.CreateCharacter(definition);
        }

        public AuraResult DestroyCharacter(AuraCharacterId character)
        {
            ThrowIfDisposed();
            return _physics.Characters.DestroyCharacter(character);
        }

        public bool TryGetCharacterState(AuraCharacterId character, out AuraCharacterState state)
        {
            ThrowIfDisposed();
            return _physics.Characters.TryGetCharacterState(character, out state);
        }

        public void MoveCharacter(AuraCharacterId character, AuraVector3 desiredTranslation, float deltaTime)
        {
            ThrowIfDisposed();
            _physics.Characters.MoveCharacter(character, desiredTranslation, deltaTime);
        }

        public AuraResult SetKinematicTarget(SimulationEntityId entity, in AuraPose pose)
        {
            ThrowIfDisposed();
            if (!_entities.TryGetBody(entity, out var body))
                return AuraResult.InvalidHandle;

            _physics.SetKinematicTarget(body, pose);
            return AuraResult.Success;
        }

        public AuraResult SetSurfaceVelocity(SimulationEntityId entity, AuraVector3 velocity)
        {
            ThrowIfDisposed();
            if (!_entities.TryGetBody(entity, out var body))
                return AuraResult.InvalidHandle;

            return _physics.SetSurfaceVelocity(body, velocity);
        }

        public bool Raycast(
            in AuraRay ray,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            ThrowIfDisposed();
            if (!_physics.Raycast(ray, maxDistance, filter, out hit))
                return false;

            hit = RemapQueryHit(hit);
            return true;
        }

        public int RaycastAll(
            in AuraRay ray,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results)
        {
            ThrowIfDisposed();
            var count = _physics.RaycastAll(ray, maxDistance, filter, results);
            for (var index = 0; index < count; index++)
                results[index] = RemapQueryHit(results[index]);

            return count;
        }

        public int OverlapSphere(
            AuraVector3 center,
            float radius,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results)
        {
            ThrowIfDisposed();
            var count = _physics.OverlapSphere(center, radius, filter, results);
            for (var index = 0; index < count; index++)
                results[index] = RemapQueryHit(results[index]);

            return count;
        }

        public bool TryGetBodyState(SimulationEntityId entity, out AuraBodyState state)
        {
            ThrowIfDisposed();
            if (!_entities.TryGetBody(entity, out var body))
            {
                state = default;
                return false;
            }

            return TryGetBodyState(body, out state);
        }

        public bool TryGetBodyState(PhysicsBodyId body, out AuraBodyState state)
        {
            ThrowIfDisposed();
            var result = _physics.GetBodyState(body, out state);
            if (result != AuraResult.Success)
                return false;

            if (_entities.TryResolveBody(body, out var entity))
                state = new AuraBodyState(body, entity, state.Pose, state.LinearVelocity, state.AngularVelocity, state.IsAwake, state.Flags);

            return true;
        }

        public int CopyBodyStates(Span<AuraBodyState> buffer)
        {
            ThrowIfDisposed();
            EnsureStateScratch(buffer.Length);
            var count = _physics.CopyBodyStates(new Span<AuraBodyState>(_stateScratch, 0, buffer.Length));

            for (var index = 0; index < count; index++)
            {
                var state = _stateScratch[index];
                if (_entities.TryResolveBody(state.Body, out var entity))
                    state = new AuraBodyState(
                        state.Body,
                        entity,
                        state.Pose,
                        state.LinearVelocity,
                        state.AngularVelocity,
                        state.IsAwake,
                        state.Flags);

                buffer[index] = state;
            }

            return count;
        }

        public int CopyEvents(Span<AuraPhysicsEvent> buffer)
        {
            ThrowIfDisposed();
            return _events.CopyTo(buffer);
        }

        public int CopyContacts(Span<AuraContact> buffer)
        {
            ThrowIfDisposed();
            return _physics.Contacts.CopyContacts(buffer);
        }

        public ulong ComputeStateHash()
        {
            ThrowIfDisposed();
            var hash = AuraStateHasher.CombineInt(AuraStateHasher.Begin(), unchecked((int)_currentTick.Value));
            hash = AuraStateHasher.CombineInt(hash, _entities.AliveCount);

            EnsureStateScratch(_entities.Capacity + 1);
            var count = CopyBodyStates(_stateScratch);
            if (count > 1)
                Array.Sort(_stateScratch, 0, count, AuraBodyStateComparer.Instance);

            for (var index = 0; index < count; index++)
                hash = AuraStateHasher.HashBodyState(hash, _stateScratch[index]);

            return hash;
        }

        public byte[] SaveState()
        {
            ThrowIfDisposed();
            return _physics.Serialization.SaveState();
        }

        public void RestoreState(byte[] state)
        {
            ThrowIfDisposed();
            _physics.Serialization.RestoreState(state);
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _systems.Clear();
            _commandHandlers.Clear();
            _pendingCommands.Clear();
            ((IDisposable)_entities).Dispose();
            _physics.Dispose();
        }

        SimulationTick IAuraSimulationContext.CurrentTick => _currentTick;

        IPhysicsQuery IAuraSimulationContext.Queries => _physics;

        SimulationEntityId IAuraSimulationContext.CreateEntity() => CreateEntity();

        bool IAuraSimulationContext.DestroyEntity(SimulationEntityId entity) => DestroyEntity(entity);

        bool IAuraSimulationContext.IsAlive(SimulationEntityId entity) => IsAlive(entity);

        PhysicsBodyId IAuraSimulationContext.AttachBody(SimulationEntityId entity, in AuraPhysicsBodyDefinition definition) =>
            AttachBody(entity, definition);

        bool IAuraSimulationContext.TryGetBody(SimulationEntityId entity, out PhysicsBodyId body) =>
            TryGetBody(entity, out body);

        bool IAuraSimulationContext.TryGetBodyState(SimulationEntityId entity, out AuraBodyState state) =>
            TryGetBodyState(entity, out state);

        bool IAuraSimulationContext.TryResolveEntity(PhysicsBodyId body, out SimulationEntityId entity) =>
            TryResolveEntity(body, out entity);

        AuraResult IAuraSimulationContext.SetKinematicTarget(SimulationEntityId entity, in AuraPose pose) =>
            SetKinematicTarget(entity, pose);

        private void DispatchCommands()
        {
            if (_pendingCommands.Count == 0)
                return;

            _commandScratch.Clear();
            _commandScratch.AddRange(_pendingCommands);
            _pendingCommands.Clear();

            for (var index = 0; index < _commandScratch.Count; index++)
            {
                var command = _commandScratch[index];
                if (_commandHandlers.TryGetValue(command.TypeId, out var handler))
                    handler.Handle(this, command);
            }
        }

        private void RunSystems(in SimulationStep step)
        {
            _systemsScratch.Clear();
            _systemsScratch.AddRange(_systems);

            for (var index = 0; index < _systemsScratch.Count; index++)
                _systemsScratch[index].Tick(this, step);
        }

        private void CaptureEvents()
        {
            _events.Clear();

            var required = _physics.PendingEventCount;
            if (required <= 0)
                return;

            if (_eventScratch.Length < required)
                _eventScratch = new AuraPhysicsEvent[required];

            var count = _physics.CopyEvents(_eventScratch);
            for (var index = 0; index < count; index++)
            {
                var value = _eventScratch[index];
                if (_entities.TryResolveBody(value.BodyA, out var entityA))
                    value = new AuraPhysicsEvent(
                        value.Type,
                        entityA,
                        value.EntityB,
                        value.BodyA,
                        value.BodyB,
                        value.ShapeA,
                        value.ShapeB,
                        value.Point,
                        value.Normal,
                        value.Impulse);

                if (_entities.TryResolveBody(value.BodyB, out var entityB))
                    value = new AuraPhysicsEvent(
                        value.Type,
                        value.EntityA,
                        entityB,
                        value.BodyA,
                        value.BodyB,
                        value.ShapeA,
                        value.ShapeB,
                        value.Point,
                        value.Normal,
                        value.Impulse);

                _events.Append(value);
            }
        }

        private AuraPhysicsQueryHit RemapQueryHit(in AuraPhysicsQueryHit hit)
        {
            if (_entities.TryResolveBody(hit.Body, out var entity))
                return new AuraPhysicsQueryHit(entity, hit.Body, hit.Shape, hit.Distance, hit.Point, hit.Normal);

            return hit;
        }

        private void EnsureStateScratch(int required)
        {
            if (_stateScratch.Length >= required)
                return;

            var size = _stateScratch.Length;
            while (size < required)
                size *= 2;

            _stateScratch = new AuraBodyState[size];
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AuraSimulationWorld));
        }
    }
}
