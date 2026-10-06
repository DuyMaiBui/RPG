using NUnit.Framework;
using RPG.Core.Navigation;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    public sealed class NavigationLineOfSightTests
    {
        private static SimulationVector2 V(float x, float y) => new SimulationVector2(x, y);

        private static NavigationGrid GridWithBlock()
        {
            var grid = new NavigationGrid(20, 20, 1f, V(-10f, -10f));
            grid.ApplyObstacle(new NavigationObstacle(1, V(0f, 0f), V(1f, 1f)));
            return grid;
        }

        [Test]
        public void HasLineOfSight_IsBlockedByAnObstacle()
        {
            var grid = GridWithBlock();

            Assert.IsFalse(grid.HasLineOfSight(V(-5f, 0f), V(5f, 0f), 0.2f));
        }

        [Test]
        public void HasLineOfSight_IsClearWhenThePathMissesTheObstacle()
        {
            var grid = GridWithBlock();

            Assert.IsTrue(grid.HasLineOfSight(V(-5f, 5f), V(5f, 5f), 0.2f));
        }

        [Test]
        public void HasLineOfSight_AccountsForTheObserverRadius()
        {
            var grid = GridWithBlock();

            // The centre line clears the block (top at y = 1) but a 0.2 radius circle grazes it.
            Assert.IsFalse(grid.HasLineOfSight(V(-5f, 1.1f), V(5f, 1.1f), 0.2f));
        }

        [Test]
        public void HasLineOfSight_IsClearWithNoObstacles()
        {
            var grid = new NavigationGrid(20, 20, 1f, V(-10f, -10f));

            Assert.IsTrue(grid.HasLineOfSight(V(-5f, 0f), V(5f, 0f), 0.2f));
        }
    }
}
