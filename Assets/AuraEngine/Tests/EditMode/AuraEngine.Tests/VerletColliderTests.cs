using System.Collections.Generic;
using AuraEngine.Core;
using NUnit.Framework;

namespace AuraEngine.Tests
{
    public sealed class VerletColliderTests
    {
        private const float DeltaTime = 1f / 60f;
        private const float Epsilon = 1e-3f;
        private static readonly AuraVector3 Gravity = new AuraVector3(0f, -9.81f, 0f);

        [Test]
        public void Cloth_DroppedOnSphere_RestsOnItWithoutPenetration()
        {
            var sphere = AuraVerletCollider.Sphere(new AuraVector3(0.2f, 0f, 0.2f), 0.5f, 1f, 0.02f);
            var grid = new VerletGrid(5, 5, 0.1f, 2f);
            grid.SetColliders(sphere);

            var worst = 0f;
            for (var step = 0; step < 600; step++)
            {
                grid.Step(Gravity);
                worst = System.Math.Max(worst, grid.WorstDepth(sphere));
            }

            Assert.Less(worst, Epsilon, "No particle may sit inside the skin shell after a step.");
            Assert.Greater(grid.Particles.Positions[grid.Index(2, 2)].Y, 0.5f, "Cloth centre rests on the sphere top.");
        }

        [Test]
        public void Cloth_DroppedOnBox_RestsOnItsTopFace()
        {
            var box = AuraVerletCollider.Box(new AuraVector3(0.5f, 0f, 0.5f), AuraQuaternion.FromEuler(0f, 0.4f, 0f), new AuraVector3(0.5f, 0.25f, 0.5f), 0.8f, 0.02f);
            var grid = new VerletGrid(6, 6, 0.2f, 1.5f);
            grid.SetColliders(box);

            var worst = 0f;
            for (var step = 0; step < 600; step++)
            {
                grid.Step(Gravity);
                worst = System.Math.Max(worst, grid.WorstDepth(box));
            }

            Assert.Less(worst, Epsilon);
            Assert.Greater(grid.Particles.Positions[grid.Index(3, 3)].Y, 0.25f, "Centre particle sits on the top face (y = 0.25 + skin).");
        }

        [Test]
        public void Cloth_DroppedOnPlane_StopsAtPlanePlusSkin()
        {
            var plane = AuraVerletCollider.Plane(new AuraVector3(0f, -1f, 0f), AuraVector3.UnitY, 1f, 0.03f);
            var grid = new VerletGrid(5, 5, 0.2f, 1f);
            grid.SetColliders(plane);

            var worst = 0f;
            for (var step = 0; step < 400; step++)
            {
                grid.Step(Gravity);
                worst = System.Math.Max(worst, grid.WorstDepth(plane));
            }

            Assert.Less(worst, Epsilon);
            for (var i = 0; i < grid.Particles.Count; i++)
                Assert.AreEqual(-0.97f, grid.Particles.Positions[i].Y, 0.01f);
        }

        [Test]
        public void Hair_PushedIntoCapsule_IsExpelled()
        {
            var capsule = AuraVerletCollider.Capsule(new AuraVector3(0.04f, -0.4f, 0f), AuraQuaternion.Identity, 0.1f, 0.8f, 0.5f, 0.01f);
            var strand = CreateStrand();
            ((IVerletColliderConsumer)strand).SetColliders(new List<AuraVerletCollider> { capsule });

            for (var step = 0; step < 240; step++)
                strand.Step(DeltaTime, Gravity, AuraVector3.Zero);

            for (var point = 2; point < strand.PointsPerStrand; point++)
            {
                AuraVerletCollisions.Query(capsule, strand.GetPoint(0, point), out _, out var depth);
                Assert.Less(depth, Epsilon, "Hair point " + point + " is still inside the capsule shell.");
            }
        }

        [Test]
        public void PinnedParticles_StayPinnedWhileColliding()
        {
            var box = AuraVerletCollider.Box(new AuraVector3(0.5f, -0.5f, 0f), AuraQuaternion.Identity, new AuraVector3(2f, 0.2f, 2f), 0.5f, 0.02f);
            var solver = new AuraClothSolver(new AuraClothDefinition(4, 8, 0.25f, AuraPose.Identity, 0.98f, 1f));
            ((IVerletColliderConsumer)solver).SetColliders(new List<AuraVerletCollider> { box });
            var top = new[] { solver.GetPosition(0, 0), solver.GetPosition(3, 0) };

            for (var step = 0; step < 300; step++)
                solver.Step(DeltaTime, Gravity, new AuraVector3(0f, 0f, 2f));

            Assert.AreEqual(top[0], solver.GetPosition(0, 0));
            Assert.AreEqual(top[1], solver.GetPosition(3, 0));
        }

        [Test]
        public void Cloth_WindPushedAgainstBox_NeverEntersIt()
        {
            var box = AuraVerletCollider.Box(new AuraVector3(0.4f, -1f, 0.6f), AuraQuaternion.Identity, new AuraVector3(2f, 2f, 0.3f), 0.3f, 0.02f);
            var solver = new AuraClothSolver(new AuraClothDefinition(4, 8, 0.25f, AuraPose.Identity, 0.98f, 1f));
            ((IVerletColliderConsumer)solver).SetColliders(new List<AuraVerletCollider> { box });

            var worst = 0f;
            for (var step = 0; step < 600; step++)
            {
                solver.Step(DeltaTime, Gravity, new AuraVector3(0f, 0f, 20f));
                for (var y = 1; y < solver.Height; y++)
                {
                    for (var x = 0; x < solver.Width; x++)
                    {
                        AuraVerletCollisions.Query(box, solver.GetPosition(x, y), out _, out var depth);
                        worst = System.Math.Max(worst, depth);
                    }
                }
            }

            Assert.Less(worst, Epsilon);
        }

