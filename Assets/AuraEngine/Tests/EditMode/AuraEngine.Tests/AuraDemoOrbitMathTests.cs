using AuraEngine.Core;
using AuraEngine.Demo;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class AuraDemoOrbitMathTests
    {
        [Test]
        public void ConstantField_SpeedIsSqrtOfAccelerationTimesRadius()
        {
            var speed = AuraDemoOrbitMath.CircularSpeed(AuraForceFieldFalloff.None, 9f, 4f, 0f, 0f, 50f, 1f);
            Assert.AreEqual(6f, speed, 1e-4f);
        }

        [Test]
        public void InverseSquare_SpeedFallsWithSqrtOfDistance()
        {
            // a = k / r^2, v = sqrt(k / r): 100 at r = 4 gives 5.
            var speed = AuraDemoOrbitMath.CircularSpeed(AuraForceFieldFalloff.InverseSquare, 100f, 4f, 0f, 0f, 50f, 1f);
            Assert.AreEqual(5f, speed, 1e-4f);
        }

        [Test]
        public void Linear_UsesMaxRadiusAsReachWhenSet_ElseZoneExtent()
        {
            Assert.AreEqual(5f, AuraDemoOrbitMath.Magnitude(AuraForceFieldFalloff.Linear, 10f, 5f, 0f, 10f, 99f), 1e-4f);
            Assert.AreEqual(5f, AuraDemoOrbitMath.Magnitude(AuraForceFieldFalloff.Linear, 10f, 5f, 0f, 0f, 10f), 1e-4f);
        }

        [Test]
        public void MinRadius_ClampsTheEffectiveDistance()
        {
            Assert.AreEqual(100f / 4f, AuraDemoOrbitMath.Magnitude(AuraForceFieldFalloff.InverseSquare, 100f, 1f, 2f, 0f, 50f), 1e-4f);
        }

        [Test]
        public void BeyondMaxRadius_OrRepulsive_GivesNoOrbit()
        {
            Assert.AreEqual(0f, AuraDemoOrbitMath.CircularSpeed(AuraForceFieldFalloff.None, 9f, 11f, 0f, 10f, 50f, 1f));
            Assert.AreEqual(0f, AuraDemoOrbitMath.CircularSpeed(AuraForceFieldFalloff.None, -9f, 4f, 0f, 0f, 50f, 1f));
        }

        [Test]
        public void AccelerationScale_ScalesTheSpeedBySqrt()
        {
            var full = AuraDemoOrbitMath.CircularSpeed(AuraForceFieldFalloff.None, 8f, 2f, 0f, 0f, 50f, 1f);
            var quarter = AuraDemoOrbitMath.CircularSpeed(AuraForceFieldFalloff.None, 8f, 2f, 0f, 0f, 50f, 0.25f);
            Assert.AreEqual(full * 0.5f, quarter, 1e-4f);
        }

        [Test]
        public void ZoneExtent_IsRadiusOrHalfDiagonal()
        {
            Assert.AreEqual(7f, AuraDemoOrbitMath.ZoneExtent(AuraForceFieldShape.Sphere, 7f, 1f, 1f, 1f), 1e-5f);
            Assert.AreEqual(3f, AuraDemoOrbitMath.ZoneExtent(AuraForceFieldShape.Box, 7f, 1f, 2f, 2f), 1e-5f);
        }
    }
}
