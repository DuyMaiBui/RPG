using AuraEngine.Core;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class QueryEdgeCaseTests
    {
        private static AuraSimulationWorld CreateWorld(out FakePhysicsBackend backend)
        {
            backend = new FakePhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition());
        }

        [Test]
        public void RaycastAll_TruncatesToBufferLength()
        {
            using var world = CreateWorld(out _);
            for (var index = 0; index < 5; index++)
            {
                var entity = world.CreateEntity();
                world.AttachBody(
                    entity,
                    AuraPhysicsBodyDefinition.CreateStatic(
                        new AuraPose(new AuraVector3(0f, 0f, 5f + index), AuraQuaternion.Identity),
                        AuraPhysicsLayer.Default,
                        AuraPhysicsLayerMask.All,
                        AuraPhysicsShapeDefinition.Sphere(0.4f)));
            }

            var results = new AuraPhysicsQueryHit[2];
            var count = world.RaycastAll(
                new AuraRay(AuraVector3.Zero, AuraVector3.UnitZ),
                100f,
                AuraPhysicsQueryFilter.All,
                results);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void OverlapSphere_WithEmptyBuffer_ReturnsZero()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();
            world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateStatic(
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(1f)));

            Assert.AreEqual(0, world.OverlapSphere(AuraVector3.Zero, 10f, AuraPhysicsQueryFilter.All, System.Span<AuraPhysicsQueryHit>.Empty));
        }

        [Test]
        public void LayerMask_NoneAndAll_Behave()
        {
            var layer = new AuraPhysicsLayer(5);

            Assert.IsFalse(AuraPhysicsLayerMask.None.Includes(layer));
            Assert.IsTrue(AuraPhysicsLayerMask.All.Includes(layer));
            Assert.AreEqual(AuraPhysicsLayerMask.None, AuraPhysicsLayerMask.None & AuraPhysicsLayerMask.All);
            Assert.AreEqual(AuraPhysicsLayerMask.All, AuraPhysicsLayerMask.All | AuraPhysicsLayerMask.None);
        }

        [Test]
        public void QueryFilter_ShouldIgnore_FollowsFlags()
        {
            var entity = new SimulationEntityId(3, 1);
            var body = new PhysicsBodyId(3, 1);

            var withoutFlag = new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, flags: AuraQueryFlags.ClosestHit);
            var withFlag = new AuraPhysicsQueryFilter(
                AuraPhysicsLayerMask.All,
                ignoredEntity: entity,
                ignoredBody: body,
                flags: AuraQueryFlags.IgnoreSelf);

            Assert.IsFalse(withoutFlag.ShouldIgnore(entity, body));
            Assert.IsTrue(withFlag.ShouldIgnore(entity, body));
            Assert.IsTrue(withFlag.ShouldIgnore(entity, new PhysicsBodyId(9, 0)));
        }

        [Test]
        public void QueryFilter_FiltersTriggers_RespectsInteraction()
        {
            var collide = new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, AuraTriggerInteraction.Collide);
            var ignore = new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, AuraTriggerInteraction.Ignore);
            var global = new AuraPhysicsQueryFilter(AuraPhysicsLayerMask.All, AuraTriggerInteraction.UseGlobal);

            Assert.IsFalse(collide.FiltersTriggers(true, true));
            Assert.IsTrue(ignore.FiltersTriggers(true, false));
            Assert.IsTrue(global.FiltersTriggers(true, true));
            Assert.IsFalse(global.FiltersTriggers(true, false));
        }
    }
}
