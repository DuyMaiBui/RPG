using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Tests
{
    public sealed class FakePhysicsWorld : IPhysicsWorld, IPhysicsJoints, IPhysicsCharacters, IPhysicsContacts, IPhysicsSerialization
    {
        private readonly List<AuraPhysicsEvent> _events;
        private readonly List<FakePhysicsBody> _bodies = new List<FakePhysicsBody>();
        private readonly List<int> _freeSlots = new List<int>();
        private readonly AuraVector3 _gravity;

        public FakePhysicsWorld(in AuraWorldDefinition definition, List<AuraPhysicsEvent> events)
        {
            _events = events;
            _gravity = definition.Gravity;
        }

        AuraPhysicsMode IPhysicsWorld.Mode => AuraPhysicsMode.Full3D;

        AuraPhysicsCapabilities IPhysicsWorld.Capabilities =>
            AuraPhysicsCapabilities.BodyStatic |
            AuraPhysicsCapabilities.BodyDynamic |
            AuraPhysicsCapabilities.BodyKinematic |
            AuraPhysicsCapabilities.QueryRaycast |
            AuraPhysicsCapabilities.QueryOverlap;

        int IPhysicsWorld.BodyCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _bodies.Count; index++)
                {
                    if (_bodies[index].Occupied)
                        count++;
                }

                return count;
            }
        }

        int IPhysicsEventSource.PendingEventCount => _events.Count;

        PhysicsBodyId IPhysicsWorld.CreateBody(in AuraPhysicsBodyDefinition definition)
        {
            if (definition.Validate() != AuraResult.Success)
                return PhysicsBodyId.Invalid;

            var body = new FakePhysicsBody
            {
                Occupied = true,
                Type = definition.Type,
                Pose = definition.InitialPose,
                LinearVelocity = definition.InitialLinearVelocity,
                AngularVelocity = definition.InitialAngularVelocity,
                Mass = definition.Mass,
                IsAwake = definition.Type != AuraBodyType.Static,
                ShapeType = definition.Shapes[0].Type,
                Radius = definition.Shapes[0].Geometry.Radius,
                HalfExtents = definition.Shapes[0].Geometry.HalfExtents,
                IsTrigger = definition.Shapes[0].IsTrigger,
            };

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
            if (!TryGetSlot(body, out var index))
                return AuraResult.InvalidHandle;

            var slot = _bodies[index];
            slot.Occupied = false;
            slot.Generation++;
            _bodies[index] = slot;
            _freeSlots.Add(index);
            return AuraResult.Success;
        }

        bool IPhysicsWorld.HasBody(PhysicsBodyId body) => TryGetSlot(body, out _);

        void IPhysicsWorld.SetKinematicTarget(PhysicsBodyId body, in AuraPose pose)
        {
            if (!TryGetSlot(body, out var index))
                return;

            var slot = _bodies[index];
            slot.Pose = pose;
            _bodies[index] = slot;
        }

        void IPhysicsWorld.ApplyImpulse(PhysicsBodyId body, AuraVector3 impulse)
        {
            if (!TryGetSlot(body, out var index))
                return;

            var slot = _bodies[index];
            slot.LinearVelocity += impulse / slot.Mass;
            _bodies[index] = slot;
        }

        AuraResult IPhysicsWorld.SetSurfaceVelocity(PhysicsBodyId body, AuraVector3 velocity) =>
            TryGetSlot(body, out _) ? AuraResult.Success : AuraResult.InvalidHandle;

        IPhysicsJoints IPhysicsWorld.Joints => this;

        IPhysicsCharacters IPhysicsWorld.Characters => this;

        IPhysicsContacts IPhysicsWorld.Contacts => this;

        IPhysicsSerialization IPhysicsWorld.Serialization => this;

        AuraJointId IPhysicsJoints.CreateJoint(in AuraJointDefinition definition) => AuraJointId.Invalid;

        AuraResult IPhysicsJoints.DestroyJoint(AuraJointId joint) => AuraResult.InvalidHandle;

        bool IPhysicsJoints.HasJoint(AuraJointId joint) => false;

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

        int IPhysicsContacts.CopyContacts(Span<AuraContact> buffer) => 0;

        ulong IPhysicsSerialization.ComputeStateHash() => 0ul;

        byte[] IPhysicsSerialization.SaveState() => System.Array.Empty<byte>();

        void IPhysicsSerialization.RestoreState(byte[] state)
        {
        }

        AuraResult IPhysicsWorld.GetBodyState(PhysicsBodyId body, out AuraBodyState state)
        {
            if (!TryGetSlot(body, out var index))
            {
                state = default;
                return AuraResult.InvalidHandle;
            }

            var slot = _bodies[index];
            state = new AuraBodyState(
                new PhysicsBodyId(index, slot.Generation),
                SimulationEntityId.None,
                slot.Pose,
                slot.LinearVelocity,
                slot.AngularVelocity,
                slot.IsAwake,
                0u);
            return AuraResult.Success;
        }

        int IPhysicsWorld.CopyBodyStates(Span<AuraBodyState> buffer)
        {
            var written = 0;
            for (var index = 0; index < _bodies.Count && written < buffer.Length; index++)
            {
                var slot = _bodies[index];
                if (!slot.Occupied)
                    continue;

                buffer[written++] = new AuraBodyState(
                    new PhysicsBodyId(index, slot.Generation),
                    SimulationEntityId.None,
                    slot.Pose,
                    slot.LinearVelocity,
                    slot.AngularVelocity,
                    slot.IsAwake,
                    0u);
            }

            return written;
        }

        void IPhysicsWorld.Step(float deltaTime)
        {
            for (var index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || body.Type != AuraBodyType.Dynamic)
                    continue;

                body.LinearVelocity += _gravity * deltaTime;
                body.Pose = new AuraPose(body.Pose.Position + body.LinearVelocity * deltaTime, body.Pose.Rotation);
                _bodies[index] = body;
            }
        }

        bool IPhysicsQuery.Raycast(
            in AuraRay ray,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            var bestDistance = float.MaxValue;
            var found = false;

            for (var index = 0; index < _bodies.Count; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || body.ShapeType != AuraShapeType.Sphere)
                    continue;

                if (IntersectSphere(ray, body, maxDistance, out var distance, out var point))
                {
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        hit = new AuraPhysicsQueryHit(
                            SimulationEntityId.None,
                            new PhysicsBodyId(index, body.Generation),
                            new PhysicsShapeId(index, body.Generation),
                            distance,
                            point,
                            (point - body.Pose.Position).Normalized());
                        found = true;
                    }
                }
            }

            return found;
        }

        int IPhysicsQuery.RaycastAll(
            in AuraRay ray,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results)
        {
            var count = 0;
            for (var index = 0; index < _bodies.Count && count < results.Length; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || body.ShapeType != AuraShapeType.Sphere)
                    continue;

                if (IntersectSphere(ray, body, maxDistance, out var distance, out var point))
                {
                    results[count++] = new AuraPhysicsQueryHit(
                        SimulationEntityId.None,
                        new PhysicsBodyId(index, body.Generation),
                        new PhysicsShapeId(index, body.Generation),
                        distance,
                        point,
                        (point - body.Pose.Position).Normalized());
                }
            }

            return count;
        }

        bool IPhysicsQuery.SphereCast(
            AuraVector3 origin,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            return false;
        }

        int IPhysicsQuery.SphereCastAll(
            AuraVector3 origin,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        bool IPhysicsQuery.BoxCast(
            AuraVector3 center,
            AuraVector3 halfExtents,
            AuraQuaternion rotation,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            return false;
        }

        int IPhysicsQuery.BoxCastAll(
            AuraVector3 center,
            AuraVector3 halfExtents,
            AuraQuaternion rotation,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        bool IPhysicsQuery.CapsuleCast(
            AuraVector3 pointA,
            AuraVector3 pointB,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            return false;
        }

        int IPhysicsQuery.CapsuleCastAll(
            AuraVector3 pointA,
            AuraVector3 pointB,
            float radius,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        bool IPhysicsQuery.ShapeCast(
            in AuraPhysicsShapeDefinition shape,
            AuraPose pose,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            out AuraPhysicsQueryHit hit)
        {
            hit = default;
            return false;
        }

        int IPhysicsQuery.ShapeCastAll(
            in AuraPhysicsShapeDefinition shape,
            AuraPose pose,
            AuraVector3 direction,
            float maxDistance,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsQuery.OverlapPoint(
            AuraVector3 point,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results)
        {
            var count = 0;
            for (var index = 0; index < _bodies.Count && count < results.Length; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || body.ShapeType != AuraShapeType.Sphere)
                    continue;

                if ((point - body.Pose.Position).Length <= body.Radius)
                    results[count++] = MakeOverlapHit(index, body, 0f);
            }

            return count;
        }

        int IPhysicsQuery.OverlapSphere(
            AuraVector3 center,
            float radius,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results)
        {
            var count = 0;
            for (var index = 0; index < _bodies.Count && count < results.Length; index++)
            {
                var body = _bodies[index];
                if (!body.Occupied || body.ShapeType != AuraShapeType.Sphere)
                    continue;

                var delta = center - body.Pose.Position;
                var sum = radius + body.Radius;
                if (delta.LengthSquared <= sum * sum)
                    results[count++] = MakeOverlapHit(index, body, delta.Length);
            }

            return count;
        }

        int IPhysicsQuery.OverlapBox(
            AuraVector3 center,
            AuraVector3 halfExtents,
            AuraQuaternion rotation,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsQuery.OverlapCapsule(
            AuraVector3 pointA,
            AuraVector3 pointB,
            float radius,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsQuery.OverlapShape(
            in AuraPhysicsShapeDefinition shape,
            AuraPose pose,
            in AuraPhysicsQueryFilter filter,
            Span<AuraPhysicsQueryHit> results) => 0;

        int IPhysicsEventSource.CopyEvents(Span<AuraPhysicsEvent> buffer)
        {
            var count = Math.Min(_events.Count, buffer.Length);
            for (var index = 0; index < count; index++)
                buffer[index] = _events[index];

            _events.RemoveRange(0, count);
            return count;
        }

        void IDisposable.Dispose()
        {
            _bodies.Clear();
            _freeSlots.Clear();
        }

        private static AuraPhysicsQueryHit MakeOverlapHit(int index, FakePhysicsBody body, float distance) =>
            new AuraPhysicsQueryHit(
                SimulationEntityId.None,
                new PhysicsBodyId(index, body.Generation),
                new PhysicsShapeId(index, body.Generation),
                distance,
                body.Pose.Position,
                AuraVector3.Zero);

        private static bool IntersectSphere(
            in AuraRay ray,
            FakePhysicsBody body,
            float maxDistance,
            out float distance,
            out AuraVector3 point)
        {
            distance = 0f;
            point = default;

            var originToCenter = ray.Origin - body.Pose.Position;
            var b = AuraVector3.Dot(originToCenter, ray.Direction);
            var c = AuraVector3.Dot(originToCenter, originToCenter) - body.Radius * body.Radius;
            var discriminant = b * b - c;
            if (discriminant < 0f)
                return false;

            var root = MathF.Sqrt(discriminant);
            var t = -b - root;
            if (t < 0f)
                t = -b + root;

            if (t < 0f || t > maxDistance)
                return false;

            distance = t;
            point = ray.GetPoint(t);
            return true;
        }

        private bool TryGetSlot(PhysicsBodyId body, out int index)
        {
            index = body.Index;
            return body.IsValid &&
                   index < _bodies.Count &&
                   _bodies[index].Occupied &&
                   _bodies[index].Generation == body.Generation;
        }
    }
}
