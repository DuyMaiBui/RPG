using AuraEngine.Core;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class EntityRegistryTests
    {
        private static AuraSimulationWorld CreateWorld(out FakePhysicsBackend backend)
        {
            backend = new FakePhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
        }

        [Test]
        public void CopyAliveEntities_ReturnsDeterministicOrder()
        {
            using var world = CreateWorld(out _);
            var a = world.CreateEntity();
            var b = world.CreateEntity();
            var c = world.CreateEntity();
            world.DestroyEntity(b);

            var buffer = new SimulationEntityId[4];
            var count = world.Entities.CopyAliveEntities(buffer);

            Assert.AreEqual(2, count);
            Assert.AreEqual(a, buffer[0]);
            Assert.AreEqual(c, buffer[1]);
        }

        [Test]
        public void TryResolveBody_MapsBackToEntity()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();
            var body = world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateStatic(
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(1f)));

            Assert.IsTrue(world.TryResolveEntity(body, out var resolved));
            Assert.AreEqual(entity, resolved);
        }

        [Test]
        public void TryResolveBody_RejectsStaleBody()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();
            var body = world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateStatic(
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(1f)));
            world.DestroyEntity(entity);

            Assert.IsFalse(world.TryResolveEntity(body, out _));
        }
    }
}
