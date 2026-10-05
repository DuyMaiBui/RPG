using System;
using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Physics;

namespace AuraEngine.Tests
{
    public sealed class RecordingPhysicsWorld : IPhysicsWorld
    {
        private readonly IPhysicsWorld _inner;
        private readonly RecordingPhysicsJoints _joints;

        public RecordingPhysicsWorld(IPhysicsWorld inner, List<AuraJointDefinition> definitions)
        {
            _inner = inner;
            _joints = new RecordingPhysicsJoints(inner.Joints, definitions);
        }

        void IDisposable.Dispose() => _inner.Dispose();

        AuraPhysicsMode IPhysicsWorld.Mode => _inner.Mode;
        AuraPhysicsCapabilities IPhysicsWorld.Capabilities => _inner.Capabilities;
        int IPhysicsWorld.BodyCount => _inner.BodyCount;
        IPhysicsJoints IPhysicsWorld.Joints => _joints;
        IPhysicsBodyControl IPhysicsWorld.BodyControl => _inner.BodyControl;
        IPhysicsJointControl IPhysicsWorld.JointControl => _inner.JointControl;
        IPhysicsForceFields IPhysicsWorld.ForceFields => _inner.ForceFields;
        IPhysicsCharacters IPhysicsWorld.Characters => _inner.Characters;
        IPhysicsVehicles IPhysicsWorld.Vehicles => _inner.Vehicles;
        IPhysicsSoftBodies IPhysicsWorld.SoftBodies => _inner.SoftBodies;
        IPhysicsRagdolls IPhysicsWorld.Ragdolls => _inner.Ragdolls;
        IPhysicsHair IPhysicsWorld.Hair => _inner.Hair;
        IPhysicsWater IPhysicsWorld.Water => _inner.Water;
        IPhysicsContacts IPhysicsWorld.Contacts => _inner.Contacts;
        IPhysicsSerialization IPhysicsWorld.Serialization => _inner.Serialization;

        PhysicsBodyId IPhysicsWorld.CreateBody(in AuraPhysicsBodyDefinition definition) => _inner.CreateBody(definition);
        AuraResult IPhysicsWorld.DestroyBody(PhysicsBodyId body) => _inner.DestroyBody(body);
        bool IPhysicsWorld.HasBody(PhysicsBodyId body) => _inner.HasBody(body);
        void IPhysicsWorld.SetKinematicTarget(PhysicsBodyId body, in AuraPose pose) => _inner.SetKinematicTarget(body, pose);
        void IPhysicsWorld.ApplyImpulse(PhysicsBodyId body, AuraVector3 impulse) => _inner.ApplyImpulse(body, impulse);
        AuraResult IPhysicsWorld.SetSurfaceVelocity(PhysicsBodyId body, AuraVector3 velocity) => _inner.SetSurfaceVelocity(body, velocity);
        AuraResult IPhysicsWorld.GetBodyState(PhysicsBodyId body, out AuraBodyState state) => _inner.GetBodyState(body, out state);
        int IPhysicsWorld.CopyBodyStates(Span<AuraBodyState> buffer) => _inner.CopyBodyStates(buffer);
        void IPhysicsWorld.Step(float deltaTime) => _inner.Step(deltaTime);

        AuraWaterId IPhysicsWorld.CreateWater(in AuraWaterDefinition definition) => _inner.CreateWater(definition);
        AuraResult IPhysicsWorld.DestroyWater(AuraWaterId water) => _inner.DestroyWater(water);
        AuraResult IPhysicsWorld.SetWaterParameters(AuraWaterId water, in AuraWaterDefinition definition) => _inner.SetWaterParameters(water, definition);
        AuraResult IPhysicsWorld.ApplyWaterStep(AuraWaterId water, float deltaTime) => _inner.ApplyWaterStep(water, deltaTime);

        AuraWaterId IPhysicsWater.CreateWater(in AuraWaterDefinition definition) => _inner.CreateWater(definition);
        AuraResult IPhysicsWater.DestroyWater(AuraWaterId water) => _inner.DestroyWater(water);
        AuraResult IPhysicsWater.SetWaterParameters(AuraWaterId water, in AuraWaterDefinition definition) => _inner.SetWaterParameters(water, definition);
        AuraResult IPhysicsWater.ApplyWaterStep(AuraWaterId water, float deltaTime) => _inner.ApplyWaterStep(water, deltaTime);

        int IPhysicsEventSource.PendingEventCount => _inner.PendingEventCount;
        int IPhysicsEventSource.CopyEvents(Span<AuraPhysicsEvent> buffer) => _inner.CopyEvents(buffer);

        bool IPhysicsQuery.Raycast(in AuraRay ray, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit) =>
            _inner.Raycast(ray, maxDistance, filter, out hit);

        int IPhysicsQuery.RaycastAll(in AuraRay ray, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.RaycastAll(ray, maxDistance, filter, results);

        bool IPhysicsQuery.SphereCast(AuraVector3 origin, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit) =>
            _inner.SphereCast(origin, radius, direction, maxDistance, filter, out hit);

        int IPhysicsQuery.SphereCastAll(AuraVector3 origin, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.SphereCastAll(origin, radius, direction, maxDistance, filter, results);

        bool IPhysicsQuery.CapsuleCast(AuraVector3 pointA, AuraVector3 pointB, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit) =>
            _inner.CapsuleCast(pointA, pointB, radius, direction, maxDistance, filter, out hit);

        int IPhysicsQuery.CapsuleCastAll(AuraVector3 pointA, AuraVector3 pointB, float radius, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.CapsuleCastAll(pointA, pointB, radius, direction, maxDistance, filter, results);

        bool IPhysicsQuery.BoxCast(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit) =>
            _inner.BoxCast(center, halfExtents, rotation, direction, maxDistance, filter, out hit);

        int IPhysicsQuery.BoxCastAll(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.BoxCastAll(center, halfExtents, rotation, direction, maxDistance, filter, results);

        bool IPhysicsQuery.ShapeCast(in AuraPhysicsShapeDefinition shape, AuraPose pose, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, out AuraPhysicsQueryHit hit) =>
            _inner.ShapeCast(shape, pose, direction, maxDistance, filter, out hit);

        int IPhysicsQuery.ShapeCastAll(in AuraPhysicsShapeDefinition shape, AuraPose pose, AuraVector3 direction, float maxDistance, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.ShapeCastAll(shape, pose, direction, maxDistance, filter, results);

        int IPhysicsQuery.OverlapPoint(AuraVector3 point, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.OverlapPoint(point, filter, results);

        int IPhysicsQuery.OverlapSphere(AuraVector3 center, float radius, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.OverlapSphere(center, radius, filter, results);

        int IPhysicsQuery.OverlapBox(AuraVector3 center, AuraVector3 halfExtents, AuraQuaternion rotation, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.OverlapBox(center, halfExtents, rotation, filter, results);

        int IPhysicsQuery.OverlapCapsule(AuraVector3 pointA, AuraVector3 pointB, float radius, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.OverlapCapsule(pointA, pointB, radius, filter, results);

        int IPhysicsQuery.OverlapShape(in AuraPhysicsShapeDefinition shape, AuraPose pose, in AuraPhysicsQueryFilter filter, Span<AuraPhysicsQueryHit> results) =>
            _inner.OverlapShape(shape, pose, filter, results);
    }
}
