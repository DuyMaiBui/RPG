using AuraEngine.Demo;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class AuraDemoSlowMotionEnvelopeTests
    {
        [Test]
        public void Idle_IsFullSpeed()
        {
            Assert.AreEqual(1f, new AuraDemoSlowMotionEnvelope().Advance(0.1f));
        }

        [Test]
        public void HoldsThenRecoversLinearly_ThenGoesIdle()
        {
            var envelope = new AuraDemoSlowMotionEnvelope();
            envelope.Trigger(0.2f, 1f, 2f);
            Assert.AreEqual(0.2f, envelope.Advance(0.5f), 1e-5f);
            Assert.AreEqual(0.2f, envelope.Advance(0.5f), 1e-5f);
            Assert.AreEqual(0.6f, envelope.Advance(1f), 1e-5f);
            Assert.IsTrue(envelope.IsActive);
            Assert.AreEqual(1f, envelope.Advance(1f), 1e-5f);
            Assert.IsFalse(envelope.IsActive);
        }

        [Test]
        public void RetriggerKeepsLowerScale_AndRestartsHold()
        {
            var envelope = new AuraDemoSlowMotionEnvelope();
            envelope.Trigger(0.1f, 1f, 1f);
            envelope.Advance(0.9f);
            envelope.Trigger(0.5f, 1f, 1f);
            Assert.AreEqual(0.1f, envelope.Advance(0.9f), 1e-5f);
        }
    }
}
