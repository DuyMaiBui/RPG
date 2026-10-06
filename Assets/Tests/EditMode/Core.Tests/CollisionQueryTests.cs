using System;
using NUnit.Framework;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    public sealed class CollisionQueryTests
    {
        private static SimulationVector2 V(float x, float y) => new SimulationVector2(x, y);

        [Test]
        public void Raycast_HitsCircleFromOutside()
        {
            var found = CollisionShapeQueries.Raycast(
                new CollisionRay(V(0f, 0f), V(1f, 0f)),
                100f,
                CollisionShape.Circle(1f),
                V(10f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(9f).Within(0.0001f));
            Assert.That(hit.Point.X, Is.EqualTo(9f).Within(0.0001f));
            Assert.That(hit.Point.Y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(hit.Normal.X, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(hit.Normal.Y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Raycast_MissesCircleWhenPointingAway()
        {
            var found = CollisionShapeQueries.Raycast(
                new CollisionRay(V(0f, 0f), V(-1f, 0f)),
                100f,
                CollisionShape.Circle(1f),
                V(10f, 0f),
                out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void Raycast_ReportsZeroDistanceWhenStartingInsideCircle()
        {
            var found = CollisionShapeQueries.Raycast(
                new CollisionRay(V(0f, 0f), V(1f, 0f)),
                100f,
                CollisionShape.Circle(5f),
                V(0f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Raycast_RespectsMaxDistance()
        {
            var found = CollisionShapeQueries.Raycast(
                new CollisionRay(V(0f, 0f), V(1f, 0f)),
                5f,
                CollisionShape.Circle(1f),
                V(10f, 0f),
                out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void Raycast_HitsAxisAlignedBoxEdge()
        {
            var found = CollisionShapeQueries.Raycast(
                new CollisionRay(V(0f, 0f), V(1f, 0f)),
                100f,
                CollisionShape.Box(V(1f, 1f)),
                V(10f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(9f).Within(0.0001f));
            Assert.That(hit.Normal.X, Is.EqualTo(-1f).Within(0.0001f));
        }

        [Test]
        public void Raycast_MissesBoxOutsideItsSlab()
        {
            var found = CollisionShapeQueries.Raycast(
                new CollisionRay(V(0f, 5f), V(1f, 0f)),
                100f,
                CollisionShape.Box(V(1f, 1f)),
                V(10f, 0f),
                out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void Raycast_HitsRotatedBoxAtItsExtremeVertex()
        {
            var found = CollisionShapeQueries.Raycast(
                new CollisionRay(V(0f, 0f), V(1f, 0f)),
                100f,
                CollisionShape.Box(V(1f, 1f), MathF.PI / 4f),
                V(10f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(10f - MathF.Sqrt(2f)).Within(0.001f));
            Assert.That(hit.Point.Y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Raycast_HitsPolygonSquareEdge()
        {
            var vertices = new[] { V(1f, -1f), V(1f, 1f), V(-1f, 1f), V(-1f, -1f) };
            var found = CollisionShapeQueries.Raycast(
                new CollisionRay(V(0f, 0f), V(1f, 0f)),
                100f,
                CollisionShape.Polygon(vertices),
                V(10f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(9f).Within(0.0001f));
            Assert.That(hit.Normal.X, Is.EqualTo(-1f).Within(0.0001f));
        }

        [Test]
        public void SweepCircle_HitsCircleWithoutTunnellingAtHighSpeed()
        {
            var found = CollisionShapeQueries.SweepCircle(
                0.5f,
                V(0f, 0f),
                V(200f, 0f),
                CollisionShape.Circle(1f),
                V(100f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(98.5f).Within(0.001f));
            Assert.That(hit.Normal.X, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(hit.Point.X, Is.EqualTo(99f).Within(0.001f));
        }

        [Test]
        public void SweepCircle_HitsAxisAlignedBoxAcrossLargeDistance()
        {
            var found = CollisionShapeQueries.SweepCircle(
                0.5f,
                V(0f, 0f),
                V(30f, 0f),
                CollisionShape.Box(V(1f, 1f)),
                V(10f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(8.5f).Within(0.001f));
            Assert.That(hit.Normal.X, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(hit.Point.X, Is.EqualTo(9f).Within(0.001f));
        }

        [Test]
        public void SweepCircle_HitsRotatedBoxAtItsVertex()
        {
            var found = CollisionShapeQueries.SweepCircle(
                0.5f,
                V(0f, 0f),
                V(30f, 0f),
                CollisionShape.Box(V(1f, 1f), MathF.PI / 4f),
                V(10f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(10f - MathF.Sqrt(2f) - 0.5f).Within(0.01f));
        }

        [Test]
        public void SweepCircle_MissesWhenPathDoesNotReachTarget()
        {
            var found = CollisionShapeQueries.SweepCircle(
                0.5f,
                V(0f, 0f),
                V(30f, 0f),
                CollisionShape.Circle(1f),
                V(10f, 10f),
                out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void SweepCircle_ReportsZeroDistanceWhenAlreadyOverlapping()
        {
            var found = CollisionShapeQueries.SweepCircle(
                0.5f,
                V(0f, 0f),
                V(10f, 0f),
                CollisionShape.Circle(1f),
                V(0.5f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SweepCircle_WithZeroRadiusBehavesLikeRaycast()
        {
            var found = CollisionShapeQueries.SweepCircle(
                0f,
                V(0f, 0f),
                V(20f, 0f),
                CollisionShape.Circle(1f),
                V(10f, 0f),
                out var hit);

            Assert.IsTrue(found);
            Assert.That(hit.Distance, Is.EqualTo(9f).Within(0.001f));
        }
    }
}
