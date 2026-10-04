using AuraEngine.Demo;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class AuraDemoWaveTests
    {
        [Test]
        public void Constant_IsOne()
        {
            Assert.AreEqual(1f, AuraDemoWave.Evaluate(AuraDemoWaveKind.Constant, 3.7f, 2f));
        }

        [Test]
        public void Sine_QuarterPeriodPeaks()
        {
            Assert.AreEqual(1f, AuraDemoWave.Evaluate(AuraDemoWaveKind.Sine, 0.25f, 1f), 1e-5f);
            Assert.AreEqual(-1f, AuraDemoWave.Evaluate(AuraDemoWaveKind.Sine, 0.75f, 1f), 1e-5f);
        }

        [Test]
        public void Square_FlipsAtHalfPeriod()
        {
            Assert.AreEqual(1f, AuraDemoWave.Evaluate(AuraDemoWaveKind.Square, 0.25f, 1f));
            Assert.AreEqual(-1f, AuraDemoWave.Evaluate(AuraDemoWaveKind.Square, 0.75f, 1f));
        }

        [Test]
        public void Triangle_RampsBetweenMinusOneAndOne()
        {
            Assert.AreEqual(-1f, AuraDemoWave.Evaluate(AuraDemoWaveKind.Triangle, 0f, 1f), 1e-5f);
            Assert.AreEqual(1f, AuraDemoWave.Evaluate(AuraDemoWaveKind.Triangle, 0.5f, 1f), 1e-5f);
            Assert.AreEqual(0f, AuraDemoWave.Evaluate(AuraDemoWaveKind.Triangle, 0.25f, 1f), 1e-5f);
        }
    }
}
