using AuraEngine.Core;
using AuraEngine.Demo;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class AuraDemoExplosionMathTests
    {
        [Test]
        public void Outside_ReturnsZero()
        {
            var impulse = AuraDemoExplosionMath.RadialImpulse(AuraVector3.Zero, new AuraVector3(6f, 0f, 0f), 10f, 5f, 0f, true);
            Assert.AreEqual(AuraVector3.Zero, impulse);
        }

        [Test]
        public void LinearFalloff_HalvesAtHalfRadius_AndPointsAway()
        {
            var impulse = AuraDemoExplosionMath.RadialImpulse(AuraVector3.Zero, new AuraVector3(2.5f, 0f, 0f), 10f, 5f, 0f, true);
            Assert.AreEqual(5f, impulse.X, 1e-4f);
            Assert.AreEqual(0f, impulse.Y, 1e-4f);
        }

        [Test]
        public void ConstantFalloff_KeepsMagnitude_AndUpwardBiasTiltsUp()
        {
            var impulse = AuraDemoExplosionMath.RadialImpulse(AuraVector3.Zero, new AuraVector3(0f, 0f, 4f), 10f, 5f, 1f, false);
            Assert.AreEqual(10f, impulse.Length, 1e-4f);
            Assert.Greater(impulse.Y, 0f);
            Assert.Greater(impulse.Z, 0f);
        }

        [Test]
        public void AtTheCentre_BlowsStraightUp()
        {
            var impulse = AuraDemoExplosionMath.RadialImpulse(AuraVector3.Zero, AuraVector3.Zero, 10f, 5f, 0f, false);
            Assert.AreEqual(10f, impulse.Y, 1e-4f);
        }
    }
}
