using System;
using System.Collections.Generic;
using System.IO;
using AuraEngine.Core;

namespace AuraEngine.Physics
{
    public sealed class ManagedPhysicsWorld : IPhysicsWorld, IPhysicsJoints, IPhysicsCharacters, IPhysicsContacts, IPhysicsSerialization
    {
        private const float Epsilon = 1e-5f;
        private const float MaxPositionCorrection = 0.2f;
        private const int ShapesPerBody = 64;
        private const int SweepSteps = 64;

        private readonly ManagedBroadphase _broadphase = new ManagedBroadphase();
        private readonly List<ManagedBody> _bodies = new List<ManagedBody>();
        private readonly List<int> _freeSlots = new List<int>();
        private readonly List<ManagedJoint> _joints = new List<ManagedJoint>();
        private readonly List<int> _freeJointSlots = new List<int>();
        private readonly List<ManagedManifold> _manifolds = new List<ManagedManifold>();
        private readonly Dictionary<long, ManagedManifold> _cache = new Dictionary<long, ManagedManifold>();
        private readonly List<(int A, int B)> _pairs = new List<(int, int)>();
        private readonly List<AuraPhysicsEvent> _events = new List<AuraPhysicsEvent>(64);
        private readonly HashSet<long> _touching = new HashSet<long>();
        private readonly HashSet<long> _previousTouching = new HashSet<long>();
        private readonly List<long> _enterKeys = new List<long>();
        private readonly List<long> _exitKeys = new List<long>();
        private readonly AuraCollisionMatrix _matrix;
        private readonly AuraSolverSettings _settings;
        private float _stepDelta = 1f / 60f;

        public ManagedPhysicsWorld(in AuraWorldDefinition definition)
        {
            Mode = definition.Mode;
            _matrix = definition.CollisionMatrix ?? AuraCollisionMatrix.CreateAllCollide();
            _settings = definition.SolverSettings ?? new AuraSolverSettings();
            Gravity = Mode == AuraPhysicsMode.Plane2D
                ? new AuraVector3(definition.Gravity.X, definition.Gravity.Y, 0f)
                : definition.Gravity;
        }

        public AuraPhysicsMode Mode { get; }

        public AuraVector3 Gravity { get; }

        AuraPhysicsCapabilities IPhysicsWorld.Capabilities => Capabilities;

        private static AuraPhysicsCapabilities Capabilities =>
            AuraPhysicsCapabilities.BodyStatic |
            AuraPhysicsCapabilities.BodyDynamic |
            AuraPhysicsCapabilities.BodyKinematic |
            AuraPhysicsCapabilities.ShapeBox |
            AuraPhysicsCapabilities.ShapeSphere |
            AuraPhysicsCapabilities.ShapeCapsule |
            AuraPhysicsCapabilities.ShapeCylinder |
            AuraPhysicsCapabilities.ShapeTriangleMesh |
            AuraPhysicsCapabilities.ShapeHeightField |
            AuraPhysicsCapabilities.QueryRaycast |
            AuraPhysicsCapabilities.QueryShapeCast |
            AuraPhysicsCapabilities.QueryOverlap |
            AuraPhysicsCapabilities.Triggers |
            AuraPhysicsCapabilities.Contacts |
            AuraPhysicsCapabilities.SleepWake |
            AuraPhysicsCapabilities.Joints;

