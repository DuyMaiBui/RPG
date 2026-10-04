using AuraEngine.Core;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class WorldReentrancyTests
    {
        private static AuraSimulationWorld CreateWorld(out FakePhysicsBackend backend)
        {
            backend = new FakePhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
        }

        [Test]
        public void CommandsEnqueuedDuringTick_RunOnNextTickOnly()
        {
            using var world = CreateWorld(out _);
            var handler = new TestCommandHandler();
            world.RegisterCommandHandler(handler);
            var system = new EnqueueCommandSystem(world);
            world.AddSystem(system);

            world.Step(new SimulationStep(new SimulationTick(1), 0.01f));
            Assert.AreEqual(1, system.TickCount);
            Assert.AreEqual(0, handler.HandleCount);

            world.Step(new SimulationStep(new SimulationTick(2), 0.01f));
            Assert.AreEqual(1, handler.HandleCount);
            Assert.AreEqual(9, handler.Sum);
        }

        [Test]
        public void CommandsAreDispatchedInEnqueueOrder()
        {
            using var world = CreateWorld(out _);
            var order = new System.Collections.Generic.List<int>();
            var handler = new RecordingCommandHandler(order);
            world.RegisterCommandHandler(handler);

            world.EnqueueCommand(new TestCommand(1));
            world.EnqueueCommand(new TestCommand(2));
            world.EnqueueCommand(new TestCommand(3));
            world.Step(new SimulationStep(new SimulationTick(1), 0.01f));

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void SystemRemovesItselfDuringTick_IsSafe()
        {
            using var world = CreateWorld(out _);
            var system = new SelfRemovingSystem(world);
            world.AddSystem(system);

            Assert.DoesNotThrow(() => world.Step(new SimulationStep(new SimulationTick(1), 0.01f)));
            Assert.AreEqual(1, system.TickCount);
            Assert.AreEqual(0, world.SystemCount);

            Assert.DoesNotThrow(() => world.Step(new SimulationStep(new SimulationTick(2), 0.01f)));
            Assert.AreEqual(1, system.TickCount);
        }

        [Test]
        public void Events_PreserveOrderAndMapBothEntities()
        {
            using var world = CreateWorld(out var backend);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, Body(AuraVector3.Zero));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, Body(AuraVector3.UnitX));

            backend.EnqueueEvent(Event(AuraPhysicsEventType.CollisionEnter, bodyA, bodyB));
            backend.EnqueueEvent(Event(AuraPhysicsEventType.CollisionExit, bodyA, bodyB));
            backend.EnqueueEvent(Event(AuraPhysicsEventType.TriggerEnter, bodyB, bodyA));

            world.Step(new SimulationStep(new SimulationTick(1), 0.01f));

            var events = new AuraPhysicsEvent[8];
            var count = world.CopyEvents(events);

            Assert.AreEqual(3, count);
            Assert.AreEqual(AuraPhysicsEventType.CollisionEnter, events[0].Type);
            Assert.AreEqual(entityA, events[0].EntityA);
            Assert.AreEqual(entityB, events[0].EntityB);
            Assert.AreEqual(AuraPhysicsEventType.CollisionExit, events[1].Type);
            Assert.AreEqual(AuraPhysicsEventType.TriggerEnter, events[2].Type);
            Assert.AreEqual(entityB, events[2].EntityA);
            Assert.AreEqual(entityA, events[2].EntityB);
        }

        [Test]
        public void ManyEventsPerTick_AreAllRetained()
        {
            using var world = CreateWorld(out var backend);
            var entity = world.CreateEntity();
            var body = world.AttachBody(entity, Body(AuraVector3.Zero));

            for (var index = 0; index < 200; index++)
                backend.EnqueueEvent(Event(AuraPhysicsEventType.CollisionEnter, body, PhysicsBodyId.Invalid));

            world.Step(new SimulationStep(new SimulationTick(1), 0.01f));

            Assert.AreEqual(200, world.PendingEventCount);

            var buffer = new AuraPhysicsEvent[world.PendingEventCount];
            Assert.AreEqual(200, world.CopyEvents(buffer));
            Assert.AreEqual(entity, buffer[199].EntityA);
        }

        [Test]
        public void BodyEntityMapping_SurvivesSlotReuse()
        {
            using var world = CreateWorld(out _);
            var first = world.CreateEntity();
            var firstBody = world.AttachBody(first, Body(AuraVector3.Zero));
            world.DestroyEntity(first);

            var second = world.CreateEntity();
            var secondBody = world.AttachBody(second, Body(AuraVector3.UnitY));

            Assert.IsFalse(world.TryResolveEntity(firstBody, out _));
            Assert.IsTrue(world.TryResolveEntity(secondBody, out var resolved));
            Assert.AreEqual(second, resolved);
        }

        private static AuraPhysicsBodyDefinition Body(AuraVector3 position) =>
            AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(position, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Sphere(1f));

        private static AuraPhysicsEvent Event(AuraPhysicsEventType type, PhysicsBodyId a, PhysicsBodyId b) =>
            new AuraPhysicsEvent(
                type,
                SimulationEntityId.None,
                SimulationEntityId.None,
                a,
                b,
                PhysicsShapeId.Invalid,
                PhysicsShapeId.Invalid,
                AuraVector3.Zero,
                AuraVector3.UnitY,
                1f);
    }
}
