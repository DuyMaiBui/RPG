using AuraEngine.Core;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class AuraThrottleRampTests
    {
        [Test]
        public void Rises_AtBoundedRate_AndClampsToTarget()
        {
            var ramp = new AuraThrottleRamp(2f, 4f);
            Assert.AreEqual(1f, ramp.Step(1f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, ramp.Step(1f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, ramp.Step(0.5f, 0.125f), 1e-5f);
        }

        [Test]
        public void Releasing_FallsAtFallRate()
        {
            var ramp = new AuraThrottleRamp(10f, 1f);
            ramp.Step(1f, 1f);
            Assert.AreEqual(0.5f, ramp.Step(0f, 0.5f), 1e-5f);
        }

        [Test]
        public void ReversingSign_UsesRiseRate()
        {
            var ramp = new AuraThrottleRamp(2f, 1f);
            ramp.Step(1f, 1f);
            Assert.AreEqual(0f, ramp.Step(-1f, 0.5f), 1e-5f);
        }

        [Test]
        public void NonPositiveDelta_LeavesValue()
        {
            var ramp = new AuraThrottleRamp(2f, 2f);
            ramp.Step(1f, 0.25f);
            Assert.AreEqual(0.5f, ramp.Step(1f, 0f), 1e-5f);
        }

        [Test]
        public void InvalidRates_Throw()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AuraThrottleRamp(0f, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new AuraThrottleRamp(1f, -1f));
        }
    }
}