        int IPhysicsWorld.BodyCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _bodies.Count; index++)
                    if (_bodies[index].Occupied)
                        count++;
                return count;
            }
        }

        int IPhysicsEventSource.PendingEventCount => _events.Count;

        PhysicsBodyId IPhysicsWorld.CreateBody(in AuraPhysicsBodyDefinition definition)
        {
            if (definition.Validate() != AuraResult.Success)
                return PhysicsBodyId.Invalid;

            var body = BuildBody(definition);
            if (_freeSlots.Count > 0)
            {
                var index = _freeSlots[_freeSlots.Count - 1];
                _freeSlots.RemoveAt(_freeSlots.Count - 1);
                body.Generation = _bodies[index].Generation;
                _bodies[index] = body;
                return new PhysicsBodyId(index, body.Generation);
            }

            _bodies.Add(body);
            return new PhysicsBodyId(_bodies.Count - 1, body.Generation);
        }

        AuraResult IPhysicsWorld.DestroyBody(PhysicsBodyId body)
        {
            if (!TryGetBody(body, out var slot))
                return AuraResult.InvalidHandle;

            slot.Occupied = false;
            slot.Generation++;
            _freeSlots.Add(body.Index);
            return AuraResult.Success;
        }

        bool IPhysicsWorld.HasBody(PhysicsBodyId body) => TryGetBody(body, out _);

        void IPhysicsWorld.SetKinematicTarget(PhysicsBodyId body, in AuraPose pose)
        {
            if (!TryGetBody(body, out var slot))
                return;

            slot.Pose = Mode == AuraPhysicsMode.Plane2D
                ? new AuraPose(new AuraVector3(pose.Position.X, pose.Position.Y, 0f), pose.Rotation)
                : pose;
            slot.LinearVelocity = AuraVector3.Zero;
            slot.AngularVelocity = AuraVector3.Zero;
            slot.Awake = true;
            slot.SleepTimer = 0f;
        }

        void IPhysicsWorld.ApplyImpulse(PhysicsBodyId body, AuraVector3 impulse)
        {
            if (!TryGetBody(body, out var slot) || slot.BodyType != AuraBodyType.Dynamic)
                return;

            slot.LinearVelocity += impulse * slot.InvMass;
            slot.Awake = true;
            slot.SleepTimer = 0f;
        }

        AuraResult IPhysicsWorld.SetSurfaceVelocity(PhysicsBodyId body, AuraVector3 velocity)
        {
            if (!TryGetBody(body, out var slot))
                return AuraResult.InvalidHandle;

            slot.SurfaceVelocity = velocity;
            return AuraResult.Success;
        }

        IPhysicsJoints IPhysicsWorld.Joints => this;

        IPhysicsCharacters IPhysicsWorld.Characters => this;

        IPhysicsContacts IPhysicsWorld.Contacts => this;

        IPhysicsSerialization IPhysicsWorld.Serialization => this;

        AuraJointId IPhysicsJoints.CreateJoint(in AuraJointDefinition definition)
        {
            if (!TryGetBody(definition.BodyA, out var bodyA) || !TryGetBody(definition.BodyB, out var bodyB))
                return AuraJointId.Invalid;

            var joint = new ManagedJoint
            {
                Occupied = true,
                Type = definition.Type,
                BodyA = definition.BodyA.Index,
                BodyB = definition.BodyB.Index,
                Distance = definition.Distance,
                AxisLocalA = bodyA.Pose.Rotation.InverseRotate(definition.AxisA).Normalized(),
                AxisLocalB = bodyB.Pose.Rotation.InverseRotate(definition.AxisB).Normalized(),
                NormalLocalA = bodyA.Pose.Rotation.InverseRotate(definition.NormalAxisA).Normalized(),
                NormalLocalB = bodyB.Pose.Rotation.InverseRotate(definition.NormalAxisB).Normalized(),
                FixedPoint = definition.AxisA,
                EnableLimit = definition.EnableLimit,
                MinLimit = definition.MinLimit,
                MaxLimit = definition.MaxLimit,
                SwingLimit = definition.SwingLimit,
                MotorEnabled = definition.MotorEnabled,
                MotorTargetVelocity = definition.MotorTargetVelocity,
                MaxMotorForce = definition.MaxMotorForce,
                SpringFrequency = definition.SpringFrequency,
                SpringDamping = definition.SpringDamping,
            };
            joint.LocalAnchorA = bodyA.Pose.Rotation.InverseRotate(definition.AnchorA - bodyA.Pose.Position);
            joint.LocalAnchorB = bodyB.Pose.Rotation.InverseRotate(definition.AnchorB - bodyB.Pose.Position);

            if (_freeJointSlots.Count > 0)
            {
                var index = _freeJointSlots[_freeJointSlots.Count - 1];
                _freeJointSlots.RemoveAt(_freeJointSlots.Count - 1);
                joint.Generation = _joints[index].Generation;
                _joints[index] = joint;
                return new AuraJointId(index, joint.Generation);
            }

            _joints.Add(joint);
            return new AuraJointId(_joints.Count - 1, joint.Generation);
        }

        AuraResult IPhysicsJoints.DestroyJoint(AuraJointId joint)
        {
            if (!TryGetJoint(joint, out var slot))
                return AuraResult.InvalidHandle;

            slot.Occupied = false;
            slot.Generation++;
            _freeJointSlots.Add(joint.Index);
            return AuraResult.Success;
        }

        bool IPhysicsJoints.HasJoint(AuraJointId joint) => TryGetJoint(joint, out _);

        AuraCharacterId IPhysicsCharacters.CreateCharacter(in AuraCharacterDefinition definition) => AuraCharacterId.Invalid;

        AuraResult IPhysicsCharacters.DestroyCharacter(AuraCharacterId character) => AuraResult.InvalidHandle;

        bool IPhysicsCharacters.TryGetCharacterState(AuraCharacterId character, out AuraCharacterState state)
        {
            state = default;
            return false;
        }

        void IPhysicsCharacters.MoveCharacter(AuraCharacterId character, AuraVector3 desiredTranslation, float deltaTime)
        {
        }

        int IPhysicsContacts.CopyContacts(Span<AuraContact> buffer)
        {
            var written = 0;
            for (var index = 0; index < _manifolds.Count && written < buffer.Length; index++)
            {
                var manifold = _manifolds[index];
                if (manifold.IsTrigger || manifold.Count == 0)
                    continue;

                var point = manifold.Points[0];
                buffer[written++] = new AuraContact(
                    SimulationEntityId.None,
                    SimulationEntityId.None,
                    new PhysicsBodyId(manifold.BodyA, 0),
                    new PhysicsBodyId(manifold.BodyB, 0),
                    point.Position,
                    manifold.Normal,
                    point.NormalImpulse);
            }

            return written;
        }

        ulong IPhysicsSerialization.ComputeStateHash()
        {
            var hash = AuraStateHasher.Begin();
            for (var index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied)
                    continue;

                hash = AuraStateHasher.CombineInt(hash, index);
                hash = AuraStateHasher.CombinePose(hash, body.Pose);
                hash = AuraStateHasher.CombineVector3(hash, body.LinearVelocity);
                hash = AuraStateHasher.CombineVector3(hash, body.AngularVelocity);
            }

            return hash;
        }

        byte[] IPhysicsSerialization.SaveState()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var count = 0;
            for (var index = 0; index < _bodies.Count; index++)
            {
                if (_bodies[index].Occupied)
                    count++;
            }

            writer.Write(count);
            for (var index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied)
                    continue;

                writer.Write(index);
                writer.Write(body.Generation);
                WriteVector(writer, body.Pose.Position);
                WriteQuaternion(writer, body.Pose.Rotation);
                WriteVector(writer, body.LinearVelocity);
                WriteVector(writer, body.AngularVelocity);
                writer.Write(body.Awake ? 1 : 0);
                writer.Write(body.SleepTimer);
            }

            writer.Flush();
            return stream.ToArray();
        }

        void IPhysicsSerialization.RestoreState(byte[] state)
        {
            if (state == null || state.Length == 0)
                return;

            using var stream = new MemoryStream(state);
            using var reader = new BinaryReader(stream);
            var count = reader.ReadInt32();
            for (var entry = 0; entry < count; entry++)
            {
                var index = reader.ReadInt32();
                var generation = reader.ReadInt32();
                var position = ReadVector(reader);
                var rotation = ReadQuaternion(reader);
                var linearVelocity = ReadVector(reader);
                var angularVelocity = ReadVector(reader);
                var awake = reader.ReadInt32() != 0;
                var sleepTimer = reader.ReadSingle();

                if (index < 0 || index >= _bodies.Count)
                    continue;

                var body = _bodies[index];
                if (!body.Occupied || body.Generation != generation)
                    continue;

                body.Pose = new AuraPose(position, rotation);
                body.LinearVelocity = linearVelocity;
                body.AngularVelocity = angularVelocity;
                body.Awake = awake;
                body.SleepTimer = sleepTimer;
            }

            _broadphase.ComputeAabbs(_bodies, Mode);
        }

        private static void WriteVector(BinaryWriter writer, AuraVector3 value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }

        private static void WriteQuaternion(BinaryWriter writer, AuraQuaternion value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
            writer.Write(value.W);
        }

        private static AuraVector3 ReadVector(BinaryReader reader) =>
            new AuraVector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        private static AuraQuaternion ReadQuaternion(BinaryReader reader) =>
            new AuraQuaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        AuraResult IPhysicsWorld.GetBodyState(PhysicsBodyId body, out AuraBodyState state)
        {
            if (!TryGetBody(body, out var slot))
            {
                state = default;
                return AuraResult.InvalidHandle;
            }

            state = ToState(body, slot);
            return AuraResult.Success;
        }

        int IPhysicsWorld.CopyBodyStates(Span<AuraBodyState> buffer)
        {
            var written = 0;
            for (var index = 0; index < _bodies.Count && written < buffer.Length; index++)
            {
                var body = _bodies[index];
                if (body.Occupied)
                    buffer[written++] = ToState(new PhysicsBodyId(index, body.Generation), body);
            }

            return written;
        }

        void IPhysicsWorld.Step(float deltaTime)
        {
            _stepDelta = deltaTime;
            IntegrateVelocities(deltaTime);
            _broadphase.ComputeAabbs(_bodies, Mode);
            _pairs.Clear();
            _pairs.AddRange(_broadphase.ComputePairs(_bodies));
            _pairs.Sort((left, right) => left.A != right.A ? left.A.CompareTo(right.A) : left.B.CompareTo(right.B));
            BuildManifolds();
            WarmStart();
            for (var iteration = 0; iteration < _settings.VelocityIterations; iteration++)
            {
                SolveContactsVelocity();
                SolveJointsVelocity();
            }

            IntegratePositions(deltaTime);
            for (var iteration = 0; iteration < _settings.PositionIterations; iteration++)
                SolvePositions();

            UpdateSleep(deltaTime);
            EmitEvents();
        }

        int IPhysicsEventSource.CopyEvents(Span<AuraPhysicsEvent> buffer)
        {
            var count = Math.Min(_events.Count, buffer.Length);
            for (var index = 0; index < count; index++)
                buffer[index] = _events[index];

            _events.RemoveRange(0, count);
            return count;
        }

        bool IPhysicsQuery.Raycast(in AuraRay ray, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit)
        {
            hit = default;
            var bestDistance = float.MaxValue;
            var found = false;

            for (var index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || !PassesFilter(body, filter))
                    continue;

                var bodyId = new PhysicsBodyId(index, body.Generation);
                for (var shapeIndex = 0; shapeIndex < body.Shapes.Length; shapeIndex++)
                {
                    var shape = body.Shapes[shapeIndex];
                    if (shape.IsTrigger && filter.FiltersTriggers(true, true))
                        continue;

                    var candidate = ManagedShapeView.FromBodyShape(body, shape);
                    if (!ManagedCollision.Ray(ray, candidate, Mode, maxDistance, out var distance, out var point, out var normal))
                        continue;

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        hit = new AuraPhysicsQueryHit(SimulationEntityId.None, bodyId, ShapeId(index, shapeIndex, body.Generation), distance, point, normal);
                        found = true;
                    }
                }
            }

            return found;
        }

        int IPhysicsQuery.RaycastAll(in AuraRay ray, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            var count = 0;
            for (var index = 0; index < _bodies.Count && count < results.Length; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || !PassesFilter(body, filter))
                    continue;

                var bodyId = new PhysicsBodyId(index, body.Generation);
                for (var shapeIndex = 0; shapeIndex < body.Shapes.Length && count < results.Length; shapeIndex++)
                {
                    var shape = body.Shapes[shapeIndex];
                    if (shape.IsTrigger && filter.FiltersTriggers(true, true))
                        continue;

                    var candidate = ManagedShapeView.FromBodyShape(body, shape);
                    if (!ManagedCollision.Ray(ray, candidate, Mode, maxDistance, out var distance, out var point, out var normal))
                        continue;

                    results[count++] = new AuraPhysicsQueryHit(SimulationEntityId.None, bodyId, ShapeId(index, shapeIndex, body.Generation), distance, point, normal);
                }
            }

            return count;
        }

        bool IPhysicsQuery.SphereCast(AuraVector3 origin, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit) =>
            Sweep(ManagedShapeView.Sphere(origin, radius), direction, maxDistance, filter, out hit);

        int IPhysicsQuery.SphereCastAll(AuraVector3 origin, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            SweepAll(ManagedShapeView.Sphere(origin, radius), direction, maxDistance, filter, results);

        bool IPhysicsQuery.CapsuleCast(AuraVector3 pointA, AuraVector3 pointB, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit) =>
            Sweep(ManagedShapeView.Capsule(pointA, pointB, radius), direction, maxDistance, filter, out hit);

        int IPhysicsQuery.CapsuleCastAll(AuraVector3 pointA, AuraVector3 pointB, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            SweepAll(ManagedShapeView.Capsule(pointA, pointB, radius), direction, maxDistance, filter, results);

        bool IPhysicsQuery.BoxCast(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit) =>
            Sweep(ManagedShapeView.Box(center, rotation, halfExtents), direction, maxDistance, filter, out hit);

        int IPhysicsQuery.BoxCastAll(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            SweepAll(ManagedShapeView.Box(center, rotation, halfExtents), direction, maxDistance, filter, results);

        bool IPhysicsQuery.ShapeCast(in AuraPhysicsShapeDefinition shape, AuraPose pose, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit) =>
            Sweep(ManagedShapeView.FromDefinition(shape, pose), direction, maxDistance, filter, out hit);

        int IPhysicsQuery.ShapeCastAll(in AuraPhysicsShapeDefinition shape, AuraPose pose, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            SweepAll(ManagedShapeView.FromDefinition(shape, pose), direction, maxDistance, filter, results);

        int IPhysicsQuery.OverlapPoint(AuraVector3 point, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            var count = 0;
            for (var index = 0; index < _bodies.Count && count < results.Length; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || !PassesFilter(body, filter))
                    continue;

                var bodyId = new PhysicsBodyId(index, body.Generation);
                for (var shapeIndex = 0; shapeIndex < body.Shapes.Length && count < results.Length; shapeIndex++)
                {
                    var shape = body.Shapes[shapeIndex];
                    if (shape.IsTrigger && filter.FiltersTriggers(true, true))
                        continue;

                    if (ManagedCollision.Contains(point, ManagedShapeView.FromBodyShape(body, shape), Mode))
                        results[count++] = new AuraPhysicsQueryHit(SimulationEntityId.None, bodyId, ShapeId(index, shapeIndex, body.Generation), 0f, point, AuraVector3.Zero);
                }
            }

            return count;
        }

        int IPhysicsQuery.OverlapSphere(AuraVector3 center, float radius, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            Overlap(ManagedShapeView.Sphere(center, radius), filter, results);

        int IPhysicsQuery.OverlapBox(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            Overlap(ManagedShapeView.Box(center, rotation, halfExtents), filter, results);

        int IPhysicsQuery.OverlapCapsule(AuraVector3 pointA, AuraVector3 pointB, float radius, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            Overlap(ManagedShapeView.Capsule(pointA, pointB, radius), filter, results);

        int IPhysicsQuery.OverlapShape(in AuraPhysicsShapeDefinition shape, AuraPose pose, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            Overlap(ManagedShapeView.FromDefinition(shape, pose), filter, results);

        void IDisposable.Dispose()
        {
            _bodies.Clear();
            _freeSlots.Clear();
            _joints.Clear();
            _freeJointSlots.Clear();
            _manifolds.Clear();
            _cache.Clear();
            _events.Clear();
            _touching.Clear();
            _previousTouching.Clear();
        }

        private static float ResolveBodyFriction(in AuraPhysicsBodyDefinition definition, ManagedShape[] shapes)
        {
            if (definition.Material.Friction > 0f)
                return definition.Material.Friction;

            var friction = 0f;
            for (var index = 0; index < shapes.Length; index++)
            {
                if (shapes[index].Friction > friction)
                    friction = shapes[index].Friction;
            }

            return friction;
        }

        private static float ResolveBodyRestitution(in AuraPhysicsBodyDefinition definition, ManagedShape[] shapes)
        {
            if (definition.Material.Restitution > 0f)
                return definition.Material.Restitution;

            var restitution = 0f;
            for (var index = 0; index < shapes.Length; index++)
            {
                if (shapes[index].Restitution > restitution)
                    restitution = shapes[index].Restitution;
            }

            return restitution;
        }

        private ManagedBody BuildBody(in AuraPhysicsBodyDefinition definition)
        {
            var shapes = new ManagedShape[definition.Shapes.Length];
            for (var index = 0; index < shapes.Length; index++)
            {
                var source = definition.Shapes[index];
                shapes[index] = new ManagedShape
                {
                    Type = source.Type,
                    LocalPose = source.LocalPose,
                    IsTrigger = source.IsTrigger,
                    Friction = source.Material.Friction,
                    Restitution = source.Material.Restitution,
                    HalfExtents = source.Geometry.HalfExtents,
                    Radius = source.Geometry.Radius,
                    Height = source.Geometry.Height,
                    MeshVertices = source.Geometry.MeshVertices,
                    MeshIndices = source.Geometry.MeshIndices,
                    HeightSamples = source.Geometry.HeightSamples,
                    HeightResolution = source.Geometry.HeightResolution,
                    HeightScale = source.Geometry.HeightScale,
                };
            }

            var isDynamic = definition.Type == AuraBodyType.Dynamic;
            var invInertia = AuraVector3.Zero;            if (isDynamic)
            {
                ManagedMass.Compute(definition, out _, out var inertia);
                var multiplier = definition.InertiaMultiplier > 0f ? definition.InertiaMultiplier : 1f;
                invInertia = new AuraVector3(
                    inertia.X > Epsilon ? 1f / (inertia.X * multiplier) : 0f,
                    inertia.Y > Epsilon ? 1f / (inertia.Y * multiplier) : 0f,
                    inertia.Z > Epsilon ? 1f / (inertia.Z * multiplier) : 0f);
            }

            return new ManagedBody
            {
                Occupied = true,
                BodyType = definition.Type,
                Layer = definition.Layer,
                CollisionMask = definition.CollisionMask,
                GroupIndex = definition.GroupIndex,
                InvMass = isDynamic ? 1f / definition.Mass : 0f,
                InvInertiaLocal = invInertia,
                GravityScale = definition.GravityScale,
                Friction = ResolveBodyFriction(definition, shapes),
                Restitution = ResolveBodyRestitution(definition, shapes),
                LinearDamping = definition.LinearDamping,
                AngularDamping = definition.AngularDamping,
                Freeze = (uint)definition.Freeze,
                AllowSleeping = definition.AllowSleeping,
                MaxLinearVelocity = definition.MaxLinearVelocity,
                MaxAngularVelocity = definition.MaxAngularVelocity,
                Pose = definition.InitialPose,
                LinearVelocity = definition.InitialLinearVelocity,
                AngularVelocity = definition.InitialAngularVelocity,
                Awake = isDynamic,
                SleepTimer = 0f,
                Shapes = shapes,
            };
        }

        private AuraBodyState ToState(PhysicsBodyId id, ManagedBody body) =>
            new AuraBodyState(id, SimulationEntityId.None, body.Pose, body.LinearVelocity, body.AngularVelocity, body.Awake, 0u);

        private void IntegrateVelocities(float deltaTime)
        {
            for (var index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || body.BodyType != AuraBodyType.Dynamic || !body.Awake)
                    continue;

                body.LinearVelocity += Gravity * (body.GravityScale * deltaTime);
                if (body.LinearDamping > 0f)
                    body.LinearVelocity *= 1f / (1f + body.LinearDamping * deltaTime);
                if (body.AngularDamping > 0f)
                    body.AngularVelocity *= 1f / (1f + body.AngularDamping * deltaTime);

                if (Mode == AuraPhysicsMode.Plane2D)
                {
                    body.LinearVelocity = new AuraVector3(body.LinearVelocity.X, body.LinearVelocity.Y, 0f);
                    body.AngularVelocity = new AuraVector3(0f, 0f, body.AngularVelocity.Z);
                }

                ApplyFreeze(body);
                ClampVelocity(body);
            }
        }

        private static void ApplyFreeze(ManagedBody body)
        {
            var freeze = body.Freeze;
            if (freeze == 0u)
                return;

            var linear = body.LinearVelocity;
            var angular = body.AngularVelocity;
            if ((freeze & 1u) != 0u) linear = new AuraVector3(0f, linear.Y, linear.Z);
            if ((freeze & 2u) != 0u) linear = new AuraVector3(linear.X, 0f, linear.Z);
            if ((freeze & 4u) != 0u) linear = new AuraVector3(linear.X, linear.Y, 0f);
            if ((freeze & 8u) != 0u) angular = new AuraVector3(0f, angular.Y, angular.Z);
            if ((freeze & 16u) != 0u) angular = new AuraVector3(angular.X, 0f, angular.Z);
            if ((freeze & 32u) != 0u) angular = new AuraVector3(angular.X, angular.Y, 0f);

            body.LinearVelocity = linear;
            body.AngularVelocity = angular;
        }

        private void ClampVelocity(ManagedBody body)
        {
            var maxLinear = body.MaxLinearVelocity > 0f ? body.MaxLinearVelocity : _settings.MaxLinearVelocity;
            var speedSquared = body.LinearVelocity.LengthSquared;
            if (speedSquared > maxLinear * maxLinear)
                body.LinearVelocity = body.LinearVelocity.Normalized() * maxLinear;

            var maxAngular = body.MaxAngularVelocity > 0f ? body.MaxAngularVelocity : _settings.MaxAngularVelocity;
            var spinSquared = body.AngularVelocity.LengthSquared;
            if (spinSquared > maxAngular * maxAngular)
                body.AngularVelocity = body.AngularVelocity.Normalized() * maxAngular;
        }

        private void IntegratePositions(float deltaTime)
        {
            for (var index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || body.BodyType != AuraBodyType.Dynamic || !body.Awake)
                    continue;

                var position = body.Pose.Position + body.LinearVelocity * deltaTime;
                var rotation = body.Pose.Rotation;
                if (body.AngularVelocity.LengthSquared > Epsilon)
                {
                    var spin = body.AngularVelocity * deltaTime;
                    var angle = spin.Length;
                    var delta = AuraQuaternion.FromAxisAngle(spin / angle, angle);
                    rotation = AuraQuaternion.Multiply(delta, rotation).Normalized();
                }

                if (Mode == AuraPhysicsMode.Plane2D)
                    position = new AuraVector3(position.X, position.Y, 0f);

                body.Pose = new AuraPose(position, rotation);
            }
        }

        private void BuildManifolds()
        {
            _manifolds.Clear();
            _touching.Clear();

            for (var index = 0; index < _pairs.Count; index++)
            {
                var (aIndex, bIndex) = _pairs[index];
                var bodyA = _bodies[aIndex];
                var bodyB = _bodies[bIndex];
                if (!CanInteract(bodyA, bodyB))
                    continue;

                if (bodyA.Awake != bodyB.Awake && bodyA.BodyType == AuraBodyType.Dynamic && bodyB.BodyType == AuraBodyType.Dynamic)
                {
                    bodyA.Awake = true;
                    bodyB.Awake = true;
                    bodyA.SleepTimer = 0f;
                    bodyB.SleepTimer = 0f;
                }

                var pairKey = Key(aIndex, bIndex);
                ManagedManifold manifold = null;
                for (var shapeA = 0; shapeA < bodyA.Shapes.Length; shapeA++)
                {
                    var viewA = ManagedShapeView.FromBodyShape(bodyA, bodyA.Shapes[shapeA]);
                    for (var shapeB = 0; shapeB < bodyB.Shapes.Length; shapeB++)
                    {
                        var viewB = ManagedShapeView.FromBodyShape(bodyB, bodyB.Shapes[shapeB]);
                        var candidate = new ManagedManifold { BodyA = aIndex, BodyB = bIndex };
                        if (!ManagedCollision.Contact(viewA, viewB, Mode, candidate))
                            continue;

                        if (manifold == null)
                        {
                            manifold = candidate;
                        }
                        else if (candidate.Count > 0 && (manifold.Count < 4 || candidate.Points[0].Penetration > manifold.Points[0].Penetration))
                        {
                            manifold = candidate;
                        }
                    }
                }

                if (manifold == null)
                    continue;

                manifold.IsTrigger = false;
                for (var shapeA = 0; shapeA < bodyA.Shapes.Length && !manifold.IsTrigger; shapeA++)
                    for (var shapeB = 0; shapeB < bodyB.Shapes.Length; shapeB++)
                        if (bodyA.Shapes[shapeA].IsTrigger || bodyB.Shapes[shapeB].IsTrigger)
                        {
                            manifold.IsTrigger = true;
                            break;
                        }

                manifold.Friction = AuraPhysicsMaterialDefinition.CombineFriction(bodyA.Friction, bodyB.Friction, AuraFrictionCombine.Average);
                manifold.Restitution = AuraPhysicsMaterialDefinition.CombineRestitution(bodyA.Restitution, bodyB.Restitution, AuraRestitutionCombine.Average);

                _manifolds.Add(manifold);
                _touching.Add(pairKey);
            }
        }

        private void WarmStart()
        {
            foreach (var manifold in _manifolds)
            {
                if (manifold.IsTrigger)
                    continue;

                var pairKey = Key(manifold.BodyA, manifold.BodyB);
                if (!_cache.TryGetValue(pairKey, out var previous) || previous.Count != manifold.Count)
                    continue;

                for (var pointIndex = 0; pointIndex < manifold.Count && pointIndex < previous.Count; pointIndex++)
                {
                    var point = manifold.Points[pointIndex];
                    point.NormalImpulse = previous.Points[pointIndex].NormalImpulse;
                    point.TangentImpulse1 = previous.Points[pointIndex].TangentImpulse1;
                    point.TangentImpulse2 = previous.Points[pointIndex].TangentImpulse2;
                    manifold.Points[pointIndex] = point;
                    ApplyImpulse(manifold, point, point.NormalImpulse, point.TangentImpulse1, point.TangentImpulse2);
                }
            }
        }

        private void SolveContactsVelocity()
        {
            for (var index = 0; index < _manifolds.Count; index++)
            {
                var manifold = _manifolds[index];
                if (manifold.IsTrigger)
                    continue;

                var bodyA = _bodies[manifold.BodyA];
                var bodyB = _bodies[manifold.BodyB];
                var normal = manifold.Normal;
                BuildTangents(normal, out var tangent1, out var tangent2);

                var conveyorActive = bodyA.SurfaceVelocity.LengthSquared > 1e-8f || bodyB.SurfaceVelocity.LengthSquared > 1e-8f;
                if (conveyorActive)
                {
                    if (bodyA.BodyType == AuraBodyType.Dynamic)
                    {
                        bodyA.Awake = true;
                        bodyA.SleepTimer = 0f;
                    }

                    if (bodyB.BodyType == AuraBodyType.Dynamic)
                    {
                        bodyB.Awake = true;
                        bodyB.SleepTimer = 0f;
                    }
                }

                for (var pointIndex = 0; pointIndex < manifold.Count; pointIndex++)
                {
                    var point = manifold.Points[pointIndex];
                    var rA = point.Position - bodyA.Pose.Position;
                    var rB = point.Position - bodyB.Pose.Position;

                    var relative = VelocityAt(bodyB, rB) - VelocityAt(bodyA, rA);
                    var normalSpeed = AuraVector3.Dot(relative, normal);

                    var kNormal = EffectiveMass(bodyA, bodyB, rA, rB, normal);
                    if (kNormal <= 0f)
                        continue;

                    var bias = normalSpeed < -1f ? -manifold.Restitution * normalSpeed : 0f;
                    var delta = -(normalSpeed - bias) / kNormal;
                    var newImpulse = MathF.Max(point.NormalImpulse + delta, 0f);
                    var applied = newImpulse - point.NormalImpulse;
                    point.NormalImpulse = newImpulse;
                    ApplyImpulse(manifold, point, applied, 0f, 0f);

                    relative = VelocityAt(bodyB, rB) - VelocityAt(bodyA, rA);
                    var tangentSpeed1 = AuraVector3.Dot(relative, tangent1) + AuraVector3.Dot(bodyB.SurfaceVelocity - bodyA.SurfaceVelocity, tangent1);
                    var kTangent1 = EffectiveMass(bodyA, bodyB, rA, rB, tangent1);
                    if (kTangent1 > 0f)
                    {
                        var delta1 = -tangentSpeed1 / kTangent1;
                        var maxFriction = manifold.Friction * point.NormalImpulse;
                        var newTangent1 = Math.Clamp(point.TangentImpulse1 + delta1, -maxFriction, maxFriction);
                        var applied1 = newTangent1 - point.TangentImpulse1;
                        point.TangentImpulse1 = newTangent1;
                        ApplyImpulse(manifold, point, 0f, applied1, 0f);
                    }

                    if (Mode == AuraPhysicsMode.Full3D)
                    {
                        relative = VelocityAt(bodyB, rB) - VelocityAt(bodyA, rA);
                        var tangentSpeed2 = AuraVector3.Dot(relative, tangent2) + AuraVector3.Dot(bodyB.SurfaceVelocity - bodyA.SurfaceVelocity, tangent2);
                        var kTangent2 = EffectiveMass(bodyA, bodyB, rA, rB, tangent2);
                        if (kTangent2 > 0f)
                        {
                            var delta2 = -tangentSpeed2 / kTangent2;
                            var maxFriction = manifold.Friction * point.NormalImpulse;
                            var newTangent2 = Math.Clamp(point.TangentImpulse2 + delta2, -maxFriction, maxFriction);
                            var applied2 = newTangent2 - point.TangentImpulse2;
                            point.TangentImpulse2 = newTangent2;
                            ApplyImpulse(manifold, point, 0f, 0f, applied2);
                        }
                    }

                    manifold.Points[pointIndex] = point;
                }
            }
        }

        private void SolvePositions()
        {
            for (var index = 0; index < _manifolds.Count; index++)
            {
                var manifold = _manifolds[index];
                if (manifold.IsTrigger)
                    continue;

                var bodyA = _bodies[manifold.BodyA];
                var bodyB = _bodies[manifold.BodyB];
                var invMassA = bodyA.Awake ? bodyA.InvMass : 0f;
                var invMassB = bodyB.Awake ? bodyB.InvMass : 0f;
                var total = invMassA + invMassB;
                if (total <= 0f)
                    continue;

                for (var pointIndex = 0; pointIndex < manifold.Count; pointIndex++)
                {
                    var penetration = manifold.Points[pointIndex].Penetration;
                    if (penetration <= 0f)
                        continue;

                    /* Clamp position correction like a max-correction setting so a
                       deep first overlap cannot teleport a body. */
                    if (penetration > MaxPositionCorrection)
                        penetration = MaxPositionCorrection;

                    var correction = manifold.Normal * (penetration * _settings.Baumgarte / total);
                    bodyA.Pose = new AuraPose(bodyA.Pose.Position - correction * invMassA, bodyA.Pose.Rotation);
                    bodyB.Pose = new AuraPose(bodyB.Pose.Position + correction * invMassB, bodyB.Pose.Rotation);
                }
            }
        }

        private void SolveJointsVelocity()
        {
            for (var index = 0; index < _joints.Count; index++)
            {
                var joint = _joints[index];
                if (!joint.Occupied)
                    continue;

                var bodyA = _bodies[joint.BodyA];
                var bodyB = _bodies[joint.BodyB];

                switch (joint.Type)
                {
                    case AuraJointType.Distance:
                        SolveDistanceJoint(joint, bodyA, bodyB);
                        break;
                    case AuraJointType.Spring:
                        SolveSpringJoint(joint, bodyA, bodyB);
                        break;
                    case AuraJointType.Slider:
                        SolveSliderJoint(joint, bodyA, bodyB);
                        break;
                    case AuraJointType.Cone:
                    case AuraJointType.SwingTwist:
                        SolvePointJoint(joint, bodyA, bodyB);
                        SolveConeLimit(joint, bodyA, bodyB);
                        break;
                    default:
                        SolvePointJoint(joint, bodyA, bodyB);
                        break;
                }
            }
        }

        private void SolveSliderJoint(ManagedJoint joint, ManagedBody bodyA, ManagedBody bodyB)
        {
            var anchorA = bodyA.Pose.TransformPoint(joint.LocalAnchorA);
            var anchorB = bodyB.Pose.TransformPoint(joint.LocalAnchorB);
            var rA = anchorA - bodyA.Pose.Position;
            var rB = anchorB - bodyB.Pose.Position;
            var axis = bodyA.Pose.Rotation.Rotate(joint.AxisLocalA).Normalized();
            BuildTangents(axis, out var tangent1, out var tangent2);

            var relative = VelocityAt(bodyB, rB) - VelocityAt(bodyA, rA);
            var error = anchorB - anchorA;
            ApplyPointAxis(bodyA, bodyB, rA, rB, relative, error, tangent1);
            relative = VelocityAt(bodyB, rB) - VelocityAt(bodyA, rA);
            ApplyPointAxis(bodyA, bodyB, rA, rB, relative, error, tangent2);

            if (joint.MotorEnabled)
            {
                relative = VelocityAt(bodyB, rB) - VelocityAt(bodyA, rA);
                var along = AuraVector3.Dot(relative, axis);
                var k = EffectiveMass(bodyA, bodyB, rA, rB, axis);
                if (k > 0f)
                {
                    var impulse = Math.Clamp((joint.MotorTargetVelocity - along) / k, -joint.MaxMotorForce * _stepDelta, joint.MaxMotorForce * _stepDelta);
                    ApplyJointImpulse(bodyA, bodyB, rA, rB, axis * impulse);
                }
            }

            if (joint.EnableLimit)
            {
                var position = AuraVector3.Dot(anchorB - anchorA, axis);
                var correction = position < joint.MinLimit ? position - joint.MinLimit : (position > joint.MaxLimit ? position - joint.MaxLimit : 0f);
                if (MathF.Abs(correction) > Epsilon)
                {
                    var k = EffectiveMass(bodyA, bodyB, rA, rB, axis);
                    if (k > 0f)
                        ApplyJointImpulse(bodyA, bodyB, rA, rB, axis * (-correction * _settings.Baumgarte / k));
                }
            }
        }

        private void SolveSpringJoint(ManagedJoint joint, ManagedBody bodyA, ManagedBody bodyB)
        {
            if (joint.SpringFrequency <= 0f)
            {
                SolveDistanceJoint(joint, bodyA, bodyB);
                return;
            }

            var anchorA = bodyA.Pose.TransformPoint(joint.LocalAnchorA);
            var anchorB = bodyB.Pose.TransformPoint(joint.LocalAnchorB);
            var delta = anchorB - anchorA;
            var length = delta.Length;
            if (length < Epsilon)
                return;

            var direction = delta / length;
            var rA = anchorA - bodyA.Pose.Position;
            var rB = anchorB - bodyB.Pose.Position;
            var speed = AuraVector3.Dot(VelocityAt(bodyB, rB) - VelocityAt(bodyA, rA), direction);
            var error = length - joint.Distance;
            var omega = 2f * MathF.PI * joint.SpringFrequency;
            var force = omega * omega * error - 2f * joint.SpringDamping * omega * speed;
            var k = EffectiveMass(bodyA, bodyB, rA, rB, direction);
            if (k <= 0f)
                return;

            ApplyJointImpulse(bodyA, bodyB, rA, rB, direction * (force * _stepDelta / k));
        }

        private void SolveConeLimit(ManagedJoint joint, ManagedBody bodyA, ManagedBody bodyB)
        {
            var limit = joint.MaxLimit > 0f ? joint.MaxLimit : (joint.SwingLimit > 0f ? joint.SwingLimit : 0f);
            if (limit <= 0f)
                return;

            var axisA = bodyA.Pose.Rotation.Rotate(joint.AxisLocalA).Normalized();
            var axisB = bodyB.Pose.Rotation.Rotate(joint.AxisLocalB).Normalized();
            var dot = Math.Clamp(AuraVector3.Dot(axisA, axisB), -1f, 1f);
            var angle = MathF.Acos(dot);
            if (angle <= limit)
                return;

            var rotationAxis = AuraVector3.Cross(axisA, axisB);
            if (rotationAxis.LengthSquared < Epsilon)
                return;

            rotationAxis = rotationAxis.Normalized();
            var k = AuraVector3.Dot(ApplyInvInertia(bodyA, rotationAxis) + ApplyInvInertia(bodyB, rotationAxis), rotationAxis);
            if (k <= 0f)
                return;

            ApplyJointAngularImpulse(bodyA, bodyB, rotationAxis * (-(angle - limit) * _settings.Baumgarte / k));
        }

        private void SolveDistanceJoint(ManagedJoint joint, ManagedBody bodyA, ManagedBody bodyB)
        {
            var anchorA = bodyA.Pose.TransformPoint(joint.LocalAnchorA);
            var anchorB = bodyB.Pose.TransformPoint(joint.LocalAnchorB);
            var delta = anchorB - anchorA;
            var length = delta.Length;
            if (length < Epsilon)
                return;

            var direction = delta / length;
            var rA = anchorA - bodyA.Pose.Position;
            var rB = anchorB - bodyB.Pose.Position;
            var relative = VelocityAt(bodyB, rB) - VelocityAt(bodyA, rA);
            var speed = AuraVector3.Dot(relative, direction);
            var error = length - joint.Distance;
            var k = EffectiveMass(bodyA, bodyB, rA, rB, direction);
            if (k <= 0f)
                return;

            var impulse = (-speed - _settings.Baumgarte * error) / k;
            joint.Accumulated += direction * impulse;
            ApplyJointImpulse(bodyA, bodyB, rA, rB, direction * impulse);
        }

        private void SolvePointJoint(ManagedJoint joint, ManagedBody bodyA, ManagedBody bodyB)
        {
            var anchorA = bodyA.Pose.TransformPoint(joint.LocalAnchorA);
            var anchorB = bodyB.Pose.TransformPoint(joint.LocalAnchorB);
            var rA = anchorA - bodyA.Pose.Position;
            var rB = anchorB - bodyB.Pose.Position;
            var relative = VelocityAt(bodyB, rB) - VelocityAt(bodyA, rA);
            var error = anchorB - anchorA;

            var axisX = AuraVector3.UnitX;
            var axisY = AuraVector3.UnitY;
            var axisZ = AuraVector3.UnitZ;
            ApplyPointAxis(bodyA, bodyB, rA, rB, relative, error, axisX);
            ApplyPointAxis(bodyA, bodyB, rA, rB, relative, error, axisY);
            if (Mode == AuraPhysicsMode.Full3D)
                ApplyPointAxis(bodyA, bodyB, rA, rB, relative, error, axisZ);

            if (joint.Type == AuraJointType.Hinge)
                SolveHingeAxis(joint, bodyA, bodyB);
        }

        private void SolveHingeAxis(ManagedJoint joint, ManagedBody bodyA, ManagedBody bodyB)
        {
            var axisA = bodyA.Pose.Rotation.Rotate(joint.AxisLocalA).Normalized();
            var axisB = bodyB.Pose.Rotation.Rotate(joint.AxisLocalB).Normalized();
            var axis = (axisA + axisB).Normalized();
            BuildTangents(axis, out var tangent1, out var tangent2);

            var error1 = AuraVector3.Dot(AuraVector3.Cross(axisA, axisB), tangent1);
            var error2 = AuraVector3.Dot(AuraVector3.Cross(axisA, axisB), tangent2);

            var angularA = ApplyInvInertia(bodyA, tangent1);
            var angularB = ApplyInvInertia(bodyB, tangent1);
            var k1 = AuraVector3.Dot(angularA + angularB, tangent1);
            if (k1 > Epsilon)
                ApplyJointAngularImpulse(bodyA, bodyB, tangent1 * (-error1 / k1));

            angularA = ApplyInvInertia(bodyA, tangent2);
            angularB = ApplyInvInertia(bodyB, tangent2);
            var k2 = AuraVector3.Dot(angularA + angularB, tangent2);
            if (k2 > Epsilon)
                ApplyJointAngularImpulse(bodyA, bodyB, tangent2 * (-error2 / k2));
        }

        private void ApplyPointAxis(ManagedBody bodyA, ManagedBody bodyB, AuraVector3 rA, AuraVector3 rB, AuraVector3 relative, AuraVector3 error, AuraVector3 axis)
        {
            var speed = AuraVector3.Dot(relative, axis);
            var k = EffectiveMass(bodyA, bodyB, rA, rB, axis);
            if (k <= 0f)
                return;

            var bias = _settings.Baumgarte * AuraVector3.Dot(error, axis);
            var impulse = (-speed - bias) / k;
            ApplyJointImpulse(bodyA, bodyB, rA, rB, axis * impulse);
        }

        private void ApplyJointImpulse(ManagedBody bodyA, ManagedBody bodyB, AuraVector3 rA, AuraVector3 rB, AuraVector3 impulse)
        {
            if (bodyA.BodyType == AuraBodyType.Dynamic && bodyA.Awake)
            {
                bodyA.LinearVelocity -= impulse * bodyA.InvMass;
                bodyA.AngularVelocity -= ApplyInvInertia(bodyA, AuraVector3.Cross(rA, impulse));
            }

            if (bodyB.BodyType == AuraBodyType.Dynamic && bodyB.Awake)
            {
                bodyB.LinearVelocity += impulse * bodyB.InvMass;
                bodyB.AngularVelocity += ApplyInvInertia(bodyB, AuraVector3.Cross(rB, impulse));
            }
        }

        private void ApplyJointAngularImpulse(ManagedBody bodyA, ManagedBody bodyB, AuraVector3 impulse)
        {
            if (bodyA.BodyType == AuraBodyType.Dynamic && bodyA.Awake)
                bodyA.AngularVelocity -= ApplyInvInertia(bodyA, impulse);
            if (bodyB.BodyType == AuraBodyType.Dynamic && bodyB.Awake)
                bodyB.AngularVelocity += ApplyInvInertia(bodyB, impulse);
        }

        private static AuraVector3 VelocityAt(ManagedBody body, AuraVector3 r) =>
            body.LinearVelocity + AuraVector3.Cross(body.AngularVelocity, r);

        private static float EffectiveMass(ManagedBody bodyA, ManagedBody bodyB, AuraVector3 rA, AuraVector3 rB, AuraVector3 axis)
        {
            var invMassA = bodyA.BodyType == AuraBodyType.Dynamic && bodyA.Awake ? bodyA.InvMass : 0f;
            var invMassB = bodyB.BodyType == AuraBodyType.Dynamic && bodyB.Awake ? bodyB.InvMass : 0f;
            var k = invMassA + invMassB;
            if (invMassA > 0f)
                k += AuraVector3.Dot(AuraVector3.Cross(ApplyInvInertia(bodyA, AuraVector3.Cross(rA, axis)), rA), axis);
            if (invMassB > 0f)
                k += AuraVector3.Dot(AuraVector3.Cross(ApplyInvInertia(bodyB, AuraVector3.Cross(rB, axis)), rB), axis);
            return k;
        }

        private static AuraVector3 ApplyInvInertia(ManagedBody body, AuraVector3 value)
        {
            var local = body.Pose.Rotation.InverseRotate(value);
            local = new AuraVector3(local.X * body.InvInertiaLocal.X, local.Y * body.InvInertiaLocal.Y, local.Z * body.InvInertiaLocal.Z);
            return body.Pose.Rotation.Rotate(local);
        }

        private void ApplyImpulse(ManagedManifold manifold, ManagedContactPoint point, float normalImpulse, float tangent1Impulse, float tangent2Impulse)
        {
            var bodyA = _bodies[manifold.BodyA];
            var bodyB = _bodies[manifold.BodyB];
            BuildTangents(manifold.Normal, out var tangent1, out var tangent2);
            var impulse = manifold.Normal * normalImpulse + tangent1 * tangent1Impulse + tangent2 * tangent2Impulse;
            var rA = point.Position - bodyA.Pose.Position;
            var rB = point.Position - bodyB.Pose.Position;

            if (bodyA.BodyType == AuraBodyType.Dynamic && bodyA.Awake)
            {
                bodyA.LinearVelocity -= impulse * bodyA.InvMass;
                bodyA.AngularVelocity -= ApplyInvInertia(bodyA, AuraVector3.Cross(rA, impulse));
            }

            if (bodyB.BodyType == AuraBodyType.Dynamic && bodyB.Awake)
            {
                bodyB.LinearVelocity += impulse * bodyB.InvMass;
                bodyB.AngularVelocity += ApplyInvInertia(bodyB, AuraVector3.Cross(rB, impulse));
            }
        }

        private void BuildTangents(AuraVector3 normal, out AuraVector3 tangent1, out AuraVector3 tangent2)
        {
            if (Mode == AuraPhysicsMode.Plane2D)
            {
                tangent1 = new AuraVector3(-normal.Y, normal.X, 0f).Normalized();
                tangent2 = AuraVector3.UnitZ;
                return;
            }

            var reference = MathF.Abs(normal.X) > 0.9f ? AuraVector3.UnitY : AuraVector3.UnitX;
            tangent1 = AuraVector3.Cross(normal, reference).Normalized();
            tangent2 = AuraVector3.Cross(normal, tangent1);
        }

        private void UpdateSleep(float deltaTime)
        {
            if (!_settings.AllowSleep)
                return;

            for (var index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || body.BodyType != AuraBodyType.Dynamic || !body.Awake || !body.AllowSleeping)
                    continue;

                if (body.LinearVelocity.LengthSquared < _settings.RestingVelocityThreshold * _settings.RestingVelocityThreshold &&
                    body.AngularVelocity.LengthSquared < _settings.AngularSleepThreshold * _settings.AngularSleepThreshold)
                {
                    body.SleepTimer += deltaTime;
                    if (body.SleepTimer >= _settings.TimeToSleep)
                    {
                        body.Awake = false;
                        body.LinearVelocity = AuraVector3.Zero;
                        body.AngularVelocity = AuraVector3.Zero;
                    }
                }
                else
                {
                    body.SleepTimer = 0f;
                }
            }
        }

        private void EmitEvents()
        {
            _events.Clear();
            _enterKeys.Clear();
            _exitKeys.Clear();

            foreach (var key in _touching)
                if (!_previousTouching.Contains(key))
                    _enterKeys.Add(key);

            foreach (var key in _previousTouching)
                if (!_touching.Contains(key))
                    _exitKeys.Add(key);

            _enterKeys.Sort();
            _exitKeys.Sort();

            for (var index = 0; index < _enterKeys.Count; index++)
            {
                var key = _enterKeys[index];
                var manifold = FindManifold(key);
                if (manifold != null)
                    _events.Add(ToEvent(manifold, manifold.IsTrigger ? AuraPhysicsEventType.TriggerEnter : AuraPhysicsEventType.CollisionEnter));
            }

            for (var index = 0; index < _exitKeys.Count; index++)
            {
                var key = _exitKeys[index];
                if (_cache.TryGetValue(key, out var manifold))
                    _events.Add(ToEvent(manifold, manifold.IsTrigger ? AuraPhysicsEventType.TriggerExit : AuraPhysicsEventType.CollisionExit));
            }

            _cache.Clear();
            foreach (var manifold in _manifolds)
                _cache[Key(manifold.BodyA, manifold.BodyB)] = manifold;

            _previousTouching.Clear();
            foreach (var key in _touching)
                _previousTouching.Add(key);
        }

        private ManagedManifold FindManifold(long key)
        {
            for (var index = 0; index < _manifolds.Count; index++)
                if (Key(_manifolds[index].BodyA, _manifolds[index].BodyB) == key)
                    return _manifolds[index];
            return null;
        }

        private static AuraPhysicsEvent ToEvent(ManagedManifold manifold, AuraPhysicsEventType type)
        {
            var point = manifold.Count > 0 ? manifold.Points[0].Position : AuraVector3.Zero;
            return new AuraPhysicsEvent(
                type,
                SimulationEntityId.None,
                SimulationEntityId.None,
                new PhysicsBodyId(manifold.BodyA, 0),
                new PhysicsBodyId(manifold.BodyB, 0),
                new PhysicsShapeId(manifold.BodyA, 0),
                new PhysicsShapeId(manifold.BodyB, 0),
                point,
                manifold.Normal,
                manifold.Count > 0 ? manifold.Points[0].NormalImpulse : 0f);
        }

        private int Overlap(in ManagedShapeView query, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            var count = 0;
            for (var index = 0; index < _bodies.Count && count < results.Length; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || !PassesFilter(body, filter))
                    continue;

                var bodyId = new PhysicsBodyId(index, body.Generation);
                for (var shapeIndex = 0; shapeIndex < body.Shapes.Length && count < results.Length; shapeIndex++)
                {
                    var shape = body.Shapes[shapeIndex];
                    if (shape.IsTrigger && filter.FiltersTriggers(true, true))
                        continue;

                    var view = ManagedShapeView.FromBodyShape(body, shape);
                    var manifold = new ManagedManifold();
                    if (!ManagedCollision.Contact(query, view, Mode, manifold) || manifold.Count == 0)
                        continue;

                    results[count++] = new AuraPhysicsQueryHit(
                        SimulationEntityId.None,
                        bodyId,
                        ShapeId(index, shapeIndex, body.Generation),
                        manifold.Points[0].Penetration,
                        manifold.Points[0].Position,
                        manifold.Normal);
                }
            }

            return count;
        }

        private bool Sweep(in ManagedShapeView query, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit)
        {
            hit = default;
            if (maxDistance <= 0f)
                return false;

            var directionNormalized = direction.Normalized();
            if (directionNormalized.LengthSquared <= Epsilon)
                return false;

            var found = false;
            var bestDistance = float.MaxValue;

            for (var index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || !PassesFilter(body, filter))
                    continue;

                if (!TrySweepBody(query, directionNormalized, maxDistance, index, body, filter, out var distance, out var shapeIndex))
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    hit = new AuraPhysicsQueryHit(
                        SimulationEntityId.None,
                        new PhysicsBodyId(index, body.Generation),
                        ShapeId(index, shapeIndex, body.Generation),
                        distance,
                        query.Translated(directionNormalized * distance).Center,
                        -directionNormalized);
                    found = true;
                }
            }

            return found;
        }

        private int SweepAll(in ManagedShapeView query, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results)
        {
            if (maxDistance <= 0f)
                return 0;

            var directionNormalized = direction.Normalized();
            if (directionNormalized.LengthSquared <= Epsilon)
                return 0;

            var count = 0;
            for (var index = 0; index < _bodies.Count && count < results.Length; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || !PassesFilter(body, filter))
                    continue;

                if (!TrySweepBody(query, directionNormalized, maxDistance, index, body, filter, out var distance, out var shapeIndex))
                    continue;

                results[count++] = new AuraPhysicsQueryHit(
                    SimulationEntityId.None,
                    new PhysicsBodyId(index, body.Generation),
                    ShapeId(index, shapeIndex, body.Generation),
                    distance,
                    query.Translated(directionNormalized * distance).Center,
                    -directionNormalized);
            }

            for (var i = 1; i < count; i++)
            {
                var value = results[i];
                var j = i - 1;
                while (j >= 0 && results[j].Distance > value.Distance)
                {
                    results[j + 1] = results[j];
                    j--;
                }

                results[j + 1] = value;
            }

            return count;
        }

        private bool TrySweepBody(in ManagedShapeView query, AuraVector3 direction, float maxDistance, int index, ManagedBody body, in AuraPhysicsQueryFilter filter, out float distance, out int shapeIndex)
        {
            distance = 0f;
            shapeIndex = -1;
            var step = maxDistance / SweepSteps;
            var found = false;
            var best = float.MaxValue;

            for (var shape = 0; shape < body.Shapes.Length; shape++)
            {
                var bodyShape = body.Shapes[shape];
                if (bodyShape.IsTrigger && filter.FiltersTriggers(true, true))
                    continue;

                var view = ManagedShapeView.FromBodyShape(body, bodyShape);
                for (var sample = 0; sample <= SweepSteps; sample++)
                {
                    var t = sample * step;
                    var manifold = new ManagedManifold();
                    if (!ManagedCollision.Contact(query.Translated(direction * t), view, Mode, manifold) || manifold.Count == 0)
                        continue;

                    if (t < best)
                    {
                        best = t;
                        shapeIndex = shape;
                        found = true;
                    }

                    break;
                }
            }

            distance = best;
            return found;
        }

        private bool CanInteract(ManagedBody a, ManagedBody b)
        {
            if (!_matrix.CanCollide(a.Layer, b.Layer))
                return false;
            if (!a.CollisionMask.Includes(b.Layer) || !b.CollisionMask.Includes(a.Layer))
                return false;
            if (a.GroupIndex != 0 && a.GroupIndex == b.GroupIndex)
                return a.GroupIndex > 0;
            return true;
        }

        private static PhysicsShapeId ShapeId(int bodyIndex, int shapeIndex, int generation) =>
            new PhysicsShapeId(bodyIndex * ShapesPerBody + shapeIndex, generation);

        private static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        private bool PassesFilter(ManagedBody body, in AuraPhysicsQueryFilter filter)
        {
            if (!filter.LayerMask.Includes(body.Layer))
                return false;

            if ((filter.Flags & AuraQueryFlags.IgnoreSelf) != 0 &&
                filter.IgnoredBody.IsValid &&
                filter.IgnoredBody.Index >= 0 &&
                filter.IgnoredBody.Index < _bodies.Count &&
                ReferenceEquals(_bodies[filter.IgnoredBody.Index], body))
                return false;

            return true;
        }

        private bool TryGetBody(PhysicsBodyId id, out ManagedBody body)
        {
            if (id.IsValid && id.Index < _bodies.Count)
            {
                var candidate = _bodies[id.Index];
                if (candidate.Occupied && candidate.Generation == id.Generation)
                {
                    body = candidate;
                    return true;
                }
            }

            body = null;
            return false;
        }

        private bool TryGetJoint(AuraJointId id, out ManagedJoint joint)
        {
            if (id.IsValid && id.Index < _joints.Count)
            {
                var candidate = _joints[id.Index];
                if (candidate.Occupied && candidate.Generation == id.Generation)
                {
                    joint = candidate;
                    return true;
                }
            }

            joint = null;
            return false;
        }
    }
}
