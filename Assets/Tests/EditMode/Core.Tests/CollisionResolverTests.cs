using NUnit.Framework;
using RPG.Core.Physics;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    public sealed class CollisionResolverTests
    {
        private static SimulationVector2 V(float x, float y) => new SimulationVector2(x, y);

        [Test]
        public void SeparateCircles_PushesOverlappingCirclesToTheMinimumDistance()
        {
            var resolved = CollisionResolver.SeparateCircles(V(0.5f, 0f), 1f, V(0f, 0f), 1f, 1f);

            Assert.That(resolved.X, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(resolved.Y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void SeparateCircles_LeavesNonOverlappingCirclesUnchanged()
        {
            var resolved = CollisionResolver.SeparateCircles(V(5f, 3f), 0.5f, V(0f, 0f), 0.5f, 1f);

            Assert.That(resolved.X, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(resolved.Y, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void SeparateCircles_UsesTheTieBreakWhenCentresCoincide()
        {
            var left = CollisionResolver.SeparateCircles(V(0f, 0f), 1f, V(0f, 0f), 1f, -1f);
            var right = CollisionResolver.SeparateCircles(V(0f, 0f), 1f, V(0f, 0f), 1f, 1f);

            Assert.That(left.X, Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(right.X, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void SeparateCircles_IsDeterministicForTheSameInput()
        {
            var first = CollisionResolver.SeparateCircles(V(0.3f, 0.4f), 1f, V(0f, 0f), 1f, 1f);
            var second = CollisionResolver.SeparateCircles(V(0.3f, 0.4f), 1f, V(0f, 0f), 1f, 1f);

            Assert.That(first.X, Is.EqualTo(second.X));
            Assert.That(first.Y, Is.EqualTo(second.Y));
        }
    }
}