        [Test]
        public void MovingPlane_CarriesRestingParticleUp()
        {
            var particles = new AuraVerletParticles(1);
            particles.Positions[0] = new AuraVector3(0f, 0.05f, 0f);
            particles.PreviousPositions[0] = particles.Positions[0];
            var consumer = (IVerletColliderConsumer)particles;
            var colliders = new List<AuraVerletCollider>(1) { default };
            consumer.SetColliders(colliders);

            var planeY = 0f;
            for (var step = 0; step < 120; step++)
            {
                planeY += 1f * DeltaTime;
                colliders[0] = AuraVerletCollider.Plane(new AuraVector3(0f, planeY, 0f), AuraVector3.UnitY, 0.5f, 0.01f, new AuraVector3(0f, 1f, 0f));
                particles.Step(DeltaTime, Gravity, AuraVector3.Zero, 1f, 3, null, float.NegativeInfinity);
            }

            Assert.AreEqual(planeY + 0.01f, particles.Positions[0].Y, Epsilon);
        }

        [Test]
        public void MovingBox_WithFullFriction_CarriesClothSideways()
        {
            var particles = new AuraVerletParticles(1);
            particles.Positions[0] = new AuraVector3(0f, 0.26f, 0f);
            particles.PreviousPositions[0] = particles.Positions[0];
            var consumer = (IVerletColliderConsumer)particles;
            var colliders = new List<AuraVerletCollider>(1) { default };
            consumer.SetColliders(colliders);

            var boxX = 0f;
            for (var step = 0; step < 120; step++)
            {
                boxX += 2f * DeltaTime;
                colliders[0] = AuraVerletCollider.Box(new AuraVector3(boxX, 0f, 0f), AuraQuaternion.Identity, new AuraVector3(5f, 0.25f, 5f), 1f, 0.01f, new AuraVector3(2f, 0f, 0f));
                particles.Step(DeltaTime, Gravity, AuraVector3.Zero, 1f, 3, null, float.NegativeInfinity);
            }

            Assert.AreEqual(boxX, particles.Positions[0].X, 0.3f, "Friction is limited by the normal impulse, so the particle lags while accelerating up to the box speed (about 0.2 m).");
            Assert.AreEqual(0.26f, particles.Positions[0].Y, Epsilon);
        }

        [Test]
        public void Collision_IsDeterministic()
        {
            var first = RunSphereDrop();
            var second = RunSphereDrop();
            for (var i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i], "Particle " + i + " differs between identical runs.");
        }

        [Test]
        public void Planar_CircleCollidesInXyAndLocksZ()
        {
            var circle = AuraVerletCollider.Sphere(new AuraVector3(0.5f, 0f, 3f), 0.5f, 0.5f, 0.02f, default, true);
            var grid = new VerletGrid(8, 1, 0.15f, 2f);
            ((IVerletColliderConsumer)grid.Particles).SetPlaneLock(true, 0f);
            grid.SetColliders(circle);

            var worst = 0f;
            for (var step = 0; step < 400; step++)
            {
                grid.Step(Gravity + new AuraVector3(0f, 0f, 5f));
                for (var i = 0; i < grid.Particles.Count; i++)
                {
                    Assert.AreEqual(0f, grid.Particles.Positions[i].Z, 1e-6f, "Z must stay locked to the plane.");
                    AuraVerletCollisions.Query(circle, grid.Particles.Positions[i], out var normal, out var depth);
                    Assert.AreEqual(0f, normal.Z, 1e-6f);
                    worst = System.Math.Max(worst, depth);
                }
            }

            Assert.Less(worst, Epsilon);
        }

        [Test]
        public void Planar_PlaneBlocksFallInXy()
        {
            var floor = AuraVerletCollider.Plane(new AuraVector3(0f, -1f, 0f), AuraVector3.UnitY, 1f, 0.01f, default, true);
            var particles = new AuraVerletParticles(1);
            particles.Positions[0] = new AuraVector3(0f, 1f, 0f);
            particles.PreviousPositions[0] = particles.Positions[0];
            var consumer = (IVerletColliderConsumer)particles;
            consumer.SetPlaneLock(true, 0f);
            consumer.SetColliders(new List<AuraVerletCollider> { floor });

            for (var step = 0; step < 300; step++)
                particles.Step(DeltaTime, Gravity, AuraVector3.Zero, 0.99f, 3, null, float.NegativeInfinity);

            Assert.AreEqual(-0.99f, particles.Positions[0].Y, Epsilon);
        }

        private static AuraVector3[] RunSphereDrop()
        {
            var sphere = AuraVerletCollider.Sphere(new AuraVector3(0.4f, 0f, 0.4f), 0.5f, 0.5f, 0.02f);
            var grid = new VerletGrid(6, 6, 0.15f, 1.5f);
            grid.SetColliders(sphere);
            for (var step = 0; step < 300; step++)
                grid.Step(Gravity);
            var copy = new AuraVector3[grid.Particles.Count];
            System.Array.Copy(grid.Particles.Positions, copy, copy.Length);
            return copy;
        }

        private static AuraHairStrandSolver CreateStrand() =>
            new AuraHairStrandSolver(new AuraHairDefinition(AuraPose.Identity, new[] { AuraVector3.Zero }, 8, 0.1f));
    }
}
