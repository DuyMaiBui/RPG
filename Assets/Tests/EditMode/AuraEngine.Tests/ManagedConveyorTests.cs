using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedConveyorTests
    {
        [Test]
        public void SurfaceVelocity_DragsRestingBody()
        {
            var backend = new ManagedPhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 8));

            var groundEntity = world.CreateEntity();
            world.AttachBody(
                groundEntity,
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(20f, 0.5f, 20f))));
            Assert.AreEqual(AuraResult.Success, world.SetSurfaceVelocity(groundEntity, new AuraVector3(3f, 0f, 0f)));

            var body = world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 0.6f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));

            for (var tick = 1; tick <= 240; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            Assert.IsTrue(world.TryGetBodyState(body, out var state));
            Assert.Greater(state.Pose.Position.X, 1f, "a conveyor surface velocity should drag the resting box in +X.");
        }
    }
}
