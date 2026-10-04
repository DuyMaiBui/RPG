using AuraEngine.Demo;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class AuraDemoIntervalTimerTests
    {
        [Test]
        public void OneShot_FiresOnceAfterDelay()
        {
            var timer = new AuraDemoIntervalTimer(1f, 0f, false);
            Assert.IsFalse(timer.Advance(0.5f));
            Assert.IsTrue(timer.Advance(0.5f));
            Assert.IsFalse(timer.Advance(5f));
        }

        [Test]
        public void Repeating_FiresEveryInterval()
        {
            var timer = new AuraDemoIntervalTimer(0f, 2f, true);
            Assert.IsTrue(timer.Advance(0.1f));
            Assert.IsFalse(timer.Advance(1f));
            Assert.IsTrue(timer.Advance(1f));
            Assert.IsFalse(timer.Advance(0.5f));
        }
    }
}
