using System;
using System.Buffers.Binary;
using AuraEngine.Core;
using AuraEngine.Serialization;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class SerializerEdgeCaseTests
    {
        [Test]
        public void NullSnapshot_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AuraStateSerializer.Serialize(null));
            Assert.Throws<ArgumentNullException>(() => AuraStateSerializer.Deserialize(null));
        }

        [Test]
        public void EmptySnapshot_RoundTrips()
        {
            var snapshot = new AuraSimulationSnapshot(new SimulationTick(42), Array.Empty<AuraBodyState>());
            var restored = AuraStateSerializer.Deserialize(AuraStateSerializer.Serialize(snapshot));

            Assert.AreEqual(snapshot.Tick, restored.Tick);
            Assert.AreEqual(0, restored.BodyStates.Length);
        }

        [Test]
        public void TruncatedBuffer_ThrowsAuraException()
        {
            var bytes = AuraStateSerializer.Serialize(
                new AuraSimulationSnapshot(new SimulationTick(1), Array.Empty<AuraBodyState>()));
            var truncated = new byte[bytes.Length - 2];
            Array.Copy(bytes, truncated, truncated.Length);

            Assert.Throws<AuraException>(() => AuraStateSerializer.Deserialize(truncated));
        }

        [Test]
        public void HugeBodyCount_DoesNotOverflow_AndThrows()
        {
            var bytes = AuraStateSerializer.Serialize(
                new AuraSimulationSnapshot(new SimulationTick(1), Array.Empty<AuraBodyState>()));
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(bytes, 12, 4), int.MaxValue);

            Assert.Throws<AuraException>(() => AuraStateSerializer.Deserialize(bytes));
        }

        [Test]
        public void NegativeBodyCount_Throws()
        {
            var bytes = AuraStateSerializer.Serialize(
                new AuraSimulationSnapshot(new SimulationTick(1), Array.Empty<AuraBodyState>()));
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(bytes, 12, 4), -1);

            Assert.Throws<AuraException>(() => AuraStateSerializer.Deserialize(bytes));
        }

        [Test]
        public void VersionMismatch_Throws()
        {
            var bytes = AuraStateSerializer.Serialize(
                new AuraSimulationSnapshot(new SimulationTick(1), Array.Empty<AuraBodyState>()));
            BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(bytes, 4, 2), 999);

            Assert.Throws<AuraException>(() => AuraStateSerializer.Deserialize(bytes));
        }

        [Test]
        public void LargeSnapshot_RoundTripsExactly()
        {
            var backend = new FakePhysicsBackend();
            using var world = new AuraSimulationWorld(backend, new AuraWorldDefinition(initialBodyCapacity: 512));
            for (var index = 0; index < 500; index++)
            {
                var entity = world.CreateEntity();
                world.AttachBody(
                    entity,
                    AuraPhysicsBodyDefinition.CreateDynamic(
                        new AuraPose(new AuraVector3(index * 0.5f, index, 0f), AuraQuaternion.Identity),
                        AuraPhysicsLayer.Default,
                        AuraPhysicsLayerMask.All,
                        AuraPhysicsShapeDefinition.Sphere(1f)));
            }

            for (var tick = 1; tick <= 20; tick++)
                world.Step(new SimulationStep(new SimulationTick((uint)tick), 1f / 60f));

            var snapshot = AuraSimulationSnapshot.Capture(world);
            var restored = AuraStateSerializer.Deserialize(AuraStateSerializer.Serialize(snapshot));

            Assert.AreEqual(snapshot.BodyStates.Length, restored.BodyStates.Length);
            for (var index = 0; index < snapshot.BodyStates.Length; index++)
                Assert.AreEqual(snapshot.BodyStates[index], restored.BodyStates[index]);
        }
    }
}
