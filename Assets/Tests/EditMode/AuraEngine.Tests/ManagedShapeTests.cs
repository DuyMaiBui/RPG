using System;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedShapeTests
    {
        private static AuraSimulationWorld CreateWorld(out ManagedPhysicsBackend backend)
        {
            backend = new ManagedPhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 8));
        }

        private static AuraPhysicsBodyDefinition Ground() =>
            AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f)));

        private static void Step(AuraSimulationWorld world, int count)
        {
            for (var index = 1; index <= count; index++)
                world.Step(new SimulationStep(new SimulationTick((uint)index), 1f / 60f));
        }

        [Test]
        public void Capsule_LandsUprightOnGround()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), Ground());
            var capsuleBody = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    new AuraPhysicsShapeDefinition(
                        AuraShapeType.Capsule,
                        AuraPose.Identity,
                        false,
                        AuraPhysicsMaterialDefinition.Default,
                        AuraPhysicsLayer.Default,
                        AuraShapeGeometry.Capsule(0.5f, 2f))));

            Step(world, 300);

            Assert.IsTrue(world.TryGetBodyState(capsuleBody, out var state));
            Assert.Less(MathF.Abs(state.Pose.Position.Y - 1f), 0.25f);
        }

        [Test]
        public void Cylinder_BehavesAsCapsuleApproximation()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), Ground());
            var cylinder = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    new AuraPhysicsShapeDefinition(
                        AuraShapeType.Cylinder,
                        AuraPose.Identity,
                        false,
                        AuraPhysicsMaterialDefinition.Default,
                        AuraPhysicsLayer.Default,
                        AuraShapeGeometry.Cylinder(0.5f, 2f))));

            Step(world, 300);

            Assert.IsTrue(world.TryGetBodyState(cylinder, out var state));
            Assert.Less(MathF.Abs(state.Pose.Position.Y - 1f), 0.25f);
        }

        [Test]
        public void CapsuleCapsule_Separate()
        {
            using var world = CreateWorld(out _);
            var first = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(-0.3f, 0f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    Capsule(0.5f, 2f)));
            var second = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0.3f, 0f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    Capsule(0.5f, 2f)));

            Step(world, 120);

            Assert.IsTrue(world.TryGetBodyState(first, out var a));
            Assert.IsTrue(world.TryGetBodyState(second, out var b));
            Assert.IsTrue(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) >= 0.9f);
        }

        [Test]
        public void CompoundBody_TwoShapesCollideWithGround()
        {
            using var world = CreateWorld(out _);
            world.AttachBody(world.CreateEntity(), Ground());

            var sphereA = AuraPhysicsShapeDefinition.Sphere(0.5f).WithLocalPose(new AuraPose(new AuraVector3(-0.6f, 0f, 0f), AuraQuaternion.Identity));
            var sphereB = AuraPhysicsShapeDefinition.Sphere(0.5f).WithLocalPose(new AuraPose(new AuraVector3(0.6f, 0f, 0f), AuraQuaternion.Identity));
            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    sphereA,
                    sphereB));

            Step(world, 300);

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Less(MathF.Abs(state.Pose.Position.Y - 0.5f), 0.25f);
        }

        private static AuraPhysicsShapeDefinition Capsule(float radius, float height) =>
            new AuraPhysicsShapeDefinition(
                AuraShapeType.Capsule,
                AuraPose.Identity,
                false,
                AuraPhysicsMaterialDefinition.Default,
                AuraPhysicsLayer.Default,
                AuraShapeGeometry.Capsule(radius, height));
    }
}
