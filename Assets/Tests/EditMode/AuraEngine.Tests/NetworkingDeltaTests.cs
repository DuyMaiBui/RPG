using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Networking;
using AuraEngine.Simulation;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class NetworkingDeltaTests
    {
        private static AuraBodyState State(float x, float y, float z, float vx, bool awake)
        {
            return new AuraBodyState(
                new PhysicsBodyId(3, 1),
                new SimulationEntityId(7, 1),
                new AuraPose(new AuraVector3(x, y, z), AuraQuaternion.Identity),
                new AuraVector3(vx, 0f, 0f),
                AuraVector3.Zero,
                awake,
                0u);
        }

        [Test]
        public void Delta_RoundTripReconstructsState()
        {
            var baseline = State(0f, 0f, 0f, 0f, true);
            var current = State(1.25f, -0.5f, 3f, 2f, false);

            var entry = AuraSnapshotDelta.FromBodyState(current, baseline, 0.001f, 0.001f);
            var payload = AuraSnapshotDelta.Encode(42u, new List<AuraDeltaEntry> { entry });

            Assert.IsTrue(AuraSnapshotDelta.TryDecode(payload, out var tick, out var entries));
            Assert.AreEqual(42u, tick);
            Assert.AreEqual(1, entries.Count);

            var restored = AuraSnapshotDelta.Apply(baseline, entries[0], 0.001f, 0.001f, current.Entity);
            Assert.AreEqual(current.Pose.Position.X, restored.Pose.Position.X, 0.005f);
            Assert.AreEqual(current.Pose.Position.Y, restored.Pose.Position.Y, 0.005f);
            Assert.AreEqual(current.Pose.Position.Z, restored.Pose.Position.Z, 0.005f);
            Assert.AreEqual(current.LinearVelocity.X, restored.LinearVelocity.X, 0.005f);
            Assert.AreEqual(current.IsAwake, restored.IsAwake);
        }

        [Test]
        public void Delta_EmptySnapshotRoundTrips()
        {
            var payload = AuraSnapshotDelta.Encode(7u, new List<AuraDeltaEntry>());
            Assert.IsTrue(AuraSnapshotDelta.TryDecode(payload, out var tick, out var entries));
            Assert.AreEqual(7u, tick);
            Assert.AreEqual(0, entries.Count);
        }

        [Test]
        public void LagCompensation_RewindsWithinBudget()
        {
            var compensator = new AuraLagCompensator(historyLength: 8);
            var states = new[] { State(0f, 0f, 0f, 0f, true) };
            compensator.Record(10u, states);
            compensator.Record(11u, new[] { State(1f, 0f, 0f, 0f, true) });
            compensator.Record(12u, new[] { State(2f, 0f, 0f, 0f, true) });

            Assert.IsTrue(compensator.TryRewind(11u, maxLagTicks: 4, out var rewind));
            Assert.AreEqual(1, rewind.Length);
            Assert.AreEqual(1f, rewind[0].Pose.Position.X, 1e-4f);

            Assert.IsFalse(compensator.TryRewind(20u, maxLagTicks: 4, out _), "a tick beyond the lag budget must not rewind.");
        }

        [Test]
        public void LagCompensation_HistoryIsBounded()
        {
            var compensator = new AuraLagCompensator(historyLength: 2);
            for (var tick = 0u; tick < 5u; tick++)
                compensator.Record(tick, new[] { State(tick, 0f, 0f, 0f, true) });

            Assert.AreEqual(2, compensator.Count);
            Assert.IsFalse(compensator.TryRewind(0u, maxLagTicks: 100, out _), "old snapshots should be evicted.");
            Assert.IsTrue(compensator.TryRewind(4u, maxLagTicks: 100, out _));
        }

        [Test]
        public void ReliableChannel_FragmentAndAcknowledge()
        {
            var channel = new AuraReliableChannel(64);
            var payload = new byte[150];
            for (var index = 0; index < payload.Length; index++)
                payload[index] = (byte)index;

            var packets = channel.Enqueue(payload);
            Assert.AreEqual(3, packets.Count);
            Assert.AreEqual(1u, packets[0].Sequence);
            Assert.AreEqual(0, packets[0].FragmentIndex);
            Assert.AreEqual(2, packets[2].FragmentIndex);
            Assert.AreEqual(1, channel.PendingCount);

            channel.Acknowledge(1u);
            Assert.AreEqual(0, channel.PendingCount);
        }

        [Test]
        public void ReliableChannel_RejectsOutOfOrder()
        {
            var channel = new AuraReliableChannel();
            Assert.IsFalse(channel.TryAccept(2u));
            Assert.IsTrue(channel.TryAccept(0u));
            Assert.IsFalse(channel.TryAccept(0u));
        }

        [Test]
        public void BaselineNegotiator_SelectsAndBoundsHistory()
        {
            var negotiator = new AuraSnapshotBaselineNegotiator(2);
            negotiator.Record(10u);
            negotiator.Record(11u);
            negotiator.Record(12u);
            Assert.AreEqual(2, negotiator.Count);
            Assert.IsTrue(negotiator.TrySelect(0u, 12u, out var baseline));
            Assert.AreEqual(12u, baseline.ServerTick);
            Assert.IsFalse(negotiator.TrySelect(1u, 10u, out _));
        }

        [Test]
        public void RollbackBuffer_RestoresStateAndInputs()
        {
            using var world = new AuraSimulationWorld(new FakePhysicsBackend(), new AuraWorldDefinition(initialBodyCapacity: 4));
            var buffer = new AuraRollbackBuffer(2);
            var inputs = new List<AuraInputCommand> { new AuraInputCommand(1u, 3u, 1, 0) };
            buffer.Record(12u, world, inputs);

            Assert.IsTrue(buffer.TryGet(12u, out var state, out var replay));
            Assert.IsNotNull(state);
            Assert.AreEqual(1, replay.Count);
            Assert.AreEqual(3u, replay[0].Sequence);
        }
    }
}
