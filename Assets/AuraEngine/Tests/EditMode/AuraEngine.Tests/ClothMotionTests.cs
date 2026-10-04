using AuraEngine.Core;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ClothMotionTests
    {
        private const float DeltaTime = 1f / 60f;
        private static readonly AuraVector3 Gravity = new AuraVector3(0f, -9.81f, 0f);

        [Test]
        public void HangingCloth_WithWind_MovesOutOfItsRestPlaneAndStaysPinned()
        {
            var solver = CreateSolver();
            var top = solver.GetPosition(3, 0);

            var maxDepth = 0f;
            for (var step = 0; step < 300; step++)
            {
                var wind = AuraVerletWind.Sample(AuraVector3.Zero, new AuraVector3(0f, 0f, 3f), 0.5f, step * DeltaTime);
                solver.Step(DeltaTime, Gravity, wind);
                maxDepth = System.Math.Max(maxDepth, System.Math.Abs(solver.GetPosition(3, 7).Z));
            }

            Assert.Greater(maxDepth, 0.1f, "Wind must visibly displace the free edge.");
            Assert.AreEqual(top, solver.GetPosition(3, 0));
        }

        [Test]
        public void HangingCloth_NeighbourDistancesStayNearRestLength()
        {
            var solver = CreateSolver();
            for (var step = 0; step < 300; step++)
                solver.Step(DeltaTime, Gravity, new AuraVector3(0f, 0f, 2f));

            for (var y = 0; y < solver.Height - 1; y++)
            {
                var length = (solver.GetPosition(2, y + 1) - solver.GetPosition(2, y)).Length;
                Assert.AreEqual(0.25f, length, 0.05f);
            }
        }

        [Test]
        public void SetOrigin_MovesPinnedRowAndFreeParticlesFollow()
        {
            var solver = CreateSolver();
            var moved = new AuraPose(new AuraVector3(5f, 0f, 0f), AuraQuaternion.Identity);
            solver.SetOrigin(moved);

            Assert.AreEqual(5f, solver.GetPosition(0, 0).X, 1e-5f);
            Assert.AreEqual(5.75f, solver.GetPosition(3, 0).X, 1e-5f);

            for (var step = 0; step < 240; step++)
            {
                solver.SetOrigin(moved);
                solver.Step(DeltaTime, Gravity, AuraVector3.Zero);
            }

            Assert.Greater(solver.GetPosition(0, 4).X, 4.5f, "Free rows must be dragged along with the anchor.");
        }

        private static AuraClothSolver CreateSolver() =>
            new AuraClothSolver(new AuraClothDefinition(4, 8, 0.25f, AuraPose.Identity, 0.98f, 1f));
    }
}
