using AuraEngine.Core;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class StateHasherTests
    {
        private static AuraBodyState State(float y) =>
            new AuraBodyState(
                new PhysicsBodyId(0, 0),
                new SimulationEntityId(0, 0),
                new AuraPose(new AuraVector3(0f, y, 0f), AuraQuaternion.Identity),
                AuraVector3.Zero,
                AuraVector3.Zero,
                true,
                0u);

        [Test]
        public void IdenticalStates_ProduceEqualHashes()
        {
            var first = AuraStateHasher.HashBodyState(AuraStateHasher.Begin(), State(1.5f));
            var second = AuraStateHasher.HashBodyState(AuraStateHasher.Begin(), State(1.5f));

            Assert.AreEqual(first, second);
        }

        [Test]
        public void DifferentStates_ProduceDifferentHashes()
        {
            var first = AuraStateHasher.HashBodyState(AuraStateHasher.Begin(), State(1.5f));
            var second = AuraStateHasher.HashBodyState(AuraStateHasher.Begin(), State(1.6f));

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void NegativeZeroAndPositiveZero_DifferByBits()
        {
            var positive = AuraStateHasher.CombineFloat(AuraStateHasher.Begin(), 0f);
            var negative = AuraStateHasher.CombineFloat(AuraStateHasher.Begin(), -0f);

            Assert.AreNotEqual(positive, negative);
        }
    }
}
