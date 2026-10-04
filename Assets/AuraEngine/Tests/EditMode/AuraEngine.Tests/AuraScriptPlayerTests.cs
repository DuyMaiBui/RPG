using AuraEngine.Core;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class AuraScriptPlayerTests
    {
        [Test]
        public void Advances_ThroughSegments_ThenFinishes()
        {
            var player = new AuraScriptPlayer(new[] { 1f, 2f }, false);
            Assert.AreEqual(0, player.SegmentIndex);
            Assert.AreEqual(0, player.Advance(0.5f));
            Assert.AreEqual(0.5f, player.Progress, 1e-5f);
            Assert.AreEqual(1, player.Advance(0.75f));
            Assert.AreEqual(-1, player.Advance(5f));
            Assert.IsTrue(player.IsFinished);
        }

        [Test]
        public void Loop_WrapsAround()
        {
            var player = new AuraScriptPlayer(new[] { 1f, 1f }, true);
            Assert.AreEqual(0, player.Advance(2.25f));
            Assert.AreEqual(0.25f, player.Progress, 1e-5f);
        }

        [Test]
        public void ZeroLengthSegments_AreSkipped()
        {
            var player = new AuraScriptPlayer(new[] { 0f, 1f, 0f }, true);
            Assert.AreEqual(1, player.SegmentIndex);
            Assert.AreEqual(1, player.Advance(3.5f));
        }

        [Test]
        public void EmptyOrAllZero_NeverAdvances_AndRestartRewinds()
        {
            Assert.IsTrue(new AuraScriptPlayer(new float[0], true).IsFinished);
            Assert.IsTrue(new AuraScriptPlayer(new[] { 0f, 0f }, true).IsFinished);

            var player = new AuraScriptPlayer(new[] { 1f }, false);
            player.Advance(2f);
            player.Restart();
            Assert.AreEqual(0, player.SegmentIndex);
        }
    }
}
