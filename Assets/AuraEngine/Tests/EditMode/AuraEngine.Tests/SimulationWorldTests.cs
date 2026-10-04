using AuraEngine.Core;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class SimulationWorldTests
    {
        private static AuraPhysicsShapeDefinition UnitSphere() => AuraPhysicsShapeDefinition.Sphere(1f);

        private static AuraPhysicsBodyDefinition KinematicBody(in AuraPose pose) =>
            AuraPhysicsBodyDefinition.CreateKinematic(
                pose,
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                UnitSphere());

        private static AuraSimulationWorld CreateWorld(out FakePhysicsBackend backend)
        {
            backend = new FakePhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
        }

        [Test]
        public void CreateAndDestroyEntity_TracksAliveState()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();

            Assert.IsTrue(world.IsAlive(entity));
            Assert.AreEqual(1, world.EntityCount);

            Assert.IsTrue(world.DestroyEntity(entity));
            Assert.IsFalse(world.IsAlive(entity));
            Assert.AreEqual(0, world.EntityCount);
        }

        [Test]
        public void DestroyEntity_RejectsStaleAndInvalidIds()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();
            world.DestroyEntity(entity);

            Assert.IsFalse(world.DestroyEntity(entity));
            Assert.IsFalse(world.DestroyEntity(SimulationEntityId.None));
            Assert.IsFalse(world.IsAlive(new SimulationEntityId(999, 0)));
        }

        [Test]
        public void ReusedSlot_BumpsGeneration()
        {
            using var world = CreateWorld(out _);
            var first = world.CreateEntity();
            world.DestroyEntity(first);
            var second = world.CreateEntity();

            Assert.AreEqual(first.Index, second.Index);
            Assert.AreEqual(first.Generation + 1, second.Generation);
            Assert.IsFalse(world.IsAlive(first));
            Assert.IsTrue(world.IsAlive(second));
        }

        [Test]
        public void CreateThousandEntities_ThenDestroyAll()
        {
            using var world = CreateWorld(out _);
            var entities = new SimulationEntityId[1000];
            for (var index = 0; index < entities.Length; index++)
                entities[index] = world.CreateEntity();

            Assert.AreEqual(1000, world.EntityCount);

            for (var index = 0; index < entities.Length; index++)
                Assert.IsTrue(world.DestroyEntity(entities[index]));

            Assert.AreEqual(0, world.EntityCount);
        }

        [Test]
        public void CreateDestroyChurn_KeepsRegistryConsistent()
        {
            using var world = CreateWorld(out _);
            for (var index = 0; index < 1000; index++)
            {
                var entity = world.CreateEntity();
                if (index % 2 == 0)
                    world.DestroyEntity(entity);
            }

            Assert.AreEqual(500, world.EntityCount);
        }

        [Test]
        public void AttachBody_MapsEntityToState()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();
            var body = world.AttachBody(entity, KinematicBody(AuraPose.Identity));

            Assert.IsTrue(body.IsValid);
            Assert.IsTrue(world.TryGetBody(entity, out var resolved));
            Assert.AreEqual(body, resolved);

            world.Step(new SimulationStep(new SimulationTick(1), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.AreEqual(entity, state.Entity);
            Assert.AreEqual(1, world.BodyCount);
        }

        [Test]
        public void DestroyEntity_DestroysAttachedBody()
        {
            using var world = CreateWorld(out _);
            var entity = world.CreateEntity();
            var body = world.AttachBody(entity, KinematicBody(AuraPose.Identity));

            world.DestroyEntity(entity);

            Assert.AreEqual(0, world.BodyCount);
            Assert.IsFalse(world.TryGetBodyState(body, out _));
        }

        [Test]
        public void DynamicBody_IntegratesGravity()
        {
            var fakeBackend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(fakeBackend, new AuraWorldDefinition(initialBodyCapacity: 1));
            var entity = world.CreateEntity();
            var body = world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateDynamic(
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    UnitSphere()));

            for (var tick = 1; tick <= 100; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(state.Pose.Position.Y, 0f);
        }

        [Test]
        public void CommandHandler_ReceivesEnqueuedCommands()
        {
            using var world = CreateWorld(out _);
            var handler = new TestCommandHandler();
            world.RegisterCommandHandler(handler);

            world.EnqueueCommand(new TestCommand(2));
            world.EnqueueCommand(new TestCommand(3));
            world.Step(new SimulationStep(new SimulationTick(1), 1f / 60f));

            Assert.AreEqual(2, handler.HandleCount);
            Assert.AreEqual(5, handler.Sum);
            Assert.IsTrue(world.IsAlive(handler.CreatedEntity));
        }

        [Test]
        public void DuplicateCommandTypeId_Throws()
        {
            using var world = CreateWorld(out _);
            world.RegisterCommandHandler(new TestCommandHandler());

            Assert.Throws<System.InvalidOperationException>(() => world.RegisterCommandHandler(new TestCommandHandler()));
        }

        [Test]
        public void SystemCount_AddRemove()
        {
            using var world = CreateWorld(out _);
            var system = new TestMoveSystem(SimulationEntityId.None, AuraVector3.UnitX);

            world.AddSystem(system);
            Assert.AreEqual(1, world.SystemCount);
            Assert.IsTrue(world.RemoveSystem(system));
            Assert.AreEqual(0, world.SystemCount);
        }
    }
}
