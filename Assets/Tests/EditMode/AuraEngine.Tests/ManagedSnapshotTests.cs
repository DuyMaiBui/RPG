using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedSnapshotTests
    {
        private static AuraSimulationWorld CreateWorld(out ManagedPhysicsBackend backend)
        {
            backend = new ManagedPhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 8));
        }

        [Test]
        public void SaveState_RestoreState_ReproducesBodyState()
        {
            using var world = CreateWorld(out _);
            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(0.5f)));

            for (var tick = 1; tick <= 60; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var before));
            var saved = world.SaveState();
            Assert.Greater(saved.Length, 0);

            for (var tick = 61; tick <= 180; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var fallen));
            Assert.Less(fallen.Pose.Position.Y, before.Pose.Position.Y - 0.5f);

            world.RestoreState(saved);

            Assert.IsTrue(world.TryGetBodyState(body, out var restored));
            Assert.AreEqual(before.Pose.Position.X, restored.Pose.Position.X, 1e-4f);
            Assert.AreEqual(before.Pose.Position.Y, restored.Pose.Position.Y, 1e-4f);
            Assert.AreEqual(before.Pose.Position.Z, restored.Pose.Position.Z, 1e-4f);
        }
    }
}
