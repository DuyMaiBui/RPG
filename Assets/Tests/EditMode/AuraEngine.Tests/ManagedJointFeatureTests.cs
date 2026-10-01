using System;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedJointFeatureTests
    {
        private static AuraSimulationWorld CreateWorld(out ManagedPhysicsBackend backend)
        {
            backend = new ManagedPhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 16));
        }

        private static AuraPhysicsBodyDefinition Box(AuraVector3 position) =>
            AuraPhysicsBodyDefinition.CreateDynamic(
                new AuraPose(position, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f)));

        private static AuraPhysicsBodyDefinition StaticBox(AuraVector3 position) =>
            AuraPhysicsBodyDefinition.CreateStatic(
                new AuraPose(position, AuraQuaternion.Identity),
                AuraPhysicsLayer.Default,
                AuraPhysicsLayerMask.All,
                AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f)));

        private static void Step(AuraSimulationWorld world, int count)
        {
            for (var tick = 1; tick <= count; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));
        }

        [Test]
        public void PointJoint_KeepsAnchorsCoincident()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, Box(new AuraVector3(-1f, 4f, 0f)));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, Box(new AuraVector3(1f, 4f, 0f)));

            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreatePoint(
                bodyA, bodyB, new AuraVector3(0f, 4f, 0f), new AuraVector3(0f, 4f, 0f)));

            Step(world, 180);

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.IsFalse(float.IsNaN(a.Pose.Position.X) || float.IsNaN(b.Pose.Position.X), "point joint NaN");
            Assert.Less(MathF.Abs(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) - 2f), 0.4f);
        }

        [Test]
        public void SliderJoint_ConstrainsPerpendicularAxes()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, Box(new AuraVector3(-1f, 4f, 0f)));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, Box(new AuraVector3(1f, 4f, 0f)));

            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateSlider(
                bodyA, bodyB, new AuraVector3(0f, 4f, 0f), new AuraVector3(0f, 4f, 0f),
                AuraVector3.UnitX, AuraVector3.UnitX));

            Step(world, 180);

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.IsFalse(float.IsNaN(a.Pose.Position.Y) || float.IsNaN(b.Pose.Position.Y), "slider joint NaN");
            Assert.Less(MathF.Abs(a.Pose.Position.Y - b.Pose.Position.Y), 0.1f, "slider allowed perpendicular drift");
        }

        [Test]
        public void ConeJoint_LimitsAngle()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var staticBody = world.AttachBody(entityA, StaticBox(AuraVector3.Zero));
            var entityB = world.CreateEntity();
            var dynamicBody = world.AttachBody(entityB, Box(new AuraVector3(0f, 2f, 0f)));

            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateCone(
                staticBody, dynamicBody, AuraVector3.Zero, AuraVector3.Zero,
                AuraVector3.UnitY, AuraVector3.UnitY, 0.3f));

            Step(world, 240);

            Assert.IsTrue(world.TryGetBodyState(dynamicBody, out var b));
            var axisA = AuraVector3.UnitY;
            var axisB = b.Pose.Rotation.Rotate(AuraVector3.UnitY).Normalized();
            var angle = MathF.Acos(Math.Clamp(AuraVector3.Dot(axisA, axisB), -1f, 1f));
            Assert.IsFalse(float.IsNaN(angle), "cone joint NaN");
            Assert.Less(angle, 0.4f, $"cone exceeded limit: {angle}");
        }

        [Test]
        public void SpringJoint_KeepsDistanceNearTarget()
        {
            using var world = CreateWorld(out _);
            var entityA = world.CreateEntity();
            var bodyA = world.AttachBody(entityA, Box(new AuraVector3(-1f, 4f, 0f)));
            var entityB = world.CreateEntity();
            var bodyB = world.AttachBody(entityB, Box(new AuraVector3(1f, 4f, 0f)));

            world.CreateJoint(entityA, entityB, AuraJointDefinition.CreateSpring(
                bodyA, bodyB, new AuraVector3(-1f, 4f, 0f), new AuraVector3(1f, 4f, 0f), 2f, 2f, 0.5f));

            Step(world, 180);

            Assert.IsTrue(world.TryGetBodyState(bodyA, out var a));
            Assert.IsTrue(world.TryGetBodyState(bodyB, out var b));
            Assert.IsFalse(float.IsNaN(a.Pose.Position.X) || float.IsNaN(b.Pose.Position.X), "spring joint NaN");
            Assert.Less(MathF.Abs(AuraVector3.Distance(a.Pose.Position, b.Pose.Position) - 2f), 1f);
        }
    }
}
