using AuraEngine.Core;
using AuraEngine.Serialization;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class StateSerializationTests
    {
        private static AuraSimulationWorld BuildWorld()
        {
            var backend = new FakePhysicsBackend();
            var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 4));
            var entity = world.CreateEntity();
            world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateDynamic(
                    AuraPose.Identity,
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Sphere(1f)));

            return world;
        }

        [Test]
        public void Snapshot_RoundTripsThroughSerializer()
        {
            using var world = BuildWorld();
            for (var tick = 1; tick <= 10; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            var snapshot = AuraSimulationSnapshot.Capture(world);
            var bytes = AuraStateSerializer.Serialize(snapshot);
            var restored = AuraStateSerializer.Deserialize(bytes);

            Assert.AreEqual(snapshot.Tick, restored.Tick);
            Assert.AreEqual(snapshot.BodyStates.Length, restored.BodyStates.Length);
            for (var index = 0; index < snapshot.BodyStates.Length; index++)
                Assert.AreEqual(snapshot.BodyStates[index], restored.BodyStates[index]);
        }

        [Test]
        public void Deserialize_RejectsWrongMagic()
        {
            var bytes = new byte[AuraStateSerializer.Serialize(
                new AuraSimulationSnapshot(new SimulationTick(0), System.Array.Empty<AuraBodyState>())).Length];
            bytes[0] = 0xFF;

            Assert.Throws<AuraException>(() => AuraStateSerializer.Deserialize(bytes));
        }

        [Test]
        public void StateHash_IsStableAcrossIdenticalRuns()
        {
            using var first = BuildWorld();
            using var second = BuildWorld();

            for (var tick = 1; tick <= 25; tick++)
            {
                var step = new SimulationStep(new SimulationTick((uint)tick), 1f / 60f);
                first.Step(step);
                second.Step(step);
            }

            Assert.AreEqual(first.ComputeStateHash(), second.ComputeStateHash());
        }
    }
}
