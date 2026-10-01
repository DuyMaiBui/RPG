using System;
using AuraEngine.Core;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ManagedContactTests
    {
        private static AuraSimulationWorld CreateWorld(out ManagedPhysicsBackend backend)
        {
            backend = new ManagedPhysicsBackend();
            return new AuraSimulationWorld(backend, new AuraWorldDefinition(AuraPhysicsMode.Full3D, default, null, 16));
        }

        [Test]
        public void Contacts_ReportGroundNormal()
        {
            using var world = CreateWorld(out _);

            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateStatic(
                    new AuraPose(new AuraVector3(0f, -0.5f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(10f, 0.5f, 10f))));

            world.AttachBody(
                world.CreateEntity(),
                AuraPhysicsBodyDefinition.CreateDynamic(
                    new AuraPose(new AuraVector3(0f, 3f, 0f), AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(0.5f)));

            for (var tick = 1; tick <= 300; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            var buffer = new AuraContact[16];
            var count = world.CopyContacts(buffer);
            Assert.Greater(count, 0, "expected at least one contact while the ball rests on the ground.");

            var vertical = false;
            for (var index = 0; index < count; index++)
            {
                if (MathF.Abs(buffer[index].Normal.Y) > 0.8f)
                    vertical = true;
            }

            Assert.IsTrue(vertical, "a resting contact should have an approximately vertical normal.");
        }
    }
}
