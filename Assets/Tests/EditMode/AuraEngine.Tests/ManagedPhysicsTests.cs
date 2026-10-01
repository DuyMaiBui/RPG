using System;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedPhysicsTests
    {
        private static AuraPhysicsBodyDefinition Ground(float top)
        {
            var center = new AuraVector3(0f, top - 0.5f, 0f);
            return AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(center, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f)));
        }

        private static AuraPhysicsBodyDefinition Ball(AuraVector3 position) =>
            AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(position, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Sphere(0.5f));

        private static AuraSimulationWorld CreateWorld(out ManagedPhysicsBackend backend)
        {
            backend = new ManagedPhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
        }

        [Test]
        public void DynamicSphere_LandsOnStaticGround()
        {
            using var world = CreateWorld(out _);
            var ground = world.CreateEntity();
            world.AttachBody(ground, Ground(0f));
            var ball = world.CreateEntity();
            var body = world.AttachBody(ball, Ball(new AuraVector3(0f, 5f, 0f)));

            for (var tick = 1; tick <= 360; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(MathF.Abs(state.Pose.Position.Y - 0.5f), 0.2f);
            Assert.Less(MathF.Abs(AuraVector3.Dot(state.LinearVelocity, AuraVector3.UnitY)), 1f);
        }

        [Test]
        public void TwoDynamicSpheres_Separate()
        {
            using var world = CreateWorld(out _);
            var first = world.CreateEntity();
            var firstBody = world.AttachBody(first, Ball(new AuraVector3(-0.4f, 0f, 0f)));
            var second = world.CreateEntity();
            var secondBody = world.AttachBody(second, Ball(new AuraVector3(0.4f, 0f, 0f)));

            for (var tick = 1; tick <= 120; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(firstBody, out var a));
            Assert.IsTrue(world.TryGetBodyState(secondBody, out var b));
            Assert.IsTrue(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) >= 0.9f);
        }

        [Test]
        public void Trigger_EmitsEventsWithoutResolving()
        {
            using var world = CreateWorld(out _);
            var trigger = world.CreateEntity();
            world.AttachBody(
                trigger,
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, 0f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(1f, 1f, 1f)).AsTrigger()));

            var mover = world.CreateEntity();
            var moverBody = world.AttachBody(
                mover,
                AuraPhysicsBodyDefinition.CreateKinematic(
                    new AuraPose(new AuraVector3(-3f, 0f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(0.5f)));

            var entered = false;
            var exited = false;
            for (var tick = 1; tick <= 240; tick++)
            {
                world.SetKinematicTarget(mover, new AuraPose(new AuraVector3(-3f + tick * 0.02f, 0f, 0f), AuraQuaternion.Identity));
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

                var events = new AuraPhysicsEvent[8];
                var count = world.CopyEvents(events);
                for (var index = 0; index < count; index++)
                {
                    if (events[index].Type == AuraPhysicsEventType.TriggerEnter)
                        entered = true;
                    if (events[index].Type == AuraPhysicsEventType.TriggerExit)
                        exited = true;
                }
            }

            Assert.IsTrue(entered, "TriggerEnter was not emitted.");
            Assert.IsTrue(exited, "TriggerExit was not emitted.");

            Assert.IsTrue(world.TryGetBodyState(moverBody, out var state));
            Assert.Greater(state.Pose.Position.X, 1f);
        }

        [Test]
        public void Raycast_HitsSphereAndBox()
        {
            using var world = CreateWorld(out _);
            var ground = world.CreateEntity();
            var groundBody = world.AttachBody(ground, Ground(0f));
            var ball = world.CreateEntity();
            var ballBody = world.AttachBody(ball, Ball(new AuraVector3(0f, 5f, 0f)));

            var down = new AuraRay(new AuraVector3(0f, 10f, 0f), new AuraVector3(0f, -1f, 0f));
            Assert.IsTrue(world.Raycast(down, 100f, AuraPhysicsQueryFilter.All, out var hit));
            Assert.AreEqual(ballBody, hit.Body);
            Assert.AreEqual(ball, hit.Entity);

            world.DestroyEntity(ball);
            Assert.IsTrue(world.Raycast(down, 100f, AuraPhysicsQueryFilter.All, out var groundHit));
            Assert.AreEqual(groundBody, groundHit.Body);
        }

        [Test]
        public void Plane2D_KeepsBodiesOnPlane()
        {
            var backend = new ManagedPhysicsBackend();
            using var world = new AuraSimulationWorld(
                backend,
                new AuraWorldDefinition(AuraPhysicsMode.Plane2D, initialBodyCapacity: 8));

            var ground = world.CreateEntity();
            world.AttachBody(ground, Ground(0f));
            var ball = world.CreateEntity();
            var body = world.AttachBody(ball, Ball(new AuraVector3(0f, 3f, 0f)));

            for (var tick = 1; tick <= 240; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.AreEqual(0f, state.Pose.Position.Z, 0.0001f);
            Assert.Less(MathF.Abs(state.Pose.Position.Y - 0.5f), 0.2f);
        }

        [Test]
        public void CollisionMatrix_DisabledPair_PassesThrough()
        {
            var matrix = AuraCollisionMatrix.CreateAllCollide();
            matrix.SetSelfCollision(AuraPhysicsLayer.Default, false);

            var backend = new ManagedPhysicsBackend();
            using var world = new AuraSimulationWorld(
                backend,
                new AuraWorldDefinition(gravity: default, collisionMatrix: matrix, initialBodyCapacity: 8));

            var ground = world.CreateEntity();
            world.AttachBody(ground, Ground(0f));
            var ball = world.CreateEntity();
            var body = world.AttachBody(ball, Ball(new AuraVector3(0f, 3f, 0f)));

            for (var tick = 1; tick <= 180; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(state.Pose.Position.Y, 0f);
        }

        [Test]
        public void ManagedBackend_IsDeterministic()
        {
            static ulong Run()
            {
                var backend = new ManagedPhysicsBackend();
                using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
                var ground = world.CreateEntity();
                world.AttachBody(ground, Ground(0f));
                for (var index = 0; index < 5; index++)
                {
                    var ball = world.CreateEntity();
                    world.AttachBody(ball, Ball(new AuraVector3(index * 0.2f, 4f + index, 0f)));
                }

                for (var tick = 1; tick <= 200; tick++)
                    world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

                return world.ComputeStateHash();
            }

            Assert.AreEqual(Run(), Run());
        }
    }
}
