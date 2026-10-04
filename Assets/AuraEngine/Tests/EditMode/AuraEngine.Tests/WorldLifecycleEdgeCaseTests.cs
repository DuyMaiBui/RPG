using System;
using AuraEngine.Core;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class WorldLifecycleEdgeCaseTests
    {
        private static AuraPhysicsBodyDefinition Sphere(AuraVector3 position) =>
            AuraPhysicsBodyDefinition.CreateKinematic(
                new AuraPose(position, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Sphere(1f));

        private static AuraPhysicsBodyDefinition Invalid() =>
            AuraPhysicsBodyDefinition.CreateKinematic(
                AuraPose.Identity,
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All);

        private static AuraSimulationWorld CreateWorld(out FakePhysicsBackend backend)
        {
            backend = new FakePhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
        }

        [Test]
        public void OperationsAfterDispose_ThrowObjectDisposed()
        {
            var world = CreateWorld(out _);
            ((IDisposable)world).Dispose();

            Assert.Throws<ObjectDisposedException>(() => world.CreateEntity());
            Assert.Throws<ObjectDisposedException>(() => world.DestroyEntity(SimulationEntityId.None));
            Assert.Throws<ObjectDisposedException>(() => world.AttachBody(SimulationEntityId.None, Sphere(AuraVector3.Zero)));
            Assert.Throws<ObjectDisposedException>(() => world.Step(new SimulationStep(new SimulationTick(1), 0.01f)));
            Assert.Throws<ObjectDisposedException>(() => world.EnqueueCommand(new TestCommand(1)));
            Assert.Throws<ObjectDisposedException>(() => world.AddSystem(new TestMoveSystem(SimulationEntityId.None, AuraVector3.UnitX)));
        }

        [Test]
        public void DoubleDispose_IsSafe()
        {
            var world = CreateWorld(out _);
            ((IDisposable)world).Dispose();

            Assert.DoesNotThrow(() => ((IDisposable)world).Dispose());
        }

        [Test]
        public void AttachBody_ReplacingWithInvalidDefinition_DoesNotKeepStaleBody()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();
            var first = world.AttachBody(entity, Sphere(AuraVector3.Zero));
            Assert.IsTrue(first.IsValid);

            var second = world.AttachBody(entity, Invalid());

            Assert.IsFalse(second.IsValid);
            Assert.IsFalse(world.TryGetBody(entity, out _));
            Assert.AreEqual(0, world.BodyCount);
        }

        [Test]
        public void AttachBody_ReplacingValidBody_DestroysPrevious()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();
            var first = world.AttachBody(entity, Sphere(AuraVector3.Zero));
            var second = world.AttachBody(entity, Sphere(AuraVector3.UnitX));

            Assert.IsTrue(second.IsValid);
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(1, world.BodyCount);
            Assert.IsFalse(world.TryGetBodyState(first, out _));
        }

        [Test]
        public void CopyBodyStates_TruncatesToBufferLength()
        {
            using var world = CreateWorld(out _);
            for (var index = 0; index < 5; index++)
            {
                var entity = world.CreateEntity();
                world.AttachBody(entity, Sphere(new AuraVector3(index, 0f, 0f)));
            }

            var buffer = new AuraBodyState[2];
            var count = world.CopyBodyStates(buffer);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void CopyBodyStates_WithEmptyBuffer_ReturnsZero()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();
            world.AttachBody(entity, Sphere(AuraVector3.Zero));

            Assert.AreEqual(0, world.CopyBodyStates(Span<AuraBodyState>.Empty));
        }

        [Test]
        public void EnqueueCommand_Null_Throws()
        {
            using var world = CreateWorld(out _);
            Assert.Throws<ArgumentNullException>(() => world.EnqueueCommand(null));
        }

        [Test]
        public void RegisterCommandHandler_Null_Throws()
        {
            using var world = CreateWorld(out _);
            Assert.Throws<ArgumentNullException>(() => world.RegisterCommandHandler(null));
        }

        [Test]
        public void TryGetBodyState_ForEntityWithoutBody_ReturnsFalse()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();

            Assert.IsFalse(world.TryGetBodyState(entity, out _));
            Assert.IsFalse(world.SetKinematicTarget(entity, AuraPose.Identity) == AuraResult.Success);
        }

        [Test]
        public void BodyChurn_ReleasesAdvanceGeneration()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();

            var first = world.AttachBody(entity, Sphere(AuraVector3.Zero));
            var second = world.AttachBody(entity, Sphere(AuraVector3.Zero));

            Assert.AreEqual(first.Index, second.Index);
            Assert.Greater(second.Generation, first.Generation);
        }

        [Test]
        public void ManyEntitiesWithBodies_DestroyAll_LeavesNoBodies()
        {
            using var world = CreateWorld(out _);
            var entities = new SimulationEntityId[2000];
            for (var index = 0; index < entities.Length; index++)
            {
                entities[index] = world.CreateEntity();
                world.AttachBody(entities[index], Sphere(new AuraVector3(index, 0f, 0f)));
            }

            Assert.AreEqual(2000, world.BodyCount);

            for (var index = 0; index < entities.Length; index++)
                world.DestroyEntity(entities[index]);

            Assert.AreEqual(0, world.BodyCount);
            Assert.AreEqual(0, world.EntityCount);
        }
    }
}
