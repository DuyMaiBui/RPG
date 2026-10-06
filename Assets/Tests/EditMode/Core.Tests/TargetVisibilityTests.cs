using NUnit.Framework;
using RPG.Core.Actors;
using RPG.Core.Navigation;
using RPG.Simulation.Contracts;

namespace RPG.Core.Tests
{
    public sealed class TargetVisibilityTests
    {
        private static NavigationGrid GridWithWallAtOrigin()
        {
            var grid = new NavigationGrid(20, 20, 1f, new SimulationVector2(-10f, -10f));
            grid.ApplyObstacle(new NavigationObstacle(1, SimulationVector2.Zero, new SimulationVector2(0.5f, 0.5f)));
            return grid;
        }

        [Test]
        public void TargetSelector_IgnoresAnEnemyBehindAWall()
        {
            var registry = new ActorRegistry();
            var attackerId = registry.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-1f, 0f), 0.3f, 0f, 3f, 1f, 1f));
            registry.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, new SimulationVector2(1f, 0f), 0.3f, 0f, 3f, 1f, 1f));

            Assert.That(registry.TryGet(attackerId, out var attacker), Is.True);
            Assert.That(
                new AnyAliveEnemyTargetSelector().TrySelect(attacker, registry, null, GridWithWallAtOrigin(), out _),
                Is.False);
        }

        [Test]
        public void TargetSelector_SelectsAnEnemyWithAClearLineOfSight()
        {
            var registry = new ActorRegistry();
            var attackerId = registry.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-1f, 0f), 0.3f, 0f, 3f, 1f, 1f));
            var enemyId = registry.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, new SimulationVector2(1f, 0f), 0.3f, 0f, 3f, 1f, 1f));
            var grid = new NavigationGrid(20, 20, 1f, new SimulationVector2(-10f, -10f));

            Assert.That(registry.TryGet(attackerId, out var attacker), Is.True);
            Assert.That(new AnyAliveEnemyTargetSelector().TrySelect(attacker, registry, null, grid, out var selected), Is.True);
            Assert.That(selected, Is.EqualTo(enemyId));
        }

        [Test]
        public void TargetSelector_SkipsAWalledEnemyForAVisibleOne()
        {
            var registry = new ActorRegistry();
            var attackerId = registry.Spawn(ActorKind.Player, FactionId.Red, new ActorSpawnData(
                10, 1, new SimulationVector2(-1f, 0f), 0.3f, 0f, 5f, 1f, 1f));
            registry.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, new SimulationVector2(1f, 0f), 0.3f, 0f, 5f, 1f, 1f));
            var visibleId = registry.Spawn(ActorKind.Monster, FactionId.Blue, new ActorSpawnData(
                10, 1, new SimulationVector2(0f, 2f), 0.3f, 0f, 5f, 1f, 1f));

            Assert.That(registry.TryGet(attackerId, out var attacker), Is.True);
            Assert.That(
                new AnyAliveEnemyTargetSelector().TrySelect(attacker, registry, null, GridWithWallAtOrigin(), out var selected),
                Is.True);
            Assert.That(selected, Is.EqualTo(visibleId));
        }
    }
}
