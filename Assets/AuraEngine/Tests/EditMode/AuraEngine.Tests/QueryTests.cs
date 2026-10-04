using AuraEngine.Core;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class QueryTests
    {
        [Test]
        public void Raycast_MapsHitBackToEntity()
        {
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition());
            var entity = world.CreateEntity();
            var body = world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, 0f, 10f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(1f)));

            var ray = new AuraRay(AuraVector3.Zero, AuraVector3.UnitZ);

            Assert.IsTrue(world.Raycast(ray, 100f, AuraPhysicsQueryFilter.All, out var hit));
            Assert.AreEqual(entity, hit.Entity);
            Assert.AreEqual(body, hit.Body);
            Assert.AreEqual(9f, hit.Distance, 0.001f);
        }

        [Test]
        public void Raycast_WithNoBodies_Misses()
        {
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition());

            Assert.IsFalse(world.Raycast(
                new AuraRay(AuraVector3.Zero, AuraVector3.UnitZ),
                100f,
                AuraPhysicsQueryFilter.All,
                out _));
        }

        [Test]
        public void OverlapSphere_MapsHitsBackToEntities()
        {
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition());
            var entity = world.CreateEntity();
            world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, 0f, 10f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(1f)));

            var results = new AuraPhysicsQueryHit[4];
            var count = world.OverlapSphere(new AuraVector3(0f, 0f, 10.5f), 1f, AuraPhysicsQueryFilter.All, results);

            Assert.AreEqual(1, count);
            Assert.AreEqual(entity, results[0].Entity);
        }

        [Test]
        public void PendingEvents_AreMappedAndConsumedOnce()
        {
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition());
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(
                entityA,
                AuraPhysicsBodyDefinition.CreateStatic(
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(1f)));

            backend.EnqueueEvent(new AuraPhysicsEvent(
                AuraPhysicsEventType.CollisionEnter,
                SimulationEntityId.None,
                SimulationEntityId.None,
                bodyA,
                PhysicsBodyId.Invalid,
                PhysicsShapeId.Invalid,
                PhysicsShapeId.Invalid,
                AuraVector3.Zero,
                AuraVector3.UnitY,
                1f));

            world.Step(new SimulationStep(new SimulationTick(1), 1f / 60f));

            var events = new AuraPhysicsEvent[4];
            var count = world.CopyEvents(events);

            Assert.AreEqual(1, count);
            Assert.AreEqual(entityA, events[0].EntityA);

            world.Step(new SimulationStep(new SimulationTick(2), 1f / 60f));
            Assert.AreEqual(0, world.CopyEvents(events));
        }
    }
}
