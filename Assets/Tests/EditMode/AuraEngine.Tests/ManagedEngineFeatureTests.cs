using System;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedEngineFeatureTests
    {
        private static AuraSimulationWorld CreateWorld(out ManagedPhysicsBackend backend, AuraPhysicsMode mode = AuraPhysicsMode.Full3D)
        {
            backend = new ManagedPhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(mode, initialBodyCapacity: 16));
        }

        private static AuraPhysicsBodyDefinition Ground() =>
            AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f)));

        private static AuraPhysicsBodyDefinition Box(AuraVector3 position, float half = 0.5f, AuraQuaternion? rotation = null) =>
            AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(position, rotation ?? AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(half, half, half)));

        private static void Step(AuraSimulationWorld world, int count)
        {
            for (var index = 1; index <= count; index++)
                world.Step(new SimulationStep(new SimulationTick((uint)index), 1f / 60f));
        }

        [Test]
        public void BoxStack_RemainsStable()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), Ground());
            var first = world.AttachBody(world.CreateEntity(), Box(new AuraVector3(0f, 0.5f, 0f)));
            var second = world.AttachBody(world.CreateEntity(), Box(new AuraVector3(0f, 1.5f, 0f)));
            var third = world.AttachBody(world.CreateEntity(), Box(new AuraVector3(0f, 2.5f, 0f)));

            Step(world, 400);

            Assert.IsTrue(world.TryGetBodyState(first, out var a));
            Assert.IsTrue(world.TryGetBodyState(second, out var b));
            Assert.IsTrue(world.TryGetBodyState(third, out var c));
            Assert.Less(MathF.Abs(a.Pose.Position.Y - 0.5f), 0.25f);
            Assert.Less(MathF.Abs(b.Pose.Position.Y - 1.5f), 0.3f);
            Assert.Less(MathF.Abs(c.Pose.Position.Y - 2.5f), 0.4f);
            Assert.Less(MathF.Abs(a.Pose.Position.X), 0.3f);
            Assert.Less(MathF.Abs(c.Pose.Position.X), 0.4f);
        }

        [Test]
        public void RotatedBox_RestOnGround()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), Ground());
            var body = world.AttachBody(
                world.CreateEntity(),
                Box(new AuraVector3(0f, 3f, 0f), 0.5f, AuraQuaternion.FromAxisAngle(AuraVector3.UnitY, 0.6f)));

            Step(world, 300);

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(MathF.Abs(state.Pose.Position.Y - 0.5f), 0.25f);
        }

        [Test]
        public void BoxRaycast_AgainstRotatedObb()
        {
            using var world = CreateWorld(out _);
            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(AuraVector3.Zero, AuraQuaternion.FromAxisAngle(AuraVector3.UnitY, MathF.PI / 4f)),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(1f, 1f, 1f))));

            var hit = world.Raycast(
                new AuraRay(new AuraVector3(-5f, 0f, 0f), AuraVector3.UnitX),
                20f,
                AuraPhysicsQueryFilter.All,
                out var result);

            Assert.IsTrue(hit);
            Assert.AreEqual(body, result.Body);
            Assert.Less(MathF.Abs(result.Distance - 3.586f), 0.2f);
        }

        [Test]
        public void DistanceJoint_KeepsDistance()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, Box(new AuraVector3(-1f, 5f, 0f)));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, Box(new AuraVector3(1f, 5f, 0f)));

            var definition = AuraJointDefinition.CreateDistance(
                bodyA,
                bodyB,
                new AuraVector3(-1f, 5f, 0f),
                new AuraVector3(1f, 5f, 0f),
                2f);
            var joint = world.CreateJoint(entityA, entityB, definition);
            Assert.IsTrue(joint.IsValid);

            Step(world, 240);

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.Less(MathF.Abs(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) - 2f), 0.2f);
        }

        [Test]
        public void FixedJoint_KeepsRelativeDistance()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, Box(new AuraVector3(-1f, 4f, 0f)));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, Box(new AuraVector3(1f, 4f, 0f)));

            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateFixed(
                bodyA,
                bodyB,
                new AuraVector3(0f, 4f, 0f),
                new AuraVector3(0f, 4f, 0f)));

            Step(world, 240);

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.Less(MathF.Abs(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) - 2f), 0.2f);
        }

        [Test]
        public void HingeJoint_KeepsAnchorCoincident()
        {
            using var world = CreateWorld(out _);
            var anchor = new AuraVector3(0f, 2f, 0f);

            var staticEntity = world.CreateEntity();
            var staticBody = world.AttachBody(
                staticEntity,
                AuraPhysicsBodyDefinition.CreateStatic(
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(0.2f, 0.2f, 0.2f))));

            var dynamicEntity = world.CreateEntity();
            var dynamicBody = world.AttachBody(dynamicEntity, Box(new AuraVector3(0f, 2f, 0f)));

            world.CreateJoint(dynamicEntity, staticEntity, AuraJointDefinition.CreateHinge(
                dynamicBody,
                staticBody,
                anchor,
                anchor,
                AuraVector3.UnitY,
                AuraVector3.UnitY));

            Step(world, 300);

            Assert.IsTrue(world.TryGetBodyState(dynamicBody, out var state));
            Assert.Less(AuraVector3.Distance(state.Pose.Position, anchor), 0.2f);
        }

        [Test]
        public void Plane2D_DistanceJoint_KeepsDistance()
        {
            using var world = CreateWorld(out _, AuraPhysicsMode.Plane2D);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, Box(new AuraVector3(-1f, 4f, 0f)));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, Box(new AuraVector3(1f, 4f, 0f)));

            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateDistance(
                bodyA,
                bodyB,
                new AuraVector3(-1f, 4f, 0f),
                new AuraVector3(1f, 4f, 0f),
                2f));

            Step(world, 240);

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.Less(MathF.Abs(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) - 2f), 0.2f);
        }

        [Test]
        public void Sleeping_BodyEventuallySleeps()
        {
            var settings = new AuraSolverSettings { TimeToSleep = 0.3f };
            var backend = new ManagedPhysicsBackend();
            using var world = new AuraSimulationWorld(
                backend,
                new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 16, 1f / 60f, settings));
            world.AttachBody(world.CreateEntity(), Ground());
            var body = world.AttachBody(world.CreateEntity(), Box(new AuraVector3(0f, 1f, 0f)));

            Step(world, 600);

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.IsFalse(state.IsAwake);
            Assert.Less(MathF.Abs(state.Pose.Position.Y - 0.5f), 0.2f);
        }

        [Test]
        public void BoxStack_IsDeterministic()
        {
            static ulong Run()
            {
                var backend = new ManagedPhysicsBackend();
                using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 16));
                world.AttachBody(world.CreateEntity(), Ground());
                for (var index = 0; index < 3; index++)
                    world.AttachBody(world.CreateEntity(), Box(new AuraVector3(0.05f * index, 0.5f + index, 0f)));

                Step(world, 300);
                return world.ComputeStateHash();
            }

            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void Plane2D_BoxStack_RemainsStable()
        {
            using var world = CreateWorld(out _, AuraPhysicsMode.Plane2D);
            world.AttachBody(world.CreateEntity(), Ground());
            var first = world.AttachBody(world.CreateEntity(), Box(new AuraVector3(0f, 0.5f, 0f)));
            var second = world.AttachBody(world.CreateEntity(), Box(new AuraVector3(0f, 1.5f, 0f)));

            Step(world, 400);

            Assert.IsTrue(world.TryGetBodyState(first, out var a));
            Assert.IsTrue(world.TryGetBodyState(second, out var b));
            Assert.Less(MathF.Abs(a.Pose.Position.Y - 0.5f), 0.25f);
            Assert.Less(MathF.Abs(b.Pose.Position.Y - 1.5f), 0.35f);
        }
    }
}
