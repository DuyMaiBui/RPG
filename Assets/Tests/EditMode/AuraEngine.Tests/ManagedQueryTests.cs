using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedQueryTests
    {
        private static AuraSimulationWorld CreateWorld(out ManagedPhysicsBackend backend)
        {
            backend = new ManagedPhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
        }

        private static AuraPhysicsBodyDefinition StaticSphere(AuraVector3 position, float radius) =>
            AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(position, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Sphere(radius));

        private static AuraPhysicsBodyDefinition StaticBox(AuraVector3 position, AuraVector3 halfExtents) =>
            AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(position, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(halfExtents));

        [Test]
        public void SphereCast_HitsTarget()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), StaticSphere(new AuraVector3(0f, 0f, 0f), 1f));

            var hit = world.Queries.SphereCast(
                new AuraVector3(-5f, 0f, 0f),
                0.25f,
                AuraVector3.UnitX,
                10f,
                AuraPhysicsQueryFilter.All,
                out var result);

            Assert.IsTrue(hit);
            Assert.Less(System.MathF.Abs(result.Distance - 3.75f), 0.35f);
        }

        [Test]
        public void CapsuleCast_HitsTarget()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), StaticBox(AuraVector3.Zero, new AuraVector3(1f, 1f, 1f)));

            var hit = world.Queries.CapsuleCast(
                new AuraVector3(-5f, 1f, 0f),
                new AuraVector3(-5f, -1f, 0f),
                0.25f,
                AuraVector3.UnitX,
                10f,
                AuraPhysicsQueryFilter.All,
                out var result);

            Assert.IsTrue(hit);
            Assert.Less(System.MathF.Abs(result.Distance - 3.75f), 0.35f);
        }

        [Test]
        public void BoxCast_HitsTarget()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), StaticBox(AuraVector3.Zero, new AuraVector3(1f, 1f, 1f)));

            var hit = world.Queries.BoxCast(
                new AuraVector3(-5f, 0f, 0f),
                new AuraVector3(0.5f, 0.5f, 0.5f),
                AuraQuaternion.Identity,
                AuraVector3.UnitX,
                10f,
                AuraPhysicsQueryFilter.All,
                out var result);

            Assert.IsTrue(hit);
            Assert.Less(System.MathF.Abs(result.Distance - 3.5f), 0.35f);
        }

        [Test]
        public void ShapeCast_HitsTarget()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), StaticSphere(AuraVector3.Zero, 1f));

            var shape = AuraPhysicsShapeDefinition.Sphere(0.5f);
            var hit = world.Queries.ShapeCast(
                shape,
                new AuraPose(new AuraVector3(-5f, 0f, 0f), AuraQuaternion.Identity),
                AuraVector3.UnitX,
                10f,
                AuraPhysicsQueryFilter.All,
                out var result);

            Assert.IsTrue(hit);
            Assert.Less(System.MathF.Abs(result.Distance - 3.5f), 0.35f);
        }

        [Test]
        public void SphereCastAll_ReturnsEveryTarget()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), StaticSphere(new AuraVector3(-2f, 0f, 0f), 0.5f));
            world.AttachBody(world.CreateEntity(), StaticSphere(new AuraVector3(2f, 0f, 0f), 0.5f));

            var results = new AuraPhysicsQueryHit[8];
            var count = world.Queries.SphereCastAll(
                new AuraVector3(-8f, 0f, 0f),
                0.25f,
                AuraVector3.UnitX,
                20f,
                AuraPhysicsQueryFilter.All,
                results);

            Assert.AreEqual(2, count);
            Assert.Less(results[0].Distance, results[1].Distance);
        }

        [Test]
        public void OverlapQueries_ReturnExpectedCounts()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), StaticSphere(new AuraVector3(2f, 0f, 0f), 0.5f));
            world.AttachBody(world.CreateEntity(), StaticBox(new AuraVector3(-2f, 0f, 0f), new AuraVector3(1f, 1f, 1f)));

            var sphereResults = new AuraPhysicsQueryHit[8];
            Assert.AreEqual(2, world.Queries.OverlapSphere(AuraVector3.Zero, 3f, AuraPhysicsQueryFilter.All, sphereResults));

            var boxResults = new AuraPhysicsQueryHit[8];
            Assert.AreEqual(2, world.Queries.OverlapBox(AuraVector3.Zero, new AuraVector3(3f, 1f, 1f), AuraQuaternion.Identity, AuraPhysicsQueryFilter.All, boxResults));

            var capsuleResults = new AuraPhysicsQueryHit[8];
            Assert.AreEqual(2, world.Queries.OverlapCapsule(new AuraVector3(-3f, 0f, 0f), new AuraVector3(3f, 0f, 0f), 0.5f, AuraPhysicsQueryFilter.All, capsuleResults));

            var shapeResults = new AuraPhysicsQueryHit[8];
            Assert.AreEqual(2, world.Queries.OverlapShape(AuraPhysicsShapeDefinition.Sphere(2f), AuraPose.Identity, AuraPhysicsQueryFilter.All, shapeResults));
        }
    }
}
