using AuraEngine.Core;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class HairMotionTests
    {
        private const float DeltaTime = 1f / 60f;
        private const float SegmentLength = 0.3f;
        private static readonly AuraVector3 Gravity = new AuraVector3(0f, -9.81f, 0f);

        [Test]
        public void Strand_WithGust_SwaysAndKeepsSegmentLength()
        {
            var solver = CreateSolver();

            var maxSway = 0f;
            var maxLengthError = 0f;
            for (var step = 0; step < 300; step++)
            {
                var wind = AuraVerletWind.Sample(AuraVector3.Zero, new AuraVector3(3f, 0f, 0f), 0.5f, step * DeltaTime);
                solver.Step(DeltaTime, Gravity, wind);
                maxSway = System.Math.Max(maxSway, System.Math.Abs(solver.GetPoint(0, 7).X));
                for (var point = 0; point < 7; point++)
                {
                    var length = (solver.GetPoint(0, point + 1) - solver.GetPoint(0, point)).Length;
                    maxLengthError = System.Math.Max(maxLengthError, System.Math.Abs(length - SegmentLength));
                }
            }

            Assert.Greater(maxSway, 0.2f);
            Assert.Less(maxLengthError, 0.05f);
        }

        [Test]
        public void SetRootPose_MovesRootAndTipTrails()
        {
            var solver = CreateSolver();
            var moved = new AuraPose(new AuraVector3(4f, 0f, 0f), AuraQuaternion.Identity);
            for (var step = 0; step < 120; step++)
            {
                solver.SetRootPose(moved);
                solver.Step(DeltaTime, Gravity, AuraVector3.Zero);
            }

            Assert.AreEqual(4f, solver.GetPoint(0, 0).X, 1e-5f);
            Assert.Greater(solver.GetPoint(0, 7).X, 3.5f);
        }

        private static AuraHairStrandSolver CreateSolver() =>
            new AuraHairStrandSolver(new AuraHairDefinition(AuraPose.Identity, new[] { AuraVector3.Zero }, 8, SegmentLength));
    }
}
