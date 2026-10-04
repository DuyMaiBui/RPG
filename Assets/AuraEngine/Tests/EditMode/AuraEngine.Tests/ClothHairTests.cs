using System;
using AuraEngine.Core;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class ClothHairTests
    {
        [Test]
        public void HairSolver_IdenticalInputs_ProduceIdenticalPositions()
        {
            var first = StepHair(CreateHairDefinition());
            var second = StepHair(CreateHairDefinition());

            Assert.AreEqual(first.StrandCount, second.StrandCount);
            Assert.AreEqual(first.PointsPerStrand, second.PointsPerStrand);
            for (var strand = 0; strand < first.StrandCount; strand++)
            {
                for (var point = 0; point < first.PointsPerStrand; point++)
                    Assert.AreEqual(first.GetPoint(strand, point), second.GetPoint(strand, point));
            }
        }

        [Test]
        public void ClothSolver_IdenticalInputs_ProduceIdenticalPositions()
        {
            var first = StepCloth(CreateClothDefinition());
            var second = StepCloth(CreateClothDefinition());

            Assert.AreEqual(first, second);
        }

        [Test]
        public void HairSolver_RootParticlesStayPinned()
        {
            var definition = CreateHairDefinition();
            var solver = StepHair(definition);

            for (var strand = 0; strand < solver.StrandCount; strand++)
            {
                var expected = definition.InitialPose.TransformPoint(definition.StrandRoots[strand]);
                Assert.AreEqual(expected, solver.GetPoint(strand, 0));
            }
        }

        [Test]
        public void ClothSolver_TopRowStaysPinned()
        {
            var definition = CreateClothDefinition();
            var solver = new AuraClothSolver(definition);
            var initial = solver.GetPositions();

            const float deltaTime = 1f / 60f;
            var gravity = new AuraVector3(0f, -9.81f, 0f);
            for (var step = 0; step < 120; step++)
                solver.Step(deltaTime, gravity, AuraVector3.Zero);

            var final = solver.GetPositions();
            for (var x = 0; x < definition.Width; x++)
                Assert.AreEqual(initial[x], final[x]);
        }

        [Test]
        public void ClothDefinition_RejectsInvalidValues()
        {
            Assert.AreNotEqual(AuraResult.Success, new AuraClothDefinition(1, 4, 0.25f, AuraPose.Identity, 0.98f, 1f).Validate());
            Assert.AreNotEqual(AuraResult.Success, new AuraClothDefinition(4, 1, 0.25f, AuraPose.Identity, 0.98f, 1f).Validate());
            Assert.AreNotEqual(AuraResult.Success, new AuraClothDefinition(4, 4, 0f, AuraPose.Identity, 0.98f, 1f).Validate());
            Assert.AreNotEqual(AuraResult.Success, new AuraClothDefinition(4, 4, float.NaN, AuraPose.Identity, 0.98f, 1f).Validate());
            Assert.AreNotEqual(AuraResult.Success, new AuraClothDefinition(4, 4, 0.25f, AuraPose.Identity, 1.5f, 1f).Validate());
            Assert.AreNotEqual(AuraResult.Success, new AuraClothDefinition(4, 4, 0.25f, AuraPose.Identity, 0.98f, -1f).Validate());
            Assert.AreEqual(AuraResult.Success, CreateClothDefinition().Validate());
        }

        [Test]
        public void VerletParticles_GroundPlaneClampsFallingParticles()
        {
            var particles = new AuraVerletParticles(2);
            particles.Positions[0] = new AuraVector3(0f, 10f, 0f);
            particles.PreviousPositions[0] = new AuraVector3(0f, 10f, 0f);
            particles.Positions[1] = new AuraVector3(1f, 10f, 0f);
            particles.PreviousPositions[1] = new AuraVector3(1f, 10f, 0f);

            const float groundPlaneY = 5f;
            for (var step = 0; step < 600; step++)
            {
                particles.Step(
                    1f / 60f,
                    new AuraVector3(0f, -9.81f, 0f),
                    AuraVector3.Zero,
                    1f,
                    1,
                    Array.Empty<AuraVerletConstraint>(),
                    groundPlaneY);
            }

            Assert.GreaterOrEqual(particles.Positions[0].Y, groundPlaneY);
            Assert.GreaterOrEqual(particles.Positions[1].Y, groundPlaneY);
        }

        private static AuraHairDefinition CreateHairDefinition() =>
            new AuraHairDefinition(
                AuraPose.Identity,
                new[] { new AuraVector3(-0.1f, 0f, 0f), new AuraVector3(0.1f, 0f, 0f) },
                6,
                0.1f);

        private static AuraClothDefinition CreateClothDefinition() =>
            new AuraClothDefinition(4, 5, 0.25f, AuraPose.Identity, 0.98f, 1f);

        private static AuraHairStrandSolver StepHair(AuraHairDefinition definition)
        {
            var solver = new AuraHairStrandSolver(definition);
            for (var step = 0; step < 120; step++)
                solver.Step(1f / 60f, new AuraVector3(0f, -9.81f, 0f), new AuraVector3(0.5f, 0f, 0f));
            return solver;
        }

        private static AuraVector3[] StepCloth(AuraClothDefinition definition)
        {
            var solver = new AuraClothSolver(definition);
            for (var step = 0; step < 120; step++)
                solver.Step(1f / 60f, new AuraVector3(0f, -9.81f, 0f), new AuraVector3(0.5f, 0f, 0f));
            return solver.GetPositions();
        }
    }
}
