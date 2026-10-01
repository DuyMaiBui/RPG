using System.Collections.Generic;
using AuraEngine.Core;
using AuraEngine.Networking;
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
    }
}
