using AuraEngine.Demo;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class AuraDemoGustCurveTests
    {
        [Test]
        public void StartsCalm_PeaksAtHalfPeriod_AndRepeats()
        {
            Assert.AreEqual(0f, AuraDemoGustCurve.Evaluate(0f, 4f, 1f), 1e-5f);
            Assert.AreEqual(1f, AuraDemoGustCurve.Evaluate(2f, 4f, 1f), 1e-5f);
            Assert.AreEqual(1f, AuraDemoGustCurve.Evaluate(6f, 4f, 1f), 1e-5f);
        }

        [Test]
        public void Sharpness_ShrinksTheShoulders()
        {
            var smooth = AuraDemoGustCurve.Evaluate(1f, 4f, 1f);
            var sharp = AuraDemoGustCurve.Evaluate(1f, 4f, 3f);
            Assert.Less(sharp, smooth);
        }

        [Test]
        public void InvalidPeriod_IsCalm()
        {
            Assert.AreEqual(0f, AuraDemoGustCurve.Evaluate(1f, 0f, 1f));
        }
    }
}
